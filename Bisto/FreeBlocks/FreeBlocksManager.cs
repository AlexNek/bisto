using Bisto.Journal;

using Microsoft.Extensions.Logging;

namespace Bisto.FreeBlocks
{
    internal class FreeBlocksManager : IFreeBlocksManager, IDisposable
    {
        // work only for unsafe context
        //public const int FreeItemRecordLength = sizeof(FreeBlock);

        public const int FreeItemRecordLength = sizeof(long) + sizeof(int);

        private readonly FreeBlockAllocation _allocation;

        private readonly FreeBlockCollection _freeBlocks;

        private readonly ILogger<FreeBlocksManager>? _logger;

        private readonly FreeBlockMerger _merger;

        private readonly FreeBlockPersistence _persistence;

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private FreeBlocksManager(
            BinaryStorageHeader header,
            JournalManager? journalManager,
            ILoggerFactory? loggerFactory,
            int cacheSize,
            int entriesPerBlock = 256)
        {
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<FreeBlocksManager>();
            }

            _freeBlocks = new FreeBlockCollection();
            _persistence = new FreeBlockPersistence(header, loggerFactory, cacheSize, entriesPerBlock);
            _allocation = new FreeBlockAllocation(_freeBlocks, journalManager, _persistence, loggerFactory);
            _merger = new FreeBlockMerger(_freeBlocks, journalManager, _persistence, loggerFactory);
        }

        public async Task AddToFreeListAsync(
            Stream? fileStream,
            long blockAddress,
            int blockSize,
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogDebug("AddToFreeListAsync: {BlockAddress} {BlockSize}", blockAddress, blockSize);
                _freeBlocks.Add(new FreeBlock(blockAddress, blockSize));
                if (!await _merger.MergeFreeBlocksAsync(fileStream, blockAddress, cancellationToken))
                {
                    await _persistence.UpdateFreeBlocksOnDiskAsync(fileStream, _freeBlocks.GetAll(), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AddToFreeListAsync: {BlockAddress} {BlockSize}", blockAddress, blockSize);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static async Task<IFreeBlocksManager> CreateAsync(
            Stream fileStream,
            BinaryStorageHeader header,
            JournalManager? journalManager,
            ILoggerFactory? loggerFactory,
            int cacheSize,
            int entriesPerBlock = 256)
        {
            var manager = new FreeBlocksManager(header, journalManager, loggerFactory, cacheSize, entriesPerBlock);
            await manager.InitializeFreeBlocksAsync(fileStream);
            return manager;
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        public async Task<List<FreeBlock>> GetFreeBlockMapAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return _freeBlocks.GetAll();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<long?> TryAllocateAsync(
            Stream fileStream,
            int blockSize,
            int dataSize = -1,
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogDebug("TryAllocateAsync: {BlockSize}", blockSize);
                var result = await _allocation.TryAllocateAsync(fileStream, blockSize, dataSize, cancellationToken);
                if (result.HasValue)
                {
                    await _persistence.UpdateFreeBlocksOnDiskAsync(fileStream, _freeBlocks.GetAll(), cancellationToken);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "");
            }
            finally
            {
                _semaphore.Release();
            }

            return null;
        }

        private async Task InitializeFreeBlocksAsync(Stream fileStream)
        {
            var blocks = await _persistence.ReadAllBlocksAsync(fileStream);
            foreach (var block in blocks)
            {
                _freeBlocks.Add(block);
            }
        }
    }
}
