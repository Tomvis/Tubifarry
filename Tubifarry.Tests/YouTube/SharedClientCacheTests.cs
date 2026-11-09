using System.Net;
using Tubifarry.Core.Records;
using Tubifarry.Download.Clients.YouTube;
using Xunit;

namespace Tubifarry.Tests.YouTube
{
    public class SharedClientCacheTests
    {
        private static ClientSessionInfo Session(string po, string visitor, params string[] cookieValues) =>
            new(new SessionTokens(po, visitor, System.DateTime.UtcNow.AddHours(1)),
                cookieValues.Length == 0 ? null : System.Array.ConvertAll(cookieValues, v => new Cookie("SID", v)));

        [Fact]
        public void Reuses_one_client_for_the_same_session_across_many_callers()
        {
            SharedClientCache<object> cache = new();
            int built = 0;
            object first = cache.GetOrCreate("k", Session("p", "v", "c"), _ => { built++; return new object(); });
            for (int i = 0; i < 50; i++)
                Assert.Same(first, cache.GetOrCreate("k", Session("p", "v", "c"), _ => { built++; return new object(); }));
            Assert.Equal(1, built);
        }

        [Fact]
        public void Empty_tokens_with_fresh_expiry_still_reuse()
        {
            SharedClientCache<object> cache = new();
            object a = cache.GetOrCreate("k", Session("", ""), _ => new object());
            Assert.Same(a, cache.GetOrCreate("k", Session("", ""), _ => new object()));
        }

        [Fact]
        public void Rebuilds_and_replaces_when_tokens_or_cookies_change()
        {
            SharedClientCache<object> cache = new();
            object a = cache.GetOrCreate("k", Session("p1", "v", "c"), _ => new object());
            object b = cache.GetOrCreate("k", Session("p2", "v", "c"), _ => new object());
            object c = cache.GetOrCreate("k", Session("p2", "v", "c2"), _ => new object());
            Assert.NotSame(a, b);
            Assert.NotSame(b, c);
            Assert.Equal(1, cache.Count);
            Assert.Same(c, cache.GetOrCreate("k", Session("p2", "v", "c2"), _ => new object()));
        }

        [Fact]
        public void Keys_are_independent()
        {
            SharedClientCache<object> cache = new();
            object s = cache.GetOrCreate("stream", Session("p", "v", "c"), _ => new object());
            object m = cache.GetOrCreate("meta", Session("p", "v"), _ => new object());
            Assert.NotSame(s, m);
            Assert.Equal(2, cache.Count);
        }
    }
}
