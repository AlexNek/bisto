namespace Bisto.Journal;

public enum EJournalOperation
{
    //TODO: replace with delete data block
    AddToFreeList = 1,

    AllocateFromFree,

    DeleteBlockFromSplit,

    WriteDataBlock,

    DeleteDataBlock,

    MergeBlocks,
}
