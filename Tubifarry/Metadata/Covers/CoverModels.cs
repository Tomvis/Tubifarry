using System.Threading;
using System.Threading.Tasks;

namespace Tubifarry.Metadata.Covers
{
    /// <summary>Identifiers used to look up a high-res cover for an album.</summary>
    public record CoverQuery(
        string ArtistName,
        string AlbumTitle,
        string? ReleaseMbId,
        string? ReleaseGroupMbId,
        string? Barcode);

    /// <summary>A candidate cover image found by a source.</summary>
    public record CoverCandidate(string Url, int Width, int Height, string Source)
    {
        public int MinEdge => Width < Height ? Width : Height;
        public bool IsSquare => Width > 0 && Height > 0
            && System.Math.Abs(Width - Height) <= (Width + Height) * 0.05 / 2;
    }

    public interface ICoverSource
    {
        /// <summary>Lower-case stable key used in the source-order setting (e.g. "itunes").</summary>
        string Key { get; }
        Task<CoverCandidate?> GetCoverAsync(CoverQuery query, CancellationToken ct);
    }
}
