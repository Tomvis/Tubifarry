using System.Collections.Generic;
using System.Linq;
using Tubifarry.Download.Clients.Soulseek;
using Tubifarry.Indexers.Soulseek;
using Xunit;

namespace Tubifarry.Tests.Soulseek
{
    public class SlskdWantedFileFilterTests
    {
        private static SlskdFileData File(string name, int? seconds = null) =>
            new(name, null, null, 1000, seconds, null, null, 0, false);

        private static readonly List<SlskdFileData> Album =
        [
            File(@"music\Metallica\72 Seasons\01 - 72 Seasons.flac", 459),
            File(@"music\Metallica\72 Seasons\02 - Shadows Follow.flac", 372),
            File(@"music\Metallica\72 Seasons\06 - Lux Æterna (Remastered).flac", 205),
            File(@"music\Metallica\72 Seasons\cover.jpg"),
        ];

        private static List<string?>? Names(List<SlskdFileData>? files) => files?.Select(f => f.Filename).ToList();

        [Fact]
        public void Nothing_unwanted_downloads_everything()
        {
            Assert.Null(SlskdWantedFileFilter.Select(Album, null, 3));
            Assert.Null(SlskdWantedFileFilter.Select(Album, [], 3));
        }

        [Fact]
        public void Picks_the_wanted_file_by_title()
        {
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(Album, [new WantedTrack("Lux Æterna", 1, 6, 205_000)], 12);
            Assert.Equal([Album[2].Filename], Names(picked));
        }

        [Fact]
        public void Title_starting_with_a_number_still_matches()
        {
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(Album, [new WantedTrack("72 Seasons", 1, 1, 459_000)], 12);
            Assert.Equal([Album[0].Filename], Names(picked));
        }

        [Fact]
        public void Artist_album_number_title_names_match()
        {
            List<SlskdFileData> files =
            [
                File(@"x\Metallica - 72 Seasons - 05 - Screaming Suicide.mp3", 330),
                File(@"x\Metallica - 72 Seasons - 06 - Lux Æterna.mp3", 205),
            ];
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Lux Aeterna", 1, 6, 205_000)], 12);
            Assert.Equal([files[1].Filename], Names(picked));

            picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Screaming Suicide", 1, 5, 331_000)], 12);
            Assert.Equal([files[0].Filename], Names(picked));
        }

        [Fact]
        public void Duration_off_by_more_than_ten_seconds_is_no_match()
        {
            Assert.Null(SlskdWantedFileFilter.Select(Album, [new WantedTrack("Lux Æterna", 1, 6, 260_000)], 12));
        }

        [Fact]
        public void Any_unmatched_wanted_track_downloads_the_whole_folder()
        {
            Assert.Null(SlskdWantedFileFilter.Select(Album, [new WantedTrack("Lux Æterna", 1, 6, 205_000), new WantedTrack("Inamorata", 1, 12, 671_000)], 12));
        }

        [Fact]
        public void Falls_back_to_disc_and_number_when_the_tracklist_lengths_agree()
        {
            List<SlskdFileData> files =
            [
                File(@"x\CD1\101 Erste.flac", 200),
                File(@"x\CD1\102 Zweite.flac", 210),
                File(@"x\CD2\2-01 Dritte.flac", 220),
            ];
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Third", 2, 1, 221_000)], 3);
            Assert.Equal([files[2].Filename], Names(picked));

            picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Second", 1, 2, 210_000)], 3);
            Assert.Equal([files[1].Filename], Names(picked));

            Assert.Null(SlskdWantedFileFilter.Select(files, [new WantedTrack("Second", 1, 2, 210_000)], 4));
        }

        [Fact]
        public void Disc_comes_from_the_folder_name()
        {
            List<SlskdFileData> files =
            [
                File(@"x\Disc 1\01 - Intro.flac", 60),
                File(@"x\Disc 2\01 - Intro.flac", 90),
            ];
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Intro", 2, 1, 90_000)], 2);
            Assert.Equal([files[1].Filename], Names(picked));
        }

        [Fact]
        public void Identical_lengths_across_a_browsed_folder_are_ignored()
        {
            List<SlskdFileData> files =
            [
                File(@"music\Metallica\[2023] 72 Seasons\01 Metallica - 72 Seasons.flac", 459),
                File(@"music\Metallica\[2023] 72 Seasons\02 Metallica - Shadows Follow.flac", 459),
                File(@"music\Metallica\[2023] 72 Seasons\10 Metallica - Too Far Gone_.flac", 459),
            ];
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Shadows Follow", 1, 2, 371_880)], 12);
            Assert.Equal([files[1].Filename], Names(picked));

            picked = SlskdWantedFileFilter.Select(files, [new WantedTrack("Too Far Gone?", 1, 10, 273_693)], 12);
            Assert.Equal([files[2].Filename], Names(picked));
        }

        [Fact]
        public void Non_audio_files_are_never_picked()
        {
            List<SlskdFileData>? picked = SlskdWantedFileFilter.Select(Album, [new WantedTrack("Shadows Follow", 1, 2, 372_000)], 12);
            Assert.DoesNotContain(picked!, f => f.Filename!.EndsWith(".jpg"));
        }
    }
}
