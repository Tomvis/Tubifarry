using System.Collections.Generic;
using Tubifarry.Download.Clients.Soulseek;
using Tubifarry.Download.Clients.YouTube;
using Xunit;

namespace Tubifarry.Tests.YouTube
{
    public class YouTubeWantedSongFilterTests
    {
        private static readonly List<YouTubeSong> Album =
        [
            new("72 Seasons", 1, 459_000),
            new("Shadows Follow", 2, 372_000),
            new("Screaming Suicide", 3, 330_000),
            new("Lux Æterna", 4, 205_000),
        ];

        [Fact]
        public void Nothing_unwanted_downloads_everything()
        {
            Assert.Null(YouTubeWantedSongFilter.Select(Album, null, 4));
            Assert.Null(YouTubeWantedSongFilter.Select(Album, [], 4));
        }

        [Fact]
        public void Picks_the_wanted_song_by_title()
        {
            Assert.Equal([3], YouTubeWantedSongFilter.Select(Album, [new WantedTrack("Lux Aeterna", 1, 6, 205_000)], 12));
        }

        [Fact]
        public void Edition_suffix_is_ignored()
        {
            List<YouTubeSong> songs = [new("Shadows Follow (2023 Remaster)", 2, 371_000), new("Lux Æterna - Live", 4, 205_000)];
            Assert.Equal([0], YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Shadows Follow", 1, 2, 372_000)], 12));
        }

        [Fact]
        public void Title_with_a_wrong_duration_is_rejected()
        {
            List<YouTubeSong> songs = [new("Lux Æterna", 4, 600_000)];
            Assert.Null(YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Lux Æterna", 1, 4, 205_000)], 4));
        }

        [Fact]
        public void Same_tracklist_length_falls_back_to_position()
        {
            List<YouTubeSong> songs = [new("Sjötíu og tvö", 1, 459_000), new("Skuggar", 2, 372_000)];
            Assert.Equal([1], YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Shadows Follow", 1, 2, 372_000)], 2));
        }

        [Fact]
        public void Position_needs_a_single_disc_and_a_fitting_duration()
        {
            List<YouTubeSong> songs = [new("A", 1, 100_000), new("B", 2, 200_000)];
            Assert.Null(YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Other", 2, 2, 200_000)], 2));
            Assert.Null(YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Other", 1, 2, 300_000)], 2));
        }

        [Fact]
        public void Near_identical_spelling_matches()
        {
            List<YouTubeSong> songs = [new("Gena Lollabridgida", 1, 240_000)];
            Assert.Equal([0], YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Gina Lollobrigida", 1, 1, 241_000)], 9));
        }

        [Fact]
        public void Any_unmatched_wanted_track_downloads_the_whole_album()
        {
            WantedTrack missing = new("Not On This Album", 1, 9, 100_000);
            Assert.Null(YouTubeWantedSongFilter.Select(Album, [new WantedTrack("Lux Æterna", 1, 4, 205_000), missing], 12));
        }

        [Fact]
        public void Several_wanted_tracks_keep_album_order()
        {
            List<WantedTrack> wanted = [new("Lux Æterna", 1, 4, 205_000), new("72 Seasons", 1, 1, 459_000)];
            Assert.Equal([0, 3], YouTubeWantedSongFilter.Select(Album, wanted, 12));
        }

        [Fact]
        public void Songs_without_a_number_or_title_are_skipped()
        {
            List<YouTubeSong> songs = [new(null, null, 205_000), new("Lux Æterna", null, 205_000)];
            Assert.Equal([1], YouTubeWantedSongFilter.Select(songs, [new WantedTrack("Lux Æterna", 1, 4, 205_000)], 12));
        }
    }
}
