using NzbDrone.Core.Music;
using Tubifarry.Download.Clients.Soulseek;

namespace Tubifarry.Indexers.Soulseek;

// Tomvis fork (MUSIC-68): Tomvis/Lidarr lets an album monitor several releases ("editions", MUSIC-63) and picks the one a
// result is for by the words in its title. A matched folder is titled after the album, so a folder holding an extra
// edition reached Lidarr as the primary one, and the tracklist check judged every folder by the first monitored release
// only. Fit the folder to each monitored edition instead, and name the extra edition it holds in the title.
public static class SlskdEditions
{
    // Editions whose mean durations are this close tie, and a tie goes to the primary
    private const double DeviationMarginSec = 0.5;

    // Null unless the album monitors several releases (always on upstream Lidarr)
    public static List<SlskdEdition>? Of(Album? album)
    {
        List<AlbumRelease> monitored = album?.AlbumReleases?.Value?.Where(r => r.Monitored).ToList() ?? [];
        if (monitored.Count < 2)
            return null;

        // Lidarr's AlbumEditions.Primary: titled like the album, no disambiguation, lowest id
        AlbumRelease primary = monitored
            .OrderByDescending(r => string.Equals(r.Title, album!.Title, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => !string.IsNullOrWhiteSpace(r.Disambiguation))
            .ThenBy(r => r.Id)
            .First();

        return monitored.Select(r =>
        {
            List<Track> tracks = r.Tracks?.Value?.Where(t => !string.IsNullOrEmpty(t.Title)).ToList() ?? [];
            return new SlskdEdition(
                $"{r.Title} {r.Disambiguation}".Trim(),
                r.Id == primary.Id,
                [.. tracks.Select(t => t.Title).Take(50)],
                r.TrackCount,
                SlskdTracklistCheck.LengthsOf([r]),
                LidarrWantedTracks.From(tracks));
        }).ToList();
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

        int fewestGap = fits.Min(f => Math.Abs(audio - f.Edition.TrackCount));
        fits = fits.Where(f => Math.Abs(audio - f.Edition.TrackCount) == fewestGap).ToList();

        double? closest = fits.Min(f => f.Deviation);
        if (closest != null)
            fits = fits.Where(f => f.Deviation <= closest + DeviationMarginSec).ToList();

        return (fits.OrderByDescending(f => f.Edition.Primary).ThenBy(f => f.Deviation).First().Edition, null);
    }
}

// A monitored release of a multi-edition album; Label holds the words Lidarr tells it apart by
public record SlskdEdition(string Label, bool Primary, List<string> Tracks, int TrackCount, List<TrackLength>? Lengths, List<WantedTrack>? Wanted);
