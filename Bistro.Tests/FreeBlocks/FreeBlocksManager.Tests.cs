using System.Text;

using Bisto.FreeBlocks;
using Bisto.Journal;

using FluentAssertions;

using Moq;

using Xunit.Abstractions;

namespace Bisto.Tests.FreeBlocks
{
    public class FreeBlocksManagerTests
    {
        private readonly ITestOutputHelper _output;

        public FreeBlocksManagerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task AddToFreeListAsync_ShouldAddBlockToFreeList()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var address = 1000L;
            var size = 2048;
            var expectedBlock = new FreeBlock(address, size);

            // Act
            await manager.AddToFreeListAsync(stream, address, size);

            // Assert
            var freeBlocks = await manager.GetFreeBlockMapAsync();
            freeBlocks.Should().Contain(expectedBlock);
        }

        [Fact]
        public async Task AddToFreeListAsync_ShouldMergeNextAdjacentBlocks()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var address1 = 1000L;
            var size1 = 2048;
            var address2 = address1 + size1;
            var size2 = 1024;
            FreeBlock expectedBlock = new FreeBlock(address1, size1 + size2);
            var expectedFreeBlocks = new List<FreeBlock> { expectedBlock };

            // Act
            await manager.AddToFreeListAsync(stream,address1, size1);
            await manager.AddToFreeListAsync(stream,address2, size2);

            // Assert
            var freeBlocks = await manager.GetFreeBlockMapAsync();
            freeBlocks.Should().HaveCount(1, "because there should be only one consolidated block");
            freeBlocks.Single().Offset.Should().Be(
                expectedBlock.Offset,
                "because the consolidated block should start at the expected offset");
            freeBlocks.Single().Size.Should().Be(
                expectedBlock.Size,
                "because the consolidated block should have the correct size");
            freeBlocks.Should().BeEquivalentTo(
                expectedFreeBlocks,
                "because the free blocks list should contain the single consolidated block.");
        }

        [Fact]
        public async Task AddToFreeListAsync_ShouldMergePreviousAdjacentBlocks()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var address1 = 1000L;
            var size1 = 2048;
            var address2 = address1 + size1;
            var size2 = 1024;
            FreeBlock expectedBlock = new FreeBlock(address1, size1 + size2);
            var expectedFreeBlocks = new List<FreeBlock> { expectedBlock };

            // Act
            await manager.AddToFreeListAsync(stream,address1, size1);
            await manager.AddToFreeListAsync(stream,address2, size2);

            // Assert
            var freeBlocks = await manager.GetFreeBlockMapAsync();
            freeBlocks.Should().HaveCount(1, "because there should be only one consolidated block");
            freeBlocks.Single().Offset.Should().Be(
                expectedBlock.Offset,
                "because the consolidated block should start at the expected offset");
            freeBlocks.Single().Size.Should().Be(
                expectedBlock.Size,
                "because the consolidated block should have the correct size");
            freeBlocks.Should().BeEquivalentTo(
                expectedFreeBlocks,
                "because the free blocks list should contain the single consolidated block.");
        }

        //TODO: Implement table block free before
        //[Fact]
        public async Task GetFreeBlockMapAsync_ShouldReduceBlocksCorrectly()
        {
            const int InitialFreeBlocksTableCount = 5;
            const int ReducedFreeBlocksTableCount = 1;
            const int EntriesPerBlock = 64;
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync(EntriesPerBlock);

            int totalEntries = InitialFreeBlocksTableCount * EntriesPerBlock;

            // Initial blocks
            var initialBlocks = GenerateFreeBlocks(totalEntries, 256, 32);

            // Add initial blocks
            foreach (var block in initialBlocks)
            {
                await manager.AddToFreeListAsync(stream,block.Offset, block.Size);
            }

            var realInitiallyFreeBlocks = await manager.GetFreeBlockMapAsync();

            // Act
            // Allocate space equivalent to one block of entries
            int freeTableBlockCount = InitialFreeBlocksTableCount - ReducedFreeBlocksTableCount;
            for (int i = 0; i < freeTableBlockCount * EntriesPerBlock; i++)
            {
                await manager.TryAllocateAsync(stream, initialBlocks[i].Size);
            }

            var finalBlocks = await manager.GetFreeBlockMapAsync();

            // Assert
            // NOTE: entriesPerBlock internally stored as fixed tables with entriesPerBlock in every table
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendFormat("because we've allocated {0} ", freeTableBlockCount);
            messageBuilder.AppendFormat("block of entries={0} ", ReducedFreeBlocksTableCount * EntriesPerBlock);
            messageBuilder.AppendFormat("and additional {0} table blocks must be free. ", freeTableBlockCount);
            //messageBuilder.AppendFormat("Received {0}", finalBlocks.Count);

            string message = messageBuilder.ToString();

            finalBlocks.Count.Should().Be(
                ReducedFreeBlocksTableCount * EntriesPerBlock + freeTableBlockCount,
                message);

            int totalInitialFreeSpace = initialBlocks.Sum(b => b.Size);
            int totalFinalFreeSpace = finalBlocks.Sum(b => b.Size);
            int allocatedSpace = initialBlocks.Take(ReducedFreeBlocksTableCount * EntriesPerBlock).Sum(b => b.Size);

            totalFinalFreeSpace.Should().Be(
                totalInitialFreeSpace - allocatedSpace,
                "because the total free space should be reduced by the allocated amount");

            // Output for debugging
            _output.WriteLine($"Initial blocks count: {initialBlocks.Count}");
            _output.WriteLine($"Initial total free space: {totalInitialFreeSpace}");
            _output.WriteLine($"Blocks allocated: {ReducedFreeBlocksTableCount}");
            _output.WriteLine($"Space allocated: {allocatedSpace}");
            _output.WriteLine($"Final blocks count: {finalBlocks.Count}");
            _output.WriteLine($"Final total free space: {totalFinalFreeSpace}");

            // Additional checks
            if (finalBlocks.Count >= initialBlocks.Count)
            {
                _output.WriteLine("Warning: The number of free blocks did not decrease as expected.");
            }

            if (totalFinalFreeSpace > totalInitialFreeSpace - allocatedSpace)
            {
                _output.WriteLine("Warning: The total free space is larger than expected after allocation.");
            }
            else if (totalFinalFreeSpace < totalInitialFreeSpace - allocatedSpace)
            {
                _output.WriteLine("Warning: The total free space is smaller than expected after allocation.");
            }
        }

        [Fact]
        public async Task GetFreeBlockMapAsync_ShouldReturnCorrectBlocksWhenBlocksAreAddedInRandomOrder()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var randomBlocks = new List<FreeBlock>
                                   {
                                       new FreeBlock(1000L, 2048),
                                       new FreeBlock(3072L, 1024),
                                       new FreeBlock(5120L, 512),
                                       new FreeBlock(15120L, 256)
                                   };

            // Act
            foreach (var block in randomBlocks)
            {
                await manager.AddToFreeListAsync(stream,block.Offset, block.Size);
            }

            var result = await manager.GetFreeBlockMapAsync();

            // Assert
            result.Should().BeEquivalentTo(
                randomBlocks,
                "because the free blocks list should contain the blocks added in random order");
        }

        [Fact]
        public async Task GetFreeBlockMapAsync_ShouldReturnExactDeletedRecords()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var deletedBlocks = new List<FreeBlock>
                                    {
                                        new FreeBlock(1000L, 2048),
                                        new FreeBlock(3072L, 1024),
                                        new FreeBlock(5120L, 512)
                                    };

            foreach (var block in deletedBlocks)
            {
                await manager.AddToFreeListAsync(stream,block.Offset, block.Size);
            }

            // Act
            var result = await manager.GetFreeBlockMapAsync();

            // Assert
            result.Should().BeEquivalentTo(deletedBlocks);
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldAllocateBlockSuccessfully()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var address = 1000L;
            var size = 2048;

            await manager.AddToFreeListAsync(stream,address, size);

            // Act
            var allocatedAddress = await manager.TryAllocateAsync(stream, size);

            // Assert
            allocatedAddress.Should().Be(address);
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldReturnCorrectAddressWhenMultipleBlocksAreAllocatedInAscendingOrder()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var blocks = new List<FreeBlock>
                             {
                                 new FreeBlock(1000L, 2048),
                                 new FreeBlock(3072L, 1024),
                                 new FreeBlock(5120L, 512),
                                 new FreeBlock(15120L, 256)
                             };

            foreach (var block in blocks)
            {
                await manager.AddToFreeListAsync(stream,block.Offset, block.Size);
            }

            var expectedAddresses = new List<long> { 1000L, 3072L, 5120L };

            // Act
            var allocatedAddresses = new List<long?>();
            foreach (var blockSize in new List<int> { 2048, 1024, 512 })
            {
                allocatedAddresses.Add(await manager.TryAllocateAsync(stream, blockSize));
            }

            // Assert
            allocatedAddresses.Should().BeEquivalentTo(expectedAddresses);
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldReturnCorrectAddressWhenMultipleBlocksAreAllocatedInDescendingOrder()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var blocks = new List<FreeBlock>
                             {
                                 new FreeBlock(1000L, 2048),
                                 new FreeBlock(3072L, 1024),
                                 new FreeBlock(5120L, 512),
                                 new FreeBlock(15120L, 256)
                             };

            foreach (var block in blocks)
            {
                await manager.AddToFreeListAsync(stream,block.Offset, block.Size);
            }

            var expectedAddresses = new List<long> { 5120L, 3072L, 1000L };

            // Act
            var allocatedAddresses = new List<long?>();
            foreach (var blockSize in new List<int> { 512, 1024, 2048 })
            {
                allocatedAddresses.Add(await manager.TryAllocateAsync(stream, blockSize));
            }

            // Assert
            allocatedAddresses.Should().BeEquivalentTo(expectedAddresses);
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldReturnCorrectAddressWhenMultipleBlocksAvailable()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var block1 = new FreeBlock(1000L, 2048);
            var block2 = new FreeBlock(3072L, 1024);
            var expectedAddress = 3072L;
            var expectedSize = 1024;

            await manager.AddToFreeListAsync(stream,block1.Offset, block1.Size);
            await manager.AddToFreeListAsync(stream,block2.Offset, block2.Size);

            // Act
            var allocatedAddress = await manager.TryAllocateAsync(stream, expectedSize);

            // Assert
            allocatedAddress.Should().Be(expectedAddress);
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldReturnNullIfNoBlockAvailable()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var size = 2048;

            // Act
            var allocatedAddress = await manager.TryAllocateAsync(stream, size);

            // Assert
            allocatedAddress.Should().BeNull();
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldReturnNullWhenNoFreeBlocksAvailable()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            // Act
            var result = await manager.TryAllocateAsync(stream, 1024);

            // Assert
            result.Should().BeNull("because there are no free blocks available");
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldReturnNullWhenRequestedSizeIsZero()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();
            var size = 0;

            // Act
            var result = await manager.TryAllocateAsync(stream, size);

            // Assert
            result.Should().BeNull("because requested size is zero");
        }

        [Fact]
        public async Task TryAllocateAsync_ShouldSplitBigFreeBlockIntoTwo()
        {
            // Arrange
            var stream = new MemoryStream();
            var manager = await CreateClassUnderTestAsync();

            var bigBlockAddress = 1000L;
            var bigBlockSize = 512;
            await manager.AddToFreeListAsync(stream,bigBlockAddress, bigBlockSize);

            var expectedSmallBlock1 = new FreeBlock(bigBlockAddress, 256);
            var expectedSmallBlock2 = new FreeBlock(bigBlockAddress + 256, 256);

            // Act
            var allocatedAddress = await manager.TryAllocateAsync(stream, 256);

            // Assert
            allocatedAddress.Should().Be(bigBlockAddress, "because 200 bytes should be allocated from the big block");

            var freeBlocks = await manager.GetFreeBlockMapAsync();
            freeBlocks.Should().HaveCount(1, "because only one block should be left after allocation");
            freeBlocks.Should().Contain(
                expectedSmallBlock2,
                "because the second block should be the remaining part after allocation");
        }

        // private async Task ClearAndAddBlocksAsync(IFreeBlocksManager manager, List<FreeBlock> blocks)
        // {
        //     // This method simulates clearing existing blocks and adding new ones
        //     // You may need to adjust this based on the actual implementation of IFreeBlocksManager
        //     var existingBlocks = await manager.GetFreeBlockMapAsync();
        //     foreach (var block in existingBlocks)
        //     {
        //         await manager.TryAllocateAsync(_fileStream, block.Size);
        //     }
        //
        //     foreach (var block in blocks)
        //     {
        //         await manager.AddToFreeListAsync(block.Offset, block.Size);
        //     }
        // }

        private async Task<IFreeBlocksManager>  CreateClassUnderTestAsync(int entriesPerBlock = 256)
        {
            var mockFileStream = new Mock<Stream>();
            var testHeader = new TestBinaryStorageHeader();
            var mockJournalManager = new Mock<JournalManager>(MockBehavior.Strict, mockFileStream.Object);

            //mockHeader.Setup(h => h.FreeBlocksTableOffset).Returns(0L);
            //mockHeader.Setup(h => h.FreeBlocksTableBlockCount).Returns(1);

            var manager = await FreeBlocksManager.CreateAsync(
                mockFileStream.Object,
                testHeader,
                mockJournalManager.Object, null,
                cacheSize: 100,
                entriesPerBlock: entriesPerBlock);

            // var manager = new Bisto.FreeBlocks.FreeBlocksManager(
            //     memoryStream,
            //     testHeader, 
            //     mockJournalManager.Object
            //     );
            return manager;
        }

        // private List<FreeBlock> GenerateFreeBlocks(int count, int maxOffset, int blockSize)
        // {
        //     var random = new Random(42);
        //     return Enumerable.Range(0, count)
        //         .Select(_ => new FreeBlock(random.Next(maxOffset), blockSize))
        //         .ToList();
        // }

        private List<FreeBlock> GenerateFreeBlocks(int count, int blockSize, int gap = 1024)
        {
            var blocks = new List<FreeBlock>();
            long currentOffset = 0;

            for (int i = 0; i < count; i++)
            {
                blocks.Add(new FreeBlock(currentOffset, blockSize));
                currentOffset += blockSize + gap; // Add a gap between blocks
            }

            return blocks;
        }

        private class TestBinaryStorageHeader : BinaryStorageHeader
        {
            public override int FreeBlocksTableEntriesPerBlock => 1;

            public override long FreeBlocksTableOffset => 0L;
        }
    }
}
