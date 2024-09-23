using Bisto.FreeBlocks;

using FluentAssertions;

using Xunit.Abstractions;

namespace Bisto.Tests.FreeBlocks;

public class FreeBlockPersistenceTests
{
    private readonly ITestOutputHelper _output;

    public FreeBlockPersistenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AllocateNewBlockAsync_CreatesBlockAndUpdatesHeader()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        var header = new BinaryStorageHeader(true); // Initialize with default values
        await header.WriteToStreamAsync(memoryStream, CancellationToken.None); // Write initial header

        var cut = CreateClassUnderTest(memoryStream, header); // Use 256 as minBlockSize

        // Act
        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, new List<FreeBlock> { new FreeBlock(100, 50) }, CancellationToken.None);

        // Assert
        // The FreeBlocksTableOffset should be exactly at the end of the header if this is the first allocation
        header.FreeBlocksTableOffset.Should().BeGreaterThanOrEqualTo(BinaryStorageHeader.HeaderSize);
        // Verify that the stream length is greater than or equal to the header size, indicating data was written
        memoryStream.Length.Should().BeGreaterThanOrEqualTo(BinaryStorageHeader.HeaderSize);
    }

    [Fact]
    public async Task ReadAllBlocksAsync_WithNoBlocks_ReturnsEmptyList()
    {
        // Arrange
        MemoryStream memoryStream = new MemoryStream();
        var cut = CreateClassUnderTest(
            memoryStream,
            new BinaryStorageHeader { FreeBlocksTableOffset = 0 });

        // Act
        var result = await cut.ReadAllBlocksAsync(memoryStream, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateFreeBlocksOnDiskAsync_AllocatesNewTableBlocksCorrectly()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        int entriesPerBlock = 64;
        int totalBlocks = 3;
        int totalEntries = totalBlocks * entriesPerBlock;

        var initialHeader = new BinaryStorageHeader { FreeBlocksTableOffset = 0 };
        await initialHeader.WriteToStreamAsync(memoryStream);

        var cut = CreateClassUnderTest(memoryStream, initialHeader, entriesPerBlock);

        BinaryStorageHeader updatedHeader =
            await Part1InitialFillingWithFreeBlocks(cut, memoryStream, totalEntries, 50, 25);

        long streamLengthAfterInitialFilling = memoryStream.Length;

        // Create more free blocks than initially allocated
        List<FreeBlock> updatedFreeBlocks = GenerateFreeBlocks(totalEntries + entriesPerBlock, 100, 40);

        // Act
        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, updatedFreeBlocks, CancellationToken.None);
        var finalReadBlocks = await cut.ReadAllBlocksAsync(memoryStream, CancellationToken.None);

        // Assert
        memoryStream.Position = 0;
        var finalHeader = await BinaryStorageHeader.ReadFromStreamAsync(memoryStream);

        finalHeader.FreeBlocksTableOffset.Should().Be(updatedHeader.FreeBlocksTableOffset);
        memoryStream.Length.Should().BeGreaterThan(streamLengthAfterInitialFilling);

        finalReadBlocks.Should().HaveCount(updatedFreeBlocks.Count);

        VerifyAllBlocksItems(finalReadBlocks, updatedFreeBlocks);

        // Verify new block allocation
        int expectedNewBlocks = (int)Math.Ceiling((double)(updatedFreeBlocks.Count - totalEntries) / entriesPerBlock);

        // Output for debugging
        _output.WriteLine($"Initial entries: {totalEntries}");
        _output.WriteLine($"Updated entries: {updatedFreeBlocks.Count}");
        _output.WriteLine($"Expected new blocks: {expectedNewBlocks}");
        _output.WriteLine($"Initial stream length: {streamLengthAfterInitialFilling}");
        _output.WriteLine($"Final stream length: {memoryStream.Length}");
    }

    [Fact]
    public async Task UpdateFreeBlocksOnDiskAsync_CreatesAdditionalTableBlocks()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        var header = new BinaryStorageHeader { FreeBlocksTableOffset = 0 };
        await header.WriteToStreamAsync(memoryStream); // Write the header to the stream
        memoryStream.Position = 0; // Reset stream position

        int minBlockSize = 256; // Small block size to force creation of multiple blocks
        var cut = CreateClassUnderTest(memoryStream, header, minBlockSize);

        // Create a large number of free blocks to force creation of multiple table blocks
        var freeBlocks = new List<FreeBlock>();
        for (int i = 0; i < 1000; i++)
        {
            freeBlocks.Add(new FreeBlock(i * 100, 50));
        }

        // Act
        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, freeBlocks, CancellationToken.None);

        // Assert
        memoryStream.Position = 0;
        var updatedHeader = await BinaryStorageHeader.ReadFromStreamAsync(memoryStream);
        updatedHeader.FreeBlocksTableOffset.Should().BeGreaterThan(0);

        // Verify that multiple blocks were created
        memoryStream.Length.Should().BeGreaterThan(minBlockSize * 2); // At least two blocks should be created

        // Read all blocks to ensure they were written correctly
        var readBlocks = await cut.ReadAllBlocksAsync(memoryStream, CancellationToken.None);
        readBlocks.Should().HaveCount(freeBlocks.Count);
        readBlocks.Should().BeEquivalentTo(freeBlocks);
    }

    [Fact]
    public async Task UpdateFreeBlocksOnDiskAsync_FreesUnusedTableBlocksCorrectly()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        int entriesPerBlock = 64;
        int totalBlocks = 5; // Start with more blocks
        int totalEntries = totalBlocks * entriesPerBlock;

        var initialHeader = new BinaryStorageHeader { FreeBlocksTableOffset = 0 };
        await initialHeader.WriteToStreamAsync(memoryStream);

        var cut = CreateClassUnderTest(memoryStream, initialHeader, entriesPerBlock);

        BinaryStorageHeader updatedHeader =
            await Part1InitialFillingWithFreeBlocks(cut, memoryStream, totalEntries, 100, 50);

        long streamLengthAfterInitialFilling = memoryStream.Length;

        // Create fewer free blocks than initially allocated
        List<FreeBlock> updatedFreeBlocks = GenerateFreeBlocks(totalEntries - entriesPerBlock, 200, 75);

        // Act
        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, updatedFreeBlocks, CancellationToken.None);
        var finalReadBlocks = await cut.ReadAllBlocksAsync(memoryStream, CancellationToken.None);

        // Assert
        memoryStream.Position = 0;
        var finalHeader = await BinaryStorageHeader.ReadFromStreamAsync(memoryStream);

        finalHeader.FreeBlocksTableOffset.Should().Be(updatedHeader.FreeBlocksTableOffset);

        // The stream length should remain the same as we don't physically remove blocks
        memoryStream.Length.Should().Be(streamLengthAfterInitialFilling);

        int blocksNeededForUpdated = (int)Math.Ceiling((double)updatedFreeBlocks.Count / entriesPerBlock);
        int actualTotalBlocks = totalBlocks - 1;
        int expectedFinalEntries = actualTotalBlocks * entriesPerBlock;
        finalReadBlocks.Count.Should().Be(expectedFinalEntries);

        // Verify all original blocks are correctly updated
        VerifyAllBlocksItems(finalReadBlocks.Take(updatedFreeBlocks.Count).ToList(), updatedFreeBlocks);

        var freedTableBlocks = finalReadBlocks.Skip(updatedFreeBlocks.Count).ToList();
        freedTableBlocks.Should().HaveCount(expectedFinalEntries - updatedFreeBlocks.Count);

        // Verify that all freed blocks have the same size (assuming they should be uniform)
        if (freedTableBlocks.Any())
        {
            int freedBlockSize = freedTableBlocks[0].Size;
            freedTableBlocks.Should().AllSatisfy(block => block.Size.Should().Be(freedBlockSize));
        }

        // Output for debugging
        _output.WriteLine($"Initial entries: {totalEntries}");
        _output.WriteLine($"Updated entries: {updatedFreeBlocks.Count}");
        _output.WriteLine($"Blocks needed for updated entries: {blocksNeededForUpdated}");
        _output.WriteLine($"Actual total blocks: {actualTotalBlocks}");
        _output.WriteLine($"Expected final entries: {expectedFinalEntries}");
        _output.WriteLine($"Initial stream length: {streamLengthAfterInitialFilling}");
        _output.WriteLine($"Final stream length: {memoryStream.Length}");
        _output.WriteLine($"Final read blocks count: {finalReadBlocks.Count}");
        if (freedTableBlocks.Any())
        {
            _output.WriteLine($"Freed block size: {freedTableBlocks[0].Size}");
        }
    }

    //[Fact]
    //public async Task UpdateFreeBlocksOnDiskAsync_ShouldNotOverrideHeaderOrPreExistingData()
    //{
    //    // Arrange
    //    var preExistingData = new byte[128]; // Data after the header that must not be overwritten
    //    for (int i = 0; i < preExistingData.Length; i++)
    //    {
    //        preExistingData[i] = (byte)(i + 1); // Fill with distinct data
    //    }

    //    var memoryStream = new MemoryStream();

    //    // Initialize and write the header at the beginning
    //    var header = new BinaryStorageHeader(true); // Initialize the header with defaults
    //    await header.WriteToStreamAsync(memoryStream); // Write header to stream

    //    // Write pre-existing data after the header
    //    memoryStream.Write(preExistingData, 0, preExistingData.Length);

    //    // Set the position back to the beginning to simulate a real scenario where we would read/write data
    //    memoryStream.Position = 0;

    //    var cut = CreateClassUnderTest(memoryStream, header);

    //    // Act
    //    await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, new List<FreeBlock> { new FreeBlock(100, 50) }, CancellationToken.None);

    //    // Assert
    //    // Initialize another header from the stream and compare it with the original
    //    memoryStream.Position = 0;
    //    var reloadedHeader = await BinaryStorageHeader.ReadFromStreamAsync(memoryStream);
    //    reloadedHeader.Description.Should().Be(header.Description);
    //    reloadedHeader.FirstFreeBlock.Should().Be(header.FirstFreeBlock);
    //    reloadedHeader.FreeBlocksTableOffset.Should().Be(header.FreeBlocksTableOffset);

    //    // Verify that the pre-existing data is intact
    //    var preExistingBuffer = new byte[preExistingData.Length];
    //    await memoryStream.ReadAsync(preExistingBuffer, 0, preExistingBuffer.Length);
    //    preExistingBuffer.Should().BeEquivalentTo(preExistingData); // Ensure pre-existing data is not overwritten

    //    // Verify that the free blocks are written after the pre-existing data
    //    memoryStream.Length.Should()
    //        .BeGreaterThan(
    //            BinaryStorageHeader.HeaderSize
    //            + preExistingData.Length); // New data was appended after the header and pre-existing data
    //}

    [Fact]
    public async Task UpdateFreeBlocksOnDiskAsync_UpdatesExistingTableBlocks()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        int entriesPerBlock = 128;
        int totalBlocks = 5;
        int totalEntries = totalBlocks * entriesPerBlock;

        var initialHeader = new BinaryStorageHeader { FreeBlocksTableOffset = 0 };
        await initialHeader.WriteToStreamAsync(memoryStream);

        var cut = CreateClassUnderTest(memoryStream, initialHeader, entriesPerBlock);

        BinaryStorageHeader updatedHeader =
            await Part1InitialFillingWithFreeBlocks(cut, memoryStream, totalEntries, 100, 50);

        long streamLengthAfterInitialFilling = memoryStream.Length;

        List<FreeBlock> updatedFreeBlocks = GenerateFreeBlocks(totalEntries, 200, 75);

        // Act
        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, updatedFreeBlocks, CancellationToken.None);
        var finalReadBlocks = await cut.ReadAllBlocksAsync(memoryStream, CancellationToken.None);

        // Assert
        memoryStream.Position = 0;
        var finalHeader = await BinaryStorageHeader.ReadFromStreamAsync(memoryStream);

        finalHeader.FreeBlocksTableOffset.Should().Be(updatedHeader.FreeBlocksTableOffset);
        memoryStream.Length.Should().Be(streamLengthAfterInitialFilling);

        finalReadBlocks.Should().HaveCount(updatedFreeBlocks.Count);

        VerifyAllBlocksItems(finalReadBlocks, updatedFreeBlocks);

        // Detailed output for debugging
        Console.WriteLine("Detailed comparison of updated vs final blocks:");
        for (int i = 0; i < Math.Min(10, finalReadBlocks.Count); i++)
        {
            Console.WriteLine($"Index {i}:");
            Console.WriteLine($"  Updated: Address={updatedFreeBlocks[i].Offset}, Size={updatedFreeBlocks[i].Size}");
            Console.WriteLine($"  Final:   Address={finalReadBlocks[i].Offset}, Size={finalReadBlocks[i].Size}");
        }
    }

    [Fact]
    public async Task UpdateFreeBlocksOnDiskAsync_WithBlocks_WritesBlocksCorrectly()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        var cut = CreateClassUnderTest(
            memoryStream,
            new BinaryStorageHeader { FreeBlocksTableOffset = 0 });
        var freeBlocks = new List<FreeBlock> { new FreeBlock(100, 50), new FreeBlock(200, 75) };

        // Act
        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, freeBlocks, CancellationToken.None);

        // Assert
        memoryStream.Position = 0;
        memoryStream.Length.Should().BeGreaterThan(0);
    }

    //  helper method to create the class under test
    private FreeBlockPersistence CreateClassUnderTest(
        Stream stream,
        BinaryStorageHeader header,
        int entriesPerBlock = 128)
    {
        int cacheSize = 1024;
        return new FreeBlockPersistence(header, null, cacheSize, entriesPerBlock);
    }

    private static List<FreeBlock> GenerateFreeBlocks(int totalEntries, int addressStep, int blockSize)
    {
        var initialFreeBlocks = new List<FreeBlock>();
        for (int i = 0; i < totalEntries; i++)
        {
            initialFreeBlocks.Add(new FreeBlock(i * addressStep, blockSize));
        }

        return initialFreeBlocks;
    }

    private static async Task<BinaryStorageHeader> Part1InitialFillingWithFreeBlocks(
        FreeBlockPersistence cut,
        MemoryStream memoryStream,
        int totalEntries,
        int addressStep,
        int blockSize)
    {
        List<FreeBlock> initialFreeBlocks = GenerateFreeBlocks(totalEntries, addressStep, blockSize);

        await cut.UpdateFreeBlocksOnDiskAsync(memoryStream, initialFreeBlocks, CancellationToken.None);
        List<FreeBlock> initialFreeBlocksWritten = initialFreeBlocks;

        memoryStream.Position = 0;
        var updatedHeader = await BinaryStorageHeader.ReadFromStreamAsync(memoryStream);

        updatedHeader.FreeBlocksTableOffset.Should().BeGreaterThan(0);

        var initialReadBlocks = await cut.ReadAllBlocksAsync(memoryStream, CancellationToken.None);
        initialReadBlocks.Should().BeEquivalentTo(initialFreeBlocksWritten);
        return updatedHeader;
    }

    private static void VerifyAllBlocksItems(List<FreeBlock> finalReadBlocks, List<FreeBlock> updatedFreeBlocks)
    {
        // Verify all blocks are correctly updated
        for (int i = 0; i < finalReadBlocks.Count; i++)
        {
            finalReadBlocks[i].Offset.Should().Be(
                updatedFreeBlocks[i].Offset,
                $"Offset at index {i} should be updated");
            finalReadBlocks[i].Size.Should().Be(
                updatedFreeBlocks[i].Size,
                $"Size at index {i} should be updated");
        }
    }
}
