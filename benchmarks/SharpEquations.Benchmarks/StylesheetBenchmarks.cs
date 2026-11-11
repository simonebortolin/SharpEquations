namespace SharpEquations.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Xsl;
    using BenchmarkDotNet.Attributes;
    using SharpEquations.Internal.Mtef;
    using SharpEquations.Internal.Xslt;

    /// <summary>
    /// The stylesheets alone, over the corpora of the tests: compiled to C# by SharpEquations.XsltGenerator, as the
    /// library runs them, and run by XslCompiledTransform, as it ran them before (the output written as text and read
    /// again). The times are per corpus.
    /// </summary>
    [MemoryDiagnoser]
    public class StylesheetBenchmarks
    {
        private XElement[] omml = [];
        private XElement[] mathML = [];
        private XElement[] mtef = [];
        private XElement[] contentMathML = [];
        private XslCompiledTransform omml2mml = null!;
        private XslCompiledTransform mml2omml = null!;
        private XslCompiledTransform mtef2mml = null!;
        private XslCompiledTransform ctop = null!;

        [GlobalSetup]
        public void Setup()
        {
            var equations = Samples.UnicodeMathCorpus()
                .Select(source => Equation.TryParse(source, EquationFormat.UnicodeMath, out var equation) ? equation : null)
                .OfType<Equation>()
                .Concat(Samples.OmmlCorpus().Select(source => Equation.Parse(source, EquationFormat.Omml)))
                .ToArray();
            mathML = equations.Select(equation => equation.ToMathML()).ToArray();
            omml = Samples.OmmlCorpus().SelectMany(source => source.DescendantsAndSelf(MathMLNamespaces.Omml + "oMath"))
                .Concat(equations.Select(equation => equation.ToOmml()))
                .ToArray();
            mtef = Samples.MtefCorpus().Select(Records).OfType<XElement>().ToArray();
            omml2mml = Load("omml2mml.xsl");
            omml = omml.Where(oMath => Converts(omml2mml, oMath)).ToArray();
            mml2omml = Load("mml2omml.xsl");
            mtef2mml = Load("mtef2mml.xsl");
            contentMathML = Samples.ContentMathMLCorpus();
            ctop = Load("ctop.xsl");
        }

        // an equation written in the text of another is two document elements for the stylesheet, and
        // XslCompiledTransform splits the characters outside the basic plane
        private static bool Converts(XslCompiledTransform stylesheet, XElement oMath)
        {
            try
            {
                return Omml2mmlStylesheet.Transform(oMath) != null && Run(stylesheet, [oMath]) == 1;
            }
            catch (Exception e) when (e is XmlException or XsltException or ArgumentException)
            {
                return false;
            }
        }

        private static XElement? Records(byte[] bytes)
        {
            var data = bytes.AsSpan();
            if (MtefParser.HasEquationNativeHeader(data))
                data = data.Slice(data[0] | data[1] << 8);
            try
            {
                var xml = MtefReader.Read(data);
                MtefPreprocessor.Prepare(xml);
                Mtef2mmlStylesheet.Transform(xml);
                return xml;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return null;
            }
        }

        private static XslCompiledTransform Load(string name)
        {
            using var reader = XmlReader.Create(Samples.Stylesheet(name), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var transform = new XslCompiledTransform();
            transform.Load(reader, XsltSettings.Default, null);
            return transform;
        }

        private static int Count(XElement? element) => element == null ? 0 : 1;

        private static int Run(XslCompiledTransform stylesheet, XElement[] inputs)
        {
            var count = 0;
            foreach (var input in inputs)
            {
                var output = new StringWriter();
                using (var reader = new XDocument(input).CreateReader())
                    stylesheet.Transform(reader, null, output);
                var text = output.ToString();
                if (text.Trim().Length == 0)
                    continue;
                using var result = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                count += Count(XElement.Load(result, LoadOptions.PreserveWhitespace));
            }
            return count;
        }

        [Benchmark]
        public int Omml2MathML()
        {
            var count = 0;
            foreach (var input in omml)
                count += Count(Omml2mmlStylesheet.Transform(input));
            return count;
        }

        [Benchmark]
        public int Omml2MathMLXslCompiledTransform() => Run(omml2mml, omml);

        [Benchmark]
        public int MathML2Omml()
        {
            var count = 0;
            foreach (var input in mathML)
                count += Count(Mml2ommlStylesheet.Transform(input));
            return count;
        }

        [Benchmark]
        public int MathML2OmmlXslCompiledTransform() => Run(mml2omml, mathML);

        [Benchmark]
        public int Mtef2MathML()
        {
            var count = 0;
            foreach (var input in mtef)
                count += Count(Mtef2mmlStylesheet.Transform(input));
            return count;
        }

        [Benchmark]
        public int Mtef2MathMLXslCompiledTransform() => Run(mtef2mml, mtef);

        [Benchmark]
        public int ContentMathML2MathML()
        {
            var count = 0;
            foreach (var input in contentMathML)
                count += Count(CtopStylesheet.Transform(input));
            return count;
        }

        [Benchmark]
        public int ContentMathML2MathMLXslCompiledTransform() => Run(ctop, contentMathML);
    }
}
