using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace Tubifarry.Metadata.Lyrics
{
    public class LyricsEnhancerSettingsValidator : AbstractValidator<LyricsEnhancerSettings>
    {
        public LyricsEnhancerSettingsValidator()
        {
            // Validate LRCLIB instance URL if enabled
            RuleFor(x => x.LrcLibInstanceUrl)
                .NotEmpty()
                .When(x => x.LrcLibEnabled)
                .WithMessage("LRCLIB instance URL is required when LRCLIB provider is enabled")
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .When(x => x.LrcLibEnabled && !string.IsNullOrEmpty(x.LrcLibInstanceUrl))
                .WithMessage("LRCLIB instance URL must be a valid URL");

            // Validate Genius API key if enabled
            RuleFor(x => x.GeniusApiKey)
                .NotEmpty()
                .When(x => x.GeniusEnabled)
                .WithMessage("Genius API key is required when Genius provider is enabled");

            // Validate at least one provider is enabled
            RuleFor(x => x)
                .Must(x => x.LrcLibEnabled || x.GeniusEnabled || x.BinimumEnabled || x.LyricsPlusEnabled || x.UnisonEnabled || x.NetEaseEnabled || x.DarkLyricsEnabled || x.MetalArchivesEnabled)
                .WithMessage("At least one lyrics provider must be enabled");

            // Validate UpdateInterval when scheduled updates are enabled
            RuleFor(x => x.UpdateInterval)
                .GreaterThanOrEqualTo(7)
                .When(x => x.EnableScheduledUpdates)
                .WithMessage("Update interval must be at least 1 week");
        }
    }

    public class LyricsEnhancerSettings : IProviderConfig
    {
        private static readonly LyricsEnhancerSettingsValidator Validator = new();

        [FieldDefinition(0, Label = "Create LRC Files", Type = FieldType.Select, SelectOptions = typeof(LyricOptions), Section = MetadataSectionType.Metadata, HelpText = "Choose what kind of LRC files to create")]
        public int LrcFileOptions { get; set; } = (int)LyricOptions.OnlyLineSynced;

        [FieldDefinition(1, Label = "Line Synced File Type", Type = FieldType.Select, SelectOptions = typeof(LineSyncedFileType), Section = MetadataSectionType.Metadata, HelpText = "File format for plain text and line-synced lyrics")]
        public int LineSyncedFileTypeOption { get; set; } = (int)LineSyncedFileType.Lrc;

        [FieldDefinition(2, Label = "Word Synced File Type", Type = FieldType.Select, SelectOptions = typeof(WordSyncedFileType), Section = MetadataSectionType.Metadata, HelpText = "File format for word-synced lyrics")]
        public int WordSyncedFileTypeOption { get; set; } = (int)WordSyncedFileType.Elrc;

        [FieldDefinition(3, Label = "Lyrics Embedding", Type = FieldType.Select, SelectOptions = typeof(LyricOptions), Section = MetadataSectionType.Metadata, HelpText = "Choose how to embed lyrics in audio files metadata")]
        public int LyricEmbeddingOption { get; set; } = (int)LyricOptions.Disabled;

        [FieldDefinition(4, Label = "Overwrite Existing LRC Files", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Overwrite existing LRC files")]
        public bool OverwriteExistingLrcFiles { get; set; }

        // LRCLIB Provider settings
        [FieldDefinition(5, Label = "Enable LRCLIB", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use LRCLIB as a lyrics provider (provides synced lyrics)")]
        public bool LrcLibEnabled { get; set; }

        [FieldDefinition(6, Label = "LRCLIB Instance URL", Type = FieldType.Url, Section = MetadataSectionType.Metadata, HelpText = "URL of the LRCLIB instance to use", Placeholder = "https://lrclib.net")]
        public string LrcLibInstanceUrl { get; set; } = "https://lrclib.net";

        // Genius Provider settings
        [FieldDefinition(7, Label = "Enable Genius", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use Genius as a lyrics provider (text only, no synced lyrics)")]
        public bool GeniusEnabled { get; set; }

        [FieldDefinition(8, Label = "Genius API Key", Type = FieldType.Textbox, Section = MetadataSectionType.Metadata, HelpText = "Your Genius API key", Privacy = PrivacyLevel.ApiKey)]
        public string GeniusApiKey { get; set; } = "";

        // Binimum Provider settings (ISRC-keyed Apple Music TTML cache)
        [FieldDefinition(9, Label = "Enable Binimum", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use Binimum as a lyrics provider")]
        public bool BinimumEnabled { get; set; }

        [FieldDefinition(10, Label = "Binimum API URL", Type = FieldType.Url, Section = MetadataSectionType.Metadata, HelpText = "URL of the Binimum lyrics API", Placeholder = "https://lyrics-api.binimum.org", Hidden = HiddenType.Hidden)]
        public string BinimumApiUrl { get; set; } = "https://lyrics-api.binimum.org";

        // LyricsPlus Provider settings (Apple Music + QQ, word-synced)
        [FieldDefinition(11, Label = "Enable LyricsPlus", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use LyricsPlus as a lyrics provider")]
        public bool LyricsPlusEnabled { get; set; }

        [FieldDefinition(12, Label = "LyricsPlus URL", Type = FieldType.Url, Section = MetadataSectionType.Metadata, HelpText = "URL of the LyricsPlus instance", Placeholder = "https://lyrics.geeked.wtf", Hidden = HiddenType.Hidden)]
        public string LyricsPlusUrl { get; set; } = "https://lyrics.geeked.wtf";

        // Unison Provider settings (crowdsourced TTML/LRC)
        [FieldDefinition(13, Label = "Enable Unison", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use Unison as a lyrics provider (crowdsourced synced lyrics; small catalog)")]
        public bool UnisonEnabled { get; set; }

        [FieldDefinition(14, Label = "Unison URL", Type = FieldType.Url, Section = MetadataSectionType.Metadata, HelpText = "URL of the Unison API instance", Placeholder = "https://unison.boidu.dev", Hidden = HiddenType.Hidden)]
        public string UnisonUrl { get; set; } = "https://unison.boidu.dev";

        // Scheduled Update Settings
        [FieldDefinition(15, Label = "Enable Scheduled Updates", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Enable automatic scheduled updates to refresh lyrics for existing files")]
        public bool EnableScheduledUpdates { get; set; }

        [FieldDefinition(16, Label = "Update Interval", Type = FieldType.Number, Unit = "days", Section = MetadataSectionType.Metadata, HelpText = "How often to run scheduled lyrics updates.")]
        public int UpdateInterval { get; set; } = 7;

        // NetEase Provider settings
        [FieldDefinition(17, Label = "Enable NetEase", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use NetEase Cloud Music as a lyrics provider (synced; strong coverage incl. metal)")]
        public bool NetEaseEnabled { get; set; }

        // DarkLyrics Provider settings
        [FieldDefinition(18, Label = "Enable DarkLyrics", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use DarkLyrics as a lyrics provider (plain text; metal archive)")]
        public bool DarkLyricsEnabled { get; set; }

        // Metal Archives Provider settings
        [FieldDefinition(19, Label = "Enable Metal Archives", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Use Encyclopaedia Metallum (plain text; requires FlareSolverr for Cloudflare)")]
        public bool MetalArchivesEnabled { get; set; }

        [FieldDefinition(20, Label = "FlareSolverr URL", Type = FieldType.Url, Section = MetadataSectionType.Metadata, HelpText = "FlareSolverr endpoint used to bypass Cloudflare for Metal Archives", Placeholder = "http://localhost:8191")]
        public string FlareSolverrUrl { get; set; } = string.Empty;

        // Local fallback tiers (lyrics-local service) — Tier 3 forced alignment & Tier 4
        // transcription. When the online providers fail, the enhancer hands the track off
        // to an out-of-process service that does source separation + Whisper. Heavy work
        // never blocks the metadata pipeline; the service writes the .lrc/.txt sidecar.
        [FieldDefinition(21, Label = "Enable Local Lyric Tiers", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Hand tracks the online providers couldn't sync to the lyrics-local service (forced alignment / transcription)")]
        public bool LocalLyricsEnabled { get; set; }

        [FieldDefinition(22, Label = "lyrics-local Service URL", Type = FieldType.Url, Section = MetadataSectionType.Metadata, HelpText = "Base URL of the lyrics-local service", Placeholder = "http://10.0.0.120:8585")]
        public string LocalLyricsServiceUrl { get; set; } = "http://10.0.0.120:8585";

        [FieldDefinition(23, Label = "Local Tier 3 (Forced Alignment)", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "When plain lyrics exist but no synced version, align the text to the audio to produce an .lrc")]
        public bool LocalAlignEnabled { get; set; } = true;

        [FieldDefinition(24, Label = "Local Tier 4 (Transcription)", Type = FieldType.Checkbox, Section = MetadataSectionType.Metadata, HelpText = "Last resort: when no lyric text exists anywhere, transcribe the vocals (labelled low-confidence)")]
        public bool LocalTranscribeEnabled { get; set; } = true;

        public LyricsEnhancerSettings() => Instance = this;

        public static LyricsEnhancerSettings? Instance { get; private set; }

        public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
    }

    /// <summary>
    /// Command for scheduled lyrics update task.
    /// </summary>
    public class LyricsUpdateCommand : Command
    {
        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => true;

        public override string CompletionMessage => _completionMessage ?? "Lyrics update completed";
        private string? _completionMessage;

        public void SetCompletionMessage(string message) => _completionMessage = message;
    }

    public enum LyricOptions
    {
        [FieldOption(Label = "Disabled", Hint = "Disabled")]
        Disabled,

        [FieldOption(Label = "Only Plain", Hint = "Use plain text lyrics if available.")]
        OnlyPlain,

        [FieldOption(Label = "Only Line Synced", Hint = "Use line-synced lyrics if available.")]
        OnlyLineSynced,

        [FieldOption(Label = "Prefer Line Synced", Hint = "Use line-synced lyrics if available, fall back to plain text.")]
        PreferLineSynced,

        [FieldOption(Label = "Only Word Synced", Hint = "Use word-synced lyrics if available.")]
        OnlyWordSynced,

        [FieldOption(Label = "Prefer Word Synced", Hint = "Use word-synced lyrics if available, fall back to line-synced or plain text.")]
        PreferWordSynced
    }

    public enum WordSyncedFileType
    {
        [FieldOption(Label = "ELRC", Hint = "Enhanced LRC format with word-level timestamps.")]
        Elrc = 0,

        [FieldOption(Label = "TTML", Hint = "Timed Text Markup Language format.")]
        Ttml = 1,

        [FieldOption(Label = "Lyricsfile", Hint = "YAML-based open lyrics format with word-by-word sync support.")]
        Lyricsfile = 2
    }

    public enum LineSyncedFileType
    {
        [FieldOption(Label = "LRC", Hint = "Standard LRC file format for line-synced and plain text lyrics.")]
        Lrc,

        [FieldOption(Label = "Use Same as Word Synced", Hint = "Use the same file type as configured for word-synced lyrics.")]
        UseSameAsWordSynced
    }
}