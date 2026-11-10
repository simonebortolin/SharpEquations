```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor 2.10GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.114
  [Host]   : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error     | StdDev   | Gen0      | Allocated |
|-------------------------------- |---------:|----------:|---------:|----------:|----------:|
| Omml2MathML                     | 20.82 ms |  19.34 ms | 1.060 ms |  250.0000 |  35.68 MB |
| Omml2MathMLXslCompiledTransform | 70.62 ms | 160.12 ms | 8.777 ms | 1000.0000 | 156.37 MB |
| MathML2Omml                     | 31.03 ms |  50.63 ms | 2.775 ms |         - |  30.23 MB |
| MathML2OmmlXslCompiledTransform | 74.67 ms |  25.59 ms | 1.403 ms | 1000.0000 | 136.31 MB |
| Mtef2MathML                     | 17.46 ms |  20.56 ms | 1.127 ms |   93.7500 |  15.71 MB |
| Mtef2MathMLXslCompiledTransform | 41.98 ms |  24.75 ms | 1.356 ms |   83.3333 |  20.89 MB |
