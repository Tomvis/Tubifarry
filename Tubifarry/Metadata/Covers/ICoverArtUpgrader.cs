using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tubifarry.Metadata.Covers
{
    public interface ICoverArtUpgrader
    {
        Task<CoverCandidate?> FindBestAsync(CoverQuery query, IReadOnlyList<string> order, int minEdge, CancellationToken ct);
    }
}
