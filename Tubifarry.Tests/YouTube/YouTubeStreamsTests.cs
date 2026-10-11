using System;
using System.Collections.Generic;
using Tubifarry.Core.Utilities;
using Tubifarry.Download.Clients.YouTube;
using Xunit;
using YouTubeMusicAPI.Models.Streaming;

namespace Tubifarry.Tests.YouTube
{
    public class YouTubeStreamsTests
    {
        private static AudioStreamInfo Audio(int itag, string codecs, int peakBitrate, long bytes, int seconds = 200) =>
            new(itag, "u", new StreamContainer(true, false, codecs.StartsWith("mp4a") ? "audio/mp4" : "audio/webm", codecs),
                DateTime.MinValue, TimeSpan.FromSeconds(seconds), bytes, peakBitrate, "AUDIO_QUALITY_HIGH", 48000, 2, 0);

        [Fact]
        public void Prefers_premium_aac_over_opus_with_a_higher_peak()
        {
            List<StreamInfo> streams = [Audio(251, "opus", 140_000, 3_100_000), Audio(774, "opus", 310_000, 6_200_000), Audio(140, "mp4a.40.2", 130_000, 3_240_000), Audio(141, "mp4a.40.2", 262_000, 6_440_000)];
            Assert.Equal(141, YouTubeStreams.BestAudio(streams)!.Itag);
        }

        [Fact]
        public void Falls_back_to_the_highest_stream_without_aac()
        {
            List<StreamInfo> streams = [Audio(251, "opus", 140_000, 3_100_000), Audio(250, "opus", 70_000, 1_500_000)];
            Assert.Equal(251, YouTubeStreams.BestAudio(streams)!.Itag);
        }

        [Fact]
        public void Average_bitrate_rounds_premium_aac_to_256()
        {
            AudioStreamInfo premium = Audio(141, "mp4a.40.2", 262_000, 6_440_000);
            Assert.Equal(256, AudioFormatHelper.RoundToStandardBitrate(YouTubeStreams.AverageKbps(premium)));
        }
    }
}
