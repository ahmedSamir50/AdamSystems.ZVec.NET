```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                   | Mean       | Error     | StdDev    | Gen0   | Allocated |
|------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Query_768Dim             | 2,821.3 μs | 182.56 μs | 261.82 μs |      - |    6987 B |
| Query_768Dim_WithVectors | 3,160.5 μs | 617.17 μs | 885.13 μs | 7.8125 |   41466 B |
| Fetch_ScalarOnly         |   193.2 μs |   9.57 μs |  13.72 μs |      - |     904 B |
