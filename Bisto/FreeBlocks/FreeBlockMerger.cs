using Bisto.Journal;

using Microsoft.Extensions.Logging;

namespace Bisto.FreeBlocks;

internal class FreeBlockMerger
{
    private readonly FreeBlockCollection _freeBlocks;

    private readonly JournalManager? _journalManager;

    private readonly ILogger<FreeBlockMerger>? _logger;

    private readonly FreeBlockPersistence _persistence;

    public FreeBlockMerger(
        FreeBlockCollection freeBlocks,
        JournalManager? journalManager,
        FreeBlockPersistence persistence,
        ILoggerFactory? loggerFactory)
    {
        if (loggerFactory != null)
        {
            _logger = loggerFactory.CreateLogger<FreeBlockMerger>();
        }

        _freeBlocks = freeBlocks;
        _journalManager = journalManager;
        _persistence = persistence;
    }

    public async Task<bool> MergeFreeBlocksAsync(Stream fileStream, long address, CancellationToken cancellationToken)
    {
        var current = _freeBlocks.GetByAddress(address);
        var mergedBlock = current;
        bool mergeOccurred = false;
        int totalMergedSize = current.Size;

        // Check previous block
        var prevFreeBlock = _freeBlocks.GetAll().FirstOrDefault(b => b.Offset + b.Size == address);
        if (prevFreeBlock != null)
        {
            // Merge with previous block
            _logger?.LogInformation(
                $"addr:{address} - Merge with previous free block addr:{prevFreeBlock.Offset}, size: {prevFreeBlock.Size}");
            _freeBlocks.Remove(prevFreeBlock.Offset, prevFreeBlock.Size);
            _freeBlocks.Remove(current.Offset, current.Size);
            mergedBlock = new FreeBlock(prevFreeBlock.Offset, prevFreeBlock.Size + current.Size);
            _freeBlocks.Add(mergedBlock);
            mergeOccurred = true;
            totalMergedSize += prevFreeBlock.Size;
        }

        // Check next block
        var next = _freeBlocks.GetAll().FirstOrDefault(b => b.Offset == mergedBlock.Offset + mergedBlock.Size);
        if (next != null)
        {
            // Merge with next block
            _logger?.LogInformation(
                $"addr:{address} - Merge with next free block offset:{next.Offset}, size: {next.Size}");

            _freeBlocks.Remove(mergedBlock.Offset, mergedBlock.Size);
            _freeBlocks.Remove(next.Offset, next.Size);
            var newMergedBlock = new FreeBlock(mergedBlock.Offset, mergedBlock.Size + next.Size);
            _freeBlocks.Add(newMergedBlock);
            mergeOccurred = true;
            totalMergedSize += next.Size;
            mergedBlock = newMergedBlock;
        }

        if (mergeOccurred)
        {
            if (_journalManager != null)
            {
                await _journalManager.LogOperationAsync(
                    EJournalOperation.MergeBlocks,
                    mergedBlock.Offset,
                    mergedBlock.Size,
                    totalMergedSize, // dataSize is the total size of all merged blocks
                    cancellationToken);
            }

            // update block header for merged block
            await _persistence.UpdateBlockHeaderAsync(
                fileStream,
                mergedBlock.Offset,
                mergedBlock.Size,
                BlockUtils.BlockState.Deleted,
                cancellationToken);

            // Update free blocks table on disk
            await _persistence.UpdateFreeBlocksOnDiskAsync(fileStream, _freeBlocks.GetAll(), cancellationToken);

            if (_journalManager != null)
            {
                await _journalManager.CommitAsync(cancellationToken);
            }

        }

        return mergeOccurred;
    }
}
