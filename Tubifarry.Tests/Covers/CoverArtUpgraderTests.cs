using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Tubifarry.Metadata.Covers;
using Xunit;

namespace Tubifarry.Tests.Covers
{
    public class CoverArtUpgraderTests
    {
        private sealed class FakeSource : ICoverSource
        {
            private readonly CoverCandidate? _result;
            public FakeSource(string key, CoverCandidate? result) { Key = key; _result = result; }
            public string Key { get; }
            public Task<CoverCandidate?> GetCoverAsync(CoverQuery q, CancellationToken ct) => Task.FromResult(_result);
        }

        private static CoverArtUpgrader Make(params ICoverSource[] sources)
            => new CoverArtUpgrader(sources, LogManager.GetCurrentClassLogger());

        private static readonly CoverQuery Q = new("A", "B", null, null, null);

        [Fact]
        public async Task Returns_first_source_in_order_meeting_threshold()
        {
            var up = Make(
                new FakeSource("itunes", new CoverCandidate("itunes.jpg", 3000, 3000, "itunes")),
                new FakeSource("caa", new CoverCandidate("caa.jpg", 1200, 1200, "caa")));
            var c = await up.FindBestAsync(Q, order: new[] { "itunes", "bandcamp", "caa" }, minEdge: 1000, CancellationToken.None);
            Assert.Equal("itunes.jpg", c!.Url);
        }

        [Fact]
        public async Task Skips_source_below_threshold_and_falls_through_order()
        {
            var up = Make(
                new FakeSource("itunes", new CoverCandidate("small.jpg", 500, 500, "itunes")),
                new FakeSource("caa", new CoverCandidate("caa.jpg", 1200, 1200, "caa")));
            var c = await up.FindBestAsync(Q, order: new[] { "itunes", "bandcamp", "caa" }, minEdge: 1000, CancellationToken.None);
            Assert.Equal("caa.jpg", c!.Url);
        }

        [Fact]
        public async Task Returns_null_when_nothing_meets_threshold()
        {
            var up = Make(new FakeSource("itunes", new CoverCandidate("small.jpg", 500, 500, "itunes")));
            var c = await up.FindBestAsync(Q, order: new[] { "itunes" }, minEdge: 1000, CancellationToken.None);
            Assert.Null(c);
        }

        [Fact]
        public async Task Rejects_unknown_size_candidate()
        {
            var up = Make(new FakeSource("caa", new CoverCandidate("caa.jpg", 0, 0, "caa")));
            var c = await up.FindBestAsync(Q, order: new[] { "caa" }, minEdge: 1000, CancellationToken.None);
            Assert.Null(c);
        }
    }
}
