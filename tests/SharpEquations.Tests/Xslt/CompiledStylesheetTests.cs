#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System.Xml.Linq;

    /// <summary>Xslt/conformance.xsl, compiled by SharpEquations.XsltGenerator.</summary>
    internal sealed partial class ConformanceStylesheet
    {
        public static partial XElement? Transform(XElement input);
    }
}

namespace SharpEquations.Tests.Xslt
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Xsl;
    using SharpEquations.Internal;
    using SharpEquations.Internal.Mtef;
    using SharpEquations.Internal.Xslt;
    using SharpEquations.Tests.Property;

    /// <summary>
    /// The stylesheets compiled to C# by SharpEquations.XsltGenerator give what XslCompiledTransform gives, for the
    /// equations of the corpora: the OMML of Word documents and of the UnicodeMathML examples, their MathML, and the
    /// MTEF of the test data.
    /// </summary>
    public class CompiledStylesheetTests
    {
        private static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

        private static readonly ConcurrentDictionary<string, XslCompiledTransform> Stylesheets = new ConcurrentDictionary<string, XslCompiledTransform>();

        /// <summary>The stylesheet of the sources run by XslCompiledTransform, as SharpEquations ran it before it was compiled to C#.</summary>
        private static XElement? Reference(string name, XElement input)
        {
            var stylesheet = Stylesheets.GetOrAdd(name, n =>
            {
                using var stream = File.OpenRead(n == "conformance.xsl"
                    ? Path.Combine(TestData.Path(), "..", "SharpEquations.Tests", "Xslt", n)
                    : Path.Combine(TestData.Path(), "..", "..", "src", "SharpEquations", "Resources", n));
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var transform = new XslCompiledTransform();
                transform.Load(reader, XsltSettings.Default, null);
                return transform;
            });
            var output = new StringWriter();
            using (var reader = new XDocument(input).CreateReader())
                stylesheet.Transform(reader, null, output);
            var text = output.ToString();
            if (text.Trim().Length == 0)
                return null;
            using var result = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XElement.Load(result, LoadOptions.PreserveWhitespace);
        }

        // the element as text, without the namespace declarations, which the two write differently
        private static string Canonical(XElement? element)
        {
            if (element == null)
                return "(null)";
            var copy = new XElement(element);
            foreach (var e in copy.DescendantsAndSelf())
                e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            return copy.ToString(SaveOptions.DisableFormatting);
        }

        private static string Run(Func<XElement> transform)
        {
            try
            {
                return Canonical(transform());
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }
        }

        // the differences for the inputs without characters outside the basic plane, and the number of those with
        private static (List<string> Differences, int Astral) Differences(string name, Func<XElement, XElement?> compiled, IEnumerable<(string Label, XElement Input)> inputs)
        {
            var differences = new List<string>();
            var astral = 0;
            foreach (var (label, input) in inputs)
            {
                var expected = Run(() => Reference(name, new XElement(input))!);
                var actual = Run(() => compiled(new XElement(input))!);
                if (expected == actual)
                    continue;
                if (input.DescendantNodesAndSelf().OfType<XText>().Any(t => t.Value.Any(char.IsSurrogate)))
                    astral++;
                else
                    differences.Add(label + "\n  expected " + Around(expected, actual) + "\n  actual   " + Around(actual, expected));
            }
            return (differences, astral);
        }

        // the text around the first character where it differs from the other
        private static string Around(string text, string other)
        {
            var i = 0;
            while (i < text.Length && i < other.Length && text[i] == other[i])
                i++;
            var start = Math.Max(0, i - 120);
            return (start > 0 ? "…" : "") + text.Substring(start, Math.Min(text.Length - start, 240));
        }

        private static IEnumerable<(string Label, XElement Omml)> WordOmml() =>
            Directory.GetFiles(TestData.Path("omml"), "*.omml")
                .OrderBy(f => f, StringComparer.Ordinal)
                .SelectMany(f => XElement.Load(f).DescendantsAndSelf(M + "oMath").Select((e, i) => (Path.GetFileName(f) + "#" + i, e)));

        private static IEnumerable<(string Label, Equation Equation)> Equations() =>
            UnicodeMathCorpusTests.Examples()
                .Select(e => (e.input, Equation: Equation.TryParse(e.input, EquationFormat.UnicodeMath, out var equation) ? equation : null))
                .Where(e => e.Equation != null)
                .Select(e => (e.input, e.Equation!))
                .Concat(WordOmml().Select(o => (o.Label, Equation.Parse(o.Omml, EquationFormat.Omml))));

        private static async Task AssertNoDifferences((List<string> Differences, int Astral) result, int inputs)
        {
            await Assert.That(inputs).IsGreaterThan(30);
            Console.WriteLine(result.Astral + " of " + inputs + " differ, with characters outside the basic plane");
            await Assert.That(result.Differences.Count).IsEqualTo(0).Because(string.Join("\n", result.Differences.Take(10)));
        }

        [Test]
        public async Task Omml2MathML()
        {
            var inputs = WordOmml().Concat(Equations().Select(e => (e.Label, e.Equation.ToOmml()))).ToList();
            await AssertNoDifferences(Differences("omml2mml.xsl", Omml2mmlStylesheet.Transform, inputs), inputs.Count);
        }

        [Test]
        public async Task MathML2Omml()
        {
            var inputs = Equations().Select(e => (e.Label, e.Equation.ToMathML())).ToList();
            await AssertNoDifferences(Differences("mml2omml.xsl", Mml2ommlStylesheet.Transform, inputs), inputs.Count);
        }

        private const string ConformanceInput = "<root xmlns:p=\"urn:p\" a=\"1\" b=\"two\"><?pi data?><!--c-->"
            + "<item id=\"1\" k=\"x\"><name>alpha</name><v>3</v><sub><v>10</v></sub></item>"
            + "<item id=\"2\" k=\"y\"><name>beta</name><v>4</v><v>5</v></item>text"
            + "<p:item id=\"3\"><name>gamma</name><v>-1.5</v></p:item>"
            + "<item id=\"4\"><name>delta</name><sub><sub><v>7</v></sub><v>8</v></sub></item>"
            + "<ch><code>c01</code><mode>m</mode></ch><ch><code>c01</code><mode>t</mode></ch><ch><code>c01</code></ch>"
            + "<ch><code>c02</code><mode>t</mode></ch><ch><code>c02</code></ch><ch><code>c03</code><mode>m</mode></ch><ch><code>c03</code></ch>"
            + "<ch><code>c04</code><code>c05</code></ch><ch><code>c05</code><code>c01</code><mode>t</mode></ch><ch plain=\"1\"><code>c06</code></ch>"
            + "<ch plain=\"1\"><code>c05</code></ch><ch><code>c09</code></ch><ch><mode>m</mode></ch><ch/><ch><code>c10</code></ch><ch><code> c07</code></ch>"
            + "</root>";

        /// <summary>
        /// What the generator compiles, beyond what the stylesheets of the library use of it on the corpora: the axes,
        /// the predicates that count, the node-sets as variables and parameters, the comparisons, the rules found by the
        /// text of a child (Xslt/conformance.xsl).
        /// </summary>
        [Test]
        public async Task GeneratorConformance()
        {
            var input = XElement.Parse(ConformanceInput);
            var expected = Canonical(Reference("conformance.xsl", new XElement(input)));
            var actual = Canonical(ConformanceStylesheet.Transform(new XElement(input)));
            await Assert.That(XElement.Parse(expected).Elements().Count()).IsGreaterThan(30);
            foreach (var (e, a) in XElement.Parse(expected).Elements().Zip(XElement.Parse(actual).Elements(), (e, a) => (e, a)))
                await Assert.That(a.ToString(SaveOptions.DisableFormatting)).IsEqualTo(e.ToString(SaveOptions.DisableFormatting));
            await Assert.That(actual).IsEqualTo(expected);
        }

        [Test]
        public async Task ContentMathML2MathML()
        {
            var inputs = Directory.GetFiles(TestData.Path("contentmathml"), "*.mml")
                .OrderBy(f => f, StringComparer.Ordinal)
                .SelectMany(f => XElement.Load(f, LoadOptions.PreserveWhitespace).DescendantsAndSelf().Where(e => e.Name.LocalName == "math").Select((e, i) => (Path.GetFileName(f) + "#" + i, e)))
                .ToList();
            await AssertNoDifferences(Differences("ctop.xsl", CtopStylesheet.Transform, inputs), inputs.Count);
        }

        [Test]
        public async Task Mtef2MathML()
        {
            var inputs = new List<(string, XElement)>();
            foreach (var file in Directory.GetFiles(TestData.Path("mtef"), "*.*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var data = File.ReadAllBytes(file).AsSpan();
                if (MtefParser.HasEquationNativeHeader(data))
                    data = data.Slice(data[0] | data[1] << 8);
                try
                {
                    var xml = MtefReader.Read(data);
                    MtefPreprocessor.Prepare(xml);
                    inputs.Add((Path.GetFileName(file), xml));
                }
                catch (Exception)
                {
                    // not MTEF
                }
            }
            await AssertNoDifferences(Differences("mtef2mml.xsl", Mtef2mmlStylesheet.Transform, inputs), inputs.Count);
        }
    }
}
