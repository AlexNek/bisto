using BenchmarkDotNet.Running;

using Bisto.Tests;
using Bisto.Tests.Performance;

class Program
{
    static async Task Main(string[] args)
    {
#if DEBUG
        var benchmark = new BinaryStorageBenchmarks();
        await benchmark.A_Setup(); // Make sure to set up the environment
        Console.WriteLine("Start");
        // Run the method directly in debug mode
        //await benchmark.WriteThenDeleteLargeBenchmark();  
        for (int i = 0; i < 10; i++)
        {
            Console. WriteLine($"Step:{i}");
            await benchmark.DeleteMultipleBlocksBenchmark();
        }

#else
        var summary = BenchmarkRunner.Run<BinaryStorageBenchmarks>();
#endif
    }
}
