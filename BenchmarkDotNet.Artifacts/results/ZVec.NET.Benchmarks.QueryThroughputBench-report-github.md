```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                 | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Query_Sync             | 3,627.2 μs | 238.64 μs | 349.80 μs |  1.01 |    0.13 |    3 |      - |   6.89 KB |        1.00 |
| Query_Sync_WithVectors | 3,663.3 μs | 186.90 μs | 273.96 μs |  1.02 |    0.12 |    3 | 7.8125 |  40.56 KB |        5.89 |
| Query_WithFilter       | 1,871.8 μs |  70.04 μs | 100.45 μs |  0.52 |    0.05 |    2 |      - |   1.79 KB |        0.26 |
| Query_WarmTinyCorpus   |   368.5 μs |   4.58 μs |   6.11 μs |  0.10 |    0.01 |    1 | 1.4648 |    6.8 KB |        0.99 |
