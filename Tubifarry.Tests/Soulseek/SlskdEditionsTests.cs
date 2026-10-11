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

        private static List<SlskdFileData> Folder(string extension, (string Title, int Seconds)[] tracks, int? reportedLength = null) =>
            [.. tracks.Select((t, i) => new SlskdFileData($@"music\Artist\Album\{i + 1:00} - {t.Title}.{extension}", null, null, 1000,
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
    }
}
