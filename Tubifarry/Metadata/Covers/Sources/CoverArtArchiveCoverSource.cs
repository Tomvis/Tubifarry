using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;

namespace Tubifarry.Metadata.Covers.Sources
{
    public class CoverArtArchiveCoverSource : ICoverSource
    {
        public const string SourceKey = "caa";
        public string Key => SourceKey;

        private readonly IHttpClient _http;
        private readonly Logger _logger;

        public CoverArtArchiveCoverSource(IHttpClient http, Logger logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>Parse a CAA index JSON; return the front image with a size hint from its largest thumbnail.</summary>
        public static CoverCandidate? ParseFront(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("images", out JsonElement images)) return null;
            foreach (JsonElement img in images.EnumerateArray())
            {
                if (!img.TryGetProperty("front", out JsonElement f) || !f.GetBoolean()) continue;
                string url = img.GetProperty("image").GetString()!;
                int size = 0;
                if (img.TryGetProperty("thumbnails", out JsonElement th))
                {
                    if (th.TryGetProperty("1200", out _)) size = 1200;
                    else if (th.TryGetProperty("500", out _)) size = 500;
                }
                return new CoverCandidate(url, size, size, SourceKey);
            }
            return null;
        }

        public async Task<CoverCandidate?> GetCoverAsync(CoverQuery query, CancellationToken ct)
        {
            string? id = query.ReleaseGroupMbId;
            string kind = "release-group";
            if (string.IsNullOrEmpty(id)) { id = query.ReleaseMbId; kind = "release"; }
            if (string.IsNullOrEmpty(id)) return null;

            HttpRequest request = new HttpRequestBuilder($"https://coverartarchive.org/{kind}/{id}")
                .Build();
            try
            {
                HttpResponse resp = await _http.GetAsync(request);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                return ParseFront(resp.Content);
            }
            catch (System.Exception ex)
            {
                _logger.Debug(ex, "CAA cover lookup failed for {0}", id);
                return null;
            }
        }
    }
}
