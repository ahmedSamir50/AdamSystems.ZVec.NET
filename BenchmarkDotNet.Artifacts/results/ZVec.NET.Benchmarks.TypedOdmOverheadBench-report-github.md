```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]     : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  Job-RCMXCL : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun  : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2


```
| Method                     | Job        | IterationCount | LaunchCount | WarmupCount | Mean         | Error         | StdDev       | Ratio  | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------- |--------------- |------------ |------------ |-------------:|--------------:|-------------:|-------:|--------:|-----:|-------:|----------:|------------:|
| Insert_Dynamic             | Job-RCMXCL | 5              | Default     | 1           |  59,204.6 ns |  16,475.67 ns |  2,549.63 ns |  1.001 |    0.05 |    3 | 0.2441 |    1440 B |        1.00 |
| Insert_Typed               | Job-RCMXCL | 5              | Default     | 1           |  70,472.6 ns |   9,279.66 ns |  1,436.04 ns |  1.192 |    0.05 |    3 | 0.3662 |    2208 B |        1.53 |
| Query_Dynamic              | Job-RCMXCL | 5              | Default     | 1           | 343,005.4 ns | 110,344.88 ns | 17,075.99 ns |  5.801 |    0.34 |    4 | 0.9766 |    6568 B |        4.56 |
| Query_Typed                | Job-RCMXCL | 5              | Default     | 1           | 375,785.8 ns | 127,134.23 ns | 33,016.36 ns |  6.356 |    0.57 |    4 | 1.4648 |    8856 B |        6.15 |
| QueryFilter_Dynamic        | Job-RCMXCL | 5              | Default     | 1           | 562,312.5 ns |  49,772.11 ns | 12,925.66 ns |  9.511 |    0.41 |    5 | 0.9766 |    6569 B |        4.56 |
| QueryFilter_Typed          | Job-RCMXCL | 5              | Default     | 1           | 641,914.0 ns | 197,673.07 ns | 51,335.07 ns | 10.857 |    0.90 |    5 | 1.9531 |    9657 B |        6.71 |
| Mapper_ToDoc               | Job-RCMXCL | 5              | Default     | 1           |     488.9 ns |     164.21 ns |     42.65 ns |  0.008 |    0.00 |    2 | 0.2174 |    1024 B |        0.71 |
| Mapper_FromDoc             | Job-RCMXCL | 5              | Default     | 1           |     213.0 ns |      29.85 ns |      4.62 ns |  0.004 |    0.00 |    1 | 0.0339 |     160 B |        0.11 |
| ExpressionFilter_Translate | Job-RCMXCL | 5              | Default     | 1           |     453.7 ns |     118.89 ns |     30.87 ns |  0.008 |    0.00 |    2 | 0.1698 |     800 B |        0.56 |
|                            |            |                |             |             |              |               |              |        |         |      |        |           |             |
| Insert_Dynamic             | MediumRun  | 15             | 2           | 10          |  62,600.7 ns |   2,790.36 ns |  3,911.69 ns |  1.004 |    0.09 |    3 | 0.2441 |    1440 B |        1.00 |
| Insert_Typed               | MediumRun  | 15             | 2           | 10          |  66,353.4 ns |   4,426.18 ns |  6,487.83 ns |  1.064 |    0.12 |    3 | 0.4272 |    2208 B |        1.53 |
| Query_Dynamic              | MediumRun  | 15             | 2           | 10          | 354,038.3 ns |  16,451.84 ns | 24,624.34 ns |  5.676 |    0.52 |    4 | 0.9766 |    6568 B |        4.56 |
| Query_Typed                | MediumRun  | 15             | 2           | 10          | 372,285.1 ns |  14,483.13 ns | 21,229.19 ns |  5.969 |    0.49 |    4 | 1.4648 |    8856 B |        6.15 |
| QueryFilter_Dynamic        | MediumRun  | 15             | 2           | 10          | 583,090.2 ns |  33,961.40 ns | 48,706.43 ns |  9.349 |    0.95 |    5 | 0.9766 |    6569 B |        4.56 |
| QueryFilter_Typed          | MediumRun  | 15             | 2           | 10          | 575,367.5 ns |  16,596.34 ns | 21,579.93 ns |  9.225 |    0.65 |    5 | 1.9531 |    9657 B |        6.71 |
| Mapper_ToDoc               | MediumRun  | 15             | 2           | 10          |     499.6 ns |      23.79 ns |     33.35 ns |  0.008 |    0.00 |    2 | 0.2174 |    1024 B |        0.71 |
| Mapper_FromDoc             | MediumRun  | 15             | 2           | 10          |     222.6 ns |      13.33 ns |     19.95 ns |  0.004 |    0.00 |    1 | 0.0339 |     160 B |        0.11 |
| ExpressionFilter_Translate | MediumRun  | 15             | 2           | 10          |     445.2 ns |      23.68 ns |     35.44 ns |  0.007 |    0.00 |    2 | 0.1698 |     800 B |        0.56 |
