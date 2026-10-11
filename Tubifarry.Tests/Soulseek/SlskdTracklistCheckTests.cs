using System.Collections.Generic;
using System.Linq;
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

        [Fact]
        public void Wanted_file_is_picked_despite_a_misspelling()
        {
            List<SlskdFileData> seaside = Folder("01 - Jibber and Twitch.flac", "02 - Gena Lollabridgida.flac", "03 - Hello Mr Sparrow.flac");
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(seaside, [new WantedTrack("Gena Lolla Brigida", 1, 2, 0)], 13);
            Assert.Equal([seaside[1].Filename], picked?.Select(f => f.Filename));
        }
    }
}
