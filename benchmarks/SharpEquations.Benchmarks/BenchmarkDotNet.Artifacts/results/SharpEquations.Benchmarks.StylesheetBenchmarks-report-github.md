```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor 2.10GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.114
  [Host]   : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean      | Error      | StdDev    | Gen0      | Allocated |
|-------------------------------- |----------:|-----------:|----------:|----------:|----------:|
| Omml2MathML                     |  9.995 ms |   8.776 ms | 0.4810 ms |   62.5000 |   9.59 MB |
| Omml2MathMLXslCompiledTransform | 83.120 ms | 102.319 ms | 5.6084 ms | 1000.0000 | 156.37 MB |
| MathML2Omml                     | 15.615 ms |  23.374 ms | 1.2812 ms |  125.0000 |  16.51 MB |
| MathML2OmmlXslCompiledTransform | 75.872 ms |  79.438 ms | 4.3543 ms | 1000.0000 | 136.31 MB |
| Mtef2MathML                     | 23.969 ms | 115.622 ms | 6.3376 ms |         - |   15.6 MB |
| Mtef2MathMLXslCompiledTransform | 45.099 ms |   5.842 ms | 0.3202 ms |   90.9091 |  20.89 MB |
