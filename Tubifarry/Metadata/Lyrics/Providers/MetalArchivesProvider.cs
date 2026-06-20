using NLog;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tubifarry.Core.Records;
using Tubifarry.Metadata.Lyrics.Converters;

namespace Tubifarry.Metadata.Lyrics.Providers
{
    public partial class MetalArchivesProvider(HttpClient httpClient, Logger logger, LyricsEnhancerSettings settings)
    {
        private const string _instrumentalMarker = "instrumental";
        private const string _notAvailableMarker = "lyrics not available";

        public async Task<Lyric?> FetchLyricsAsync(string artistName, string trackTitle, string albumName)
        {
            if (string.IsNullOrWhiteSpace(settings.FlareSolverrUrl))
                return null;

            try
            {
                string searchUrl = $"https://www.metal-archives.com/search/ajax-advanced/searching/songs/?songTitle={Uri.EscapeDataString(trackTitle)}&bandName={Uri.EscapeDataString(artistName)}&releaseTitle={Uri.EscapeDataString(albumName)}";
                logger.Trace($"Searching Metal Archives via FlareSolverr: {searchUrl}");

                string? solved = await SolveAsync(searchUrl);
                if (string.IsNullOrWhiteSpace(solved))
                    return null;

                string? lyricsId = ExtractLyricsId(solved, trackTitle, albumName);
                if (string.IsNullOrWhiteSpace(lyricsId))
                {
                    logger.Debug($"Metal Archives: no matching song found for '{trackTitle}' on album '{albumName}'");
                    return null;
                }

                string lyricsUrl = $"https://www.metal-archives.com/release/ajax-view-lyrics/id/{lyricsId}";
                logger.Trace($"Fetching Metal Archives lyrics via FlareSolverr: {lyricsUrl}");

                string? solved2 = await SolveAsync(lyricsUrl);
                if (string.IsNullOrWhiteSpace(solved2))
                    return null;

                string? lyrics = ExtractLyricsFromPage(solved2);
                if (string.IsNullOrWhiteSpace(lyrics))
                    return null;

                if (lyrics.Contains(_instrumentalMarker, StringComparison.OrdinalIgnoreCase)
                    || lyrics.Contains(_notAvailableMarker, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Debug($"Metal Archives: lyrics not available or instrumental for '{trackTitle}'");
                    return null;
                }

                return new PlainTextConverter().Read(lyrics);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching lyrics from Metal Archives for track: {trackTitle} by {artistName}");
                return null;
            }
        }

        private async Task<string?> SolveAsync(string url)
        {
            string endpoint = $"{settings.FlareSolverrUrl.TrimEnd('/')}/v1";

            string body = JsonSerializer.Serialize(new { cmd = "request.get", url, maxTimeout = 60000 });

            using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"FlareSolverr request failed for {url}. Status: {response.StatusCode}");
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("solution", out JsonElement solution))
                return null;

            if (!solution.TryGetProperty("response", out JsonElement responseEl))
                return null;

            return responseEl.GetString();
        }

        private string? ExtractLyricsId(string solved, string trackTitle, string albumName)
        {
            // The browser may render JSON inside <pre> tags or with HTML encoding
            string decoded = System.Web.HttpUtility.HtmlDecode(solved);

            // Extract JSON object from the response
            Match jsonMatch = JsonObjectRegex().Match(decoded);
            if (!jsonMatch.Success)
            {
                logger.Debug("Metal Archives: could not find JSON object in FlareSolverr response");
                return null;
            }

            using JsonDocument doc = JsonDocument.Parse(jsonMatch.Value);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("aaData", out JsonElement aaData) || aaData.ValueKind != JsonValueKind.Array)
                return null;

            string normalizedTrack = NormalizeForComparison(trackTitle);
            string normalizedAlbum = NormalizeForComparison(albumName);

            foreach (JsonElement row in aaData.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                    continue;

                JsonElement[] cells = row.EnumerateArray().ToArray();
                if (cells.Length < 4)
                    continue;

                // cell[1] = album link HTML, cell[3] = song title HTML
                string albumCell = System.Web.HttpUtility.HtmlDecode(cells[1].GetString() ?? string.Empty);
                string songCell = System.Web.HttpUtility.HtmlDecode(cells[3].GetString() ?? string.Empty);

                // Extract text from HTML links
                string albumText = AllHtmlTagsRegex().Replace(albumCell, string.Empty).Trim();
                string songText = AllHtmlTagsRegex().Replace(songCell, string.Empty).Trim();

                string normalizedAlbumText = NormalizeForComparison(albumText);
                string normalizedSongText = NormalizeForComparison(songText);

                bool albumMatches = normalizedAlbumText.Contains(normalizedAlbum, StringComparison.OrdinalIgnoreCase)
                    || normalizedAlbum.Contains(normalizedAlbumText, StringComparison.OrdinalIgnoreCase);

                bool trackMatches = string.Equals(normalizedSongText, normalizedTrack, StringComparison.OrdinalIgnoreCase)
                    || normalizedSongText.Contains(normalizedTrack, StringComparison.OrdinalIgnoreCase)
                    || normalizedTrack.Contains(normalizedSongText, StringComparison.OrdinalIgnoreCase);

                if (!albumMatches || !trackMatches)
                    continue;

                // Search all cells for lyricsLink_<id>
                foreach (JsonElement cell in cells)
                {
                    string cellText = System.Web.HttpUtility.HtmlDecode(cell.GetString() ?? string.Empty);
                    Match lyricsMatch = LyricsLinkRegex().Match(cellText);
                    if (lyricsMatch.Success)
                        return lyricsMatch.Groups[1].Value;
                }
            }

            return null;
        }

        private static string? ExtractLyricsFromPage(string solved)
        {
            // Extract <body> content if present, otherwise use full content
            Match bodyMatch = BodyContentRegex().Match(solved);
            string content = bodyMatch.Success ? bodyMatch.Groups[1].Value : solved;

            string text = BrTagRegex().Replace(content, "\n");
            text = AllHtmlTagsRegex().Replace(text, string.Empty);
            text = System.Web.HttpUtility.HtmlDecode(text).Trim();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string NormalizeForComparison(string input)
            => NonAlphanumericRegex().Replace(input.ToLowerInvariant(), string.Empty);

        [GeneratedRegex(@"\{.*\}", RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex JsonObjectRegex();

        [GeneratedRegex(@"lyricsLink_(\d+)", RegexOptions.Compiled)]
        private static partial Regex LyricsLinkRegex();

        [GeneratedRegex(@"<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex BodyContentRegex();

        [GeneratedRegex(@"<br[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex BrTagRegex();

        [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
        private static partial Regex AllHtmlTagsRegex();

        [GeneratedRegex(@"[^a-z0-9]", RegexOptions.Compiled)]
        private static partial Regex NonAlphanumericRegex();
    }
}
