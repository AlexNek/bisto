# `IBinaryStorage` Interface

- `long RootUsedBlock { get; }`  
  Retrieves the address/offset of the root used block.

- `Task DeleteAsync(long dataAddress, CancellationToken cancellationToken = default)`  
  Deletes the block of data located at the specified address asynchronously.

- `Task<List<FreeBlock>> GetFreeBlockMapAsync(CancellationToken cancellationToken = default)`  
  Retrieves a list of free blocks in the storage, with each block's address and size.

- `Task<byte[]?> ReadAsync(long dataAddress, CancellationToken cancellationToken = default)`  
  Reads the data from the specified address asynchronously.

- `Task<int> ReadDataSizeAsync(long dataAddress, CancellationToken cancellationToken = default)`  
  Retrieves the size of the data block located at the specified address.

- `Task<byte[]?> ReadRootBlockAsync(CancellationToken cancellationToken = default)`  
  Reads the root block's data asynchronously.

- `Task<long> WriteAsync(byte[] data, long? existingBlockOffset = null, CancellationToken cancellationToken = default)`  
  Writes data to the storage asynchronously, optionally overwriting an existing block or creating a new one.

- `Task<long> WriteRootBlockAsync(byte[] data, CancellationToken cancellationToken = default)`  
  Writes data to the root block asynchronously.
  
- `BinaryStorageHeaderInfo GetHeaderRo()`  
  Get the storage header as readonly
