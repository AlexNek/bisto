# FreeBlockPersistence

The `FreeBlockPersistence` class is responsible for persisting and loading free blocks to and from disk. It uses a linked list approach to store free blocks, where each block in the list can contain multiple free block entries.

**Writing Blocks to Disk:**

1. **`UpdateFreeBlocksOnDiskAsync(List<FreeBlock> allBlocks, CancellationToken cancellationToken)`:** This method takes a list of all free blocks and persists them to disk.
2. **Block Size and Entries:** It calculates the number of free block entries that can fit in a single block based on the minimum block size (`_minBlockSize`) and the size of each free block entry (`FreeBlocksManager.FreeItemRecordLength`).
3. **Iterating through Blocks:** It iterates through the list of free blocks, dividing them into chunks that fit into a single block.
4. **Allocating New Blocks:** If the current block address is 0 (meaning no block has been allocated yet), it allocates a new block using `AllocateNewBlockAsync()`. The header's `FreeBlocksTableOffset` is updated to point to the first allocated block.
5. **Writing Block Data:** For each chunk of free blocks, it writes the block data to disk using `WriteBlockAsync()`. This includes the next block address (forming the linked list) and the serialized free block entries.
6. **Updating Current Address:** If it's not the last block, the current address is moved to the next block's address.

**Linking Blocks:**

- Each block on disk contains a field (`nextBlockAddress`) that stores the address of the next block in the list.
- The last block in the list has a `nextBlockAddress` of 0, indicating the end of the list.
- The `BinaryStorageHeader`'s `FreeBlocksTableOffset` points to the first block in the list.

**Reading Blocks from Disk:**

1. **`ReadAllBlocksAsync(CancellationToken cancellationToken = default)`:** This method reads all free blocks from disk.
2. **Starting from Header:** It starts reading from the address specified in the header's `FreeBlocksTableOffset`.
3. **Iterating through Blocks:** It iterates through the linked list of blocks until it encounters a block with a `nextBlockAddress` of 0.
4. **Reading Block Data:** For each block, it reads the block data using `ReadBlockAsync()`, deserializes the free block entries, and adds them to the `allBlocks` list.

**In summary:**

- Free blocks are stored on disk as a linked list of blocks.
- Each block contains multiple free block entries and the address of the next block in the list.
- The `FreeBlockPersistence` class handles reading and writing these blocks, ensuring the linked list structure is maintained.