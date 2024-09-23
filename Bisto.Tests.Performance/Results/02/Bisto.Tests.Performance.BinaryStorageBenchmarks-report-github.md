```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4037/23H2/2023Update/SunValley3)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]   : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  IterationTime=10s  

```
| Method                        | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|------------------------------ |---------:|---------:|---------:|-------:|-------:|----------:|
| DeleteMultipleBlocksBenchmark | 579.8 μs | 11.53 μs | 16.16 μs | 8.2999 | 0.1671 |  49.99 KB |
| ReadBenchmark                 | 144.1 μs |  2.87 μs |  6.36 μs | 0.7911 | 0.0141 |    4.6 KB |
| WriteBenchmark                | 113.5 μs |  1.74 μs |  1.93 μs | 0.3395 | 0.0113 |   2.08 KB |
| WriteThenDeleteLargeBenchmark | 317.8 μs |  5.88 μs |  7.64 μs | 5.3400 | 0.0948 |  32.58 KB |
| WriteThenDeleteSmallBenchmark | 311.8 μs |  4.42 μs |  4.14 μs | 5.3458 | 0.1222 |  32.64 KB |
