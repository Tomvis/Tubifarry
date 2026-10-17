using System.Text.RegularExpressions;
using NzbDrone.Core.Music;
using Tubifarry.Download.Clients.Soulseek;

namespace Tubifarry.Indexers.Soulseek;

// Tomvis fork (MUSIC-72): a folder that matches only by name (another album, a compilation, one stray track)
// fails import and is blocklisted hours later, or worse, imports as the wrong album. Reject it at search time
// when its file titles don't cover the release's tracklist. Replayed on 1,593 past grabs: rejects 55% of the
// failed imports; the "good" grabs it rejects were nearly all wrong content Lidarr imported anyway.
public static partial class SlskdTracklistCheck
{
    private const double MinCoverage = 0.8;
    private const int DurationToleranceSec = 10;
    private const int MaxReleases = 25;
    private const int MaxTitles = 200;

    private record AudioFile(HashSet<string> Titles, int? Seconds);

    // Why the folder doesn't fit, or null to keep it
    public static string? Reject(IReadOnlyList<SlskdFileData> files, IReadOnlyList<string>? tracks, int trackCount, IReadOnlyList<WantedTrack>? wanted,
        IReadOnlyList<TrackLength>? lengths = null)
    {
        List<AudioFile>? audio = Audio(files, tracks);
        if (audio == null || tracks == null)
            return null;

        Dictionary<string, List<int>> known = Known(lengths);

        // Partial album (MUSIC-64): every wanted track must be there; extra files are skipped at enqueue
        if (wanted is { Count: > 0 })
        {
            List<WantedTrack> missing = wanted.Where(w => !Covered(w.Title, audio)).ToList();
            if (missing.Count > 0)
                return $"lacks wanted {string.Join(", ", missing.Select(w => $"\"{w.Title}\""))}";

            List<WantedTrack> wrong = wanted.Where(w => LengthCheck(w.Title, audio, known) == false).ToList();
            return wrong.Count == 0 ? null : $"wanted {string.Join(", ", wrong.Select(w => $"\"{w.Title}\""))} has the wrong duration";
        }

        int matched = tracks.Count(t => Covered(t, audio));
        if (matched < tracks.Count * MinCoverage)
            return $"titles cover {matched}/{tracks.Count} tracks";

        int expected = Math.Max(trackCount, tracks.Count);
        if (audio.Count > Math.Max(2 * expected, expected + 6))
            return $"{audio.Count} audio files for {expected} tracks";

        // MUSIC-74: a fake with the right titles (a Eurobeat rip named as Elvis songs) gives itself away by its durations
        List<bool> checkedTracks = tracks.Select(t => LengthCheck(t, audio, known)).OfType<bool>().ToList();
        int off = checkedTracks.Count(fits => !fits);
        if (off >= Math.Max(2, checkedTracks.Count / 3.0))
            return $"durations miss {off}/{checkedTracks.Count} tracks";

        return null;
    }

    // MUSIC-104: a covers album ("Kerrang! Presents 'Remastered'": "01 - Machine Head - Battery") fits the tracklist and its
    // durations. The segment before each file's title names its artist; reject when nearly every file names a different
    // artist and the album's own artist names under half of them. Replayed on 930 imported slskd folders: none rejected.
    // Kept: featured guests, Various Artists, splits (folder says "split" or lists the artists), "0101"/"A2"/"Album (2003) 03"
    // numbering, and work prefixes ("Sergio Leone Suite - Deborah's Theme" for "Sergio Leone Suite: Deborah's Theme").
    public static string? OtherArtists(IReadOnlyList<SlskdFileData> files, IReadOnlyList<string>? tracks, string? artist)
    {
        string own = SlskdWantedFileFilter.Normalize(artist);
        if (own.Length == 0 || tracks is not { Count: > 0 } || SlskdTextProcessor.IsVariousArtists(artist!))
            return null;

        List<HashSet<string>> trackVariants = tracks.Select(SlskdWantedFileFilter.TitleVariants).ToList();
        HashSet<string> works = tracks.Where(t => t.Contains(':')).Select(t => SlskdWantedFileFilter.Normalize(t[..t.IndexOf(':')])).ToHashSet();
        List<(string Name, string Key)> named = files.Where(SlskdWantedFileFilter.IsAudio)
            .Select(f => ArtistSegment(Stem(f), trackVariants))
            .OfType<string>()
            .Where(n => !works.Contains(SlskdWantedFileFilter.Normalize(n)))
            .Select(n => (n, Digits().Replace(SlskdWantedFileFilter.Normalize(n), "")))
            .Where(n => n.Item2.Length >= 3)
            .ToList();
        if (named.Count < 3)
            return null;

        int ownFiles = named.Count(n => SameArtist(n.Key, own));
        List<(string Name, string Key)> others = named.Where(n => !SameArtist(n.Key, own)).DistinctBy(n => n.Key).ToList();
        if (others.Count < 3 || others.Count < named.Count * 0.75 || ownFiles * 2 >= named.Count)
            return null;

        string folder = Path.GetFileName(Path.GetDirectoryName((files[0].Filename ?? "").Replace('\\', '/')) ?? "");
        string folderKey = SlskdWantedFileFilter.Normalize(folder);
        if (SplitWord().IsMatch(folder) || others.Any(o => folderKey.Contains(o.Key, StringComparison.Ordinal)))
            return null;

        return $"files by other artists ({string.Join(", ", others.Take(3).Select(o => o.Name))}{(others.Count > 3 ? ", …" : "")})";
    }

    // "01 - Machine Head - Battery" -> "Machine Head": the segment just before the one where a track title starts.
    // The title is the longest suffix equal to a track title, else the shortest that matches one loosely
    // (containment would find "Battery" in "Machine Head - Battery" too).
    private static string? ArtistSegment(string stem, List<HashSet<string>> trackVariants)
    {
        string[] parts = SegmentSeparator().Split(stem);
        List<(int Start, string Title)> suffixes = Enumerable.Range(1, Math.Max(0, parts.Length - 1))
            .Select(i => (i, SlskdWantedFileFilter.Normalize(string.Join(" - ", parts[i..]))))
            .ToList();
        int start = suffixes.Where(s => trackVariants.Any(v => v.Contains(s.Title))).Select(s => s.Start).DefaultIfEmpty(0).First();
        if (start == 0)
        {
            start = suffixes.Where(s => trackVariants.Any(v => SlskdWantedFileFilter.TitleMatches(v, [s.Title])))
                .Select(s => s.Start).DefaultIfEmpty(0).Last();
        }

        if (start == 0)
            return null;

        string segment = LeadingNumber().Replace(parts[start - 1], "").Trim();
        return SlskdWantedFileFilter.Normalize(segment).Length == 0 || PlainNumber().IsMatch(segment) ? null : segment;
    }

    // "Santana feat. Rob Thomas", "Santana & Everlast" carry the album's artist
    private static bool SameArtist(string named, string own) =>
        named.Contains(own, StringComparison.Ordinal) || (named.Length >= 4 && own.Contains(named, StringComparison.Ordinal));

    // Mean seconds between the folder's files and the closest known duration of their tracks; null when nothing compares.
    // Editions often differ by only a few seconds a track (Ihsahn orchestral: 1-9 s), inside the check's tolerance.
    public static double? Deviation(IReadOnlyList<SlskdFileData> files, IReadOnlyList<string>? tracks, IReadOnlyList<TrackLength>? lengths)
    {
        List<AudioFile>? audio = Audio(files, tracks);
        if (audio == null || tracks == null)
            return null;

        Dictionary<string, List<int>> known = Known(lengths);
        List<int> gaps = [];
        foreach (string track in tracks)
        {
            if (!known.TryGetValue(SlskdWantedFileFilter.Normalize(track), out List<int>? expected))
                continue;

            HashSet<string> variants = SlskdWantedFileFilter.TitleVariants(track);
            gaps.AddRange(audio.Where(f => f.Seconds > 0 && SlskdWantedFileFilter.TitleMatches(variants, f.Titles))
                .Select(f => expected.Min(e => Math.Abs(f.Seconds!.Value - e)))
                .Take(1));
        }

        return gaps.Count == 0 ? null : gaps.Average();
    }

    // The folder's audio files with the lengths worth comparing; null when there is nothing to check
    private static List<AudioFile>? Audio(IReadOnlyList<SlskdFileData> files, IReadOnlyList<string>? tracks)
    {
        List<AudioFile> audio = files.Where(SlskdWantedFileFilter.IsAudio)
            .Select(f => new AudioFile(SlskdWantedFileFilter.TitleCandidates(Stem(f)), ReportedLength(f)))
            .ToList();
        if (audio.Count == 0 || tracks is not { Count: > 0 } || Uninformative(files))
            return null;

        // A browsed folder (indexer's directory expansion) copies the search hit's length onto every file
        if (audio.Count > 1 && audio.All(f => f.Seconds == audio[0].Seconds))
            audio = audio.Select(f => f with { Seconds = null }).ToList();

        // A file named after several tracks holds them merged ("Twilight of the Gods, Through Blood by Thunder, ...")
        List<HashSet<string>> trackVariants = tracks.Select(SlskdWantedFileFilter.TitleVariants).ToList();
        return audio.Select(f => f.Seconds != null && trackVariants.Count(v => SlskdWantedFileFilter.TitleMatches(v, f.Titles)) > 1
            ? f with { Seconds = null }
            : f).ToList();
    }

    private static Dictionary<string, List<int>> Known(IReadOnlyList<TrackLength>? lengths) =>
        lengths?.ToDictionary(l => l.Title, l => l.Seconds) ?? [];

    // Every distinct duration each title has on any of the album's releases (editions differ: remasters, edits,
    // live versions), so a folder of another edition still fits. Null when there is nothing to compare.
    public static List<TrackLength>? LengthsOf(IEnumerable<AlbumRelease>? releases)
    {
        Dictionary<string, SortedSet<int>> byTitle = [];
        foreach (AlbumRelease release in (releases ?? []).OrderByDescending(r => r.Monitored).Take(MaxReleases))
        {
            foreach (Track track in release.Tracks?.Value ?? [])
            {
                string title = SlskdWantedFileFilter.Normalize(track.Title);
                int seconds = (int)Math.Round(track.Duration / 1000.0);
                if (title.Length == 0 || seconds <= 0)
                    continue;

                if (!byTitle.TryGetValue(title, out SortedSet<int>? set))
                    byTitle[title] = set = [];
                set.Add(seconds);
            }
        }

        return byTitle.Count == 0 ? null : byTitle.Take(MaxTitles).Select(kv => new TrackLength(kv.Key, [.. kv.Value])).ToList();
    }

    private static bool Covered(string title, List<AudioFile> audio)
    {
        HashSet<string> variants = SlskdWantedFileFilter.TitleVariants(title);
        return variants.Count > 0 && audio.Any(f => SlskdWantedFileFilter.TitleMatches(variants, f.Titles));
    }

    // Whether a file carrying the title has one of its known durations; null when nothing can be compared
    private static bool? LengthCheck(string title, List<AudioFile> audio, Dictionary<string, List<int>> known)
    {
        if (!known.TryGetValue(SlskdWantedFileFilter.Normalize(title), out List<int>? expected))
            return null;

        HashSet<string> variants = SlskdWantedFileFilter.TitleVariants(title);
        List<int> seen = audio.Where(f => f.Seconds > 0 && SlskdWantedFileFilter.TitleMatches(variants, f.Titles))
            .Select(f => f.Seconds!.Value)
            .ToList();
        if (seen.Count == 0)
            return null;

        return seen.Any(s => expected.Any(e => Math.Abs(s - e) <= DurationToleranceSec));
    }

    // "Track01.flac", "Songs Of The Dusk [1].flac": names carry no titles, so there is nothing to check
    private static bool Uninformative(IReadOnlyList<SlskdFileData> files)
    {
        List<string> names = files.Where(SlskdWantedFileFilter.IsAudio)
            .Select(f => Digits().Replace(SlskdWantedFileFilter.Normalize(Stem(f)), ""))
            .ToList();
        return names.Count >= 2 && names.GroupBy(n => n).Max(g => g.Count()) >= names.Count * 0.6;
    }

    // Peers derive an MP3's length from its size and nominal bitrate, so a VBR file reports 1.3-5x its real length
    private static int? ReportedLength(SlskdFileData file)
    {
        string extension = !string.IsNullOrWhiteSpace(file.Extension) ? file.Extension : Path.GetExtension(file.Filename ?? "");
        return extension.TrimStart('.').Equals("mp3", StringComparison.OrdinalIgnoreCase) ? null : file.Length;
    }

    private static string Stem(SlskdFileData file) =>
        Path.GetFileNameWithoutExtension((file.Filename ?? "").Replace('\\', '/'));

    [GeneratedRegex(@"\d")]
    private static partial Regex Digits();

    [GeneratedRegex(@"\s+-\s+")]
    private static partial Regex SegmentSeparator();

    [GeneratedRegex(@"^\s*\d{1,3}(?:[-.]\d{1,2})?(?:\s*[.)_-]\s*|\s+|$)")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^\d{1,3}$")]
    private static partial Regex PlainNumber();

    [GeneratedRegex(@"\bsplit\b", RegexOptions.IgnoreCase)]
    private static partial Regex SplitWord();
}

// A normalized track title and its durations in seconds across the album's releases
public record TrackLength(string Title, List<int> Seconds);
