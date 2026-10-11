using System.Text;
using System.Text.Json.Nodes;

namespace Tubifarry.Download.Clients.YouTube;

// Tomvis fork (MUSIC-64): YouTube puts some songs behind a sensitive-content interstitial ("Screaming Suicide":
// playabilityStatus CONTENT_CHECK_REQUIRED). YouTubeMusicAPI doesn't acknowledge it, so GetStreamingDataAsync throws
// "This media stream is not playable". Acknowledge it on every player request, as yt-dlp does.
public class YouTubeContentCheckHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.Content != null && request.RequestUri?.AbsolutePath.EndsWith("/player", StringComparison.Ordinal) == true)
        {
            string body = await request.Content.ReadAsStringAsync(cancellationToken);
            string? mediaType = request.Content.Headers.ContentType?.MediaType;
            request.Content = new StringContent(AddContentChecks(body), Encoding.UTF8, mediaType ?? "application/json");
        }

        return await base.SendAsync(request, cancellationToken);
    }

    public static string AddContentChecks(string body)
    {
        if (JsonNode.Parse(body) is not JsonObject json)
            return body;

        json["contentCheckOk"] = true;
        json["racyCheckOk"] = true;
        return json.ToJsonString();
    }
}
