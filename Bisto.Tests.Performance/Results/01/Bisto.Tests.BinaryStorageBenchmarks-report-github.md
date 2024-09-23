```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4037/23H2/2023Update/SunValley3)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.303
  [Host]     : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2


```
| Method          | Mean     | Error    | StdDev   |
|---------------- |---------:|---------:|---------:|
| WriteBenchmark  | 85.86 μs | 2.819 μs | 8.042 μs |
| ReadBenchmark   | 93.41 μs | 2.707 μs | 7.811 μs |
| DeleteBenchmark | 45.33 μs | 0.906 μs | 1.538 μs |
