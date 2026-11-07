namespace SharpEquations.Benchmarks
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;

    /// <summary>One equation read from UnicodeMath, MathML, OMML and LaTeX.</summary>
    [MemoryDiagnoser]
    public class ParseBenchmarks
    {
        private Dictionary<EquationFormat, string> sources = [];

        [ParamsSource(nameof(Names))]
        public string Sample { get; set; } = "";

        public static IEnumerable<string> Names => Samples.Names;

        [GlobalSetup]
        public void Setup() => sources = Samples.InEveryFormat(Sample);

        [Benchmark(Baseline = true)]
        public Equation UnicodeMath() => Equation.Parse(sources[EquationFormat.UnicodeMath], EquationFormat.UnicodeMath);

        [Benchmark]
        public Equation MathML() => Equation.Parse(sources[EquationFormat.MathML], EquationFormat.MathML);

        [Benchmark]
        public Equation Omml() => Equation.Parse(sources[EquationFormat.Omml], EquationFormat.Omml);

        [Benchmark]
        public Equation LaTeX() => Equation.Parse(sources[EquationFormat.LaTeX], EquationFormat.LaTeX);
    }
}
