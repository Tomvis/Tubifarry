using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music;
using Tubifarry.Download.Clients.Soulseek;
using Tubifarry.Indexers.Soulseek;
using Xunit;

namespace Tubifarry.Tests.Soulseek
{
    // Folders and tracklists from real slskd grabs (MUSIC-72)
    public class SlskdTracklistCheckTests
    {
        private static List<SlskdFileData> Folder(params string[] names) =>
            [.. names.Select(n => new SlskdFileData($@"music\Artist\Album\{n}", null, null, 1000, null, null, null, 0, false))];

        private static readonly List<string> Panopticon2008 =
            ["Intro", "Flag Burner, Torch Bearer", "I, Hedonist", "…Speaking…", "The Lay of Grimnir", "Archetype", "Emma’s Song", "I.C.B.M."];

        private static readonly List<string> RiverFlowsInYou =
            ["River Flows in You", "May Be", "Love Me", "It's Your Day", "If I Could See You Again", "Destiny of Love", "Tears on Love",
             "Yellow Room", "Kiss the Rain", "Dreams", "Spring Rain", "Do You", "Because I Love You"];

        [Fact]
        public void Same_named_album_by_another_artist_is_rejected()
        {
            List<SlskdFileData> isis = Folder("01 - So Did We.flac", "02 - Backlit.flac", "03 - In Fiction.flac", "04 - Wills Dissolve.flac",
                "05 - Syndic Calls.flac", "06 - Altered Course.flac", "07 - Grinning Mouths.flac", "folder.jpg");
            Assert.Equal("titles cover 0/8 tracks", SlskdTracklistCheck.Reject(isis, Panopticon2008, 8, null));
        }

        [Fact]
        public void Compilation_holding_the_album_is_rejected()
        {
            List<SlskdFileData> box = Folder([.. RiverFlowsInYou.Select((t, i) => $"CD2 {i + 1:00} {t}.flac"),
                .. Enumerable.Range(1, 30).Select(i => $"CD1 {i:00} Piece {(char)('A' + i)}{(char)('z' - i)}.flac")]);
            Assert.Equal("43 audio files for 13 tracks", SlskdTracklistCheck.Reject(box, RiverFlowsInYou, 13, null));
        }

        [Fact]
        public void One_track_of_an_album_is_rejected()
        {
            Assert.Equal("titles cover 1/2 tracks",
                SlskdTracklistCheck.Reject(Folder("05 - The End Is Growing Near.flac"), ["Haunted America II", "The End Is Growing Near"], 2, null));
        }

        [Fact]
        public void Album_with_bonus_tracks_is_kept()
        {
            List<SlskdFileData> deluxe = Folder([.. Panopticon2008.Select((t, i) => $"{i + 1:00}. {t}.flac"), "09. Bonus Demo.flac", "10. Another Bonus.flac", "cover.jpg"]);
            Assert.Null(SlskdTracklistCheck.Reject(deluxe, Panopticon2008, 8, null));
        }

        [Fact]
        public void Spelling_and_numbering_differences_still_match()
        {
            List<SlskdFileData> roads = Folder("01 – The Echoes Of A Disharmonic Evensong.flac", "02 – Where Mountains Pierce The Sky.flac",
                "03 – The Long Road Part 1 One Last Fire.flac", "04 – The Long Road Part 2 Capricious Miles.flac",
                "05 – The Long Road Part 3 The Sigh Of Summer.flac", "06 – Norwegian Nights.flac");
            List<string> tracks = ["The Echoes of a Disharmonic Evensong", "Where Mountains Pierce the Sky", "The Long Road: 1. One Last Fire",
                "The Long Road: 2. Capricious Miles", "The Long Road: 3. The Sigh of Summer", "Norwegian Nights"];
            Assert.Null(SlskdTracklistCheck.Reject(roads, tracks, 6, null));

            List<SlskdFileData> altar = Folder("01. Altar of Plagues - 0.mp3", "02. Altar of Plagues - Question of Peril.mp3",
                "03. Altar of Plagues - Hypnotic Soundscapes.mp3", "04. Altar of Plagues - Towars a Higher State of Being.mp3",
                "05. Altar of Plagues - This Void.mp3", "06. Altar of Plagues - 00.mp3");
            Assert.Null(SlskdTracklistCheck.Reject(altar, ["0", "Questions of Peril", "Hypnotic Soundscapes", "Towards a Higher State of Being", "This Void", "00"], 6, null));
        }

        [Fact]
        public void Title_glued_to_a_tag_and_underscore_names_match()
        {
            Assert.Null(SlskdTracklistCheck.Reject(Folder("01 - DrenchXW - Ghost II.flac", "02 - FallXW - Ghost II.flac", "03 - MendXW - Ghost II.flac"),
                ["Drench", "Fall", "Mend"], 3, null));
            Assert.Null(SlskdTracklistCheck.Reject(Folder("Distorted Dreams_6am_01_6am.flac"), ["6am"], 1, null));
        }

        [Fact]
        public void Work_prefix_is_optional()
        {
            Assert.Null(SlskdTracklistCheck.Reject(Folder("01. The Mission - Gabriel's Oboe.flac", "02. The Falls.flac"),
                ["The Mission: Gabriel's Oboe", "The Mission: The Falls"], 2, null));
        }

        [Fact]
        public void Names_without_titles_are_not_judged()
        {
            Assert.Null(SlskdTracklistCheck.Reject(Folder("Insomnium - Songs Of The Dusk [1].flac", "Insomnium - Songs Of The Dusk [2].flac",
                "Insomnium - Songs Of The Dusk [3].flac"), ["Flowers of the Night", "Stained in Red", "Song of the Dusk"], 3, null));
            Assert.Null(SlskdTracklistCheck.Reject(Folder("Track01.flac", "Track02.flac"), ["A Song", "Another"], 2, null));
        }

        [Fact]
        public void No_tracklist_keeps_everything()
        {
            Assert.Null(SlskdTracklistCheck.Reject(Folder("01 - Whatever.flac"), null, 0, null));
            Assert.Null(SlskdTracklistCheck.Reject(Folder("01 - Whatever.flac"), [], 0, null));
        }

        [Fact]
        public void Partial_album_needs_only_the_wanted_tracks()
        {
            List<SlskdFileData> twoTracks = Folder("02 - Shadows Follow.flac", "06 - Lux Æterna.flac");
            List<string> tracks = ["72 Seasons", "Shadows Follow", "Screaming Suicide", "Sleepwalk My Life Away", "You Must Burn!", "Lux Æterna"];

            Assert.Null(SlskdTracklistCheck.Reject(twoTracks, tracks, 6, [new WantedTrack("Lux Æterna", 1, 6, 205_000)]));
            Assert.Equal("lacks wanted \"Screaming Suicide\"",
                SlskdTracklistCheck.Reject(twoTracks, tracks, 6, [new WantedTrack("Screaming Suicide", 1, 3, 330_000)]));
        }

        private static readonly List<string> MasterOfPuppets =
            ["Battery", "Master of Puppets", "The Thing That Should Not Be", "Welcome Home (Sanitarium)", "Disposable Heroes", "Leper Messiah", "Orion", "Damage, Inc."];

        // MUSIC-104: a covers album fits the tracklist and its durations, but each file names another artist
        [Fact]
        public void Covers_album_by_other_artists_is_rejected()
        {
            List<SlskdFileData> kerrang = Folder("01 - Machine Head - Battery.mp3", "02 - Trivium - Master Of Puppets.mp3",
                "03 - Mendeed - The Thing That Should Not Be.mp3", "04 - Bullet For My Valentine - Welcome Home (Sanitarium).mp3",
                "05 - Chimaira - Disposable Heroes.mp3", "06 - Fightstar - Leper Messiah.mp3", "07 - Mastodon - Orion (Instrumental).mp3",
                "08 - Funeral For a Friend - Damage Inc..mp3", "Kerrang! Presents 'Remastered'.jpg");
            Assert.Equal("files by other artists (Machine Head, Trivium, Mendeed, …)", SlskdTracklistCheck.OtherArtists(kerrang, MasterOfPuppets, "Metallica"));
        }

        [Fact]
        public void Files_named_after_the_album_artist_are_kept()
        {
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder([.. MasterOfPuppets.Select((t, i) => $"{i + 1:00} - Metallica - {t}.flac")]), MasterOfPuppets, "Metallica"));
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder([.. MasterOfPuppets.Select((t, i) => $"Metallica - Master of Puppets - {i + 1:00} - {t}.flac")]), MasterOfPuppets, "Metallica"));
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder([.. MasterOfPuppets.Select((t, i) => $"{i + 1:00} - {t} - Live in Seattle {1989 + i}.flac")]), MasterOfPuppets, "Metallica"));
        }

        [Fact]
        public void Guests_split_albums_and_compilations_are_kept()
        {
            List<string> supernatural = ["Yaleo", "Love of My Life", "Put Your Lights On", "Smooth", "Maria Maria"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("01 - Santana - Yaleo.flac", "02 - Santana feat. Dave Matthews - Love of My Life.flac",
                "03 - Santana & Everlast - Put Your Lights On.flac", "04 - Santana feat. Rob Thomas - Smooth.flac", "05 - Santana feat. The Product G&B - Maria Maria.flac"),
                supernatural, "Santana"));

            List<string> split = ["Panopticon I", "Panopticon II", "Bitter Spring I", "Bitter Spring II"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("01 - Panopticon - Panopticon I.flac", "02 - Panopticon - Panopticon II.flac",
                "03 - When Bitter Spring Sleeps - Bitter Spring I.flac", "04 - When Bitter Spring Sleeps - Bitter Spring II.flac"), split, "Panopticon"));

            List<SlskdFileData> various = Folder("01 - Machine Head - Battery.mp3", "02 - Trivium - Master Of Puppets.mp3", "03 - Mendeed - The Thing That Should Not Be.mp3");
            Assert.Null(SlskdTracklistCheck.OtherArtists(various, MasterOfPuppets, "Various Artists"));
            Assert.Null(SlskdTracklistCheck.OtherArtists(various, MasterOfPuppets, null));
        }

        private static List<SlskdFileData> FolderIn(string folder, params string[] names) =>
            [.. names.Select(n => new SlskdFileData($@"music\{folder}\{n}", null, null, 1000, null, null, null, 0, false))];

        // Replay of 930 imported slskd folders: none of these is a covers album
        [Fact]
        public void Numbering_works_and_chapters_are_not_artists()
        {
            List<string> elvis = ["I Got a Woman", "Blue Moon", "Tutti Frutti", "Money Honey"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("0101 - I Got a Woman.flac", "0102 - Blue Moon.flac", "0103 - Tutti Frutti.flac", "0104 - Money Honey.flac"),
                elvis, "Elvis Presley"));
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("A1 - I Got a Woman.flac", "A2 - Blue Moon.flac", "B1 - Tutti Frutti.flac", "B2 - Money Honey.flac"),
                elvis, "Elvis Presley"));
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("Marla Glen - Friends (2003) 01 - I Got a Woman.flac", "Marla Glen - Friends (2003) 02 - Blue Moon.flac",
                "Marla Glen - Friends (2003) 03 - Tutti Frutti.flac", "Marla Glen - Friends (2003) 04 - Money Honey.flac"), elvis, "Elvis Presley"));

            List<string> morricone = ["Sergio Leone Suite: Deborah's Theme", "Giuseppe Tornatore Suite: Playing Love", "Brian DePalma Suite: Main Theme", "Moses and Marco Polo Suite: Journey"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("01. Sergio Leone Suite - Deborah's Theme.flac", "02. Giuseppe Tornatore Suite - Playing Love.flac",
                "03. Brian DePalma Suite - Main Theme.flac", "04. Moses and Marco Polo Suite - Journey.flac"), morricone, "Ennio Morricone"));

            List<string> element = ["Used", "In the Flesh", "Ashes", "Morning on Earth", "Idioglossia", "Her Voices"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(Folder("01 - Chapter I - Used.flac", "02 - Chapter I - In the Flesh.flac", "03 - Chapter II - Ashes.flac",
                "04 - Chapter II - Morning on Earth.flac", "05 - Chapter III - Idioglossia.flac", "06 - Chapter III - Her Voices.flac"), element, "Pain of Salvation"));
        }

        [Fact]
        public void Split_named_by_its_folder_is_kept()
        {
            List<string> breed = ["Veil of Maya", "Martyr", "Out of the Body", "Bastard", "Lost Breed"];
            string[] files = ["01 Fear Factory - Martyr.flac", "02 Pestilence - Out of the Body.flac", "03 Cynic - Veil Of Maya.flac",
                "04 Treponem Pal - Bastard.flac", "05 Believer - Lost Breed.flac"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(FolderIn("Cynic - 1993 - The Breed Beyond (Split)", files), breed, "Cynic"));

            List<string> elemental = ["Kings", "Ritual", "Abyss", "Grave"];
            Assert.Null(SlskdTracklistCheck.OtherArtists(FolderIn("Wildernessking, Oak Pantheon, Kess'khtak, Liber Necris - 2014 - Elemental Nightmares - I",
                "01 - Wildernessking - Kings.flac", "02 - Oak Pantheon - Ritual.flac", "03 - Kess'khtak - Abyss.flac", "04 - Liber Necris - Grave.flac"), elemental, "Wildernessking"));
        }

        private static List<SlskdFileData> Timed(params (string Name, int Seconds)[] files) =>
            [.. files.Select(f => new SlskdFileData($@"music\Elvis Presley\Gold Collection Vol. 3\{f.Name}", null, null, 1000, f.Seconds, null, null, 0, false))];

        private static readonly List<string> GoldVol3 = ["I Got Stung", "Hound Dog", "Love Me Tender", "Jailhouse Rock", "Don't Be Cruel", "All Shook Up"];

        private static readonly List<TrackLength> GoldVol3Lengths =
            [new("igotstung", [111]), new("hounddog", [136, 138]), new("lovemetender", [161]), new("jailhouserock", [146]), new("dontbecruel", [123]), new("allshookup", [118])];

        // MUSIC-73: every "Gold Collection" on Soulseek is a Eurobeat rip renamed to Elvis titles
        [Fact]
        public void Right_titles_with_wrong_durations_are_rejected()
        {
            List<SlskdFileData> fake = Timed(("01 - I Got Stung.flac", 317), ("02 - Hound Dog.flac", 284), ("03 - Love Me Tender.flac", 165),
                ("04 - Jailhouse Rock.flac", 301), ("05 - Don't Be Cruel.flac", 296), ("06 - All Shook Up.flac", 270));
            Assert.Equal("durations miss 5/6 tracks", SlskdTracklistCheck.Reject(fake, GoldVol3, 6, null, GoldVol3Lengths));
            Assert.Null(SlskdTracklistCheck.Reject(fake, GoldVol3, 6, null, null));
        }

        [Fact]
        public void Another_edition_and_a_hidden_track_are_kept()
        {
            List<SlskdFileData> real = Timed(("01 - I Got Stung.flac", 112), ("02 - Hound Dog.flac", 138), ("03 - Love Me Tender.flac", 160),
                ("04 - Jailhouse Rock.flac", 147), ("05 - Don't Be Cruel.flac", 124), ("06 - All Shook Up.flac", 418));
            Assert.Null(SlskdTracklistCheck.Reject(real, GoldVol3, 6, null, GoldVol3Lengths));
        }

        // Replay (MUSIC-74): VBR MP3s report 2x, 2.5x, 3.37x their length; Bathory's first CD track merges three songs
        [Fact]
        public void Mp3_lengths_and_merged_tracks_are_not_judged()
        {
            List<SlskdFileData> vbr = Timed([.. GoldVol3.Select((t, i) => ($"{i + 1:00} - {t}.mp3", 2 * (int)GoldVol3Lengths[i].Seconds[0]))]);
            Assert.Null(SlskdTracklistCheck.Reject(vbr, GoldVol3, 6, null, GoldVol3Lengths));

            List<string> twilight = ["Twilight of the Gods", "Through Blood by Thunder", "Blood and Iron", "Under the Runes", "To Enter Your Mountain", "Bond of Blood", "Hammerheart"];
            List<TrackLength> lengths = [new("twilightofthegods", [664]), new("throughbloodbythunder", [406]), new("bloodandiron", [628]),
                new("undertherunes", [358]), new("toenteryourmountain", [455]), new("bondofblood", [456]), new("hammerheart", [281])];
            List<SlskdFileData> merged = Timed(("01 - Twilight of the Gods , Through Blood by Thunder , Blood and Iron.flac", 1844), ("02 - Under the Runes.flac", 358),
                ("03 - To Enter Your Mountain.flac", 455), ("04 - Bond of Blood.flac", 456), ("05 - Hammerheart.flac", 281), ("06 - Outro.flac", 23));
            Assert.Null(SlskdTracklistCheck.Reject(merged, twilight, 8, null, lengths));
        }

        [Fact]
        public void Browsed_folder_lengths_are_not_judged()
        {
            List<SlskdFileData> browsed = Timed([.. GoldVol3.Select((t, i) => ($"{i + 1:00} - {t}.flac", 317))]);
            Assert.Null(SlskdTracklistCheck.Reject(browsed, GoldVol3, 6, null, GoldVol3Lengths));
        }

        [Fact]
        public void Wanted_track_with_the_wrong_duration_is_rejected()
        {
            List<SlskdFileData> fake = Timed(("01 - I Got Stung.flac", 317), ("02 - Hound Dog.flac", 137));
            Assert.Equal("wanted \"I Got Stung\" has the wrong duration",
                SlskdTracklistCheck.Reject(fake, GoldVol3, 6, [new WantedTrack("I Got Stung", 1, 1, 111_000)], GoldVol3Lengths));
            Assert.Null(SlskdTracklistCheck.Reject(fake, GoldVol3, 6, [new WantedTrack("Hound Dog", 1, 2, 136_000)], GoldVol3Lengths));
        }

        [Fact]
        public void Lengths_merge_every_release_and_skip_unknown_durations()
        {
            static AlbumRelease Release(bool monitored, params (string Title, int Ms)[] tracks) => new()
            {
                Monitored = monitored,
                Tracks = new([.. tracks.Select(t => new Track { Title = t.Title, Duration = t.Ms })])
            };

            List<TrackLength>? lengths = SlskdTracklistCheck.LengthsOf(
                [Release(false, ("Hound Dog", 138_400), ("Bonus", 0)), Release(true, ("Hound Dog", 136_000), ("I Got Stung", 111_000))]);

            Assert.Equal(["hounddog:136,138", "igotstung:111"], lengths!.Select(l => $"{l.Title}:{string.Join(",", l.Seconds)}").Order());
            Assert.Null(SlskdTracklistCheck.LengthsOf([]));
        }

        [Fact]
        public void Wanted_file_is_picked_despite_a_misspelling()
        {
            List<SlskdFileData> seaside = Folder("01 - Jibber and Twitch.flac", "02 - Gena Lollabridgida.flac", "03 - Hello Mr Sparrow.flac");
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(seaside, [new WantedTrack("Gena Lolla Brigida", 1, 2, 0)], 13);
            Assert.Equal([seaside[1].Filename], picked?.Select(f => f.Filename));
        }
    }
}
