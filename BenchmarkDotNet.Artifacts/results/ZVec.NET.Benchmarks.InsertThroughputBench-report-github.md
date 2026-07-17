```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method        | InvocationCount | UnrollFactor | Mean         | Error         | StdDev        | Gen0   | Allocated |
|-------------- |---------------- |------------- |-------------:|--------------:|--------------:|-------:|----------:|
| Insert_Single | Default         | 16           |     58.13 μs |      94.69 μs |      5.190 μs | 0.2441 |   1.39 KB |
| Insert_Batch  | 1               | 1            | 63,777.35 μs | 200,990.91 μs | 11,016.990 μs |      - | 453.59 KB |
