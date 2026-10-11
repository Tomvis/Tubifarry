using Tubifarry.Core.Model;

namespace Tubifarry.Indexers.Soulseek
{
    public interface ISlskdItemsParser
    {
        SlskdFolderData ParseFolderName(string folderPath);
        AlbumData CreateAlbumData(string searchId, IGrouping<string, SlskdFileData> directory, SlskdSearchData searchData, SlskdFolderData folderData, SlskdSettings? settings = null, int expectedTrackCount = 0, string? editionLabel = null);
    }
}
