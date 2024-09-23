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
