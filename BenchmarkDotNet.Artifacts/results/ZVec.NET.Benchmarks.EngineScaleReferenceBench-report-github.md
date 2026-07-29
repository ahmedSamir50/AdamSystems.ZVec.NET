```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                                        | Mean     | Error     | StdDev    | Allocated |
|---------------------------------------------- |---------:|----------:|----------:|----------:|
| Local_10k_Flat_Query_vs_Upstream_8500plus_QPS | 3.461 ms | 0.2113 ms | 0.3162 ms |   6.96 KB |
