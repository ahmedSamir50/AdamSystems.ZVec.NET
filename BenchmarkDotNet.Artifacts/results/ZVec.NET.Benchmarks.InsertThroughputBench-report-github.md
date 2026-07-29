```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method        | InvocationCount | UnrollFactor | Mean         | Error        | StdDev       | Gen0   | Allocated |
|-------------- |---------------- |------------- |-------------:|-------------:|-------------:|-------:|----------:|
| Insert_Single | Default         | 16           |     65.85 μs |     3.579 μs |     5.246 μs | 0.2441 |   1.42 KB |
| Insert_Batch  | 1               | 1            | 61,035.57 μs | 4,725.463 μs | 6,926.522 μs |      - | 446.15 KB |
