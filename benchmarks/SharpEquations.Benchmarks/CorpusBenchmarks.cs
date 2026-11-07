namespace SharpEquations.Benchmarks
{
    using System.Xml.Linq;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// The corpora of the tests, converted whole: the 738 examples of UnicodeMathML and the 192 equations of Word
    /// documents. The times are per equation.
    /// </summary>
    [MemoryDiagnoser]
    public class CorpusBenchmarks
    {
        private const int UnicodeMathCount = 738;
        private const int OmmlCount = 192;

        private string[] unicodeMath = [];
        private XElement[] omml = [];

        [GlobalSetup]
        public void Setup()
        {
            unicodeMath = Samples.UnicodeMathCorpus();
            omml = Samples.OmmlCorpus();
        }

        [Benchmark(OperationsPerInvoke = UnicodeMathCount)]
        public int UnicodeMathToMathML() => ConvertUnicodeMath(EquationFormat.MathML);

        [Benchmark(OperationsPerInvoke = UnicodeMathCount)]
        public int UnicodeMathToLaTeX() => ConvertUnicodeMath(EquationFormat.LaTeX);

        [Benchmark(OperationsPerInvoke = UnicodeMathCount)]
        public int UnicodeMathToOmml() => ConvertUnicodeMath(EquationFormat.Omml);

        [Benchmark(OperationsPerInvoke = OmmlCount)]
        public int OmmlToMathML() => ConvertOmml(EquationFormat.MathML);

        [Benchmark(OperationsPerInvoke = OmmlCount)]
        public int OmmlToUnicodeMath() => ConvertOmml(EquationFormat.UnicodeMath);

        [Benchmark(OperationsPerInvoke = OmmlCount)]
        public int OmmlToLaTeX() => ConvertOmml(EquationFormat.LaTeX);

        private int ConvertUnicodeMath(EquationFormat destination)
        {
            var length = 0;
            foreach (var source in unicodeMath)
            {
                if (EquationConverter.TryConvert(source, EquationFormat.UnicodeMath, destination, out var result))
                    length += result.Length;
            }
            return length;
        }

        private int ConvertOmml(EquationFormat destination)
        {
            var length = 0;
            foreach (var source in omml)
                length += EquationConverter.Convert(source, EquationFormat.Omml, destination).Length;
            return length;
        }
    }
}
