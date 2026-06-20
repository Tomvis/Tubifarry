using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Tubifarry.Metadata.Covers
{
    public class CoverArtUpgrader : ICoverArtUpgrader
    {
        private readonly Dictionary<string, ICoverSource> _sources;
        private readonly Logger _logger;

        public CoverArtUpgrader(IEnumerable<ICoverSource> sources, Logger logger)
        {
            _sources = sources.ToDictionary(s => s.Key, System.StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        public async Task<CoverCandidate?> FindBestAsync(CoverQuery query, IReadOnlyList<string> order, int minEdge, CancellationToken ct)
        {
            foreach (string key in order)
            {
                if (!_sources.TryGetValue(key, out ICoverSource? src)) continue;
                CoverCandidate? c = await src.GetCoverAsync(query, ct);
                if (c != null && c.MinEdge >= minEdge)
                {
                    _logger.Debug("High-res cover for {0} - {1} from {2} ({3}px)",
                        query.ArtistName, query.AlbumTitle, c.Source, c.MinEdge);
                    return c;
                }
            }
            return null;
        }
    }
}
