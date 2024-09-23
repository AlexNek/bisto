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
