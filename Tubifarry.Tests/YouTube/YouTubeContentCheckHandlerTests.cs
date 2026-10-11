using System.Text.Json.Nodes;
using Tubifarry.Download.Clients.YouTube;
using Xunit;

namespace Tubifarry.Tests.YouTube
{
    public class YouTubeContentCheckHandlerTests
    {
        [Fact]
        public void Player_body_acknowledges_content_checks_and_keeps_the_rest()
        {
            JsonNode body = JsonNode.Parse(YouTubeContentCheckHandler.AddContentChecks("""{"videoId":"ZDyDpdFZHBo","context":{"client":{"clientName":"WEB_REMIX"}}}"""))!;
            Assert.True((bool)body["contentCheckOk"]!);
            Assert.True((bool)body["racyCheckOk"]!);
            Assert.Equal("ZDyDpdFZHBo", (string)body["videoId"]!);
            Assert.Equal("WEB_REMIX", (string)body["context"]!["client"]!["clientName"]!);
        }

        [Fact]
        public void Non_object_body_is_left_alone()
        {
            Assert.Equal("[1]", YouTubeContentCheckHandler.AddContentChecks("[1]"));
        }
    }
}
