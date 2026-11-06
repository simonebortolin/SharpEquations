#nullable enable

namespace SharpEquations.Tests.MathML
{
    using System.Threading.Tasks;
    using System;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>MathML read into the normalized MathML an equation holds.</summary>
    public class MathMLReaderTests
    {
        private const string Namespace = "http://www.w3.org/1998/Math/MathML";
        private static readonly XNamespace Mml = Namespace;
        private static readonly EquationOptions Strict = new EquationOptions { Strict = true };

        private static string Read(string mathML, EquationOptions? options = null)
        {
            var math = Equation.Parse(mathML, EquationFormat.MathML, options).ToString();
            return math.Replace(" xmlns=\"" + Namespace + "\"", "");
        }

        [Test]
        [Arguments("<math><mfenced><mi>x</mi></mfenced></math>",
            "<math><mrow><mo>(</mo><mi>x</mi><mo>)</mo></mrow></math>")]
        [Arguments("<math><mfenced open=\"[\" close=\"]\"><mi>a</mi><mi>b</mi><mi>c</mi></mfenced></math>",
            "<math><mrow><mo>[</mo><mi>a</mi><mo>,</mo><mi>b</mi><mo>,</mo><mi>c</mi><mo>]</mo></mrow></math>")]
        [Arguments("<math><mfenced separators=\"; |\"><mi>a</mi><mi>b</mi><mi>c</mi><mi>d</mi></mfenced></math>",
            "<math><mrow><mo>(</mo><mi>a</mi><mo>;</mo><mi>b</mi><mo>|</mo><mi>c</mi><mo>|</mo><mi>d</mi><mo>)</mo></mrow></math>")]
        [Arguments("<math><mfenced separators=\"\"><mi>a</mi><mi>b</mi></mfenced></math>",
            "<math><mrow><mo>(</mo><mi>a</mi><mi>b</mi><mo>)</mo></mrow></math>")]
        [Arguments("<math><mfenced open=\"{\" close=\"\"><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow></mfenced></math>",
            "<math><mrow><mo>{</mo><mi>a</mi><mo>+</mo><mi>b</mi><mo /></mrow></math>")]
        [Arguments("<math><mfenced intent=\"interval\"><mn>0</mn><mn>1</mn></mfenced></math>",
            "<math><mrow intent=\"interval\"><mo>(</mo><mrow><mn>0</mn><mo>,</mo><mn>1</mn></mrow><mo>)</mo></mrow></math>")]
        [Arguments("<math><mfenced><mfenced><mi>x</mi></mfenced></mfenced></math>",
            "<math><mrow><mo>(</mo><mrow><mo>(</mo><mi>x</mi><mo>)</mo></mrow><mo>)</mo></mrow></math>")]
        public async Task WritesMfencedAsAnMrow(string mathML, string expected)
        {
            await Assert.That(Read(mathML)).IsEqualTo(expected);
        }

        [Test]
        [Arguments("<math><mrow intent=\":fenced\"><mo></mo><mi>x</mi><mo></mo></mrow></math>", "<math><mi>x</mi></math>")]
        [Arguments("<math><mrow intent=\":fenced\"><mo></mo><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mo></mo></mrow></math>",
            "<math><mi>a</mi><mo>+</mo><mi>b</mi></math>")]
        [Arguments("<math><mrow intent=\":fenced\"><mo></mo><mi>x</mi><mo>)</mo></mrow></math>", "<math><mrow><mo /><mi>x</mi><mo>)</mo></mrow></math>")]
        [Arguments("<math><mrow intent=\":fenced\" arg=\"a\"><mo></mo><mi>x</mi><mo></mo></mrow></math>",
            "<math><mi>x</mi></math>")]
        public async Task DropsInvisibleFences(string mathML, string expected)
        {
            await Assert.That(Read(mathML)).IsEqualTo(expected);
        }

        [Test]
        public async Task WritesLinksAsMrowsWithHref()
        {
            await Assert.That(Read("<math><a href=\"https://example.org\"><mi>x</mi></a></math>")).IsEqualTo("<math><mrow href=\"https://example.org\"><mi>x</mi></mrow></math>");
            await Assert.That(Equation.Parse(@"\href(msn.com&x)", EquationFormat.UnicodeMath).ToMathML().Descendants(Mml + "a")).IsEmpty();
        }

        [Test]
        [Arguments("<math><mi mathcolor=\"red\" mathbackground=\"#fff\" mathsize=\"big\" style=\"x\" color=\"red\" background=\"x\" fontsize=\"1\" fontfamily=\"f\" fontweight=\"bold\" fontstyle=\"italic\" mathvariant=\"bold\">x</mi></math>",
            "<math><mi>𝐱</mi></math>")]
        [Arguments("<math><mstyle mathcolor=\"red\"><mi>a</mi><mi>b</mi></mstyle></math>", "<math><mi>a</mi><mi>b</mi></math>")]
        [Arguments("<math><mstyle mathcolor=\"red\"><mi>a</mi></mstyle></math>", "<math><mi>a</mi></math>")]
        [Arguments("<math><mstyle mathcolor=\"red\" displaystyle=\"true\"><mi>a</mi></mstyle></math>", "<math><mstyle displaystyle=\"true\"><mi>a</mi></mstyle></math>")]
        [Arguments("<math><mstyle mathcolor=\"red\"><mstyle mathsize=\"2\"><mi>a</mi></mstyle></mstyle></math>", "<math><mi>a</mi></math>")]
        public async Task DropsFormatting(string mathML, string expected)
        {
            await Assert.That(Read(mathML)).IsEqualTo(expected);
        }

        [Test]
        public async Task KeepsFormattingWhenAsked()
        {
            const string coloured = "<math><mstyle mathcolor=\"red\"><mi>a</mi><mi>b</mi></mstyle></math>";
            await Assert.That(Read(coloured, new EquationOptions { PreserveFormatting = true })).IsEqualTo(coloured);
        }

        [Test]
        [Arguments("<math><mi>x</mi></math>")]
        [Arguments("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>")]
        [Arguments("<mml:math><mml:mi>x</mml:mi></mml:math>")]
        [Arguments("<m:math xmlns:m=\"http://www.w3.org/1998/Math/MathML\"><m:mi>x</m:mi></m:math>")]
        [Arguments("<?xml version=\"1.0\"?><math><mi>x</mi></math>")]
        [Arguments("  <!-- a comment --><math><mi>x</mi></math>  ")]
        [Arguments("<math><mi>x</mi></math><!-- after -->")]
        public async Task ReadsEveryNamespaceForm(string mathML)
        {
            await Assert.That(Read(mathML)).IsEqualTo("<math><mi>x</mi></math>");
        }

        [Test]
        public async Task MovesUnqualifiedElementsInsideOtherMathMLToItsNamespace()
        {
            var math = new XElement(Mml + "math", new XElement("mi", "x"));
            await Assert.That(Equation.Parse(math, EquationFormat.MathML).ToMathML().Elements().Single().Name).IsEqualTo(Mml + "mi");
        }

        [Test]
        [Arguments("<math><semantics><mi>x</mi><annotation-xml encoding=\"MathML-Content\"><ci>x</ci></annotation-xml></semantics></math>")]
        [Arguments("<math><semantics><mi>x</mi><annotation encoding=\"application/x-tex\">x</annotation></semantics></math>")]
        [Arguments("<math><mtable><mlabeledtr><mtd><mtext>1</mtext></mtd><mtd><mi>x</mi></mtd></mlabeledtr></mtable></math>")]
        [Arguments("<math><mstack><msrow><mn>1</mn></msrow><msline/></mstack></math>")]
        [Arguments("<math><mmultiscripts><mi>X</mi><none/><mi>a</mi><mprescripts/><mi>b</mi><none/></mmultiscripts></math>")]
        [Arguments("<math><maction actiontype=\"toggle\"><mi>a</mi><mi>b</mi></maction><mglyph alt=\"g\"/><mspace width=\"1em\"/><ms>s</ms></math>")]
        public async Task AcceptsPresentationMathMLWhenStrict(string mathML)
        {
            await Assert.That(() => Equation.Parse(mathML, EquationFormat.MathML, Strict)).ThrowsNothing();
        }

        [Test]
        [Arguments("<math><foo/></math>", "foo")]
        [Arguments("<math><apply><plus/><ci>x</ci></apply></math>", "apply")]
        [Arguments("<math><mi>x</mi><svg xmlns=\"http://www.w3.org/2000/svg\"/></math>", "svg")]
        public async Task RejectsOtherElementsWhenStrict(string mathML, string element)
        {
            await Assert.That(() => Equation.Parse(mathML, EquationFormat.MathML, Strict)).Throws<FormatException>().WithMessageContaining(element);
            await Assert.That(() => Equation.Parse(mathML, EquationFormat.MathML)).ThrowsNothing();
        }

        [Test]
        public async Task DoesNotResolveExternalEntities()
        {
            const string xxe = "<!DOCTYPE math [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><math><mi>&xxe;</mi></math>";
            await Assert.That(() => Equation.Parse(xxe, EquationFormat.MathML)).ThrowsExactly<FormatException>();
        }

        [Test]
        public async Task ReadsTheDisplayAttribute()
        {
            await Assert.That(Equation.Parse("<math display=\"block\"><mi>x</mi></math>", EquationFormat.MathML).ToString(EquationFormat.Omml)).StartsWith("<m:oMathPara");
        }
    }
}
