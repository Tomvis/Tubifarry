using Tubifarry.Metadata.Covers;
using Tubifarry.Metadata.Covers.Sources;
using Xunit;

namespace Tubifarry.Tests.Covers
{
    public class ITunesCoverSourceTests
    {
        [Fact]
        public void Upgrades_artwork_url_to_max_resolution()
        {
            var hi = ITunesCoverSource.ToHighRes("https://is1.mzstatic.com/image/thumb/abc/100x100bb.jpg");
            Assert.Equal("https://is1.mzstatic.com/image/thumb/abc/3000x3000bb.jpg", hi);
        }

        [Fact]
        public void Builds_search_term_from_artist_and_album()
        {
            var term = ITunesCoverSource.BuildTerm(new CoverQuery("Ulcerate", "Vermis", null, null, null));
            Assert.Equal("Ulcerate Vermis", term);
        }

        [Fact]
        public void Key_is_itunes()
        {
            Assert.Equal("itunes", ITunesCoverSource.SourceKey);
        }

        [Theory]
        [InlineData("Currents", "In Vain", "Currents", "In Vain", true)]
        [InlineData("Currents (Deluxe Edition)", "In Vain", "Currents", "In Vain", true)]
        [InlineData("Greatest Hits", "Some Other Band", "Currents", "In Vain", false)]
        [InlineData("Currents", "Tame Impala", "Currents", "In Vain", false)]
        public void Match_gate_requires_album_and_artist_overlap(
            string resAlbum, string resArtist, string qAlbum, string qArtist, bool expected)
            => Assert.Equal(expected, ITunesCoverSource.IsReasonableMatch(resAlbum, resArtist, qAlbum, qArtist));
    }
}
