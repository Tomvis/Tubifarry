using System.Text.RegularExpressions;
using Tubifarry.Download.Clients.Soulseek;

namespace Tubifarry.Indexers.Soulseek;

// Tomvis fork (MUSIC-72): a folder that matches only by name (another album, a compilation, one stray track)
// fails import and is blocklisted hours later, or worse, imports as the wrong album. Reject it at search time
// when its file titles don't cover the release's tracklist. Replayed on 1,593 past grabs: rejects 55% of the
// failed imports; the "good" grabs it rejects were nearly all wrong content Lidarr imported anyway.
public static partial class SlskdTracklistCheck
{
    private const double MinCoverage = 0.8;

    // Why the folder doesn't fit, or null to keep it
    public static string? Reject(IReadOnlyList<SlskdFileData> files, IReadOnlyList<string>? tracks, int trackCount, IReadOnlyList<WantedTrack>? wanted)
    {
        List<HashSet<string>> audio = files.Where(SlskdWantedFileFilter.IsAudio)
            .Select(f => SlskdWantedFileFilter.TitleCandidates(Stem(f)))
            .ToList();
        if (audio.Count == 0 || tracks is not { Count: > 0 } || Uninformative(files))
            return null;

        // Partial album (MUSIC-64): every wanted track must be there; extra files are skipped at enqueue
        if (wanted is { Count: > 0 })
        {
            List<WantedTrack> missing = wanted.Where(w => !Covered(w.Title, audio)).ToList();
            return missing.Count == 0 ? null : $"lacks wanted {string.Join(", ", missing.Select(w => $"\"{w.Title}\""))}";
        }

        int matched = tracks.Count(t => Covered(t, audio));
        if (matched < tracks.Count * MinCoverage)
            return $"titles cover {matched}/{tracks.Count} tracks";

        int expected = Math.Max(trackCount, tracks.Count);
        if (audio.Count > Math.Max(2 * expected, expected + 6))
            return $"{audio.Count} audio files for {expected} tracks";

        return null;
    }

    private static bool Covered(string title, List<HashSet<string>> audio)
    {
        HashSet<string> variants = SlskdWantedFileFilter.TitleVariants(title);
        return variants.Count > 0 && audio.Any(fileTitles => SlskdWantedFileFilter.TitleMatches(variants, fileTitles));
    }

    // "Track01.flac", "Songs Of The Dusk [1].flac": names carry no titles, so there is nothing to check
    private static bool Uninformative(IReadOnlyList<SlskdFileData> files)
    {
        List<string> names = files.Where(SlskdWantedFileFilter.IsAudio)
            .Select(f => Digits().Replace(SlskdWantedFileFilter.Normalize(Stem(f)), ""))
            .ToList();
        return names.Count >= 2 && names.GroupBy(n => n).Max(g => g.Count()) >= names.Count * 0.6;
    }

    private static string Stem(SlskdFileData file) =>
        Path.GetFileNameWithoutExtension((file.Filename ?? "").Replace('\\', '/'));

    [GeneratedRegex(@"\d")]
    private static partial Regex Digits();
}
