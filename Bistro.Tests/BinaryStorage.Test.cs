using FluentAssertions;

namespace Bisto.Tests
{
    public class BinaryStorageTests : IDisposable
    {
        private const string TestFileName = "testfile.bin";
        //private readonly Mock<IFileStreamProvider> _fileStreamProviderMock;
        //private readonly MemoryStream _memoryStream;
        //private readonly BinaryStorage _binaryStorage;

        public void Dispose()
        {
            //_binaryStorage?.Dispose();
            //_memoryStream?.Dispose();
        }

        [Fact]
        public async Task ShouldHandleConcurrentCallsToWriteBlockAsyncWithoutDeadlock()
        {
            // Arrange
            var data1 = new byte[] { 1, 2, 3, 4 };
            var data2 = new byte[] { 5, 6, 7, 8 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Act
                var task1 = binaryStorage.WriteAsync(data1);
                var task2 = binaryStorage.WriteAsync(data2);

                await Task.WhenAll(task1, task2);
                
                var offset1 = await task1; // Get the offset returned by task1
                var offset2 = await task2; // Get the offset returned by task2

                var result1 = await binaryStorage.ReadAsync(offset1);
                var result2 = await binaryStorage.ReadAsync(offset2);
                
                // Assert
                // memoryStream.Position = 0;
                // var writtenData = new byte[data2.Length];
                // int readLength = await memoryStream.ReadAsync(writtenData, 0, data2.Length);
                // readLength.Should().Be(data2.Length);
                // writtenData.Should().Equal(data2);
                
                offset1.Should().NotBe(offset2, "each write should have a unique offset");
                result1.Should().Equal(data1, "data1 should be written correctly");
                result2.Should().Equal(data2, "data2 should be written correctly after concurrent writes");
                
            }
        }

        private static async Task<BinaryStorage> CreateClassUnterTest(MemoryFileStreamProvider fileStreamProvider)
        {
            var binaryStorage = await BinaryStorage.CreateAsync(TestFileName, fileStreamProvider);
            return binaryStorage;
        }

        [Fact]
        public async Task ShouldHandleEmptyDataInputCorrectly()
        {
            // Arrange
            var emptyData = new byte[0];
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Act
                var address = await binaryStorage.WriteRootBlockAsync(emptyData);

                // Assert
                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = address;
                var writtenData = new byte[emptyData.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, emptyData.Length);
                readLength.Should().Be(emptyData.Length);
                writtenData.Should().Equal(emptyData);
            }
        }

        [Fact]
        public async Task ShouldHandleLargeDataInputCorrectly()
        {
            // Arrange
            var largeData = new byte[1024 * 1024]; // 1 MB of data
            new Random().NextBytes(largeData); // Fill with random data
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Act
                var address = await binaryStorage.WriteRootBlockAsync(largeData);

                // Assert
                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = address + BlockUtils.BlockHeaderSize;
                var writtenData = new byte[largeData.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, largeData.Length);
                readLength.Should().Be(largeData.Length);
                writtenData.Should().Equal(largeData);
            }
        }

        [Fact]
        public async Task ShouldHandleNullDataInputGracefully()
        {
            // Arrange
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Act
                Func<Task> act = async () => await binaryStorage.WriteRootBlockAsync(null);

                // Assert
                await act.Should().ThrowAsync<ArgumentNullException>();
            }
        }

        [Fact]
        public async Task ShouldHandleSingleByteDataInputCorrectly()
        {
            // Arrange
            var singleByteData = new byte[] { 0x01 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Act
                var address = await binaryStorage.WriteAsync(singleByteData);

                // Assert
                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = address;
                var writtenData = new byte[singleByteData.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, singleByteData.Length);
                readLength.Should().Be(singleByteData.Length);
                writtenData.Should().Equal(singleByteData);
            }
        }

        [Fact]
        public async Task ShouldNotUpdateRootUsedBlockIfWrittenTheSameDataLength()
        {
            // Arrange
            var initialData = new byte[] { 1, 2, 3, 4 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Write initial data to set RootUsedBlock
                var initialAddress = await binaryStorage.WriteRootBlockAsync(initialData);

                // Act
                await binaryStorage.WriteRootBlockAsync(initialData);

                // Assert
                binaryStorage.RootUsedBlock.Should().Be(initialAddress);
                binaryStorage.RootUsedBlock.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public async Task ShouldUpdateRootUsedBlockIfWrittenBiggerDataLength()
        {
            // Arrange
            var initialData = new byte[] { 1, 2, 3, 4 };
            var newData = new byte[] { 5, 6, 7, 8, 9, 10, 11, 12 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Write initial data to set RootUsedBlock
                var initialAddress = await binaryStorage.WriteRootBlockAsync(initialData);

                // Act
                await binaryStorage.WriteRootBlockAsync(newData);

                // Assert
                binaryStorage.RootUsedBlock.Should().NotBe(initialAddress);
                binaryStorage.RootUsedBlock.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public async Task ShouldWriteAsyncWithBlockOffset()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);
                var memoryStream = fileStreamProvider.GetStream(TestFileName);

                // Act
                var addressWithoutOffset = await binaryStorage.WriteAsync(data);
                var addressWithOffset = await binaryStorage.WriteAsync(data, addressWithoutOffset);

                // Assert
                var readBytes = await binaryStorage.ReadAsync(addressWithoutOffset);
                readBytes.Should().Equal(data);
                
                memoryStream.Position = addressWithOffset + BlockUtils.BlockHeaderSize;
                var writtenDataWithOffset = new byte[data.Length];
                int readLengthWithOffset = await memoryStream.ReadAsync(writtenDataWithOffset, 0, data.Length);
                readLengthWithOffset.Should().Be(data.Length);
                writtenDataWithOffset.Should().Equal(data);
            }
        }

        [Fact]
        public async Task ShouldWriteAsyncWithoutBlockOffset()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);

                // Act
                var addressWithoutOffset = await binaryStorage.WriteAsync(data);

                // Assert
                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = addressWithoutOffset + BlockUtils.BlockHeaderSize;
                var writtenDataWithoutOffset = new byte[data.Length];
                int readLengthWithoutOffset = await memoryStream.ReadAsync(writtenDataWithoutOffset, 0, data.Length);
                readLengthWithoutOffset.Should().Be(data.Length);
                writtenDataWithoutOffset.Should().Equal(data);
            }
        }

        [Fact]
        public async Task ShouldWriteDataIntoDifferentOffsetIfNewSizeIsBigger()
        {
            // Arrange
            var initialData = new byte[] { 1, 2, 3, 4 };
            var largerData = new byte[] { 5, 6, 7, 8, 9, 10, 11, 12 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);
                // Write initial data to set an existing block
                var initialAddress = await binaryStorage.WriteAsync(initialData);

                // Act
                var newAddress = await binaryStorage.WriteAsync(largerData, initialAddress);

                // Assert
                newAddress.Should().NotBe(initialAddress);

                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = newAddress + BlockUtils.BlockHeaderSize;
                var writtenData = new byte[largerData.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, largerData.Length);
                readLength.Should().Be(largerData.Length);
                writtenData.Should().Equal(largerData);
            }
        }

        [Fact]
        public async Task ShouldWriteDataIntoSameOffsetIfNewSizeIsSmaller()
        {
            // Arrange
            var initialData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var smallerData = new byte[] { 9, 10, 11, 12 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);
                // Write initial data to set an existing block
                var initialAddress = await binaryStorage.WriteAsync(initialData);

                // Act
                var newAddress = await binaryStorage.WriteAsync(smallerData, initialAddress);

                // Assert
                newAddress.Should().Be(initialAddress);

                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = newAddress + BlockUtils.BlockHeaderSize;
                var writtenData = new byte[smallerData.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, smallerData.Length);
                readLength.Should().Be(smallerData.Length);
                writtenData.Should().Equal(smallerData);
            }
        }

        [Fact]
        public async Task ShouldWriteDataToStreamWhenRootUsedBlockIsInitiallyZero()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);
                // Act
                var address = await binaryStorage.WriteRootBlockAsync(data);

                // Assert
                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = address + BlockUtils.BlockHeaderSize;
                var writtenData = new byte[data.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, data.Length);
                readLength.Should().Be(data.Length);
                writtenData.Should().Equal(data);
            }
        }

        [Fact]
        public async Task ShouldWriteDataToStreamWhenRootUsedBlockIsNonZero()
        {
            // Arrange
            var initialData = new byte[] { 5, 6, 7, 8 };
            var newData = new byte[] { 9, 10, 11, 12 };
            using (var fileStreamProvider = new MemoryFileStreamProvider())
            {
                BinaryStorage binaryStorage = await CreateClassUnterTest(fileStreamProvider);
                
                // Write initial data to set RootUsedBlock
                var initialAddress = await binaryStorage.WriteRootBlockAsync(initialData);

                // Act
                await binaryStorage.WriteRootBlockAsync(newData);

                // Assert
                var memoryStream = fileStreamProvider.GetStream(TestFileName);
                memoryStream.Position = initialAddress + BlockUtils.BlockHeaderSize;
                var writtenData = new byte[newData.Length];
                int readLength = await memoryStream.ReadAsync(writtenData, 0, newData.Length);
                readLength.Should().Be(newData.Length);
                writtenData.Should().Equal(newData);
            }
        }
    }
}
