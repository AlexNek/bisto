using Bisto.FreeBlocks;

namespace Bisto
{
    /// <summary>
    /// Defines a contract for a binary storage system, implementing block-level reading, writing, and deletion operations.
    /// Implements the <see cref="System.IDisposable" /> interface for proper resource management.
    /// </summary>
    public interface IBinaryStorage : IAsyncDisposable
    {
        /// <summary>
        /// Gets the offset of the root block that stores used data.
        /// </summary>
        /// <value>The offset of the root used block as a long.</value>
        long RootUsedBlock { get; }

        Task CloseStreamIfNeededAsync();

        /// <summary>
        /// Deletes the block of data located at the specified address asynchronously.
        /// </summary>
        /// <param name="dataAddress">The address of the data block to delete.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        Task DeleteAsync(long dataAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a map of free blocks in the storage asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of tuples where each tuple contains the block's offset and size.</returns>
        Task<List<FreeBlock>> GetFreeBlockMapAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the header as read only.
        /// </summary>
        /// <returns>BinaryStorageHeaderInfo.</returns>
        BinaryStorageHeaderInfo GetHeaderRo();

        /// <summary>
        /// Reads the data from the specified address asynchronously.
        /// </summary>
        /// <param name="dataAddress">The address of the data block to read.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The data read as a byte array, or null if the data cannot be found.</returns>
        Task<byte[]?> ReadAsync(long dataAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the size of the data block located at the specified address asynchronously.
        /// </summary>
        /// <param name="dataAddress">The address of the data block.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The size of the data block.</returns>
        Task<int> ReadDataSizeAsync(long dataAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the root block data asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The root block's data as a byte array, or null if it cannot be read.</returns>
        Task<byte[]?> ReadRootBlockAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes data to the storage asynchronously, optionally updating an existing block.
        /// </summary>
        /// <param name="data">The data to be written.</param>
        /// <param name="existingBlockOffset">The optional offset of an existing block to overwrite.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The offset where the data was written.</returns>
        Task<long> WriteAsync(
            byte[] data,
            long? existingBlockOffset = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes data to the root block asynchronously.
        /// </summary>
        /// <param name="data">The data to be written to the root block.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The offset of the written root block.</returns>
        Task<long> WriteRootBlockAsync(byte[] data, CancellationToken cancellationToken = default);
    }
}
