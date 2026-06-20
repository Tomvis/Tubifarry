using NLog;
using System.Text.RegularExpressions;
using Tubifarry.Core.Records;
using Tubifarry.Metadata.Lyrics.Converters;

namespace Tubifarry.Metadata.Lyrics.Providers
{
    public partial class DarkLyricsProvider(HttpClient httpClient, Logger logger, LyricsEnhancerSettings settings)
    {
        private const string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        public async Task<Lyric?> FetchLyricsAsync(string artistName, string trackTitle, string albumName)
        {
            try
            {
                string band = Slug(artistName);
                string album = Slug(albumName);

                if (string.IsNullOrEmpty(band) || string.IsNullOrEmpty(album))
                {
                    logger.Debug($"DarkLyrics: empty slug for artist '{artistName}' or album '{albumName}'");
                    return null;
                }

                string pageUrl = $"http://www.darklyrics.com/lyrics/{band}/{album}.html";
                logger.Trace($"Fetching DarkLyrics page: {pageUrl}");

                using HttpRequestMessage request = new(HttpMethod.Get, pageUrl);
                request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

                HttpResponseMessage response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    logger.Debug($"DarkLyrics page not found for {albumName} by {artistName}. Status: {response.StatusCode}");
                    return null;
                }

                string html = await response.Content.ReadAsStringAsync();
                string? lyrics = ExtractTrackLyrics(html, trackTitle);

                if (string.IsNullOrWhiteSpace(lyrics))
                    return null;

                return new PlainTextConverter().Read(lyrics);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching lyrics from DarkLyrics for track: {trackTitle} by {artistName}");
                return null;
            }
        }

        private static string Slug(string input)
        {
            string lower = input.ToLowerInvariant();
            return NonAlphanumericRegex().Replace(lower, string.Empty);
        }

        private string? ExtractTrackLyrics(string html, string trackTitle)
        {
            // Find the lyrics div
            Match lyricsDivMatch = LyricsDivRegex().Match(html);
            if (!lyricsDivMatch.Success)
            {
                logger.Debug("DarkLyrics: could not find lyrics div");
                return null;
            }

            string lyricsDiv = lyricsDivMatch.Groups[1].Value;

            // Find all h3 headers with song titles
            MatchCollection h3Matches = SongHeaderRegex().Matches(lyricsDiv);
            if (h3Matches.Count == 0)
            {
                logger.Debug("DarkLyrics: no song headers found");
                return null;
            }

            string normalizedTarget = NormalizeForComparison(trackTitle);

            for (int i = 0; i < h3Matches.Count; i++)
            {
                Match h3 = h3Matches[i];
                string songTitle = h3.Groups[1].Value.Trim();

                // Strip leading number: "N. Title" → "Title"
                string cleanTitle = SongNumberPrefixRegex().Replace(songTitle, string.Empty).Trim();
                string normalizedCandidate = NormalizeForComparison(cleanTitle);

                bool matches = string.Equals(normalizedTarget, normalizedCandidate, StringComparison.OrdinalIgnoreCase)
                    || normalizedTarget.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase)
                    || normalizedCandidate.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                    continue;

                // Extract content between this h3 and the next h3 or end markers
                int start = h3.Index + h3.Length;
                int end;

                if (i + 1 < h3Matches.Count)
                    end = h3Matches[i + 1].Index;
                else
                {
                    // Look for closing markers
                    Match endMarker = EndMarkerRegex().Match(lyricsDiv, start);
                    end = endMarker.Success ? endMarker.Index : lyricsDiv.Length;
                }

                string lyricsHtml = lyricsDiv[start..end];

                // Process HTML
                string text = BrTagRegex().Replace(lyricsHtml, "\n");
                text = AllHtmlTagsRegex().Replace(text, string.Empty);
                text = System.Web.HttpUtility.HtmlDecode(text).Trim();

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                logger.Trace($"DarkLyrics: found lyrics for '{cleanTitle}'");
                return text;
            }

            logger.Debug($"DarkLyrics: no matching song found for '{trackTitle}'");
            return null;
        }

        private static string NormalizeForComparison(string input)
            => NonAlphanumericRegex().Replace(input.ToLowerInvariant(), string.Empty);

        [GeneratedRegex(@"[^a-z0-9]", RegexOptions.Compiled)]
        private static partial Regex NonAlphanumericRegex();

        [GeneratedRegex(@"<div[^>]*class=""lyrics""[^>]*>(.*?)</div>\s*(?:<div|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex LyricsDivRegex();

        [GeneratedRegex(@"<h3><a[^>]*>([^<]+)</a></h3>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
        private static partial Regex SongHeaderRegex();

        [GeneratedRegex(@"^\d+\.\s*", RegexOptions.Compiled)]
        private static partial Regex SongNumberPrefixRegex();

        [GeneratedRegex(@"<div[^>]*class=""(?:thanks|note)""", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
        private static partial Regex EndMarkerRegex();

        [GeneratedRegex(@"<br[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex BrTagRegex();

        [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
        private static partial Regex AllHtmlTagsRegex();
    }
}
