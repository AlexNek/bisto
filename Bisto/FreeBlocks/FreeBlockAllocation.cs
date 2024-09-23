using Bisto.Journal;

using Microsoft.Extensions.Logging;

namespace Bisto.FreeBlocks;

internal class FreeBlockAllocation
{
    private const int MinBlockSize = 16;

    private readonly FreeBlockCollection _freeBlocks;

    private readonly JournalManager? _journalManager;

    private readonly FreeBlockPersistence _persistence;

    public FreeBlockAllocation(
        FreeBlockCollection freeBlocks,
        JournalManager? journalManager,
        FreeBlockPersistence persistence,
        ILoggerFactory? loggerFactory)
    {
        _freeBlocks = freeBlocks;
        _journalManager = journalManager;
        _persistence = persistence;
    }

    public async Task<long?> TryAllocateAsync(
        Stream fileStream,
        int blockSize,
        int dataSize = -1,
        CancellationToken cancellationToken = default)
    {
        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockSize),
                "Block allocation size must be greater than zero.");
        }

        foreach (var kvp in _freeBlocks.GetBySize())
        {
            if (kvp.Key >= blockSize && kvp.Value.Count > 0)
            {
                var block = kvp.Value[0];
                long allocatedOffset = block.Offset;

                if (_journalManager != null)
                {
                    await _journalManager.LogOperationAsync(
                        EJournalOperation.AllocateFromFree,
                        allocatedOffset,
                        blockSize,
                        dataSize,
                        cancellationToken);
                }

                _freeBlocks.Remove(block.Offset, block.Size);

                // Update the allocated block's header
                await _persistence.UpdateBlockHeaderAsync(
                    fileStream,
                    allocatedOffset,
                    blockSize,
                    BlockUtils.BlockState.Allocated,
                    cancellationToken);

                if (block.Size > blockSize + MinBlockSize)
                {
                    // Create a new free block for the remaining space
                    var newFreeBlockOffset = block.Offset + blockSize;
                    var newFreeBlockSize = block.Size - blockSize;
                    if (_journalManager != null)
                    {
                        await _journalManager.LogOperationAsync(
                            EJournalOperation.DeleteBlockFromSplit,
                            newFreeBlockOffset,
                            newFreeBlockSize,
                            -1,
                            cancellationToken);
                    }

                    _freeBlocks.Add(new FreeBlock(newFreeBlockOffset, newFreeBlockSize));

                    // Update the new free block's header
                    await _persistence.UpdateBlockHeaderAsync(
                        fileStream,
                        newFreeBlockOffset,
                        newFreeBlockSize,
                        BlockUtils.BlockState.Deleted,
                        cancellationToken);
                }

                // Update free blocks table on disk
                await _persistence.UpdateFreeBlocksOnDiskAsync(fileStream, _freeBlocks.GetAll(), cancellationToken);

                if (_journalManager != null)
                {
                    await _journalManager.CommitAsync(cancellationToken);
                }


                return allocatedOffset;
            }
        }

        return null;
    }
}
