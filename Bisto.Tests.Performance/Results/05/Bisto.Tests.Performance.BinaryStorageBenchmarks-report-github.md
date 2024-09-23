```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]   : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  IterationTime=20s  

```
| Method                        | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|------------------------------ |---------:|---------:|---------:|--------:|-------:|----------:|
| DeleteMultipleBlocksBenchmark | 765.7 μs | 14.50 μs | 15.51 μs | 19.8199 | 3.3097 | 120.07 KB |
| ReadBenchmark                 | 144.6 μs |  2.21 μs |  1.96 μs |  0.7857 | 0.1946 |   4.69 KB |
| WriteBenchmark                | 118.9 μs |  1.45 μs |  1.36 μs |  0.4414 | 0.1028 |   2.71 KB |
| WriteThenDeleteLargeBenchmark | 434.8 μs |  5.74 μs |  4.80 μs | 11.0307 | 2.1975 |  67.13 KB |
| WriteThenDeleteSmallBenchmark | 435.8 μs |  4.48 μs |  4.19 μs | 11.0357 | 2.1986 |  67.17 KB |
