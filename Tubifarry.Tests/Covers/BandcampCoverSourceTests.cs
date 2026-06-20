using Tubifarry.Metadata.Covers.Sources;
using Xunit;

namespace Tubifarry.Tests.Covers
{
    public class BandcampCoverSourceTests
    {
        [Fact]
        public void Builds_original_art_url_from_art_id()
            => Assert.Equal("https://f4.bcbits.com/img/a3460825363_0.jpg",
                            BandcampCoverSource.ArtUrl(3460825363));

        [Theory]
        [InlineData("Vermis", "Ulcerate", "Vermis", "Ulcerate", true)]
        [InlineData("Some Other Album", "Ulcerate", "Vermis", "Ulcerate", false)]
        public void Match_gate_requires_album_and_artist_overlap(
            string resName, string resBand, string qAlbum, string qArtist, bool expected)
            => Assert.Equal(expected, BandcampCoverSource.IsReasonableMatch(resName, resBand, qAlbum, qArtist));

        [Fact]
        public void Key_is_bandcamp() => Assert.Equal("bandcamp", BandcampCoverSource.SourceKey);
    }
}
