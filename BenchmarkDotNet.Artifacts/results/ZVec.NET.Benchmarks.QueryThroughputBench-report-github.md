```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method               | Mean       | Error      | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|-----------:|----------:|-----:|-------:|-------:|----------:|
| Query_Sync           | 3,334.8 μs | 3,434.5 μs | 188.26 μs |    3 | 7.8125 |      - |  44.35 KB |
| Query_WithFilter     | 1,769.0 μs |   350.5 μs |  19.21 μs |    2 |      - |      - |   5.57 KB |
| Query_WarmTinyCorpus |   494.8 μs |   968.8 μs |  53.10 μs |    1 | 8.7891 | 0.9766 |  44.27 KB |
