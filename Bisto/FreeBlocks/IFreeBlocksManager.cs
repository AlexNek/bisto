namespace Bisto.FreeBlocks;

internal interface IFreeBlocksManager: IDisposable
{
    Task<List<FreeBlock>> GetFreeBlockMapAsync(CancellationToken cancellationToken = default);
    Task AddToFreeListAsync(
        Stream? fileStream,
        long dataAddress,
        int blockSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries the allocate data block.
    /// </summary>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="blockSize">Size of the block.</param>
    /// <param name="dataSize">Size of the data. For information only</param>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Task&lt;System.Nullable&lt;System.Int64&gt;&gt;.</returns>
    Task<long?> TryAllocateAsync(
        Stream fileStream,
        int blockSize,
        int dataSize = -1,
        CancellationToken cancellationToken = default);
}