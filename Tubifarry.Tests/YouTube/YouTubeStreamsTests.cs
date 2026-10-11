using System;
using System.Collections.Generic;
using Tubifarry.Download.Clients.YouTube;
using Xunit;

namespace Tubifarry.Tests.YouTube
{
    public class YouTubeStreamsTests
    {
        private static YouTubeStream Audio(string codecs, int peakBitrate, long bytes) => new(codecs, peakBitrate, bytes, TimeSpan.FromSeconds(200));

        [Fact]
        public void Prefers_premium_aac_over_opus_with_a_higher_peak()
        {
            List<YouTubeStream> streams = [Audio("opus", 140_000, 3_100_000), Audio("opus", 310_000, 6_200_000), Audio("mp4a.40.2", 130_000, 3_240_000), Audio("mp4a.40.2", 262_000, 6_440_000)];
            Assert.Equal(3, YouTubeStreams.BestIndex(streams));
        }

        [Fact]
        public void Falls_back_to_the_highest_stream_without_aac()
        {
            List<YouTubeStream> streams = [Audio("opus", 70_000, 1_500_000), Audio("opus", 140_000, 3_100_000)];
            Assert.Equal(1, YouTubeStreams.BestIndex(streams));
            Assert.Null(YouTubeStreams.BestIndex([]));
        }

        [Fact]
        public void Average_bitrate_comes_from_size_not_peak()
        {
            Assert.Equal(257, YouTubeStreams.AverageKbps(Audio("mp4a.40.2", 310_000, 6_440_000)));
            Assert.Equal(310, YouTubeStreams.AverageKbps(Audio("mp4a.40.2", 310_000, 0)));
        }
    }
}
