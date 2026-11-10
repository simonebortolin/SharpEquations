```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor 2.10GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.114
  [Host]   : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method            | Mean      | Error       | StdDev    | Gen0   | Allocated |
|------------------ |----------:|------------:|----------:|-------:|----------:|
| UnicodeMathToOmml | 401.20 μs | 1,057.16 μs | 57.946 μs | 1.3550 | 219.93 KB |
| OmmlToMathML      |  23.52 μs |    11.55 μs |  0.633 μs | 0.2441 |  34.35 KB |
| OmmlToUnicodeMath | 249.92 μs |   626.42 μs | 34.336 μs |      - |  96.29 KB |
| OmmlToLaTeX       |  28.39 μs |    13.31 μs |  0.730 μs | 0.1628 |  26.79 KB |
