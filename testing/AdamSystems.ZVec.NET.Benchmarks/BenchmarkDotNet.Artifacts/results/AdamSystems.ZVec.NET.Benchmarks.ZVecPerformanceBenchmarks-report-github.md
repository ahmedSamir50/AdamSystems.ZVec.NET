```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]     : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2


```
| Method         | Mean     | Error    | StdDev   | Gen0   | Allocated |
|--------------- |---------:|---------:|---------:|-------:|----------:|
| InsertDocument |       NA |       NA |       NA |     NA |        NA |
| QueryVector    | 538.0 ns | 11.71 ns | 33.21 ns | 0.0439 |     208 B |

Benchmarks with issues:
  ZVecPerformanceBenchmarks.InsertDocument: DefaultJob
