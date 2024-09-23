using Microsoft.Extensions.Logging;

namespace Bisto.FreeBlocks
{
    /// <summary>
    /// Manages the persistence of free blocks to the storage stream.
    /// </summary>
    internal class FreeBlockPersistence
    {
        private const int
            TableHeaderSize = sizeof(long) + sizeof(int); // Size of the header in each free block table block

        //TODO: Improve, don't cache byte buffer, cache table itself
        private readonly MemoryCacheManager _cacheManager; // Cache for storing free block table blocks

        private readonly int _entriesPerBlock; // Number of free block entries that can fit in a single block

        //private readonly Stream _fileStream; // The underlying storage stream

        private readonly BinaryStorageHeader _header; // The storage header containing metadata

        private readonly ILogger<FreeBlockPersistence>? _logger;

        private readonly int _minBlockSize; // Minimum size of a free block table block

        /// <summary>
        /// Initializes a new instance of the <see cref="FreeBlockPersistence"/> class.
        /// </summary>
        /// <param name="header">The storage header containing metadata.</param>
        /// <param name="loggerFactory"></param>
        /// <param name="cacheSize">The size of the cache for storing free block table blocks.</param>
        /// <param name="entriesPerBlock">The number of free block entries that can fit in a single block.</param>
        public FreeBlockPersistence(
            BinaryStorageHeader header,
            ILoggerFactory? loggerFactory,
            int cacheSize,
            int entriesPerBlock)
        {
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<FreeBlockPersistence>();
            }

            _header = header ?? throw new ArgumentNullException(nameof(header));
            _cacheManager = new MemoryCacheManager(cacheSize);
            _entriesPerBlock = entriesPerBlock;
            _minBlockSize = CalculateMinBlockSize(entriesPerBlock);
        }

        /// <summary>
        /// Read all blocks as an asynchronous operation.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A Task&lt;List`1&gt; representing the asynchronous operation.</returns>
        public async Task<List<FreeBlock>> ReadAllBlocksAsync(
            Stream fileStream,
            CancellationToken cancellationToken = default)
        {
            var allBlocks = new List<FreeBlock>(); // List to store all free blocks
            long currentAddress = _header.FreeBlocksTableOffset; // Start from the address in the header
            _logger?.LogInformation($"Read allBlocks from: {currentAddress}");

            // Iterate through the linked list of free block table blocks
            while (currentAddress != 0)
            {
                var block = await ReadBlockAsync(
                                fileStream,
                                currentAddress,
                                cancellationToken); // Read the current block
                if (block.FreeBlocks.Count == 0 || block.FreeBlocks.Count > _entriesPerBlock)
                {
                    throw new InvalidOperationException("Invalid free block table list count");
                }

                allBlocks.AddRange(block.FreeBlocks); // Add the free blocks from the current block to the list
                currentAddress = block.NextBlockAddress; // Move to the next block in the chain
            }

            return allBlocks;
        }

        /// <summary>
        /// Updates the header of a block in the storage stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="offset">The offset of the block.</param>
        /// <param name="size">The size of the block.</param>
        /// <param name="state">The state of the block.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task UpdateBlockHeaderAsync(
            Stream fileStream,
            long offset,
            int size,
            BlockUtils.BlockState state,
            CancellationToken cancellationToken)
        {
            var header = new BlockUtils.BlockHeader(
                size - BlockUtils.BlockHeaderSize,
                size,
                state); // Create a new block header
            fileStream.Seek(offset, SeekOrigin.Begin); // Seek to the block offset
            await BlockUtils.WriteBlockHeaderAsync(
                fileStream,
                header,
                cancellationToken); // Write the header to the stream
        }

        /// <summary>
        /// Update free blocks on disk as an asynchronous operation.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <param name="allBlocks">All blocks.</param>
        /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task UpdateFreeBlocksOnDiskAsync(
            Stream fileStream,
            List<FreeBlock> allBlocks,
            CancellationToken cancellationToken)
        {
            long currentAddress = _header.FreeBlocksTableOffset;
            long previousAddress = 0;
            long nextAfterLastAddress = 0;
            bool noFreeBlocksExists = currentAddress == 0;

            // Calculate the number of blocks required
            int numBlocks = (allBlocks.Count + _entriesPerBlock - 1) / _entriesPerBlock;

            _logger?.LogInformation($"Updating free blocks on disk: {numBlocks}, allBlocks.Count: {allBlocks.Count}");

            // Iterate over the calculated number of blocks
            for (int blockIndex = 0; blockIndex < numBlocks; blockIndex++)
            {
                // Calculate the start and end indices for the current block
                int startIndex = blockIndex * _entriesPerBlock;
                int endIndex = Math.Min(startIndex + _entriesPerBlock, allBlocks.Count);

                // Get the free blocks for the current block
                var blockEntries = allBlocks.GetRange(startIndex, endIndex - startIndex);
                bool isLastBlock = endIndex >= allBlocks.Count;

                currentAddress = await HandleBlockAllocationAsync(
                                     fileStream,
                                     currentAddress,
                                     previousAddress,
                                     noFreeBlocksExists,
                                     cancellationToken);

                long nextBlockAddress = await HandleNextBlockAddressAsync(
                                            fileStream,
                                            currentAddress,
                                            isLastBlock,
                                            noFreeBlocksExists,
                                            cancellationToken);

                // Check for the possible next allocated table block
                if (isLastBlock && !noFreeBlocksExists)
                {
                    nextAfterLastAddress = await ReadNextBlockAddressAsync(
                                               fileStream,
                                               currentAddress,
                                               cancellationToken);
                }

                await WriteTableBlockAsync(
                    fileStream,
                    currentAddress,
                    blockEntries,
                    isLastBlock ? 0 : nextBlockAddress,
                    cancellationToken);

                previousAddress = currentAddress;
                currentAddress = nextBlockAddress > 0 ? nextBlockAddress : 0;
            }

            // TODO: Implement cleanup of leftover blocks. Now is partially only, we need to call owner but the problem owner will be call this function again
            await CleanupLeftoverBlocksAsync(fileStream, nextAfterLastAddress, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Allocates a new block in the storage stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="size">The size of the block to allocate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The address of the newly allocated block.</returns>
        private async Task<long> AllocateNewBlockAsync(Stream fileStream, int size, CancellationToken cancellationToken)
        {
            // Calculate the start address for the new block
            long headerSize = BinaryStorageHeader.HeaderSize;
            long newBlockAddress = Math.Max(headerSize, fileStream.Length);
            _logger?.LogInformation($"Allocating new table block: {newBlockAddress}, size: {size}");

            fileStream.Seek(newBlockAddress, SeekOrigin.Begin); // Seek to the new block address
            await UpdateBlockHeaderAsync(
                fileStream,
                newBlockAddress,
                size,
                BlockUtils.BlockState.FreeBlocksTable,
                cancellationToken); // Write the block header
            // Write an empty block of the specified size
            await fileStream.WriteAsync(new byte[size], 0, size, cancellationToken);

            // Return the address of the newly allocated block
            return newBlockAddress;
        }

        /// <summary>
        /// Calculates the minimum block size required to store the specified number of free block entries.
        /// </summary>
        /// <param name="entriesPerBlock">The number of entries per block.</param>
        /// <returns>The minimum block size.</returns>
        private int CalculateMinBlockSize(int entriesPerBlock)
        {
            int dataSize =
                TableHeaderSize + entriesPerBlock * FreeBlocksManager.FreeItemRecordLength; // Calculate data size
            return BlockUtils.RoundUpToPowerOfTwo(
                dataSize + BlockUtils.BlockHeaderSize); // Round up to nearest power of 2
        }

        ///  <summary>
        /// TODO: Implement this method to clean up leftover blocks
        ///  it is not enough here, we must call parent method to collect free blocks.
        ///  we don't have parent methos now 
        ///  </summary>
        ///  <param name="fileStream"></param>
        ///  <param name="startAddress"></param>
        ///  <param name="cancellationToken"></param>
        private async Task CleanupLeftoverBlocksAsync(
            Stream fileStream,
            long startAddress,
            CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Cleaning up leftover table blocks: {startAddress}. NOT implemented yet.");

            long currentAddress = startAddress;
            while (currentAddress != 0)
            {
                long nextAddress = await ReadNextBlockAddressAsync(fileStream, currentAddress, cancellationToken);
                await MarkBlockAsUnusedAsync(fileStream, currentAddress, cancellationToken);
                currentAddress = nextAddress;
            }
        }

        /// <summary>
        /// Deserializes a free block table block from a byte array.
        /// </summary>
        /// <param name="data">The byte array containing the block data.</param>
        /// <returns>A <see cref="FreeBlockTableEntry"/> object representing the deserialized block.</returns>
        private FreeBlockTableEntry DeserializeBlock(byte[] data)
        {
            long nextBlockAddress = TableBlockHeaderRead(data, out int recordCount);

            var freeBlocks = new List<FreeBlock>(recordCount); // Initialize the list with the expected capacity

            int offset = TableHeaderSize; // Start reading entries after the header
            for (int i = 0; i < recordCount; i++)
            {
                long blockOffset = BitConverter.ToInt64(data, offset); // Read the offset of the free block
                int blockSize = BitConverter.ToInt32(data, offset + sizeof(long)); // Read the size of the free block

                // Only add valid blocks (non-zero offset and size)
                if (blockOffset != 0 || blockSize != 0)
                {
                    freeBlocks.Add(new FreeBlock(blockOffset, blockSize)); // Add the free block to the list
                }

                offset += FreeBlocksManager.FreeItemRecordLength; // Move to the next entry
            }

            return new FreeBlockTableEntry(freeBlocks, nextBlockAddress); // Return the deserialized block
        }

        private async Task<long> HandleBlockAllocationAsync(
            Stream fileStream,
            long currentAddress,
            long previousAddress,
            bool noFreeBlocksExists,
            CancellationToken cancellationToken = default)
        {
            if (currentAddress == 0)
            {
                currentAddress = await AllocateNewBlockAsync(fileStream, _minBlockSize, cancellationToken);
                if (previousAddress == 0)
                {
                    _header.FreeBlocksTableOffset = currentAddress;
                    await _header.WriteToStreamAsync(fileStream, cancellationToken);
                }
                else
                {
                    await UpdateNextBlockAddressAsync(fileStream, previousAddress, currentAddress, cancellationToken);
                }
            }
            else if (noFreeBlocksExists)
            {
                var newTableAddress = await AllocateNewBlockAsync(fileStream, _minBlockSize, cancellationToken);
                await UpdateNextBlockAddressAsync(fileStream, currentAddress, newTableAddress, cancellationToken);
                currentAddress = newTableAddress;
            }

            return currentAddress;
        }

        private async Task<long> HandleNextBlockAddressAsync(
            Stream fileStream,
            long currentAddress,
            bool isLastBlock,
            bool noFreeBlocksExists,
            CancellationToken cancellationToken)
        {
            if (!isLastBlock && !noFreeBlocksExists)
            {
                return await ReadNextBlockAddressAsync(fileStream, currentAddress, cancellationToken);
            }

            return 0;
        }

        /// <summary>
        /// Marks a block as unused in the storage stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="address">The address of the block to mark as unused.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task MarkBlockAsUnusedAsync(Stream fileStream, long address, CancellationToken cancellationToken)
        {
            await UpdateBlockHeaderAsync(
                fileStream,
                address,
                _minBlockSize,
                BlockUtils.BlockState.Deleted,
                cancellationToken); // Update the block header to mark it as deleted

            // Clear cache for this block
            _cacheManager.Remove(address);
            //TODO: add to unused list but we aware of recursion call
        }

        private async Task MarkRemainingBlocksAsUnusedAsync(
            Stream fileStream,
            long currentAddress,
            CancellationToken cancellationToken)
        {
            while (currentAddress != 0)
            {
                long nextAddress = await ReadNextBlockAddressAsync(fileStream, currentAddress, cancellationToken);
                await MarkBlockAsUnusedAsync(fileStream, currentAddress, cancellationToken);
                currentAddress = nextAddress;
            }
        }

        /// <summary>
        /// Reads a free block table block from the storage stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="address">The address of the block to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="FreeBlockTableEntry"/> object representing the read block.</returns>
        private async Task<FreeBlockTableEntry> ReadBlockAsync(
            Stream fileStream,
            long address,
            CancellationToken cancellationToken)
        {
            // Check if the block is in the cache
            if (_cacheManager.TryGetValue(address, out byte[] cachedData))
            {
                _logger?.LogInformation($"Read block from cache: {address}");
                return DeserializeBlock(cachedData); // Return the block from the cache
            }

            _logger?.LogInformation($"Read block from stream: {address}");

            var header = await ReadBlockHeaderAsync(fileStream, address, cancellationToken); // Read the block header
            byte[] buffer = new byte[header.DataSize]; // Create a buffer to hold the block data

            try
            {
                fileStream.Seek(address + BlockUtils.BlockHeaderSize, SeekOrigin.Begin); // Seek to the block data
                await fileStream.ReadAsync(buffer, 0, header.DataSize, cancellationToken); // Read the block data
                var block = DeserializeBlock(buffer); // Deserialize the block
                _cacheManager.Set(address, buffer); // Add the block to the cache
                return block;
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading block at address {address}", ex);
            }
        }

        /// <summary>
        /// Reads the header of a block from the storage stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="address">The address of the block.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="BlockUtils.BlockHeader"/> object representing the block header.</returns>
        private async Task<BlockUtils.BlockHeader> ReadBlockHeaderAsync(
            Stream fileStream,
            long address,
            CancellationToken cancellationToken)
        {
            fileStream.Seek(address, SeekOrigin.Begin); // Seek to the block address
            var header = await BlockUtils.ReadBlockHeaderAsync(fileStream, cancellationToken); // Read the block header

            if (header.BlockSize == 0)
            {
                throw new InvalidDataException($"Free table block at address {address} cannot have zero block size");
            }

            return header;
        }

        /// <summary>
        /// Reads the address of the next block in the chain from the current block.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="currentAddress">The address of the current block.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The address of the next block.</returns>
        private async Task<long> ReadNextBlockAddressAsync(
            Stream fileStream,
            long currentAddress,
            CancellationToken cancellationToken)
        {
            //TODO: not effective read all, I need NextBlock address only
            // var block = await ReadBlockAsync(currentAddress, cancellationToken); // Read the current block
            // return block.NextBlockAddress; // Return the next block address from the block
            var header = await ReadBlockHeaderAsync(
                             fileStream,
                             currentAddress,
                             cancellationToken); // Read the block header
            // _fileStream.Seek(address + BlockUtils.BlockHeaderSize, SeekOrigin.Begin); // Seek to the block data
            byte[] buffer = new byte[TableHeaderSize]; // Create a buffer to hold the block data
            int readCount = await fileStream.ReadAsync(buffer, 0, TableHeaderSize, cancellationToken);
            long nextBlockAddress = TableBlockHeaderRead(buffer, out int recordCount);
            return nextBlockAddress;
        }

        private static long TableBlockHeaderRead(byte[] buffer, out int recordCount)
        {
            int index = 0;
            long nextBlockAddress = BitConverter.ToInt64(buffer, index); // Read the next block address
            index += sizeof(long);
            recordCount = BitConverter.ToInt32(buffer, index); // Read the number of free block entries
            return nextBlockAddress;
        }

        private static void TableBlockHeaderWrite(byte[] buffer, long nextBlockAddress, int blocksCount)
        {
            int index = 0;
            // Write next block address
            BitConverter.GetBytes(nextBlockAddress).CopyTo(buffer, index); // Write the next block address
            index += sizeof(long);
            BitConverter.GetBytes(blocksCount).CopyTo(buffer, index); // Write the number of free block entries
        }

        private void UpdateCache(long currentAddress, byte[] buffer)
        {
            // Update cache if exists
            if (_cacheManager.TryGetValue(currentAddress, out byte[] cachedData))
            {
                Buffer.BlockCopy(
                    buffer,
                    0,
                    cachedData,
                    0,
                    sizeof(long)); // Update the next block address in the cached data
                _cacheManager.Set(currentAddress, cachedData); // Update the cache
            }
        }

        /// <summary>
        /// Updates the next block address in the current block.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="currentAddress">The address of the current block.</param>
        /// <param name="nextAddress">The address of the next block.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task UpdateNextBlockAddressAsync(
            Stream fileStream,
            long currentAddress,
            long nextAddress,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[sizeof(long)]; // Create a buffer to hold the next block address
            BitConverter.GetBytes(nextAddress).CopyTo(buffer, 0); // Write the next block address to the buffer

            fileStream.Seek(
                currentAddress + BlockUtils.BlockHeaderSize,
                SeekOrigin.Begin); // Seek to the next block address field
            await fileStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken); // Write the buffer to the stream

            UpdateCache(currentAddress, buffer);
        }

        /// <summary>
        /// Writes a block of free block entries to the storage stream.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="blockAddress">The block address begin where to write the block.</param>
        /// <param name="blocks">The list of free blocks to write.</param>
        /// <param name="nextBlockAddress">The address of the next block in the chain.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task WriteTableBlockAsync(
            Stream fileStream,
            long blockAddress,
            List<FreeBlock> blocks,
            long nextBlockAddress,
            CancellationToken cancellationToken)
        {
            // Calculate block size based on number of free blocks and the size of the next block address
            int blockSize = TableHeaderSize + blocks.Count * FreeBlocksManager.FreeItemRecordLength;
            byte[] buffer = new byte[blockSize]; // Create a buffer to hold the block data

            _logger?.LogInformation(
                $"Write table block: {blockAddress} - {blockAddress + blockSize}, nextBlockAddress: {nextBlockAddress}");
            TableBlockHeaderWrite(buffer, nextBlockAddress, blocks.Count);

            // Write free blocks
            for (int i = 0; i < blocks.Count; i++)
            {
                int offset =
                    TableHeaderSize
                    + i * FreeBlocksManager.FreeItemRecordLength; // Calculate the offset of the current entry
                BitConverter.GetBytes(blocks[i].Offset).CopyTo(buffer, offset); // Write the offset of the free block
                BitConverter.GetBytes(blocks[i].Size)
                    .CopyTo(buffer, offset + sizeof(long)); // Write the size of the free block
            }

            // Write buffer to stream
            // Note: Table block don't have block header so we need to skip it
            fileStream.Seek(blockAddress + BlockUtils.BlockHeaderSize, SeekOrigin.Begin); // Seek to the block address
            await fileStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken); // Write the buffer to the stream
            _cacheManager.Set(blockAddress, buffer); // Add the block to the cache
        }
    }
}
