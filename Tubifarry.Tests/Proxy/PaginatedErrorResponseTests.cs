using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NSubstitute;
using NzbDrone.Common.Http;
using Tubifarry.Metadata.Proxy.MetadataProvider.Deezer;
using Tubifarry.Metadata.Proxy.MetadataProvider.Discogs;
using Xunit;

namespace Tubifarry.Tests.Proxy
{
    // MUSIC-10: an error response made the paginated fetch call TryGetProperty on a default JsonElement,
    // which threw InvalidOperationException and 500'd Lidarr's artist lookup.
    public class PaginatedErrorResponseTests
    {
        private static IHttpClient ClientReturning(HttpStatusCode status, string body)
        {
            IHttpClient client = Substitute.For<IHttpClient>();
            client.GetAsync(Arg.Any<HttpRequest>())
                .Returns(ci => Task.FromResult(new HttpResponse(ci.Arg<HttpRequest>(), new HttpHeader(), body, status)));
            return client;
        }

        [Fact]
        public async Task Discogs_search_with_rejected_token_returns_empty()
        {
            DiscogsApiService api = new(ClientReturning(HttpStatusCode.Unauthorized,
                "{\"message\":\"Invalid consumer token. Please register an app before making requests.\"}"), "Test/1.0")
            { AuthToken = "revoked" };

            List<DiscogsSearchItem> results = await api.SearchAsync(new DiscogsSearchParameter(Query: "Steven Wilson", Type: "artist"));

            Assert.Empty(results);
        }

        [Fact]
        public async Task Discogs_artist_releases_with_rejected_token_returns_empty()
        {
            DiscogsApiService api = new(ClientReturning(HttpStatusCode.Unauthorized, "{\"message\":\"Invalid consumer token.\"}"), "Test/1.0");

            Assert.Empty(await api.GetArtistReleasesAsync(12345));
        }

        [Fact]
        public async Task Deezer_search_with_error_status_returns_empty()
        {
            DeezerApiService api = new(ClientReturning(HttpStatusCode.InternalServerError, "{\"error\":{\"message\":\"boom\"}}"), "Test/1.0");

            List<DeezerSearchItem>? results = await api.SearchAsync(new DeezerSearchParameter(Query: "Steven Wilson"));

            Assert.NotNull(results);
            Assert.Empty(results);
        }
    }
}
