using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;

namespace Tubifarry.Metadata.Covers.Sources
{
    public class ITunesCoverSource : ICoverSource
    {
        public const string SourceKey = "itunes";
        public string Key => SourceKey;

        private readonly IHttpClient _http;
        private readonly Logger _logger;

        public ITunesCoverSource(IHttpClient http, Logger logger)
        {
            _http = http;
            _logger = logger;
        }

        public static string BuildTerm(CoverQuery q) => $"{q.ArtistName} {q.AlbumTitle}".Trim();

        // Apple rejects absurd sizes (100000x100000 -> HTTP 400); 3000x3000 is the practical max that returns a real image.
        public static string ToHighRes(string artworkUrl100) =>
            artworkUrl100.Replace("100x100bb", "3000x3000bb");

        public async Task<CoverCandidate?> GetCoverAsync(CoverQuery query, CancellationToken ct)
        {
            HttpRequest request = new HttpRequestBuilder("https://itunes.apple.com/search")
                .AddQueryParam("term", BuildTerm(query))
                .AddQueryParam("entity", "album")
                .AddQueryParam("limit", "5")
                .Build();
            request.RequestTimeout = System.TimeSpan.FromSeconds(10);
            try
            {
                HttpResponse resp = await _http.GetAsync(request);
                using JsonDocument doc = JsonDocument.Parse(resp.Content);
                JsonElement results = doc.RootElement.GetProperty("results");
                JsonElement first = results.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Object) return null;
                if (!first.TryGetProperty("artworkUrl100", out JsonElement art)) return null;
                string raw = art.GetString()!;
                if (!raw.Contains("100x100bb")) return null;
                string url = ToHighRes(raw);
                // iTunes art is always square; report 3000 as the nominal high-res edge.
                return new CoverCandidate(url, 3000, 3000, SourceKey);
            }
            catch (System.Exception ex)
            {
                _logger.Debug(ex, "iTunes cover lookup failed for {0} - {1}", query.ArtistName, query.AlbumTitle);
                return null;
            }
        }
    }
}
