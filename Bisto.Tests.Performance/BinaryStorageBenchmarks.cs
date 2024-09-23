using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Bisto.Tests.Performance
{
    [SimpleJob(RuntimeMoniker.Net80)] 
    [IterationTime(20*1000)]// 20 seconds
    [MemoryDiagnoser]  // Tracks memory allocations
    public class BinaryStorageBenchmarks
    {
        private const int BlockCount = 10; // Total number of blocks to write and delete

        private const int SmallDeleteCount = 10; // For testing with a small number of deleted blocks

        private List<long> _addresses;

        private IBinaryStorage _storage;


        [GlobalSetup]
        public async Task A_Setup()
        {
            if(File.Exists(BinaryStoragePerformanceTests.TestFilename))
            {
                File.Delete(BinaryStoragePerformanceTests.TestFilename);
            }

            // important for performance now.
            StorageOptions? options = new StorageOptions() { UseJournaling = false };

            _storage = await BinaryStorage.CreateAsync(
                           BinaryStoragePerformanceTests.TestFilename,
                           new FileStreamProvider(),
                           true,
                           null,
                           options);

            await IterationSetup();

        }

        [GlobalCleanup]
        public void A_Cleanup()
        {
            _storage.DisposeAsync();
            File.Delete(BinaryStoragePerformanceTests.TestFilename);
        }

        public async Task IterationSetup()
        {
            _addresses = new List<long>();

            // Write a fresh set of blocks before each iteration
            for (int i = 0; i < BlockCount; i++) // Example: writing 1000 blocks per iteration
            {
                var data = new byte[BinaryStoragePerformanceTests.TestDataSize];
                new Random().NextBytes(data);
                long address = await _storage.WriteAsync(data);
                _addresses.Add(address); // Store the block addresses
            }
        }
        /// <summary>
        /// Benchmark to test deleting a large number of blocks after writing them.
        /// </summary>
        [Benchmark] 
        public async Task DeleteMultipleBlocksBenchmark()
        {
            // Delete all blocks that were written during the setup phase
            //Console.WriteLine("Starting DeleteMultipleBlocksBenchmark");
            foreach (var address in _addresses)
            {
                //Console.WriteLine($"DeleteMultipleBlocksBenchmark iteration at: {address}");
                await _storage.DeleteAsync(address).ConfigureAwait(false); 
            }
            await IterationSetup();
            //Console.WriteLine("Completed DeleteMultipleBlocksBenchmark");
        }

        /// <summary>
        /// Benchmark to test reading from the storage.
        /// </summary>
        [Benchmark]
        public async Task ReadBenchmark()
        {
            var data = new byte[BinaryStoragePerformanceTests.TestDataSize];
            new Random().NextBytes(data);
            var address = await _storage.WriteAsync(data);

            await _storage.ReadAsync(address);
        }

        

        /// <summary>
        /// Benchmark to test the write operation without deletion.
        /// </summary>
        [Benchmark]
        public async Task WriteBenchmark()
        {
            var data = new byte[BinaryStoragePerformanceTests.TestDataSize];
            new Random().NextBytes(data);
            await _storage.WriteAsync(data);
        }

        /// <summary>
        /// Benchmark to test writing and then deleting a large number of blocks.
        /// </summary>
        [Benchmark]
        public async Task WriteThenDeleteLargeBenchmark()
        {
            var data = new byte[BinaryStoragePerformanceTests.TestDataSize];
            new Random().NextBytes(data);

            // Write and delete a large number of blocks
            for (int i = 0; i < BlockCount; i++)
            {
                var address = await _storage.WriteAsync(data);
                await _storage.DeleteAsync(address);
            }
        }

        /// <summary>
        /// Benchmark to test writing and then deleting a small number of blocks.
        /// </summary>
        [Benchmark]
        public async Task WriteThenDeleteSmallBenchmark()
        {
            var data = new byte[BinaryStoragePerformanceTests.TestDataSize];
            new Random().NextBytes(data);

            // Write and delete a small number of blocks
            for (int i = 0; i < SmallDeleteCount; i++)
            {
                var address = await _storage.WriteAsync(data);
                await _storage.DeleteAsync(address);
            }
        }
    }
}
