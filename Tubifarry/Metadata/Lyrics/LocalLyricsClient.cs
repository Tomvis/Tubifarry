using NLog;
using System.Text;
using System.Text.Json;

namespace Tubifarry.Metadata.Lyrics
{
    /// <summary>
    /// Fire-and-forget enqueue hook for the local fallback tiers (lyrics-local service
    /// on .120). When the 8 online providers fail to reach the desired sync level, the
    /// enhancer hands the track off here so the GPU/CPU-heavy work (source separation +
    /// Whisper) happens out-of-process and never blocks the Lidarr metadata pipeline.
    ///
    ///   - Tier 3 (align):     plain text exists but no synced version -> POST the text.
    ///   - Tier 4 (transcribe): no text anywhere -> POST without text.
    ///
    /// The service maps the Lidarr-namespace audio path to its own NFS mount and writes
    /// the .lrc/.txt sidecar itself. Failures here are swallowed: the local tiers are a
    /// best-effort backfill, and the standalone sweep covers anything missed.
    /// </summary>
    public class LocalLyricsClient(HttpClient httpClient, Logger logger, LyricsEnhancerSettings settings)
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

        public async Task EnqueueAsync(string audioPath, string? plainText, string source)
        {
            if (string.IsNullOrWhiteSpace(settings.LocalLyricsServiceUrl))
                return;

            try
            {
                string endpoint = $"{settings.LocalLyricsServiceUrl.TrimEnd('/')}/enqueue";
                string body = JsonSerializer.Serialize(new
                {
                    audio_path = audioPath,
                    plain_text = plainText,
                    source
                });

                using CancellationTokenSource cts = new(Timeout);
                using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                using HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);
                if (response.IsSuccessStatusCode)
                    logger.Trace($"lyrics-local: enqueued [{source}] {audioPath}");
                else
                    logger.Debug($"lyrics-local: enqueue returned {response.StatusCode} for {audioPath}");
            }
            catch (Exception ex)
            {
                logger.Debug($"lyrics-local: enqueue failed for {audioPath}: {ex.Message}");
            }
        }
    }
}
