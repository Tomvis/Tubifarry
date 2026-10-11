using YouTubeMusicAPI.Models.Streaming;

namespace Tubifarry.Download.Clients.YouTube;

// Tomvis fork (MUSIC-64): an audio stream, reduced to what stream selection needs (testable without the
// plugin's merged YouTubeMusicAPI types).
public record YouTubeStream(string? Codecs, int PeakBitrate, long Bytes, TimeSpan Duration);

// Pick the AAC stream (141 with Premium, else 140) over Opus. Opus (774/251) has the higher peak bitrate, so
// "highest Bitrate" chose it, labelled it AAC 320 and saved it as .m4a; Lidarr has no Opus quality.
public static class YouTubeStreams
{
    public static AudioStreamInfo? BestAudio(IEnumerable<StreamInfo> streams)
    {
        List<AudioStreamInfo> audio = streams.OfType<AudioStreamInfo>().ToList();
        int? best = BestIndex(audio.Select(Reduce).ToList());
        return best == null ? null : audio[best.Value];
    }

    public static int AverageKbps(AudioStreamInfo stream) => AverageKbps(Reduce(stream));

    public static int? BestIndex(IReadOnlyList<YouTubeStream> streams)
    {
        IEnumerable<(YouTubeStream Stream, int Index)> indexed = streams.Select((s, i) => (s, i)).ToList();
        return indexed.Where(x => IsAac(x.Stream)).OrderByDescending(x => x.Stream.PeakBitrate).Select(x => (int?)x.Index).FirstOrDefault()
            ?? indexed.OrderByDescending(x => x.Stream.PeakBitrate).Select(x => (int?)x.Index).FirstOrDefault();
    }

    // Average kbps from size and duration; the peak bitrate overstates it
    public static int AverageKbps(YouTubeStream stream) =>
        stream.Bytes > 0 && stream.Duration.TotalSeconds > 0
            ? (int)(stream.Bytes * 8 / stream.Duration.TotalSeconds / 1000)
            : stream.PeakBitrate / 1000;

    private static bool IsAac(YouTubeStream stream) => stream.Codecs?.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase) == true;

    private static YouTubeStream Reduce(AudioStreamInfo s) => new(s.Container?.Codecs, s.Bitrate, s.ContentLenght, s.Duration);
}
