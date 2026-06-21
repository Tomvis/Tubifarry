using Tubifarry.Metadata.Covers.Sources;
using Xunit;

namespace Tubifarry.Tests.Covers
{
    public class CoverArtArchiveCoverSourceTests
    {
        [Fact]
        public void Picks_front_image_and_1200_thumb_size()
        {
            string json = @"{ ""images"": [
                { ""front"": true, ""image"": ""http://coverartarchive.org/release-group/rg/1.jpg"",
                  ""thumbnails"": { ""500"": ""http://x/500.jpg"", ""1200"": ""http://x/1200.jpg"" } },
                { ""front"": false, ""image"": ""http://x/back.jpg"" } ] }";
            var c = CoverArtArchiveCoverSource.ParseFront(json);
            Assert.NotNull(c);
            Assert.Equal("http://coverartarchive.org/release-group/rg/1.jpg", c!.Url);
            Assert.Equal(1200, c.Width);
        }

        [Fact]
        public void Returns_null_when_no_front_image()
        {
            string json = @"{ ""images"": [ { ""front"": false, ""image"": ""http://x/b.jpg"" } ] }";
            Assert.Null(CoverArtArchiveCoverSource.ParseFront(json));
        }

        [Fact]
        public void Key_is_caa() => Assert.Equal("caa", CoverArtArchiveCoverSource.SourceKey);

        [Theory]
        [InlineData("c4a98a8e-3b1f-4f2a-9a1a-2b3c4d5e6f70", true)]
        [InlineData("12345@deezer", false)]
        [InlineData("", false)]
        public void IsMbId_validates_guid_format(string id, bool expected)
            => Assert.Equal(expected, CoverArtArchiveCoverSource.IsMbId(id));

        [Fact]
        public void ParseDiscArt_finds_Medium_image()
        {
            string json = @"{ ""images"": [
                { ""types"":[""Front""], ""image"":""http://x/front.jpg"" },
                { ""types"":[""Medium""], ""image"":""http://x/disc.jpg"" } ] }";
            Assert.Equal("http://x/disc.jpg", CoverArtArchiveCoverSource.ParseDiscArt(json));
        }

        [Fact]
        public void ParseDiscArt_null_when_no_disc()
            => Assert.Null(CoverArtArchiveCoverSource.ParseDiscArt(@"{""images"":[{""types"":[""Front""],""image"":""http://x/f.jpg""}]}"));
    }
}
