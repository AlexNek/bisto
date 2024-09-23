## **Free Block Management**

The `FreeBlocksManager` maintains a record of free blocks in a file. A **free block** is a chunk of memory in the storage file that is available for future writes. The class provides methods to:

- Add free blocks back to the free list when data is deleted (`AddToFreeListAsync`).

- Allocate blocks for new data (`TryAllocateAsync`).

- Keep track of all free blocks, organized in memory for efficient access.



### **Data Structures**

Two **sorted dictionaries** are used to manage free blocks:

1. **By Address (`_freeBlocksByAddress`)**: This dictionary maintains free blocks sorted by their physical address (offset) in the file. This is useful when merging adjacent free blocks.



```csharp

private readonly SortedDictionary<long, (long Offset, int Size)> _freeBlocksByAddress;

```



2. **By Size (`_freeBlocksBySize`)**: This dictionary maintains free blocks sorted by their size. This makes it easy to quickly find a block of sufficient size for allocation.



```csharp

private readonly SortedDictionary<int, List<(long Offset, int Size)>> _freeBlocksBySize;

```

### **How Free Blocks Are Stored and Read**



Free blocks are stored in the file as a table of free block entries (offsets and sizes). These entries are read into memory during initialization and updated as operations occur.



1. **Storage Location**

   - The location of the free block table is defined by `_tableStartOffset`, which is extracted from the `Bisto.V2.BinaryStorageHeader`. This header keeps metadata like the start offset and the number of blocks in the free block table.



private readonly SortedDictionary<long, (long Offset, int Size)> _freeBlocksByAddress;



2. **How Free Blocks Are Read**

   - When the `FreeBlocksManager` is initialized, it reads all free blocks from the file using the method `InitializeFreeBlocksAsync`.

   - The table is split into blocks, each of size `_blockSize`, which is typically 4096 bytes. The class reads these blocks sequentially from the file using `ReadBlockAsync`, deserializes them, and adds the entries into the in-memory dictionaries.



   - `DeserializeBlock` is responsible for converting the binary data (offset and size pairs) into a list of `(long Offset, int Size)` tuples, which represent the free blocks.



3. **How Free Blocks Are Written**

   - When free blocks are updated (e.g., when blocks are merged, added, or allocated), they need to be written back to the file. This is done by the `UpdateFreeBlocksOnDiskAsync` method, which organizes the free blocks into pages (according to the `_blockSize`), serializes them, and writes them back to disk.



   - The `WriteBlockAsync` method serializes the free block data into binary format and writes it to the appropriate offset in the file.



### **Key Methods**



#### 1. **`AddToFreeListAsync`**

When a block of data is no longer needed, this method adds the block (address and size) to the free list.

- It first logs the operation using `JournalManager` for crash recovery.

- Then it adds the block to the in-memory dictionaries (`_freeBlocksByAddress` and `_freeBlocksBySize`).

- It attempts to merge the block with adjacent free blocks if possible (to prevent fragmentation).

- Finally, the changes are written back to the file.



#### 2. **`TryAllocateAsync`**

When new data needs to be written, this method attempts to find a free block of sufficient size.

- It searches through the `_freeBlocksBySize` dictionary to find a block that fits.

- Once a block is found, it logs the operation and removes the block from the free list.

- If the block is larger than required, the remaining space is added back to the free list.



### **How `JournalManager` Works**

- **`JournalManager`** is used to log all operations related to free block management (e.g., adding to the free list or allocating blocks). This is essential for crash recovery, ensuring that incomplete operations can be rolled back.

- The class logs every operation in a journal file, and then commits these operations to ensure consistency.



### **Conclusion**

In summary, the `FreeBlocksManager` now tracks free blocks in memory using two sorted dictionaries for efficient lookup. Free blocks are read from and written to a file in blocks, and the `JournalManager` ensures that changes are logged and can be safely recovered in case of a crash. This structure helps manage file storage efficiently, preventing fragmentation and ensuring fast allocation and deallocation of space.



#### remarks

the FreeBlocksManager is designed to handle concurrent access and ensure data integrity when writing to the file, especially in scenarios where different data could be present. Here's how it manages these challenges:



### 1. **Locking Mechanism**



The class uses a `SemaphoreSlim` and `ReaderWriterLockSlim` to ensure that only one thread can modify the free block data at a time. This prevents multiple operations from corrupting the file or in-memory structures:



- The `SemaphoreSlim _semaphore` is used to control access to critical sections of code, especially during read/write operations like `AddToFreeListAsync` and `TryAllocateAsync`. It ensures that only one operation modifies the free block data at any given time.



- `ReaderWriterLockSlim` is used to provide thread-safe access to read and write operations. This helps ensure that multiple threads can read the free block data concurrently, but only one thread can modify it at a time.





### 2. **Journal Logging for Crash Recovery**



The use of `JournalManager` ensures that changes to the free block list are logged before they are committed to the actual file. This means that in the event of a crash or unexpected shutdown, the system can roll back incomplete operations. Here’s how it works:



- Each operation (e.g., adding to the free list, allocating a block) is logged via the JournalManager.

- Once the operation is successfully logged, the system can safely proceed with modifying the actual file.



### 3. **Caching**



The class uses a `MemoryCacheManager` to cache blocks of free block data read from the file. This prevents unnecessary reads and writes to the disk, improving performance and reducing the chance of data corruption due to multiple file access attempts.



### 4. **Safe Updates to the Free Block Table**



When updating the free block table in the file (e.g., merging blocks or adding new ones), the class uses structured methods like `UpdateFreeBlocksOnDiskAsync` and `WriteBlockAsync` to ensure data is written back in a controlled and consistent manner. This ensures that:



- The file is not overwritten arbitrarily.

- The blocks are written sequentially in a structured format, preventing data corruption.



### 5. **Separation of Data**



The system ensures that only the free block table section of the file is written to, without affecting other data. The `FreeBlocksManager` is specifically designed to manage the free blocks section, which is separated from other parts of the file, such as the actual data blocks or metadata.



In short, `FreeBlocksManager` does not write data blindly. It employs proper synchronization mechanisms, logging, and structured updates to handle different data being present in the file, ensuring integrity and safety in a multi-threaded or concurrent environment.

