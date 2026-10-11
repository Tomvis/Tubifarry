using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using Tubifarry.Core.Utilities;
using Tubifarry.Indexers.Soulseek;

namespace Tubifarry.Download.Clients.Soulseek;

// Tomvis fork (MUSIC-64): Lidarr wants only some tracks of an album, so enqueue just those files.
public record WantedTrack(string Title, int Disc, int Number, int DurationMs);

// Track.Monitored exists only in Tomvis/Lidarr, so read it by reflection and keep building against upstream
// Lidarr. Upstream (no property) = every track wanted.
public static class LidarrWantedTracks
{
    private static readonly PropertyInfo? Monitored = typeof(Track).GetProperty("Monitored");

    public static (List<WantedTrack>? Wanted, int TrackCount) For(RemoteAlbum remoteAlbum, ITrackService trackService)
    {
        if (Monitored == null || remoteAlbum.Albums is not { Count: 1 })
            return (null, 0);

        List<Track> tracks = trackService.GetTracksByAlbum(remoteAlbum.Albums[0].Id);
        if (tracks.All(IsWanted))
            return (null, tracks.Count);

        List<WantedTrack> wanted = tracks.Where(IsWanted)
            .Select(t => new WantedTrack(t.Title, t.MediumNumber, t.AbsoluteTrackNumber, t.Duration))
            .ToList();
        return (wanted, tracks.Count);
    }

    private static bool IsWanted(Track track) => (bool)Monitored!.GetValue(track)!;
}

public static partial class SlskdWantedFileFilter
{
    private const int DurationToleranceSec = 10;

    private record Candidate(SlskdFileData File, HashSet<string> Titles, int Disc, int Number, int? Seconds);

    // Audio files to enqueue, or null for the whole folder: nothing is unwanted, or a wanted track has no match
    // (a partial grab would leave it missing and the next search would re-grab this folder).
    public static List<SlskdFileData>? Select(IReadOnlyList<SlskdFileData> files, IReadOnlyList<WantedTrack>? wanted, int releaseTrackCount)
    {
        if (wanted == null || wanted.Count == 0)
            return null;

        List<Candidate> audio = files.Where(IsAudio).Select(ToCandidate).ToList();

        // A browsed folder (indexer's directory expansion) copies the search hit's length onto every file
        if (audio.Count > 1 && audio.All(c => c.Seconds == audio[0].Seconds))
            audio = audio.Select(c => c with { Seconds = null }).ToList();

        List<SlskdFileData> picked = [];
        foreach (WantedTrack want in wanted)
        {
            Candidate? match = ByTitle(audio, want) ?? ByPosition(audio, want, releaseTrackCount);
            if (match == null)
                return null;

            if (!picked.Contains(match.File))
                picked.Add(match.File);
        }

        return picked;
    }

    private static Candidate? ByTitle(List<Candidate> audio, WantedTrack want)
    {
        string title = Normalize(want.Title);
        if (title.Length == 0)
            return null;

        List<Candidate> candidates = audio.Where(c => c.Titles.Contains(title) && DurationFits(c, want)).ToList();
        return candidates.FirstOrDefault(c => c.Disc == want.Disc && c.Number == want.Number)
            ?? candidates.OrderBy(c => DurationGap(c, want)).FirstOrDefault();
    }

    // Same tracklist length: trust disc/number when the durations agree (titles differ by translation etc.)
    private static Candidate? ByPosition(List<Candidate> audio, WantedTrack want, int releaseTrackCount)
    {
        if (audio.Count != releaseTrackCount || want.DurationMs <= 0)
            return null;

        return audio.FirstOrDefault(c => c.Disc == want.Disc && c.Number == want.Number && c.Seconds > 0 && DurationFits(c, want));
    }

    private static bool DurationFits(Candidate c, WantedTrack want) =>
        want.DurationMs <= 0 || c.Seconds is not > 0 || DurationGap(c, want) <= DurationToleranceSec;

    private static int DurationGap(Candidate c, WantedTrack want) =>
        Math.Abs((c.Seconds ?? 0) - (int)Math.Round(want.DurationMs / 1000.0));

    private static bool IsAudio(SlskdFileData file) =>
        AudioFormatHelper.GetAudioCodecFromExtension(
            (!string.IsNullOrWhiteSpace(file.Extension) ? file.Extension : Path.GetExtension(file.Filename ?? "")).TrimStart('.')) != AudioFormat.Unknown;

    // "Artist - Album - 06 - Title.flac": every " - " suffix is a title candidate ("Title", "06 - Title", ...)
    // after the leading "06 ", "06.", "1-06 " or "106 " numbering is stripped.
    private static Candidate ToCandidate(SlskdFileData file)
    {
        string path = (file.Filename ?? "").Replace('\\', '/');
        string stem = Path.GetFileNameWithoutExtension(path);
        string[] parts = SegmentSeparator().Split(stem);
        int disc = 0, number = 0;

        // Numbering leads the name ("06 Title") or is its own segment ("Artist - Album - 06 - Title")
        Match numbering = LeadingNumber().Match(stem);
        if (!numbering.Success && parts.FirstOrDefault(p => PlainNumber().IsMatch(p)) is string segment)
            numbering = LeadingNumber().Match(segment + " ");
        if (numbering.Success)
        {
            number = int.Parse(numbering.Groups["track"].Value);
            if (numbering.Groups["disc"].Success)
                disc = int.Parse(numbering.Groups["disc"].Value);
            else if (numbering.Groups["track"].Value.Length == 3)
                (disc, number) = (number / 100, number % 100);
        }

        if (disc == 0)
        {
            Match parent = DiscFolder().Match(Path.GetFileName(Path.GetDirectoryName(path) ?? ""));
            disc = parent.Success ? int.Parse(parent.Groups["disc"].Value) : 1;
        }

        HashSet<string> titles = [];
        for (int i = 0; i < parts.Length; i++)
        {
            string title = Normalize(string.Join(" - ", parts[i..]));
            if (title.Length > 0)
                titles.Add(title);

            string stripped = LeadingNumber().Replace(parts[i], "", 1);
            if (stripped != parts[i] && Normalize(string.Join(" - ", [stripped, .. parts[(i + 1)..]])) is { Length: > 0 } rest)
                titles.Add(rest);
        }

        return new Candidate(file, titles, disc, number, file.Length);
    }

    // "Song (feat. X) [2009 Remaster]" and "Song - 2009 Remaster" both become "song"; "Lux Æterna" = "Lux Aeterna"
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string t = Regex.Replace(title, @"\([^)]*\)|\[[^\]]*\]", " ");
        t = Regex.Replace(t, @"\s+-\s+[^-]*\b(remaster(ed)?|version|mix|edit|live|mono|stereo|demo|bonus)\b.*$", " ", RegexOptions.IgnoreCase);
        t = t.Normalize(NormalizationForm.FormD);

        StringBuilder sb = new();
        foreach (char c in t)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c) switch { 'æ' => "ae", 'œ' => "oe", 'ø' => "o", 'ß' => "ss", 'ł' => "l", 'đ' => "d", char l => l.ToString() });
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"^\s*(?:(?<disc>\d{1,2})[-.](?<track>\d{1,2})|(?<track>\d{1,3}))(?:\s*[.)_-]\s*|\s+)")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"\b(?:cd|disc|disk)\s*(?<disc>\d{1,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DiscFolder();

    [GeneratedRegex(@"^\d{1,3}$")]
    private static partial Regex PlainNumber();

    [GeneratedRegex(@"\s+-\s+")]
    private static partial Regex SegmentSeparator();
}
