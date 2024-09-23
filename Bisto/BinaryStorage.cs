using System.Data;
using System.Timers;

using Bisto.FreeBlocks;
using Bisto.Journal;

using Microsoft.Extensions.Logging;

using Timer = System.Timers.Timer;

namespace Bisto;

public class BinaryStorage : IBinaryStorage
{
    public static readonly StorageOptions DefaultOptions = new StorageOptions();

    private readonly string _filename;

    private readonly IFileStreamProvider _fileStreamProvider;

    private readonly ILogger<BinaryStorage>? _logger;

    private readonly ILoggerFactory? _loggerFactory;

    private readonly StorageOptions? _options;

    private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);

    private readonly bool _useJounaling;

    private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

    private Stream? _fileStream;

    private IFreeBlocksManager _freeBlocksManager;

    private BinaryStorageHeader _header;

    private Timer? _inactivityTimer;

    private JournalManager? _journalManager;

    //private Stream? _journalStream;

    private TimeSpan _streamInactivityTimeout = TimeSpan.FromMinutes(5); // 5 minutes

    private bool _streamOpen;

    public long RootUsedBlock => _header.RootUsedBlock;

    // Private constructor to prevent direct instantiation
    private BinaryStorage(
        string filename,
        IFileStreamProvider fileStreamProvider,
        ILoggerFactory? loggerFactory,
        StorageOptions? options)
    {
        _filename = filename;
        _fileStreamProvider = fileStreamProvider;
        _loggerFactory = loggerFactory;
        _options = options;
        if (_options is { UseJournaling: true })
        {
            _useJounaling = true;
        }

        _logger = null;
        if (_loggerFactory != null)
        {
            _logger = _loggerFactory.CreateLogger<BinaryStorage>();
        }
    }

    public async Task CloseStreamIfNeededAsync()
    {
        // Acquire both semaphores to ensure no operations are in progress
        // Use 'WaitAsync' to avoid potential deadlocks
        if (await _writeSemaphore.WaitAsync(_streamInactivityTimeout) &&
            await _readSemaphore.WaitAsync(_streamInactivityTimeout))
        {
            try
            {
                await CloseStreamAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing stream");
            }
            finally
            {
                _writeSemaphore.Release();
                _readSemaphore.Release();
                _inactivityTimer?.Stop();
            }
        }
    }

    // Static factory method for asynchronous creation
    public static async Task<BinaryStorage> CreateAsync(
        string filename,
        IFileStreamProvider fileStreamProvider,
        bool createMode = false,
        ILoggerFactory? loggerFactory = null,
        StorageOptions? options = null)
    {
        var storage = new BinaryStorage(filename, fileStreamProvider, loggerFactory, options);
        storage.SetStreamInactivityTimeout(options?.StreamInactivityTimeout);
        await storage.OpenStreamAsync(fileStreamProvider, createMode);

        if (storage._fileStream.Length == 0)
        {
            await storage.InitializeFileAsync();
        }
        else
        {
            storage._header = await BinaryStorageHeader.ReadFromStreamAsync(storage._fileStream);
            storage._header.Validate();
        }

        options ??= DefaultOptions;

        storage._freeBlocksManager = await FreeBlocksManager.CreateAsync(
                                         storage._fileStream,
                                         storage._header,
                                         storage._journalManager,
                                         loggerFactory,
                                         options.CacheSize,
                                         storage._header.FreeBlocksTableEntriesPerBlock
                                     );
        return storage;
    }

    public async Task DeleteAsync(long blockAddress, CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Ensure the stream is open
            await OpenStreamIfNeededAsync();
            await DeleteInternalAsync(blockAddress, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error deleting block");
        }
        finally
        {
            _writeSemaphore.Release();
            StartInactivityTimer(); // Restart the timer after the operation
        }
    }

    public void Dispose()
    {
        _freeBlocksManager?.Dispose();
        CloseStreamAsync(); // Close stream resources
        _readSemaphore?.Dispose();
        _writeSemaphore?.Dispose();
    }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.</summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(_fileStreamProvider);
        await CastAndDispose(_readSemaphore);
        await CastAndDispose(_writeSemaphore);
        if (_fileStream != null)
        {
            await _fileStream.DisposeAsync();
        }

        await CastAndDispose(_freeBlocksManager);
        if (_inactivityTimer != null)
        {
            await CastAndDispose(_inactivityTimer);
        }

        if (_journalManager != null)
        {
            await _journalManager.DisposeAsync();
        }
    }

    public async Task<List<FreeBlock>> GetFreeBlockMapAsync(CancellationToken cancellationToken = default)
    {
        await OpenStreamIfNeededAsync();
        return await _freeBlocksManager.GetFreeBlockMapAsync(cancellationToken);
    }

    public BinaryStorageHeaderInfo GetHeaderRo()
    {
        // Create and return a new read-only header info object
        return new BinaryStorageHeaderInfo(_header);
    }

    public async Task<byte[]?> ReadAsync(long dataAddress, CancellationToken cancellationToken = default)
    {
        await _readSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Ensure the stream is open
            await OpenStreamIfNeededAsync();

            // Perform the actual read operation
            return await ReadInternalAsync(dataAddress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading data");
        }
        finally
        {
            _readSemaphore.Release();
            StartInactivityTimer(); // Restart the timer after the operation
        }

        return null;
    }

    // Interface method implementation: ReadDataSizeAsync
    public async Task<int> ReadDataSizeAsync(long blockAddress, CancellationToken cancellationToken = default)
    {
        await OpenStreamIfNeededAsync();
        var blockHeader = await ReadBlockHeaderAsync(blockAddress, cancellationToken);
        StartInactivityTimer(); // Restart the timer after the operation        
        return blockHeader.DataSize;
    }

    // Interface method implementation: ReadRootBlockAsync
    public async Task<byte[]?> ReadRootBlockAsync(CancellationToken cancellationToken = default)
    {
        if (_header.RootUsedBlock == 0)
        {
            return null;
        }

        return await ReadAsync(_header.RootUsedBlock, cancellationToken);
    }

    // public async Task WriteAsync(byte[] data, long dataAddress, CancellationToken cancellationToken = default)
    // {
    //     await _writeSemaphore.WaitAsync(cancellationToken);
    //     try
    //     {
    //         await OpenStreamIfNeededAsync();
    //
    //         // Perform the actual write operation
    //         await WriteInternalAsync(data, dataAddress, cancellationToken);
    //
    //         // You may want to explicitly flush to disk here if necessary
    //     }
    //     finally
    //     {
    //         _writeSemaphore.Release();
    //
    //         // Optionally close the stream if it should not stay open
    //         // CloseStreamIfNeeded();
    //     }
    // }

    public async Task<long> WriteAsync(
        byte[] data,
        long? existingBlockOffset = null,
        CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            await OpenStreamIfNeededAsync();

            return await WriteInternalAsync(data, existingBlockOffset, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing data");
        }
        finally
        {
            _writeSemaphore.Release();
            StartInactivityTimer(); // Restart the timer after the operation
        }

        return 0;
    }

    public async Task<long> WriteRootBlockAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        await _writeSemaphore.WaitAsync();
        try
        {
            long offset;

            await OpenStreamIfNeededAsync();
            if (_header.RootUsedBlock != 0)
            {
                // Update the existing block
                offset = await WriteInternalAsync(data, _header.RootUsedBlock);
            }
            else
            {
                // Allocate a new block
                offset = await WriteInternalAsync(data);
            }

            // Update the header if the offset has changed
            if (_header.RootUsedBlock != offset)
            {
                _header.RootUsedBlock = offset;

                // Write the header to the stream
                await _header.WriteToStreamAsync(_fileStream);
            }

            return offset;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing data");
        }
        finally
        {
            _writeSemaphore.Release();
            StartInactivityTimer(); // Restart the timer after the operation
        }

        return 0;
    }

    private async Task<long> AllocateBlockAsync(
        int blockSize,
        int dataSize = -1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await OpenStreamIfNeededAsync();
            long? offset = await _freeBlocksManager.TryAllocateAsync(
                               _fileStream,
                               blockSize,
                               dataSize,
                               cancellationToken);
            if (offset == null)
            {
                // If no free block is found, allocate a new block at the end of the file
                offset = _fileStream.Length;
                _fileStream.SetLength(offset.Value + blockSize);
            }

            _logger?.LogInformation(
                $"Allocated block at address {offset} for block size {blockSize} and data size {dataSize}");

            return offset.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error allocating block");
        }
        finally
        {
            StartInactivityTimer(); // Restart the timer after the operation
        }

        return 0;
    }

    private static async ValueTask CastAndDispose(IDisposable resource)
    {
        if (resource is IAsyncDisposable resourceAsyncDisposable)
        {
            await resourceAsyncDisposable.DisposeAsync();
        }
        else
        {
            resource.Dispose();
        }
    }

    private async ValueTask CloseStreamAsync()
    {
        if (!_streamOpen)
        {
            return; // Prevent closing if already closed
        }

        if (_journalManager != null)
        {
            await _journalManager.DisposeAsync();
        }

        //all stream will be disposed there
        await CastAndDispose(_fileStreamProvider);

        _streamOpen = false; // Mark stream as closed
    }

    private async Task CloseStreamInternalAsync()
    {
        _logger?.LogInformation("Closing streams");
        // Acquire both semaphores to ensure no operations are in progress
        // Use 'WaitAsync' to avoid potential deadlocks
        if (await _writeSemaphore.WaitAsync(_streamInactivityTimeout) &&
            await _readSemaphore.WaitAsync(_streamInactivityTimeout))
        {
            try
            {
                // Double-check if the timer is still running (it might have been restarted)
                if (_inactivityTimer?.Enabled == true)
                {
                    await CloseStreamAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing stream");
            }
            finally
            {
                _writeSemaphore.Release();
                _readSemaphore.Release();
            }
        }
    }

    private async Task DeleteInternalAsync(long blockAddress, CancellationToken cancellationToken)
    {
        var blockHeader = await ReadBlockHeaderAsync(blockAddress, cancellationToken);
        _logger?.LogInformation(
            $"Try to delete block at address {blockAddress}: blockSize:{blockHeader.BlockSize}, dataSize:{blockHeader.DataSize}, state:{blockHeader.State}");

        if (blockHeader.State != BlockUtils.BlockState.Used)
        {
            throw new DataException(
                $"Block at address {blockAddress} is not used. Only used blocks allowed to delete");
        }

        // Change block state to Deleted
        await BlockUtils.WriteBlockHeaderAsync(
            _fileStream!,
            new BlockUtils.BlockHeader(
                blockHeader.DataSize,
                blockHeader.BlockSize,
                BlockUtils.BlockState.Deleted
            ),
            cancellationToken);

        // Write to journal
        if (_journalManager != null)
        {
            await _journalManager.LogOperationAsync(
                EJournalOperation.DeleteDataBlock,
                blockAddress,
                blockHeader.BlockSize,
                blockHeader.DataSize,
                cancellationToken);
        }

        // Add to free list
        await _freeBlocksManager.AddToFreeListAsync(
            _fileStream,
            blockAddress,
            blockHeader.BlockSize,
            cancellationToken);

        // If we're deleting the root block, update the header
        if (blockAddress == _header.RootUsedBlock)
        {
            _logger?.LogInformation($"Deleting root block at address {blockAddress}");

            _header.RootUsedBlock = 0;
            await _header.WriteToStreamAsync(_fileStream, cancellationToken);
        }

        if (_journalManager != null)
        {
            await _journalManager.CommitAsync(cancellationToken);
        }
    }

    private async Task InitializeFileAsync()
    {
        _header = new BinaryStorageHeader(true); // Create a new header with default values.

        if (_options is not null)
        {
            // Customize the header based on the provided options.
            _header.FreeBlocksTableEntriesPerBlock = _options.EntriesPerBlock;

            // Set the storage flags based on options (e.g., using rounded block size).
            StorageFlags flags = StorageFlags.None;
            if(_options.UseRoundedBlockSize)
            {
                flags |= StorageFlags.UseRoundedBlockSize;
            }

            _header.StorageFlags = flags;
        }

        // Write the header to the stream.
        _fileStream!.Seek(0, SeekOrigin.Begin); // Ensure writing starts at the beginning.
        await _header.WriteToStreamAsync(_fileStream);
        await _fileStream.FlushAsync();
    }

    private void OnInactivityTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _logger?.LogInformation("Inactivity timer elapsed. Closing the storage stream.");
        // Close the stream on a separate thread to avoid blocking the timer thread
        Task.Run(CloseStreamInternalAsync);
    }

    private async Task OpenStreamAsync(IFileStreamProvider fileStreamProvider, bool createMode = false)
    {
        await Task.CompletedTask;

        if (_streamOpen)
        {
            return; // Prevent reopening if already open
        }

        _logger?.LogInformation($"Open stream: {_filename}, createMode: {createMode}");

        _fileStream = fileStreamProvider.GetFileStream(
            _filename,
            createMode ? FileMode.CreateNew : FileMode.OpenOrCreate);

        if (_useJounaling)
        {
            string journalFilename = Path.ChangeExtension(_filename, ".journal");
            var journalStream = fileStreamProvider.GetFileStream(journalFilename);
            _journalManager = new JournalManager(journalStream);
        }

        _streamOpen = true; // Mark stream as open
    }

    private async Task OpenStreamIfNeededAsync()
    {
        await Task.CompletedTask;
        StopInactivityTimer(); // Reset the timer on activity
        if (_fileStream == null || !_fileStream.CanRead || !_fileStream.CanWrite || !_streamOpen)
        {
            // Logic to open the stream, for example:
            //_fileStream = _fileStreamProvider.GetFileStream(_filename);
            await OpenStreamAsync(_fileStreamProvider);
            //_fileStream = new FileStream(_filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
    }

    private async Task<BlockUtils.BlockHeader> ReadBlockHeaderAsync(
        long blockAddress,
        CancellationToken cancellationToken)
    {
        _fileStream!.Seek(blockAddress, SeekOrigin.Begin);
        return await BlockUtils.ReadBlockHeaderAsync(_fileStream, cancellationToken);
    }

    private async Task<BlockUtils.BlockHeader> ReadDataHeaderInternalAsync(
        long dataAddress,
        CancellationToken cancellationToken)
    {
        _fileStream!.Seek(dataAddress, SeekOrigin.Begin);
        return await BlockUtils.ReadBlockHeaderAsync(_fileStream, cancellationToken);
    }

    private async Task<byte[]> ReadInternalAsync(long dataAddress, CancellationToken cancellationToken)
    {
        _fileStream!.Seek(dataAddress, SeekOrigin.Begin);
        var header = await ReadDataHeaderInternalAsync(dataAddress, cancellationToken);
        _logger?.LogInformation(
            $"Read data at address {dataAddress}: blockSize:{header.BlockSize}, dataSize:{header.DataSize}, state:{header.State}");
        if (header.BlockSize <= 0)
        {
            throw new DataException(
                $"DataHeader is invalid: Block size is {header.BlockSize} at address {dataAddress}");
        }

        if (header.DataSize > header.BlockSize)
        {
            throw new DataException(
                $"DataHeader is invalid: Data size {header.DataSize} is greater than block size {header.BlockSize} at address {dataAddress}");
        }

        var buffer = new byte[header.DataSize];
        int readCount = await _fileStream.ReadAsync(buffer, 0, header.DataSize, cancellationToken);
        if (readCount != header.DataSize)
        {
            throw new DataException($"Read {readCount} bytes instead of {header.DataSize}");
        }

        return buffer;
    }

    private void SetStreamInactivityTimeout(int? durationMinutes)
    {
        if (durationMinutes.HasValue)
        {
            _streamInactivityTimeout = TimeSpan.FromMinutes(durationMinutes.Value);
        }
    }

    private void StartInactivityTimer()
    {
        // Create a new timer if it doesn't exist or is disposed
        _inactivityTimer ??= new Timer(_streamInactivityTimeout.TotalMilliseconds);

        _inactivityTimer.Elapsed += OnInactivityTimerElapsed;
        _inactivityTimer.AutoReset = false; // Only trigger the event once
        _inactivityTimer.Start();
    }

    private void StopInactivityTimer()
    {
        _inactivityTimer?.Stop();
    }

    private async Task<long> UpdateBlockAsync(
        long blockAddress,
        int newBlockSize,
        byte[] data,
        CancellationToken cancellationToken)
    {
        // read old block info from disk
        var header = await ReadBlockHeaderAsync(blockAddress, cancellationToken);
        _logger?.LogInformation(
            $"Update block at address {blockAddress}: blockSize:{header.BlockSize}, dataSize:{header.DataSize}, state:{header.State}");
        if (header.BlockSize == newBlockSize)
        {
            // If the block  is the same, just update the data
            await WriteDataToBlockAsync(blockAddress, newBlockSize, data, cancellationToken);
            return blockAddress;
        }

        _logger?.LogInformation(
            $"Block size updated - old block size: {header.BlockSize}, new block size: {newBlockSize}");
        // If the block size changed, delete the old block and allocate a new one
        await DeleteInternalAsync(blockAddress, cancellationToken);
        return await WriteInternalAsync(data, null, cancellationToken);
    }

    private async Task WriteDataToBlockAsync(
        long blockAddress,
        int blockSize,
        byte[] data,
        CancellationToken cancellationToken)
    {
        if (_journalManager != null)
        {
            await _journalManager.LogOperationAsync(
                EJournalOperation.WriteDataBlock,
                blockAddress,
                blockSize,
                data.Length,
                cancellationToken);
        }

        _fileStream!.Seek(blockAddress, SeekOrigin.Begin);

        await BlockUtils.WriteBlockHeaderAsync(
            _fileStream!,
            new BlockUtils.BlockHeader(data.Length, blockSize, BlockUtils.BlockState.Used),
            cancellationToken);
        _logger?.LogInformation(
            $"Write data to block at address {blockAddress}: blockSize:{blockSize}, dataSize:{data.Length}, state:{BlockUtils.BlockState.Used}");

        _fileStream!.Seek(blockAddress + BlockUtils.BlockHeaderSize, SeekOrigin.Begin);
        await _fileStream.WriteAsync(data, 0, data.Length, cancellationToken);

        if (_journalManager != null)
        {
            await _journalManager.CommitAsync(cancellationToken);
        }
    }

    private async Task<long> WriteInternalAsync(
        byte[] data,
        long? existingBlockAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var blockWithHeaderSize = data.Length + BlockUtils.BlockHeaderSize;
        int blockSize = _header.StorageFlags.HasFlag(StorageFlags.UseRoundedBlockSize)
                            ? BlockUtils.RoundUpToPowerOfTwo(blockWithHeaderSize)
                            : blockWithHeaderSize;
        _logger?.LogInformation(
            $"Write internal to block at address {existingBlockAddress}: blockSize:{blockSize}, dataSize:{data.Length}, state:{BlockUtils.BlockState.Used}");
        long blockAddress;
        if (existingBlockAddress.HasValue)
        {
            blockAddress = await UpdateBlockAsync(existingBlockAddress.Value, blockSize, data, cancellationToken);
        }
        else
        {
            blockAddress = await AllocateBlockAsync(blockSize, data.Length, cancellationToken);
            await WriteDataToBlockAsync(blockAddress, blockSize, data, cancellationToken);
        }

        return blockAddress;
    }
}
