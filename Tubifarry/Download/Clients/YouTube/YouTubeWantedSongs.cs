using Tubifarry.Download.Clients.Soulseek;

namespace Tubifarry.Download.Clients.YouTube;

// Tomvis fork (MUSIC-64): a YouTube Music album song, reduced to what the wanted-track matcher needs.
public record YouTubeSong(string? Title, int? Number, int DurationMs);

// Same rules as the slskd and Tidal matchers: title + duration, else position on an equal-length tracklist,
// else near-identical spelling. Titles are normalized by SlskdWantedFileFilter.
public static class YouTubeWantedSongFilter
{
    private const int DurationToleranceMs = 10_000;

    // Indexes of the songs to download in album order, or null for the whole album: nothing is unwanted, or a
    // wanted track has no match (a partial grab would leave it missing and the next search would re-grab it).
    public static List<int>? Select(IReadOnlyList<YouTubeSong> songs, IReadOnlyList<WantedTrack>? wanted, int releaseTrackCount)
    {
        if (wanted == null || wanted.Count == 0)
            return null;

        List<(int Index, YouTubeSong Song, string Title)> candidates = songs
            .Select((song, i) => (i, song, SlskdWantedFileFilter.Normalize(song.Title)))
            .ToList();

        SortedSet<int> picked = [];
        foreach (WantedTrack want in wanted)
        {
            int? match = ByTitle(candidates, want) ?? ByPosition(candidates, want, releaseTrackCount) ?? ByFuzzyTitle(candidates, want);
            if (match == null)
                return null;

            picked.Add(match.Value);
        }

        return [.. picked];
    }

    private static int? ByTitle(List<(int Index, YouTubeSong Song, string Title)> candidates, WantedTrack want)
    {
        string title = SlskdWantedFileFilter.Normalize(want.Title);
        if (title.Length == 0)
            return null;

        List<(int Index, YouTubeSong Song, string Title)> hits = candidates.Where(c => c.Title == title && DurationFits(c.Song, want)).ToList();
        if (hits.Count == 0)
            return null;

        int? samePosition = hits.Where(c => want.Disc <= 1 && c.Song.Number == want.Number).Select(c => (int?)c.Index).FirstOrDefault();
        return samePosition ?? hits.MinBy(c => DurationGap(c.Song, want)).Index;
    }

    // Same tracklist length: trust the number when the durations agree (titles differ by translation etc.).
    // YouTube numbers songs across the whole album, so only a first-disc track can be placed this way.
    private static int? ByPosition(List<(int Index, YouTubeSong Song, string Title)> candidates, WantedTrack want, int releaseTrackCount)
    {
        if (candidates.Count != releaseTrackCount || want.DurationMs <= 0 || want.Disc > 1)
            return null;

        return candidates.Where(c => c.Song.Number == want.Number && c.Song.DurationMs > 0 && DurationFits(c.Song, want))
            .Select(c => (int?)c.Index)
            .FirstOrDefault();
    }

    private static int? ByFuzzyTitle(List<(int Index, YouTubeSong Song, string Title)> candidates, WantedTrack want)
    {
        HashSet<string> variants = SlskdWantedFileFilter.TitleVariants(want.Title);
        return candidates.Where(c => c.Title.Length > 0 && DurationFits(c.Song, want) && SlskdWantedFileFilter.TitleMatches(variants, [c.Title]))
            .OrderBy(c => DurationGap(c.Song, want))
            .Select(c => (int?)c.Index)
            .FirstOrDefault();
    }

    private static bool DurationFits(YouTubeSong song, WantedTrack want) =>
        want.DurationMs <= 0 || song.DurationMs <= 0 || DurationGap(song, want) <= DurationToleranceMs;

    private static int DurationGap(YouTubeSong song, WantedTrack want) => Math.Abs(song.DurationMs - want.DurationMs);
}
