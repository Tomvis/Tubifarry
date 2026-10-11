using YouTubeMusicAPI.Models.Streaming;

namespace Tubifarry.Download.Clients.YouTube;

// Tomvis fork (MUSIC-64): pick the AAC stream (141 with Premium, else 140) over Opus. Opus (774/251) has the higher
// peak bitrate, so "highest Bitrate" chose it, labelled it AAC 320 and saved it as .m4a; Lidarr has no Opus quality.
public static class YouTubeStreams
{
    public static AudioStreamInfo? BestAudio(IEnumerable<StreamInfo> streams)
    {
        List<AudioStreamInfo> audio = streams.OfType<AudioStreamInfo>().ToList();
        return audio.Where(IsAac).OrderByDescending(s => s.Bitrate).FirstOrDefault()
            ?? audio.OrderByDescending(s => s.Bitrate).FirstOrDefault();
    }

    // Average kbps from size and duration; Bitrate is YouTube's peak, which overstates it
    public static int AverageKbps(AudioStreamInfo stream) =>
        stream.ContentLenght > 0 && stream.Duration.TotalSeconds > 0
            ? (int)(stream.ContentLenght * 8 / stream.Duration.TotalSeconds / 1000)
            : stream.Bitrate / 1000;

    private static bool IsAac(AudioStreamInfo stream) =>
        stream.Container?.Codecs?.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase) == true;
}
