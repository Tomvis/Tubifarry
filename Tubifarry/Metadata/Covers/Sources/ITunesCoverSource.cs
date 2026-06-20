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
                .WithRateLimit(2.0) // iTunes 403-blocks on rapid bursts; space requests per-host
                .Build();
            request.RequestTimeout = System.TimeSpan.FromSeconds(10);
            try
            {
                HttpResponse resp = await _http.GetAsync(request);
                using JsonDocument doc = JsonDocument.Parse(resp.Content);
                if (!doc.RootElement.TryGetProperty("results", out JsonElement results)) return null;
                foreach (JsonElement r in results.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Object) continue;
                    string artist = r.TryGetProperty("artistName", out var an) ? an.GetString() ?? "" : "";
                    string album = r.TryGetProperty("collectionName", out var cn) ? cn.GetString() ?? "" : "";
                    // guard against results[0] being a different album/artist (wrong-cover at scale)
                    if (!IsReasonableMatch(album, artist, query.AlbumTitle, query.ArtistName)) continue;
                    if (!r.TryGetProperty("artworkUrl100", out JsonElement art)) continue;
                    string raw = art.GetString() ?? string.Empty;
                    if (!raw.Contains("100x100bb")) continue;
                    // iTunes art is always square; report 3000 as the nominal high-res edge.
                    return new CoverCandidate(ToHighRes(raw), 3000, 3000, SourceKey);
                }
                return null;
            }
            catch (System.Exception ex)
            {
                _logger.Debug(ex, "iTunes cover lookup failed for {0} - {1}", query.ArtistName, query.AlbumTitle);
                return null;
            }
        }

        public static bool IsReasonableMatch(string resAlbum, string resArtist, string qAlbum, string qArtist)
        {
            bool albumOk = !string.IsNullOrEmpty(resAlbum) &&
                (resAlbum.Contains(qAlbum, System.StringComparison.OrdinalIgnoreCase) ||
                 qAlbum.Contains(resAlbum, System.StringComparison.OrdinalIgnoreCase));
            bool artistOk = !string.IsNullOrEmpty(resArtist) &&
                (resArtist.Contains(qArtist, System.StringComparison.OrdinalIgnoreCase) ||
                 qArtist.Contains(resArtist, System.StringComparison.OrdinalIgnoreCase));
            return albumOk && artistOk;
        }
    }
}
