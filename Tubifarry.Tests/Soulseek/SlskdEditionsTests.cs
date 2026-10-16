using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music;
using Tubifarry.Download.Clients.Soulseek;
using Tubifarry.Indexers.Soulseek;
using Xunit;

namespace Tubifarry.Tests.Soulseek
{
    // Editions and durations from Lidarr's MusicBrainz data (MUSIC-68): BTBAM Alaska (album 24901), Ihsahn (orchestral)
    public class SlskdEditionsTests
    {
        private static readonly (string Title, int Seconds)[] AlaskaMain =
            [("All Bodies", 372), ("Alaska", 238), ("Croakies and Boatshoes", 142), ("Selkies: The Endless Obsession", 443),
             ("Breathe In, Breathe Out", 56), ("Roboturner", 427), ("Backwards Marathon", 507), ("Medicine Wheel", 258),
             ("The Primer", 286), ("Autodidact", 331), ("Laser Speed", 173)];

        private static readonly (string Title, int Seconds)[] AlaskaInstrumental =
            [("All Bodies", 373), ("Alaska", 243), ("Selkies: The Endless Obsession", 445), ("Breathe In, Breathe Out", 57),
             ("Medicine Wheel", 260), ("The Primer", 288), ("Autodidact", 332), ("Laser Speed", 173)];

        private static readonly (string Title, int Seconds)[] IhsahnMain =
            [("Cervus Venator", 79), ("The Promethean Spark", 292), ("Pilgrimage to Oblivion", 260), ("Twice Born", 217),
             ("A Taste of the Ambrosia", 263), ("Anima Extraneae", 100), ("Blood Trails to Love", 305), ("Hubris and Blue Devils", 474),
             ("The Distance Between Us", 270), ("At the Heart of All Things Broken", 553), ("Sonata Profana", 104)];

        private static readonly (string Title, int Seconds)[] IhsahnOrchestral =
            [("Cervus Venator", 83), ("The Promethean Spark", 293), ("Pilgrimage to Oblivion", 262), ("Twice Born", 219),
             ("A Taste of the Ambrosia", 261), ("Anima Extraneae", 98), ("Blood Trails to Love", 307), ("Hubris and Blue Devils", 476),
             ("The Distance Between Us", 261), ("At the Heart of All Things Broken", 550), ("Sonata Profana", 111)];

        private static AlbumRelease Release(int id, string title, bool monitored, (string Title, int Seconds)[] tracks, string disambiguation = "") => new()
        {
            Id = id,
            Title = title,
            Disambiguation = disambiguation,
            Monitored = monitored,
            TrackCount = tracks.Length,
            Tracks = new([.. tracks.Select(t => new Track { Title = t.Title, Duration = t.Seconds * 1000 })])
        };

        private static Album Album(string title, params AlbumRelease[] releases) => new() { Title = title, AlbumReleases = new([.. releases]) };

        // Instrumental's id is lower than the main release's: the primary is still the one titled like the album
        private static readonly Album Alaska = Album("Alaska",
            Release(76902, "Alaska", false, AlaskaMain),
            Release(76901, "Alaska (instrumental)", true, AlaskaInstrumental),
            Release(76903, "Alaska", true, AlaskaMain));

        private static readonly Album Ihsahn = Album("Ihsahn",
            Release(84482, "Ihsahn (orchestral)", true, IhsahnOrchestral),
            Release(84485, "Ihsahn", true, IhsahnMain));

        private static List<SlskdFileData> Folder(string extension, (string Title, int Seconds)[] tracks, int? reportedLength = null, string folder = "Album", string suffix = "") =>
            [.. tracks.Select((t, i) => new SlskdFileData($@"music\Artist\{folder}\{i + 1:00} - {t.Title}{suffix}.{extension}", null, null, 1000,
                reportedLength ?? t.Seconds, null, null, 0, false))];

        [Fact]
        public void One_monitored_release_has_no_editions()
        {
            Assert.Null(SlskdEditions.Of(Album("Alaska", Release(1, "Alaska", true, AlaskaMain), Release(2, "Alaska (instrumental)", false, AlaskaInstrumental))));
            Assert.Null(SlskdEditions.Of(null));
        }

        [Fact]
        public void Primary_is_the_release_titled_like_the_album()
        {
            List<SlskdEdition> editions = SlskdEditions.Of(Alaska)!;
            Assert.Equal(["Alaska (instrumental):extra", "Alaska:primary"], editions.Select(e => $"{e.Label}:{(e.Primary ? "primary" : "extra")}"));
        }

        [Fact]
        public void Folder_of_the_extra_edition_is_named_after_it()
        {
            (SlskdEdition? edition, _) = SlskdEditions.Fit(Folder("flac", AlaskaInstrumental), SlskdEditions.Of(Alaska)!);
            Assert.Equal("Alaska (instrumental)", edition?.Label);
        }

        [Fact]
        public void Folder_of_the_main_album_is_the_primary()
        {
            (SlskdEdition? edition, _) = SlskdEditions.Fit(Folder("flac", AlaskaMain), SlskdEditions.Of(Alaska)!);
            Assert.True(edition?.Primary);
        }

        // Ihsahn's editions differ by 1-9 s a track, inside the tracklist check's tolerance: the closer durations decide
        [Fact]
        public void Same_tracklist_editions_are_told_apart_by_durations()
        {
            List<SlskdEdition> editions = SlskdEditions.Of(Ihsahn)!;
            Assert.Equal("Ihsahn (orchestral)", SlskdEditions.Fit(Folder("flac", IhsahnOrchestral), editions).Edition?.Label);
            Assert.True(SlskdEditions.Fit(Folder("flac", IhsahnMain), editions).Edition?.Primary);
        }

        // MP3 lengths are unreliable (MUSIC-74) and a browsed folder copies one length onto every file
        [Fact]
        public void Without_durations_a_same_tracklist_folder_is_the_primary()
        {
            List<SlskdEdition> editions = SlskdEditions.Of(Ihsahn)!;
            Assert.True(SlskdEditions.Fit(Folder("mp3", IhsahnOrchestral), editions).Edition?.Primary);
            Assert.True(SlskdEditions.Fit(Folder("flac", IhsahnOrchestral, reportedLength: 300), editions).Edition?.Primary);
        }

        // Live 10-11: a "Cover" subfolder and a guitar-tabs folder were tagged as the 8-track instrumental
        [Fact]
        public void Folder_without_audio_is_the_primary()
        {
            List<SlskdFileData> covers = [new(@"music\Artist\Alaska (2005)\Cover\front.jpg", null, null, 1000, null, null, null, 0, false)];
            Assert.True(SlskdEditions.Fit(covers, SlskdEditions.Of(Alaska)!).Edition?.Primary);
        }

        [Fact]
        public void Folder_fitting_no_edition_is_rejected_with_each_reason()
        {
            List<SlskdFileData> isis = Folder("flac", [("So Did We", 400), ("Backlit", 450), ("In Fiction", 540), ("Wills Dissolve", 420),
                ("Syndic Calls", 580), ("Altered Course", 590), ("Grinning Mouths", 450), ("Holy Tears", 360)]);
            (SlskdEdition? edition, string? reason) = SlskdEditions.Fit(isis, SlskdEditions.Of(Alaska)!);
            Assert.Null(edition);
            Assert.Equal("Alaska (instrumental): titles cover 0/8 tracks; Alaska: titles cover 0/11 tracks", reason);
        }

        // Before MUSIC-68 the check used only the first monitored release, so a folder of the other edition was rejected
        [Fact]
        public void Folder_of_either_edition_passes_the_tracklist_check()
        {
            List<SlskdEdition> editions = SlskdEditions.Of(Alaska)!;
            Assert.NotNull(SlskdTracklistCheck.Reject(Folder("flac", AlaskaInstrumental), [.. AlaskaMain.Select(t => t.Title)], 11, null));
            Assert.NotNull(SlskdEditions.Fit(Folder("flac", AlaskaInstrumental), editions).Edition);
        }

        // MUSIC-104: a held edition (Tomvis/Lidarr MUSIC-102) is a bestedition target, usually the album from another
        // mastering: same title, same tracklist, durations a second or so apart
        private static AlbumRelease Mastering(int id, int year, string country, (string Title, int Seconds)[] tracks, int shift = 0) => new()
        {
            Id = id,
            Title = "Alaska",
            Disambiguation = "",
            Monitored = true,
            ReleaseDate = new DateTime(year, 1, 1),
            Country = [country],
            Label = ["Victory Records"],
            Media = [new Medium { Number = 1, Format = "CD" }],
            TrackCount = tracks.Length,
            Tracks = new([.. tracks.Select(t => new Track { Title = t.Title, Duration = (t.Seconds + shift) * 1000 })])
        };

        private static readonly (string Title, int Seconds)[] AlaskaRemaster = [.. AlaskaMain.Select(t => (t.Title, t.Seconds + 1))];

        // The held remaster has the lower id, which made it the primary before
        private static readonly Album AlaskaHeld = new()
        {
            Title = "Alaska",
            ReleaseDate = new DateTime(2005, 9, 6),
            AlbumReleases = new([Mastering(90001, 2012, "Japan", AlaskaMain, shift: 1), Mastering(76903, 2005, "United States", AlaskaMain)])
        };

        private static List<SlskdEdition> HeldEditions() => SlskdEditions.Of(AlaskaHeld, r => r.Id == 90001)!;

        private static SlskdEdition? FitHeld(string folder, string suffix = "", (string Title, int Seconds)[]? tracks = null) =>
            SlskdEditions.Fit(Folder("flac", tracks ?? AlaskaMain, folder: folder, suffix: suffix), HeldEditions()).Edition;

        [Fact]
        public void Held_edition_is_never_the_primary_and_is_named_like_Lidarr_names_it()
        {
            List<SlskdEdition> editions = HeldEditions();
            Assert.Equal(["Alaska (2012 CD Japan):held", "Alaska:primary"],
                editions.Select(e => $"{e.Label}:{(e.Primary ? "primary" : e.Held ? "held" : "extra")}"));
            Assert.Equal(2012, editions[0].Year);
        }

        // Upstream Lidarr has no Held: nothing is held and the primary is picked as before
        [Fact]
        public void Held_is_read_from_Lidarr_when_it_has_it()
        {
            List<SlskdEdition> editions = SlskdEditions.Of(AlaskaHeld)!;
            Assert.DoesNotContain(editions, e => e.Held);
            Assert.Equal(1, editions.Count(e => e.Primary));
        }

        // Durations can't tell masterings apart: the remaster's own durations still go to the primary
        [Fact]
        public void Folder_fitting_both_without_clues_is_the_primary()
        {
            Assert.True(FitHeld("Between the Buried and Me - Alaska", tracks: AlaskaRemaster)?.Primary);
            Assert.True(FitHeld("Alaska")?.Primary);
        }

        // ...and clues beat durations: the original's durations in a folder named after the remaster are the remaster
        [Theory]
        [InlineData("Alaska (2012)")]
        [InlineData("Between the Buried and Me - Alaska [2012 Remaster] FLAC")]
        [InlineData("Alaska (Remastered)")]
        [InlineData("Alaska [Japanese Edition]")]
        [InlineData("Alaska (2005) [2012 Remaster]")]
        public void Folder_naming_the_held_editions_data_is_the_held_one(string folder)
        {
            Assert.True(FitHeld(folder)?.Held);
        }

        [Fact]
        public void File_names_carry_clues_too()
        {
            Assert.True(FitHeld("Alaska", suffix: " (2012 Remaster)")?.Held);
        }

        // Victory Records and CD are on both editions, the original's year is the primary's, and a tie is no evidence
        [Theory]
        [InlineData("Alaska [CD] Victory Records")]
        [InlineData("Alaska (2005)")]
        [InlineData("Alaska (2005) [Japan]")]
        public void Clues_the_primary_shares_or_matches_are_no_evidence(string folder)
        {
            Assert.False(FitHeld(folder)?.Held);
        }

        [Fact]
        public void Two_held_editions_named_equally_go_to_the_primary()
        {
            Album album = new()
            {
                Title = "Alaska",
                ReleaseDate = new DateTime(2005, 9, 6),
                AlbumReleases = new([Mastering(90001, 2012, "Japan", AlaskaMain), Mastering(90002, 2012, "Germany", AlaskaMain), Mastering(76903, 2005, "United States", AlaskaMain)])
            };
            List<SlskdEdition> editions = SlskdEditions.Of(album, r => r.Id > 90000)!;
            Assert.True(SlskdEditions.Fit(Folder("flac", AlaskaMain, folder: "Alaska (2012 Remaster)"), editions).Edition?.Primary);
            Assert.Equal("Alaska (2012 CD Germany)", SlskdEditions.Fit(Folder("flac", AlaskaMain, folder: "Alaska (2012 German Remaster)"), editions).Edition?.Label);
        }
    }
}
