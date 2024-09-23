using Bisto.Journal;

using FluentAssertions;

namespace Bisto.Tests.Journal
{
    public class JournalManagerTests : IDisposable
    {
        private readonly JournalManager _journalManager;

        private readonly MemoryStream _memoryStream;

        public JournalManagerTests()
        {
            _memoryStream = new MemoryStream();
            _journalManager = new JournalManager(_memoryStream);
        }

        [Fact]
        public async Task CommitAsync_ShouldHandleALargeNumberOfConcurrentLogOperations()
        {
            // Arrange
            var journalManager = new JournalManager(_memoryStream);
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var maxConcurrentOperations = 1000; // Define a maximum number of concurrent operations for testing

            // Act
            var logOperations = new List<Task>();
            for (int i = 0; i < maxConcurrentOperations; i++)
            {
                logOperations.Add(
                    journalManager.LogOperationAsync(
                        EJournalOperation.AllocateFromFree,
                        2000 + i,
                        200,
                        0,
                        cancellationToken));
            }

            await Task.WhenAll(logOperations);

            // Assert
            var uncommittedEntries = await journalManager.ReadUncommittedEntriesAsync();
            uncommittedEntries.Should().HaveCount(maxConcurrentOperations);

            // Cancel the cancellation token source to stop the concurrent operations
            cancellationTokenSource.Cancel();
        }

        [Fact]
        public async Task CommitAsync_ShouldMarkEntriesAsCommitted()
        {
            // Arrange
            await _journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);
            await _journalManager.LogOperationAsync(EJournalOperation.AllocateFromFree, 2000, 200, 0);

            // Act
            await _journalManager.CommitAsync();
            var uncommittedEntries = await _journalManager.ReadUncommittedEntriesAsync();

            // Assert
            uncommittedEntries.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_ShouldInitializeNewJournal_WhenStreamIsEmpty()
        {
            // Assert
            _journalManager.Size.Should().Be(64); // HeaderSize
            _journalManager.CreationTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _journalManager.LastModifiedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _journalManager.EntryCount.Should().Be(0);
            _journalManager.Version.Should().Be(1);
        }

        [Fact]
        public void Constructor_ShouldReadExistingJournal_WhenStreamIsNotEmpty()
        {
            // Arrange
            var existingStream = new MemoryStream();
            var existingManager = new JournalManager(existingStream);
            existingManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0).Wait();

            // Act
            existingStream.Position = 0;
            var newManager = new JournalManager(existingStream);

            // Assert
            newManager.EntryCount.Should().Be(1);
            newManager.CreationTime.Should().BeCloseTo(existingManager.CreationTime, TimeSpan.FromSeconds(1));
            newManager.LastModifiedTime.Should().BeCloseTo(existingManager.LastModifiedTime, TimeSpan.FromSeconds(1));
            newManager.Version.Should().Be(existingManager.Version);
        }

        public void Dispose()
        {
            _memoryStream.Dispose();
        }

        [Fact]
        public async Task LogOperationAsync_ShouldHandleConcurrentCallsCorrectly()
        {
            // Arrange
            var journalManager = new JournalManager(_memoryStream);
            var task1 = journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);
            var task2 = journalManager.LogOperationAsync(EJournalOperation.AllocateFromFree, 2000, 200, 0);

            // Act
            await Task.WhenAll(task1, task2);

            // Assert
            _memoryStream.Position = JournalManager.HeaderSize;
            byte[] lengthBytes = new byte[sizeof(int)];
            _memoryStream.Read(lengthBytes, 0, sizeof(int));
            int entry1Length = BitConverter.ToInt32(lengthBytes);

            _memoryStream.Position += entry1Length;
            _memoryStream.Read(lengthBytes, 0, sizeof(int));
            int entry2Length = BitConverter.ToInt32(lengthBytes);

            entry1Length.Should().BeGreaterThan(0);
            entry2Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task LogOperationAsync_ShouldIncreaseEntryCount()
        {
            // Arrange
            var initialEntryCount = _journalManager.EntryCount;

            // Act
            await _journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);

            // Assert
            _journalManager.EntryCount.Should().Be(initialEntryCount + 1);
        }

        [Fact]
        public async Task LogOperationAsync_ShouldNotBlock_WhenJournalIsFull()
        {
            // Arrange
            var journalStream = new MemoryStream();
            var journalManager = new JournalManager(journalStream);
            int maxJournalSize = 1000; // Define a maximum journal size for testing

            // Fill the journal with entries until it's full
            while (journalManager.Size < maxJournalSize)
            {
                await journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);
            }

            // Act
            var logOperationTask = journalManager.LogOperationAsync(EJournalOperation.AllocateFromFree, 2000, 200, 0);

            // Assert
            logOperationTask.Status.Should().Be(TaskStatus.RanToCompletion);
        }

        [Fact]
        public async Task LogOperationAsync_ShouldUpdateLastModifiedTime()
        {
            // Arrange
            var initialLastModifiedTime = _journalManager.LastModifiedTime;

            // Act
            await Task.Delay(1000); // Ensure some time passes
            await _journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);

            // Assert
            _journalManager.LastModifiedTime.Should().BeAfter(initialLastModifiedTime);
        }

        [Fact]
        public async Task ReadUncommittedEntriesAsync_ShouldHandleConcurrentCallsCorrectly()
        {
            // Arrange
            var journalManager = new JournalManager(_memoryStream);
            var task1 = journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);
            var task2 = journalManager.LogOperationAsync(EJournalOperation.AllocateFromFree, 2000, 200, 0);

            // Act
            await Task.WhenAll(task1, task2);

            // Assert
            _memoryStream.Position = JournalManager.HeaderSize;
            byte[] lengthBytes = new byte[sizeof(int)];
            _memoryStream.Read(lengthBytes, 0, sizeof(int));
            int entry1Length = BitConverter.ToInt32(lengthBytes);

            _memoryStream.Position += entry1Length;
            _memoryStream.Read(lengthBytes, 0, sizeof(int));
            int entry2Length = BitConverter.ToInt32(lengthBytes);

            // Modify the assertion to check the order of the entries
            var uncommittedEntries = await journalManager.ReadUncommittedEntriesAsync();

            var entry1Operation = uncommittedEntries.First(e => e.Offset == 1000).Operation;
            var entry2Operation = uncommittedEntries.First(e => e.Offset == 2000).Operation;

            entry1Operation.Should().Be(EJournalOperation.AddToFreeList);
            entry2Operation.Should().Be(EJournalOperation.AllocateFromFree);
        }

        [Fact]
        public async Task ReadUncommittedEntriesAsync_ShouldReturnUncommittedEntries()
        {
            // Arrange
            await _journalManager.LogOperationAsync(EJournalOperation.AddToFreeList, 1000, 100, 0);
            await _journalManager.LogOperationAsync(EJournalOperation.AllocateFromFree, 2000, 200, 0);

            // Act
            var uncommittedEntries = await _journalManager.ReadUncommittedEntriesAsync();

            // Assert
            uncommittedEntries.Should().HaveCount(2);
            uncommittedEntries[0].Operation.Should().Be(EJournalOperation.AddToFreeList);
            uncommittedEntries[0].Offset.Should().Be(1000);
            uncommittedEntries[0].BlockSize.Should().Be(100);
            uncommittedEntries[1].Operation.Should().Be(EJournalOperation.AllocateFromFree);
            uncommittedEntries[1].Offset.Should().Be(2000);
            uncommittedEntries[1].BlockSize.Should().Be(200);
        }
    }
}
