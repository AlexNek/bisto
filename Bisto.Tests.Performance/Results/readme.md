# Results
Results Table
- **Method**: The name of the benchmark method being tested.
- **Mean**: The average execution time of the method over multiple runs (in milliseconds for this benchmark). Lower is better.
- **Error**: The margin of error in the mean measurement. Smaller is better, indicating more reliable results.
- **StdDev**: Standard deviation, a measure of how spread out the execution times were. Lower values indicate more consistent performance.
- **Gen0, Gen1, Gen2**: Information about garbage collection pressure. These columns show the number of garbage collections that occurred in each generation during the benchmark. Higher numbers can indicate more memory allocation and potential performance overhead.
- **Allocated**: The total amount of memory allocated by the method during the benchmark (in megabytes). Lower is generally better.

## Understanding Margin of Error

Benchmarking involves running a piece of code multiple times and measuring how long it takes. Due to various factors (like background processes, CPU fluctuations, garbage collection), you'll rarely get the exact same execution time on every run.
The "Error" value, often expressed as a plus/minus range (±), tells you how much the measured mean execution time could vary due to these random factors. A smaller margin of error means the results are more precise and reliable.

**Example**
Let's say you have these benchmark results:
- Method A: Mean = 100ms, Error = ±5ms
- Method B: Mean = 105ms, Error = ±20ms

Here's what you can infer:
- Method A: The actual average execution time of Method A is likely somewhere between 95ms and 105ms (100ms ± 5ms).
- Method B: The actual average execution time of Method B is less certain. It could be anywhere between 85ms and 125ms (105ms ± 20ms).

### Why Smaller Error Is Better
- **Reliability**: A smaller error margin gives you more confidence that the measured mean execution time is close to the true average performance.
- **Meaningful Comparisons**: When comparing different methods, a large error margin can make it difficult to determine if one method is genuinely faster or if the difference is just due to random variation.
- **Identifying Performance Issues**: If you make a code change and see a performance improvement that's within the margin of error, it's less likely to be a real improvement.

### How to Reduce Margin of Error
BenchmarkDotNet automatically tries to achieve a reasonable margin of error. However, you can influence it:
- **Increase Iteration Count**: Running the benchmark for more iterations (controlled by IterationCount in BenchmarkDotNet config) can reduce the impact of random variations.
- **Warm-up Phase**: BenchmarkDotNet usually includes a warm-up phase to reduce the influence of initial JIT compilation and caching effects.
- **Control External Factors**: Try to run benchmarks on a system with minimal background processes to reduce interference.
- **Analyze Outliers**: BenchmarkDotNet can help identify and potentially exclude outlier measurements that significantly skew the results.


## 01
30.08.2024 initial implementation
## 02
13.09.2024 old varaint 10 items for delete
## 03
13.09.2024 old variant 100 item fo delete

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4037/23H2/2023Update/SunValley3)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]   : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  IterationTime=20s  
```

| Method                        | Mean     | Error   | StdDev  | Gen0   | Gen1   | Allocated |
|------------------------------ |---------:|--------:|--------:|-------:|-------:|----------:|
| DeleteMultipleBlocksBenchmark | 502.7 μs | 9.48 μs | 8.87 μs | 8.2954 | 0.1733 |  50.04 KB |
| ReadBenchmark                 | 132.9 μs | 2.65 μs | 6.75 μs | 0.7927 | 0.0127 |   4.61 KB |
| WriteBenchmark                | 116.7 μs | 2.33 μs | 4.92 μs | 0.3383 | 0.0058 |   2.09 KB |
| WriteThenDeleteLargeBenchmark | 322.7 μs | 6.21 μs | 8.50 μs | 5.3410 | 0.1071 |   32.5 KB |
| WriteThenDeleteSmallBenchmark | 320.5 μs | 5.83 μs | 9.25 μs | 5.3040 | 0.1132 |  32.31 KB |


## 04
19.09.2024 new variant with deletion table and jounaling

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]   : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  IterationTime=20s  
```

| Method                        | Mean        | Error       | StdDev      | Gen0       | Gen1      | Gen2     | Allocated |
|------------------------------ |------------:|------------:|------------:|-----------:|----------:|---------:|----------:|
| DeleteMultipleBlocksBenchmark | 12,419.2 ms | 1,683.89 ms | 4,965.00 ms | 37562.5000 | 2125.0000 | 187.5000 | 223.95 MB |
| ReadBenchmark                 |    606.9 ms |     7.44 ms |     6.96 ms |   979.1667 |   41.6667 |        - |   5.87 MB |
| WriteBenchmark                |    593.0 ms |     8.33 ms |     7.79 ms |   979.1667 |   41.6667 |        - |   5.88 MB |
| WriteThenDeleteLargeBenchmark | 16,848.7 ms |   119.05 ms |   111.36 ms | 29000.0000 | 1000.0000 |        - | 177.34 MB |
| WriteThenDeleteSmallBenchmark | 17,223.8 ms |    76.11 ms |    67.47 ms | 30000.0000 | 1000.0000 |        - | 179.52 MB |

Analysis
The results show the performance of different methods in your code:
DeleteMultipleBlocksBenchmark: This method seems to be the slowest, taking an average of ~12.4 seconds and allocating a significant amount of memory (223.95 MB). This suggests there might be room for optimization in how you're deleting multiple blocks.
WriteThenDeleteLargeBenchmark and WriteThenDeleteSmallBenchmark: These methods also take a considerable amount of time (~16-17 seconds) and allocate a lot of memory. This indicates that the combined operations of writing and then deleting data, especially for larger data sizes, could be impacting performance.
ReadBenchmark and WriteBenchmark: These methods appear to be relatively fast (~593-606 ms) and allocate much less memory compared to the other methods.
Recommendations
Investigate DeleteMultipleBlocksBenchmark: Profile this method to identify potential bottlenecks. Consider if there are more efficient ways to delete multiple blocks, such as batching operations or optimizing memory management.
Analyze Write/Delete Patterns: Examine the WriteThenDelete* benchmarks to understand the data sizes and access patterns involved. If possible, try to minimize the frequency of writing and then immediately deleting large amounts of data.
Memory Optimization: Look for opportunities to reduce memory allocations in the slower methods. This could involve reusing objects, optimizing data structures, or using memory pooling techniques.
## 05
23.09.2024 new deletion variant without jounaling

| Method                        | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|------------------------------ |---------:|---------:|---------:|--------:|-------:|----------:|
| DeleteMultipleBlocksBenchmark | 765.7 μs | 14.50 μs | 15.51 μs | 19.8199 | 3.3097 | 120.07 KB |
| ReadBenchmark                 | 144.6 μs |  2.21 μs |  1.96 μs |  0.7857 | 0.1946 |   4.69 KB |
| WriteBenchmark                | 118.9 μs |  1.45 μs |  1.36 μs |  0.4414 | 0.1028 |   2.71 KB |
| WriteThenDeleteLargeBenchmark | 434.8 μs |  5.74 μs |  4.80 μs | 11.0307 | 2.1975 |  67.13 KB |
| WriteThenDeleteSmallBenchmark | 435.8 μs |  4.48 μs |  4.19 μs | 11.0357 | 2.1986 |  67.17 KB |
