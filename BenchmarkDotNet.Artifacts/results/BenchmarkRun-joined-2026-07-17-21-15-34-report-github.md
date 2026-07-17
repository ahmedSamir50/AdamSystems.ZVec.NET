```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Type                   | Method               | InvocationCount | UnrollFactor | Mean             | Error             | StdDev           | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------- |--------------------- |---------------- |------------- |-----------------:|------------------:|-----------------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| FilterParsingBench     | Build_SimpleFilter   | Default         | 16           |         57.28 ns |          62.66 ns |         3.435 ns | 0.000 |    0.00 |    1 | 0.0272 |      - |     128 B |       0.003 |
| InsertThroughputBench  | Insert_Single        | Default         | 16           |     54,886.21 ns |      71,144.16 ns |     3,899.652 ns | 0.020 |    0.00 |    3 | 0.2441 |      - |    1360 B |       0.030 |
| MemoryDiagnosisBench   | Query_768Dim         | Default         | 16           |  2,545,310.03 ns |     429,634.33 ns |    23,549.707 ns | 0.940 |    0.05 |    7 | 7.8125 |      - |   45410 B |       1.000 |
| QueryThroughputBench   | Query_Sync           | Default         | 16           |  2,460,976.56 ns |     937,654.27 ns |    51,395.995 ns | 0.909 |    0.05 |    7 | 7.8125 |      - |   45410 B |       1.000 |
| VectorMarshallingBench | Query_ReadOnlyMemory | Default         | 16           |  2,714,846.48 ns |   3,180,774.60 ns |   174,348.990 ns | 1.003 |    0.08 |    7 | 7.8125 |      - |   45410 B |       1.000 |
| FilterParsingBench     | Build_CompoundFilter | Default         | 16           |        197.28 ns |         345.38 ns |        18.931 ns | 0.000 |    0.00 |    2 | 0.1154 |      - |     544 B |       0.012 |
| MemoryDiagnosisBench   | Fetch_ScalarOnly     | Default         | 16           |    183,523.04 ns |      25,604.47 ns |     1,403.467 ns | 0.068 |    0.00 |    4 | 0.2441 |      - |    1248 B |       0.027 |
| QueryThroughputBench   | Query_WithFilter     | Default         | 16           |  1,449,144.01 ns |     622,034.28 ns |    34,095.798 ns | 0.535 |    0.03 |    6 |      - |      - |    5705 B |       0.126 |
| VectorMarshallingBench | Query_ExplicitCopy   | Default         | 16           |  2,563,488.28 ns |     849,796.84 ns |    46,580.232 ns | 0.947 |    0.05 |    7 | 7.8125 |      - |   48506 B |       1.068 |
| QueryThroughputBench   | Query_WarmTinyCorpus | Default         | 16           |    317,840.38 ns |      71,149.24 ns |     3,899.930 ns | 0.117 |    0.01 |    5 | 9.2773 | 0.9766 |   45328 B |       0.998 |
|                        |                      |                 |              |                  |                   |                  |       |         |      |        |        |           |             |
| InsertThroughputBench  | Insert_Batch         | 1               | 1            | 56,558,700.00 ns | 127,513,631.07 ns | 6,989,452.423 ns |     ? |       ? |    1 |      - |      - |  464480 B |           ? |
