using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using Tubifarry.Core.FFmpeg;
using Tubifarry.Core.Records;
using Tubifarry.Download.Base;
using Tubifarry.Download.Clients.Soulseek;
using YouTubeMusicAPI.Client;

namespace Tubifarry.Download.Clients.YouTube
{
    /// <summary>
    /// Interface for YouTube download manager
    /// </summary>
    public interface IYoutubeDownloadManager : IBaseDownloadManager<YouTubeDownloadRequest, YouTubeDownloadOptions, YoutubeClient>;

    /// <summary>
    /// YouTube download manager using the base download manager implementation
    /// </summary>
    public class YoutubeDownloadManager : BaseDownloadManager<YouTubeDownloadRequest, YouTubeDownloadOptions, YoutubeClient>, IYoutubeDownloadManager
    {
        private YouTubeMusicClient? _youTubeClient;
        private YouTubeMusicClient? _metadataClient;
        private SessionTokens? _sessionToken;
        private readonly IAudioProcessingService _audioProcessing;
        private readonly ITrackService _trackService;

        public YoutubeDownloadManager(IAudioProcessingService audioProcessing, ITrackService trackService, Logger logger) : base(logger)
        {
            _audioProcessing = audioProcessing;
            _trackService = trackService;
            _requesthandler.MaxParallelism = 2;
        }

        protected override async Task<YouTubeDownloadRequest> CreateDownloadRequest(
            RemoteAlbum remoteAlbum,
            IIndexer indexer,
            NamingConfig namingConfig,
            YoutubeClient provider)
        {
            await UpdateClientAsync(provider);
            (List<WantedTrack>? wanted, int trackCount) = LidarrWantedTracks.For(remoteAlbum, _trackService);

            YouTubeDownloadOptions options = new()
            {
                YouTubeMusicClient = _youTubeClient,
                MetadataClient = _metadataClient,
                SignedIn = !string.IsNullOrWhiteSpace(provider.Settings.CookiePath),
                Handler = _requesthandler,
                AudioProcessing = _audioProcessing,
                DownloadPath = provider.Settings.DownloadPath,
                Chunks = provider.Settings.Chunks,
                DelayBetweenAttemps = TimeSpan.FromSeconds(5),
                NumberOfAttempts = 2,
                RandomDelayMin = provider.Settings.RandomDelayMin,
                RandomDelayMax = provider.Settings.RandomDelayMax,
                MaxDownloadSpeed = provider.Settings.MaxDownloadSpeed * 1024,
                NamingConfig = namingConfig,
                UseID3v2_3 = provider.Settings.UseID3v2_3,
                ReEncodeOptions = (ReEncodeOptions)provider.Settings.ReEncode,
                ClientInfo = DownloadClientItemClientInfo.FromDownloadClient(provider, false),
                UseSponsorBlock = provider.Settings.UseSponsorBlock,
                SponsorBlockApiEndpoint = provider.Settings.SponsorBlockApiEndpoint,
                TrustedSessionGeneratorUrl = provider.Settings.TrustedSessionGeneratorUrl,
                IsTrack = false,
                ItemId = remoteAlbum.Release.DownloadUrl,
                WantedTracks = wanted,
                ReleaseTrackCount = trackCount
            };

            return new YouTubeDownloadRequest(remoteAlbum, options);
        }

        private async Task UpdateClientAsync(YoutubeClient provider)
        {
            if (_sessionToken?.IsValid == true)
                return;
            _sessionToken = await TrustedSessionHelper.GetTrustedSessionTokensAsync(provider.Settings.TrustedSessionGeneratorUrl);
            _youTubeClient = await TrustedSessionHelper.CreateAuthenticatedClientAsync(provider.Settings.TrustedSessionGeneratorUrl, provider.Settings.CookiePath);
            _metadataClient = await TrustedSessionHelper.CreateMetadataClientAsync(provider.Settings.TrustedSessionGeneratorUrl);
        }
    }
}