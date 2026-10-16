using System.Reflection;
using System.Text.RegularExpressions;
using NzbDrone.Core.Music;
using Tubifarry.Download.Clients.Soulseek;

namespace Tubifarry.Indexers.Soulseek;

// Tomvis fork (MUSIC-68): Tomvis/Lidarr lets an album monitor several releases ("editions", MUSIC-63) and picks the one a
// result is for by the words in its title. A matched folder is titled after the album, so a folder holding an extra
// edition reached Lidarr as the primary one, and the tracklist check judged every folder by the first monitored release
// only. Fit the folder to each monitored edition instead, and name the extra edition it holds in the title.
// MUSIC-104: Tomvis/Lidarr also holds editions (MUSIC-102), bestedition targets that are usually the album from another
// mastering: same title and tracklist, durations a second apart. Name a held edition the way Lidarr does, and give it
// a folder only when the folder's names carry its release data.
public static class SlskdEditions
{
    // Editions whose mean durations are this close tie, and a tie goes to the primary
    private const double DeviationMarginSec = 0.5;

    // AlbumRelease.Held exists only in Tomvis/Lidarr; the plugin still builds against upstream
    private static readonly PropertyInfo? HeldProperty = typeof(AlbumRelease).GetProperty("Held");

    // Lidarr's AlbumEditions clue words: a later mastering, label-name filler, short country names
    private static readonly string[] ReissueWords = ["remaster", "remastered", "remastering", "reissue", "reissued"];
    private static readonly HashSet<string> LabelNoise =
    [
        "record", "recording", "music", "musik", "entertainment", "media", "production", "distribution", "group", "label",
        "company", "co", "ltd", "inc", "llc", "corp", "corporation", "gmbh", "international", "the", "of", "on", "and", "a"
    ];
    private static readonly Dictionary<string, string[]> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Japan", ["japanese", "jpn", "jp"] },
        { "United States", ["usa"] },
        { "United Kingdom", ["uk"] },
        { "Germany", ["german"] },
        { "Europe", ["eu"] }
    };

    // Null unless the album monitors several releases (always on upstream Lidarr)
    public static List<SlskdEdition>? Of(Album? album, Func<AlbumRelease, bool>? isHeld = null)
    {
        List<AlbumRelease> monitored = album?.AlbumReleases?.Value?.Where(r => r.Monitored).ToList() ?? [];
        if (monitored.Count < 2)
            return null;

        isHeld ??= r => HeldProperty?.GetValue(r) is true;

        // Lidarr's AlbumEditions.Primary: not held, titled like the album, no disambiguation, lowest id
        AlbumRelease primary = monitored
            .OrderBy(isHeld)
            .ThenByDescending(r => string.Equals(r.Title, album!.Title, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => !string.IsNullOrWhiteSpace(r.Disambiguation))
            .ThenBy(r => r.Id)
            .First();

        // Clues count only when no other monitored edition has them, as in Lidarr's HeldTarget
        int? originalYear = Year(album!.ReleaseDate) ?? monitored.Select(r => Year(r.ReleaseDate)).Min();
        Dictionary<int, List<SlskdEditionClue>> clues = monitored.ToDictionary(r => r.Id, r => Clues(r, originalYear));

        return monitored.Select(r =>
        {
            List<Track> tracks = r.Tracks?.Value?.Where(t => !string.IsNullOrEmpty(t.Title)).ToList() ?? [];
            bool held = r.Id != primary.Id && isHeld(r);
            return new SlskdEdition(
                held ? HeldLabel(album, r) : $"{r.Title} {r.Disambiguation}".Trim(),
                r.Id == primary.Id,
                [.. tracks.Select(t => t.Title).Take(50)],
                r.TrackCount,
                SlskdTracklistCheck.LengthsOf([r]),
                LidarrWantedTracks.From(tracks))
            {
                Held = held,
                Year = Year(r.ReleaseDate),
                Clues = [.. clues[r.Id].Where(c => !monitored.Any(o => o.Id != r.Id && clues[o.Id].Any(x => x.Key == c.Key)))]
            };
        }).ToList();
    }

    // Lidarr's AlbumEditions.EditionTitle for a held edition: its own title, else the album's qualified by
    // disambiguation, or year, format and country
    private static string HeldLabel(Album album, AlbumRelease release)
    {
        if (!string.IsNullOrWhiteSpace(release.Title) && !string.Equals(release.Title, album.Title, StringComparison.OrdinalIgnoreCase))
            return release.Title;

        string label = release.Disambiguation;
        if (string.IsNullOrWhiteSpace(label))
            label = string.Join(" ", new[] { Year(release.ReleaseDate)?.ToString(), FormatMedia(release.Media), release.Country?.FirstOrDefault() }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(label))
            label = release.ForeignReleaseId;

        return $"{album.Title} ({label})";
    }

    // MusicBrainz style: "2xCD", "CD + DVD"
    private static string? FormatMedia(List<Medium>? media) => media is not { Count: > 0 } ? null
        : string.Join(" + ", media.OrderBy(m => m.Number).GroupBy(m => m.Format).Select(g => g.Count() > 1 ? $"{g.Count()}x{g.Key}" : g.Key));

    private static List<SlskdEditionClue> Clues(AlbumRelease release, int? originalYear)
    {
        List<SlskdEditionClue> clues = [];
        int? year = Year(release.ReleaseDate);
        if (year != null)
            clues.Add(new($"year:{year}", [[year.Value.ToString()]]));
        if (year > originalYear)
            clues.Add(new("reissue", [.. ReissueWords.Select(w => new[] { w })]));

        foreach (string label in release.Label ?? [])
        {
            string[] words = [.. Words(label).Where(w => !LabelNoise.Contains(w)).Order()];
            if (words.Length > 0)
                clues.Add(new($"label:{string.Join(" ", words)}", [words]));
        }

        foreach (string country in release.Country ?? [])
        {
            if (string.IsNullOrWhiteSpace(country) || country.StartsWith('['))
                continue;
            List<string[]> alternatives = [[.. Words(country)]];
            if (CountryAliases.TryGetValue(country, out string[]? aliases))
                alternatives.AddRange(aliases.Select(a => new[] { a }));
            clues.Add(new($"country:{country.ToLowerInvariant()}", alternatives));
        }

        foreach (Medium medium in release.Media ?? [])
        {
            string format = (medium.Format ?? "").ToLowerInvariant();
            string[]? words = format.Contains("vinyl") ? ["vinyl", "lp"] : format.Contains("sacd") ? ["sacd"]
                : format.Contains("cassette") ? ["cassette", "tape"] : format.Contains("digital") ? ["web"] : format.Contains("cd") ? ["cd"] : null;
            if (words != null)
                clues.Add(new($"format:{words[0]}", [.. words.Select(w => new[] { w })]));
        }

        return [.. clues.GroupBy(c => c.Key).Select(g => g.First())];
    }

    // MusicBrainz has unknown dates as DateTime.MinValue or 0001 too
    private static int? Year(DateTime? date) => date?.Year >= 1900 ? date.Value.Year : null;

    // Lidarr's AlbumEditions.Words: lowercase, split on anything but letters and digits, a plural "s" dropped
    private static HashSet<string> Words(string? text) =>
        [.. Regex.Split((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length > 0)
            .Select(w => w.Length > 3 && w.EndsWith('s') ? w[..^1] : w)];

    // What the folder's names say, less the track titles'
    private static HashSet<string> NameWords(IReadOnlyList<SlskdFileData> files, IReadOnlyList<SlskdEdition> editions)
    {
        HashSet<string> titles = [.. editions.SelectMany(e => e.Tracks).SelectMany(Words)];
        HashSet<string> words = [.. files.SelectMany(f => Words(Path.ChangeExtension(f.Filename?.Replace('\\', '/'), null)))];
        words.ExceptWith(titles);
        return words;
    }

    // The edition the folder holds, or null with why it fits none: fewest missing or extra files, then closest durations,
    // then the primary
    public static (SlskdEdition? Edition, string? Reason) Fit(IReadOnlyList<SlskdFileData> files, IReadOnlyList<SlskdEdition> editions)
    {
        int audio = files.Count(SlskdWantedFileFilter.IsAudio);
        List<(SlskdEdition Edition, double? Deviation)> fits = [];
        List<string> reasons = [];
        foreach (SlskdEdition edition in editions)
        {
            if (SlskdTracklistCheck.Reject(files, edition.Tracks, edition.TrackCount, edition.Wanted, edition.Lengths) is string reason)
                reasons.Add($"{edition.Label}: {reason}");
            else
                fits.Add((edition, SlskdTracklistCheck.Deviation(files, edition.Tracks, edition.Lengths)));
        }

        if (fits.Count == 0)
            return (null, string.Join("; ", reasons));

        // No audio (cover scans, guitar tabs) says nothing about the edition; the check passes it on to Lidarr as is
        if (audio == 0)
            return (fits.Find(f => f.Edition.Primary).Edition ?? fits[0].Edition, null);

        int fewestGap = fits.Min(f => Math.Abs(audio - f.Edition.TrackCount));
        fits = fits.Where(f => Math.Abs(audio - f.Edition.TrackCount) == fewestGap).ToList();

        // MUSIC-104: durations can't tell a held edition from the primary it remasters. A folder fitting both is the held
        // one only when its names carry more of that edition's clues than of any other edition's (Lidarr's HeldTarget)
        if (fits.Any(f => f.Edition.Primary) && fits.Any(f => f.Edition.Held))
        {
            HashSet<string> words = NameWords(files, editions);
            List<(SlskdEdition Edition, int Score)> scores = [.. editions
                .Select(e => (e, e.Clues.Count(c => c.Matches(words))))
                .OrderByDescending(x => x.Item2)];
            if (scores[0].Score > 0 && scores[0].Edition.Held && scores[1].Score < scores[0].Score && fits.Any(f => ReferenceEquals(f.Edition, scores[0].Edition)))
                return (scores[0].Edition, null);
            fits = fits.Where(f => !f.Edition.Held).ToList();
        }

        double? closest = fits.Min(f => f.Deviation);
        if (closest != null)
            fits = fits.Where(f => f.Deviation <= closest + DeviationMarginSec).ToList();

        return (fits.OrderByDescending(f => f.Edition.Primary).ThenBy(f => f.Deviation).First().Edition, null);
    }
}

// A monitored release of a multi-edition album; Label holds the words Lidarr tells it apart by, Clues the release data
// only it has (MUSIC-104)
public record SlskdEdition(string Label, bool Primary, List<string> Tracks, int TrackCount, List<TrackLength>? Lengths, List<WantedTrack>? Wanted)
{
    public bool Held { get; init; }
    public int? Year { get; init; }
    public List<SlskdEditionClue> Clues { get; init; } = [];
}

// Release data a folder may name: one of the alternatives, each a set of words all in the names
public record SlskdEditionClue(string Key, List<string[]> Alternatives)
{
    public bool Matches(HashSet<string> words) => Alternatives.Any(a => a.Length > 0 && a.All(words.Contains));
}
