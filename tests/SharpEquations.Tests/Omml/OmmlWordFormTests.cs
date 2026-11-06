#nullable enable

namespace SharpEquations.Tests.Omml
{
    using System.Threading.Tasks;
    using TUnit.Assertions.Enums;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// OMML as Word writes it, through the public API: plain letters with their style as m:sty or m:scr, upright
    /// names as one identifier, underbars as m:bar; and the same equation whatever the format it is read from.
    /// </summary>
    public class OmmlWordFormTests
    {
        private static readonly XNamespace M = MathMLNamespaces.Omml;

        private const string Namespaces = " xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";

        private static string Runs(XElement omml) => string.Join(" ", omml.Descendants(M + "r").Select(r =>
            (r.Element(M + "rPr")?.Elements().Select(p => p.Name.LocalName + "=" + (string?)p.Attribute(M + "val")).FirstOrDefault() ?? "-") + ":" + r.Element(M + "t")?.Value));

        [Test]
        [Arguments(@"x", "-:x")]
        [Arguments(@"x+y", "-:x+y")]
        [Arguments(@"\mathbf{y}", "sty=b:y")]
        [Arguments(@"\boldsymbol{y}", "sty=bi:y")]
        [Arguments(@"\mathrm{d}", "sty=p:d")]
        [Arguments(@"\mathcal{L}", "scr=script:L")]
        [Arguments(@"\mathbb{R}", "scr=double-struck:R")]
        [Arguments(@"\mathfrak{g}", "scr=fraktur:g")]
        [Arguments(@"\mathsf{s}", "scr=sans-serif:s")]
        [Arguments(@"\alpha", "-:α")]
        [Arguments(@"\mathbf{1}", "sty=b:1")]
        public async Task WritesLettersAsWord(string latex, string runs)
        {
            var omml = Equation.Parse(latex, EquationFormat.LaTeX).ToOmml();
            await Assert.That(Runs(omml)).IsEqualTo(runs);
            await Assert.That(omml.Descendants(M + "t").Select(t => t.Value).Where(t => t.Any(char.IsSurrogate))).IsEmpty().Because("no mathematical alphanumerics");
        }

        [Test]
        [Arguments("x", EquationFormat.UnicodeMath)]
        [Arguments("x", EquationFormat.LaTeX)]
        [Arguments("<math><mi>x</mi></math>", EquationFormat.MathML)]
        [Arguments("<math><mrow><mi>x</mi></mrow></math>", EquationFormat.MathML)]
        [Arguments("<math><mi mathvariant=\"italic\">x</mi></math>", EquationFormat.MathML)]
        [Arguments("<m:oMath" + Namespaces + "><m:r><m:t>x</m:t></m:r></m:oMath>", EquationFormat.Omml)]
        public async Task ReadsTheSameEquationFromEveryFormat(string source, EquationFormat format)
        {
            await Assert.That(Equation.Parse(source, format)).IsEqualTo(Equation.Parse("𝑥", EquationFormat.UnicodeMath));
        }

        [Test]
        [Arguments(@"\mathbf{y}+\mathcal{L}+\mathrm{d}+\alpha^2+\sqrt{x}")]
        [Arguments(@"\underline{x+y}+\overline{z}")]
        [Arguments(@"\max_i x_i")]
        [Arguments(@"\frac{a+b}{c}")]
        public async Task KeepsTheEquationThroughOmml(string latex)
        {
            var equation = Equation.Parse(latex, EquationFormat.LaTeX);
            await Assert.That(Equation.Parse(equation.ToOmml(), EquationFormat.Omml)).IsEqualTo(equation);
        }

        /// <summary>
        /// An n-ary operator keeps its form through OMML; the intent with which UnicodeMath and LaTeX mark its operand
        /// (:largeop) has no place in OMML, so the equations are compared as they are written.
        /// </summary>
        [Test]
        [Arguments(@"\sum_{i=1}^n a_i")]
        [Arguments(@"\int_0^1 f(x)\,dx")]
        [Arguments(@"\prod_{k} k")]
        public async Task KeepsNaryOperatorsThroughOmml(string latex)
        {
            var equation = Equation.Parse(latex, EquationFormat.LaTeX);
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back.ToString(EquationFormat.LaTeX)).IsEqualTo(equation.ToString(EquationFormat.LaTeX));
            await Assert.That(back.ToOmml().ToString()).IsEqualTo(equation.ToOmml().ToString());
        }

        [Test]
        public async Task ReadsUprightNamesAsOneIdentifier()
        {
            var equation = Equation.Parse("<m:oMath" + Namespaces + "><m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>max</m:t></m:r><m:r><m:t>x</m:t></m:r></m:oMath>", EquationFormat.Omml);
            await Assert.That(equation.ToMathML().Elements().Select(e => e.Value)).IsEquivalentTo(new[] { "max", "x" }, CollectionOrdering.Matching);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(@"\max x");
        }

        [Test]
        public async Task WritesUnderbarsAsBars()
        {
            var omml = Equation.Parse("▁(x+y)", EquationFormat.UnicodeMath).ToOmml();
            await Assert.That(omml.Descendants(M + "bar").Single().Element(M + "barPr")?.Element(M + "pos")?.Attribute(M + "val")?.Value).IsEqualTo("bot");
            await Assert.That(omml.Descendants(M + "borderBox")).IsEmpty();
        }
    }
}
