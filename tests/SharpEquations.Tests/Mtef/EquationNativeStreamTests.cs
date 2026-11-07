namespace SharpEquations.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;
    using TUnit.Assertions.Enums;

    /// <summary>
    /// "Equation Native" streams of Equation Editor 3.0 objects of Word documents of Apache POI, and payloads that only
    /// look like MTEF, from the fixtures of https://github.com/Mengfei-Li/document-equation-migration.
    /// </summary>
    public class EquationNativeStreamTests
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        // a path under tests/data/mtef, with slashes
        private static Equation Read(string path) =>
            Equation.Parse(File.ReadAllBytes(TestData.Path(Path.Combine(new[] { "mtef" }.Concat(path.Split('/')).ToArray()))).AsSpan(), EquationFormat.Mtef);

        private static string Text(XElement element) => Regex.Replace(element.Value, @"\s", string.Empty).Normalize(NormalizationForm.FormKC);

        // the text as it reads, with every space one space and the zero-width spaces left out
        private static string Words(XElement element)
        {
            var text = new StringBuilder();
            foreach (var c in element.Value.Normalize(NormalizationForm.FormKC))
            {
                if (c == '​')
                    continue;
                text.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }
            return Regex.Replace(text.ToString(), " +", " ").Trim();
        }

        private static bool IsArrow(char c) => (c >= '←' && c <= '⇿') || (c >= '⟰' && c <= '⟿') || (c >= '⤀' && c <= '⥿');

        [Test]
        [Arguments("bug61268-0001.eqn", "<math><mi>P</mi><mo>=</mo><mi>α</mi><mi>G</mi><mo>+</mo><mi>k</mi><mi>G</mi></math>")]
        [Arguments("bug61268-0003.eqn", "<math><mrow><mo>[</mo><mn>1</mn><mo>,</mo><mi>n</mi><mo>−</mo><mn>1</mn><mo>]</mo></mrow></math>")]
        public async Task ConvertsEquationEditorObjectsOfApachePoi(string file, string mathML)
        {
            await Assert.That(Read("poi/" + file)).IsEqualTo(Equation.Parse(mathML, EquationFormat.MathML));
        }

        /// <summary>
        /// Two rows of big summation operators with both limits (template selector 43, variation 2 of MTEF 3):
        /// the mean and the variance of x(y).
        /// </summary>
        [Test]
        public async Task ConvertsSummationOperatorsWithBothLimits()
        {
            var math = Read("poi/bug50936_1-0013.eqn").ToMathML();
            var rows = math.Descendants(Mml + "mtable").Single().Elements(Mml + "mtr").ToList();
            await Assert.That(rows.Count).IsEqualTo(2);
            foreach (var row in rows)
            {
                var fraction = row.Descendants(Mml + "mfrac").Single();
                var sum = fraction.Elements().First().Descendants(Mml + "munderover").Single();
                await Assert.That(sum.Elements().Select(Text)).IsEquivalentTo(new[] { "Σ", "i=1", "n(y)" }, CollectionOrdering.Matching);
            }
            await Assert.That(rows.Select(r => Text(r.Descendants(Mml + "mfrac").Single().Elements().Last()))).IsEquivalentTo(new[] { "n(y)", "n(y)-1" }, CollectionOrdering.Matching);
            await Assert.That(rows[0].Descendants(Mml + "mover").First().Elements().Select(e => e.Value)).IsEquivalentTo(new[] { "x", "¯" }, CollectionOrdering.Matching);
            await Assert.That(math.Value.Any(IsArrow)).IsFalse();
        }

        /// <summary>
        /// The spaces of Equation Editor 3.0 (0xEB00 to 0xEB08 in the space typeface), also in MathType equations first
        /// written with it, are spaces, not the arrows of MathType's encoding with the same codes.
        /// </summary>
        [Test]
        [Arguments("poi/bug50936_1-0013.eqn")]
        [Arguments("records/mathtype3/matrix_2x1.eqn")]
        [Arguments("records/mathtype3/frac.eqn")]
        [Arguments("records/mathtype5/mathtype5_frac_sub.eqn")]
        public async Task ReadsEquationEditorSpacesAsSpaces(string path)
        {
            var math = Read(path).ToMathML();
            await Assert.That(math.Value.Any(IsArrow)).IsFalse().Because(math.Value);
        }

        [Test]
        public async Task KeepsTheSpacesBetweenWords()
        {
            var cells = Read("records/mathtype3/matrix_2x1.eqn").ToMathML().Descendants(Mml + "mtd").Select(Words).ToList();
            await Assert.That(cells).IsEquivalentTo(new[] { "1 if regions r and s share a common boundary (edge)", "0 otherwise" }, CollectionOrdering.Matching);
            var formula = Read("records/mathtype5/mathtype5_frac_sub.eqn").ToMathML();
            await Assert.That(Words(formula)).StartsWith("Psti= 0,8*");
        }

        [Test]
        public async Task PreservesRowsAndColumnsOfNonSquareMatrices()
        {
            var table = Read("records/mathtype3/matrix_2x1.eqn").ToMathML().Descendants(Mml + "mtable").Single();
            var rows = table.Elements(Mml + "mtr").ToList();
            await Assert.That(rows.Count).IsEqualTo(2);
            await Assert.That(rows.All(row => row.Elements(Mml + "mtd").Count() == 1)).IsTrue();
        }

        public static IEnumerable<string> PayloadsThatOnlyLookLikeMtef() =>
            Directory.GetFiles(TestData.Path(Path.Combine("mtef", "invalid")), "*.bin").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal)!;

        /// <summary>
        /// A synthetic EQNOLEFILEHDR and MTEF header followed by text, and a stream that names MTEF in text: not equations.
        /// </summary>
        [Test]
        [MethodDataSource(nameof(PayloadsThatOnlyLookLikeMtef))]
        public async Task RejectsPayloadsThatOnlyLookLikeMtef(string file)
        {
            var data = File.ReadAllBytes(TestData.Path(Path.Combine("mtef", "invalid", file)));
            await Assert.That(() => Equation.Parse(data.AsSpan(), EquationFormat.Mtef)).Throws<InvalidDataException>();
            await Assert.That(() => Equation.Parse(data.Skip(28).ToArray().AsSpan(), EquationFormat.Mtef)).Throws<InvalidDataException>();
        }
    }
}
