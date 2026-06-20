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
            Assert.Equal("https://is1.mzstatic.com/image/thumb/abc/100000x100000bb.jpg", hi);
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
    }
}
