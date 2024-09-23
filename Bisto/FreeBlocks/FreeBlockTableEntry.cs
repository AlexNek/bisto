namespace Bisto.FreeBlocks;

internal class FreeBlockTableEntry
{
    public List<FreeBlock> FreeBlocks { get; }

    public long NextBlockAddress { get; }

    public FreeBlockTableEntry(List<FreeBlock> freeBlocks, long nextBlockAddress)
    {
        FreeBlocks = freeBlocks;
        NextBlockAddress = nextBlockAddress;
    }
}
