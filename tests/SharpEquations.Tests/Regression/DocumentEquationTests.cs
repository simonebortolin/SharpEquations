#nullable enable

namespace SharpEquations.Tests.Regression
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// Equations of real documents through the public API: the 192 OMML equations saved by Word and the MathType and
    /// Equation Editor 3.0 objects of the test data are read (strictly for OMML), written in every format, and keep
    /// their structures.
    /// </summary>
    public class DocumentEquationTests
    {
        private static readonly XNamespace M = MathMLNamespaces.Omml;
        private static readonly EquationOptions Strict = new EquationOptions { Strict = true };
        private static readonly EquationFormat[] Writable = { EquationFormat.MathML, EquationFormat.Omml, EquationFormat.UnicodeMath, EquationFormat.LaTeX };

        private static string Dir(string name) => TestData.Path(name);

        public static IEnumerable<string> OmmlFiles() => Directory.GetFiles(Dir("omml"), "*.omml").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal)!;

        public static IEnumerable<string> MtefFiles() =>
            Directory.GetFiles(Dir("mtef"), "*.eqn", SearchOption.AllDirectories).Select(f => f.Substring(Dir("mtef").Length + 1)).OrderBy(f => f, StringComparer.Ordinal);

        private static byte[] EquationNative(string file) => File.ReadAllBytes(file);

        [Test]
        [MethodDataSource(nameof(OmmlFiles))]
        public async Task ReadsWordEquations(string file)
        {
            var omml = XElement.Load(Path.Combine(Dir("omml"), file));
            var equation = Equation.Parse(omml, EquationFormat.Omml, Strict);
            await Assert.That(equation.ToMathML().HasElements).IsTrue();
            foreach (var format in Writable)
                await Assert.That(equation.ToString(format)).IsNotNull().Because(format.ToString());
            await Assert.That(equation.ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word })).IsNotNull();
            // written as OMML, the equation keeps its structures: an integral or a matrix does not become text
            var back = equation.ToOmml();
            foreach (var kind in new[] { "nary", "f", "rad", "sSub", "sSup", "sSubSup", "m" })
                await Assert.That(back.Descendants(M + kind).Count()).IsEqualTo(omml.Descendants(M + kind).Count()).Because(kind);
            await Assert.That(Equation.Parse(back, EquationFormat.Omml, Strict)).IsEqualTo(Equation.Parse(back, EquationFormat.Omml));
        }

        [Test]
        [MethodDataSource(nameof(OmmlFiles))]
        public async Task WritesWordEquationsAsLaTeXThatReadsBack(string file)
        {
            var equation = Equation.Parse(XElement.Load(Path.Combine(Dir("omml"), file)), EquationFormat.Omml);
            var latex = equation.ToString(EquationFormat.LaTeX);
            await Assert.That(Equation.TryParse(latex, EquationFormat.LaTeX, out _)).IsTrue().Because(latex);
            var unicodeMath = equation.ToString(EquationFormat.UnicodeMath);
            await Assert.That(Equation.TryParse(unicodeMath, EquationFormat.UnicodeMath, out _)).IsTrue().Because(unicodeMath);
        }

        [Test]
        [MethodDataSource(nameof(MtefFiles))]
        public async Task ReadsMathTypeEquations(string file)
        {
            var native = EquationNative(Path.Combine(Dir("mtef"), file));
            var equation = Equation.Parse(native.AsSpan(), EquationFormat.Mtef);
            await Assert.That(equation).IsEqualTo(Equation.Parse(native.Skip(28).ToArray().AsSpan(), EquationFormat.Mtef)).Because("with or without the Equation Native header");
            foreach (var format in Writable)
                await Assert.That(equation.ToString(format)).IsNotNull().Because(format.ToString());
        }

        [Test]
        public async Task RejectsBytesThatAreNotMtef()
        {
            await Assert.That(() => Equation.Parse(new byte[] { 9, 9, 9, 9 }.AsSpan(), EquationFormat.Mtef)).Throws<InvalidDataException>();
            await Assert.That(() => Equation.Parse(Array.Empty<byte>().AsSpan(), EquationFormat.Mtef)).Throws<InvalidDataException>();
        }
    }
}
