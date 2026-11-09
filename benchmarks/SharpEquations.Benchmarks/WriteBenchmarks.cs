namespace SharpEquations.Benchmarks
{
    using System.Collections.Generic;
    using System.Xml.Linq;
    using BenchmarkDotNet.Attributes;

    /// <summary>One equation written in UnicodeMath, MathML, OMML and LaTeX.</summary>
    [MemoryDiagnoser]
    public class WriteBenchmarks
    {
        private static readonly EquationOptions WordLaTeX = new() { LaTeXFlavor = LaTeXFlavor.Word };

        private Equation equation = null!;

        [ParamsSource(nameof(Names))]
        public string Sample { get; set; } = "";

        public static IEnumerable<string> Names => Samples.Names;

        [GlobalSetup]
        public void Setup() => equation = Equation.Parse(Samples.InEveryFormat(Sample)[EquationFormat.UnicodeMath], EquationFormat.UnicodeMath);

        [Benchmark(Baseline = true)]
        public string UnicodeMath() => equation.ToString(EquationFormat.UnicodeMath);

        // the MathML text of an equation is cached (ToString(EquationFormat.MathML) returns it): the element is what is written
        [Benchmark]
        public XElement MathML() => equation.ToMathML();

        [Benchmark]
        public string Omml() => equation.ToString(EquationFormat.Omml);

        [Benchmark]
        public string LaTeX() => equation.ToString(EquationFormat.LaTeX);

        [Benchmark]
        public string LaTeXWord() => equation.ToString(EquationFormat.LaTeX, WordLaTeX);
    }
}
