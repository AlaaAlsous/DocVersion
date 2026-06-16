namespace DocVersion.Core.Models;

public enum EventsType
{
    FileCreated = 0,
    FileUpdated = 1,
    FileDeleted = 2,
    FolderCreated = 5,
    FolderDeleted = 7,
    FolderRenamed = 8,
    FileRenamed = 9,
    BinRestored = 10,
    BinPermanentDeleted = 11,
    BinEmptied = 12,
}
