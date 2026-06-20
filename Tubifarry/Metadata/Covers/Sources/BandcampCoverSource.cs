using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;

namespace Tubifarry.Metadata.Covers.Sources
{
    public class BandcampCoverSource : ICoverSource
    {
        public const string SourceKey = "bandcamp";
        public string Key => SourceKey;

        private readonly IHttpClient _http;
        private readonly Logger _logger;

        public BandcampCoverSource(IHttpClient http, Logger logger)
        {
            _http = http;
            _logger = logger;
        }

        public static string ArtUrl(long artId) => $"https://f4.bcbits.com/img/a{artId}_0.jpg";

        public static bool IsReasonableMatch(string resName, string resBand, string qAlbum, string qArtist)
        {
            bool albumOk = !string.IsNullOrEmpty(resName) &&
                (resName.Contains(qAlbum, System.StringComparison.OrdinalIgnoreCase) ||
                 qAlbum.Contains(resName, System.StringComparison.OrdinalIgnoreCase));
            bool artistOk = !string.IsNullOrEmpty(resBand) &&
                (resBand.Contains(qArtist, System.StringComparison.OrdinalIgnoreCase) ||
                 qArtist.Contains(resBand, System.StringComparison.OrdinalIgnoreCase));
            return albumOk && artistOk;
        }

        public async Task<CoverCandidate?> GetCoverAsync(CoverQuery query, CancellationToken ct)
        {
            HttpRequest request = new HttpRequestBuilder("https://bandcamp.com/api/fuzzysearch/1/autocomplete")
                .AddQueryParam("q", $"{query.ArtistName} {query.AlbumTitle}")
                .Build();
            try
            {
                HttpResponse resp = await _http.GetAsync(request);
                using JsonDocument doc = JsonDocument.Parse(resp.Content);
                if (!doc.RootElement.TryGetProperty("auto", out JsonElement auto)) return null;
                if (!auto.TryGetProperty("results", out JsonElement results)) return null;
                foreach (JsonElement r in results.EnumerateArray())
                {
                    if (r.GetProperty("type").GetString() != "a") continue; // album
                    string name = r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string band = r.TryGetProperty("band_name", out var b) ? b.GetString() ?? "" : "";
                    if (!IsReasonableMatch(name, band, query.AlbumTitle, query.ArtistName)) continue;
                    if (!r.TryGetProperty("art_id", out JsonElement artId)) continue;
                    return new CoverCandidate(ArtUrl(artId.GetInt64()), 1500, 1500, SourceKey);
                }
                return null;
            }
            catch (System.Exception ex)
            {
                _logger.Debug(ex, "Bandcamp cover lookup failed for {0} - {1}", query.ArtistName, query.AlbumTitle);
                return null;
            }
        }
    }
}
