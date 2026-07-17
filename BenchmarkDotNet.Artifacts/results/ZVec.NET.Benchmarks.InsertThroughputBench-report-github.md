```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method        | InvocationCount | UnrollFactor | Mean         | Error         | StdDev        | Median       | Gen0   | Allocated |
|-------------- |---------------- |------------- |-------------:|--------------:|--------------:|-------------:|-------:|----------:|
| Insert_Single | Default         | 16           |     53.12 μs |      3.941 μs |      5.899 μs |     51.79 μs | 0.2441 |   1.36 KB |
| Insert_Batch  | 1               | 1            | 59,434.44 μs | 10,179.090 μs | 14,920.379 μs | 51,605.80 μs |      - | 446.09 KB |
