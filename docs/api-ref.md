# `IBinaryStorage` Interface

## Properties

- **`long RootUsedBlock { get; }`**  
  Retrieves the address (offset) of the root used block in the storage.

## Methods

- **`Task DeleteAsync(long dataAddress, CancellationToken cancellationToken = default)`**  
  Deletes the block of data located at the specified address asynchronously.
  - **Parameters**:
    - `long dataAddress`: The address of the data block to be deleted.
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.

- **`Task<List<FreeBlock>> GetFreeBlockMapAsync(CancellationToken cancellationToken = default)`**  
  Retrieves a list of free blocks in the storage, including each block's address and size.
  - **Parameters**:
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.

- **`Task<byte[]?> ReadAsync(long dataAddress, CancellationToken cancellationToken = default)`**  
  Reads the data from the specified address asynchronously.
  - **Parameters**:
    - `long dataAddress`: The address of the data block to read.
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.
  - **Returns**: A task representing the asynchronous read operation, containing the data as a byte array, or null if the address is invalid.

- **`Task<int> ReadDataSizeAsync(long dataAddress, CancellationToken cancellationToken = default)`**  
  Retrieves the size of the data block located at the specified address.
  - **Parameters**:
    - `long dataAddress`: The address of the data block.
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.
  - **Returns**: A task representing the asynchronous operation, containing the size of the data block in bytes.

- **`Task<byte[]?> ReadRootBlockAsync(CancellationToken cancellationToken = default)`**  
  Reads the root block's data asynchronously.
  - **Parameters**:
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.
  - **Returns**: A task representing the asynchronous read operation, containing the root block's data as a byte array.

- **`Task<long> WriteAsync(byte[] data, long? existingBlockOffset = null, CancellationToken cancellationToken = default)`**  
  Writes data to the storage asynchronously, optionally overwriting an existing block or creating a new one.
  - **Parameters**:
    - `byte[] data`: The data to write to the storage.
    - `long? existingBlockOffset`: (Optional) The offset of an existing block to overwrite. If null, a new block will be created.
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.
  - **Returns**: A task representing the asynchronous write operation, containing the address of the block where the data was written.

- **`Task<long> WriteRootBlockAsync(byte[] data, CancellationToken cancellationToken = default)`**  
  Writes data to the root block asynchronously.
  - **Parameters**:
    - `byte[] data`: The data to write to the root block.
    - `CancellationToken cancellationToken`: (Optional) A token to cancel the operation.
  - **Returns**: A task representing the asynchronous write operation, containing the address of the root block.

- **`BinaryStorageHeaderInfo GetHeaderRo()`**  
  Retrieves the storage header as a read-only object.
  - **Returns**: An instance of `BinaryStorageHeaderInfo` containing metadata about the storage.
