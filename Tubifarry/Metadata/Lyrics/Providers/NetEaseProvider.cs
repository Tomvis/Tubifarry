using NLog;
using System.Text.Json;
using Tubifarry.Core.Records;
using Tubifarry.Metadata.Lyrics.Converters;

namespace Tubifarry.Metadata.Lyrics.Providers
{
    public class NetEaseProvider(HttpClient httpClient, Logger logger, LyricsEnhancerSettings settings)
    {
        private const string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private const string _referer = "https://music.163.com";

        public async Task<Lyric?> FetchLyricsAsync(string artistName, string trackTitle, string albumName, int durationSeconds)
        {
            try
            {
                long? songId = await SearchSongAsync(artistName, trackTitle);
                if (songId == null)
                    return null;

                string? lrc = await FetchLrcAsync(songId.Value);
                if (string.IsNullOrWhiteSpace(lrc))
                    return null;

                return new LrcConverter().Read(lrc);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching lyrics from NetEase for track: {trackTitle} by {artistName}");
                return null;
            }
        }

        private async Task<long?> SearchSongAsync(string artistName, string trackTitle)
        {
            string searchUrl = $"https://music.163.com/api/search/get?s={Uri.EscapeDataString(artistName + " " + trackTitle)}&type=1&limit=5";
            logger.Trace($"Searching for track on NetEase: {searchUrl}");

            using HttpRequestMessage request = new(HttpMethod.Get, searchUrl);
            request.Headers.TryAddWithoutValidation("Referer", _referer);
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"NetEase search failed for {trackTitle} by {artistName}. Status: {response.StatusCode}");
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("result", out JsonElement result))
                return null;

            if (!result.TryGetProperty("songs", out JsonElement songs) || songs.ValueKind != JsonValueKind.Array)
                return null;

            long? artistMatch = null;
            long? titleMatch = null;

            foreach (JsonElement song in songs.EnumerateArray())
            {
                long id = song.GetProperty("id").GetInt64();
                string songName = song.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;

                bool titleMatches = songName.Contains(trackTitle, StringComparison.OrdinalIgnoreCase)
                    || trackTitle.Contains(songName, StringComparison.OrdinalIgnoreCase);

                bool artistMatches = false;
                if (song.TryGetProperty("artists", out JsonElement artists) && artists.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement artist in artists.EnumerateArray())
                    {
                        string artistSongName = artist.TryGetProperty("name", out JsonElement aN) ? aN.GetString() ?? string.Empty : string.Empty;
                        if (artistSongName.Contains(artistName, StringComparison.OrdinalIgnoreCase)
                            || artistName.Contains(artistSongName, StringComparison.OrdinalIgnoreCase))
                        {
                            artistMatches = true;
                            break;
                        }
                    }
                }

                if (artistMatches && titleMatches)
                    return id;

                if (artistMatch == null && artistMatches)
                    artistMatch = id;

                if (titleMatch == null && titleMatches)
                    titleMatch = id;
            }

            if (artistMatch != null)
                return artistMatch;

            return titleMatch;
        }

        private async Task<string?> FetchLrcAsync(long songId)
        {
            string lrcUrl = $"https://music.163.com/api/song/lyric?id={songId}&lv=1&kv=1&tv=-1";
            logger.Trace($"Fetching LRC from NetEase: {lrcUrl}");

            using HttpRequestMessage request = new(HttpMethod.Get, lrcUrl);
            request.Headers.TryAddWithoutValidation("Referer", _referer);
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"NetEase LRC fetch failed for song id {songId}. Status: {response.StatusCode}");
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("lrc", out JsonElement lrc))
                return null;

            if (!lrc.TryGetProperty("lyric", out JsonElement lyricEl))
                return null;

            string? lyricText = lyricEl.GetString();
            return string.IsNullOrWhiteSpace(lyricText) ? null : lyricText;
        }
    }
}
