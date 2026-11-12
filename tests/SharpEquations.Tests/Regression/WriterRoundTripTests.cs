#nullable enable

namespace SharpEquations.Tests.Regression
{
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// Presentation MathML that the writers wrote as text they could not read back, or read back to an equation that
    /// changed again on the next round trip (found with the Content MathML corpus, but not specific to it).
    /// </summary>
    public class WriterRoundTripTests
    {
        private static readonly EquationOptions Word = new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word };

        private static Equation MathML(string content) =>
            Equation.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\">" + content + "</math>", EquationFormat.MathML);

        private static Equation RoundTrip(Equation equation, EquationFormat format, EquationOptions? options = null) =>
            format == EquationFormat.Omml
                ? Equation.Parse(equation.ToOmml(), EquationFormat.Omml)
                : Equation.Parse(equation.ToString(format, options), format, options);

        [Test]
        [Arguments("<mi>sin</mi><msub><mo>|</mo><mi>ℤ</mi></msub>", "sin├|_ℤ")]
        [Arguments("<mi>f</mi><msubsup><mo>|</mo><mi>a</mi><mi>b</mi></msubsup>", "𝑓├|_𝑎^𝑏")]
        [Arguments("<mrow><mn/><mo>/</mo><mn/></mrow>", "ⓡ(/&U+002F)")]
        [Arguments("<mo>=</mo><mo>/</mo><mo>=</mo>", "=ⓡ(/&U+002F)=")]
        public async Task UnicodeMathWritesWhatItReads(string mathML, string unicodeMath)
        {
            var equation = MathML(mathML);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(unicodeMath);
            var back = RoundTrip(equation, EquationFormat.UnicodeMath);
            await Assert.That(RoundTrip(back, EquationFormat.UnicodeMath)).IsEqualTo(back);
        }

        /// <summary>
        /// UnicodeMath that reads to an equation the writer wrote as text that read back otherwise: a script of a matrix
        /// in brackets (⒨(…)²), a function in a denominator (𝑎/ sin⁡𝛼), a denominator that ends with a script before
        /// what follows the fraction (1/4𝜋𝜀₀ 𝑞₁𝑞₂/𝑟²), and the space after a comma that two spaces type (𝑎,  𝑏).
        /// </summary>
        [Test]
        [Arguments("(■(a&b@c&d))^2")]
        [Arguments("(■(a&b@c&d))^(-1)=1/(ad-bc) (■(d&-b@-c&a))")]
        [Arguments("[■(a&b@c&d)]_ij")]
        [Arguments("|■(a&b@c&d)|^2")]
        [Arguments("a/sin⁡α")]
        [Arguments("a/sin⁡α=b/sin⁡β=c/sin⁡γ")]
        [Arguments("(sin⁡α)/a")]
        [Arguments("1/(4πε_0) (q_1 q_2)/r^2")]
        [Arguments("1/(x_1) y")]
        [Arguments("1/(ab^2) c/d")]
        [Arguments("A=√s,  s=2")]
        [Arguments("f(x,  y)")]
        [Arguments("x^(2/3)")]
        [Arguments("a_(x/2)+b^(1/n)")]
        public async Task UnicodeMathReadsBackWhatItWrites(string unicodeMath)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            await Assert.That(RoundTrip(equation, EquationFormat.UnicodeMath)).IsEqualTo(equation).Because(equation.ToString(EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task UnicodeMathReadsTwoSpacesAfterACommaAsASpace()
        {
            await Assert.That(Equation.Parse("a,  b", EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a,\u2002b", EquationFormat.UnicodeMath));
        }

        [Test]
        [Arguments("<mrow><mo>[</mo><mi>a</mi><mo>|</mo><mi>b</mi><mo>]</mo></mrow>")]
        [Arguments("<mrow><mo>[</mo><mi>a</mi><mo>‖</mo><mi>b</mi><mo>]</mo></mrow>")]
        [Arguments("<mrow><mo>(</mo><mi>a</mi><mo>|</mo><mi>b</mi><mo>)</mo></mrow>")]
        [Arguments("<mi>x</mi><mo>+</mo><mrow><mo>[</mo><mi>a</mi><mo>|</mo><mi>b</mi><mo>]</mo></mrow>")]
        public async Task UnicodeMathKeepsABarInsideBrackets(string mathML)
        {
            var equation = MathML(mathML);
            await Assert.That(RoundTrip(equation, EquationFormat.UnicodeMath)).IsEqualTo(equation);
        }

        [Test]
        public async Task UnicodeMathIsStableOnABarFenceAfterAnOpenBracket()
        {
            // [𝑎|𝑏] read as UnicodeMath: [, 𝑎 and the fence |𝑏], which is written back as it was, without a ┤
            var equation = Equation.Parse("[𝑎|𝑏]", EquationFormat.UnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo("[𝑎|𝑏]");
            await Assert.That(RoundTrip(equation, EquationFormat.UnicodeMath)).IsEqualTo(equation);
        }

        [Test]
        public async Task UnicodeMathReadsANameMarkedAsAFunctionWithoutOperand()
        {
            await Assert.That(Equation.Parse("NaN⁡,", EquationFormat.UnicodeMath))
                .IsEqualTo(MathML("<mi>NaN</mi><mo>⁡</mo><mo>,</mo>"));
        }

        [Test]
        [Arguments(false)]
        [Arguments(true)]
        public async Task LaTeXOperatorNameWithoutOperandIsStable(bool word)
        {
            var options = word ? Word : null;
            var equation = Equation.Parse(@"\operatorname{NaN},\operatorname{true}", EquationFormat.LaTeX, options);
            await Assert.That(equation).IsEqualTo(MathML("<mi>NaN</mi><mo>⁡</mo><mo>,</mo><mi>true</mi><mo>⁡</mo>"));
            var written = MathML("<mi>NaN</mi><mo>,</mo><mi>true</mi>");
            await Assert.That(written.ToString(EquationFormat.LaTeX, options)).IsEqualTo(@"\operatorname{NaN},\operatorname{true}");
            var back = RoundTrip(written, EquationFormat.LaTeX, options);
            await Assert.That(back).IsEqualTo(equation);
            await Assert.That(RoundTrip(back, EquationFormat.LaTeX, options)).IsEqualTo(back);
        }

        [Test]
        [Arguments("<mi>□</mi>", @"\□", "<mo intent=\":text\">□</mo>")]
        [Arguments("<mi>□</mi><mo>+</mo><mi>x</mi>", @"\□+𝑥", "<mo intent=\":text\">□</mo><mo>+</mo><mi>x</mi>")]
        [Arguments("<mi>√</mi><mi>x</mi>", @"\√𝑥", "<mo intent=\":text\">√</mo><mi>x</mi>")]
        public async Task WordLaTeXEscapesAStructureOperatorAsAnIdentifier(string mathML, string latex, string read)
        {
            var equation = MathML(mathML);
            await Assert.That(equation.ToString(EquationFormat.LaTeX, Word)).IsEqualTo(latex);
            var back = RoundTrip(equation, EquationFormat.LaTeX, Word);
            await Assert.That(back).IsEqualTo(MathML(read));
            await Assert.That(RoundTrip(back, EquationFormat.LaTeX, Word)).IsEqualTo(back);
        }

        [Test]
        [Arguments("<mi>□</mi>", @"\square", "<mo intent=\":text\">□</mo>")]
        [Arguments("<mi>□</mi><mi>x</mi>", @"\square x", "<mo intent=\":text\">□</mo><mi>x</mi>")]
        [Arguments("<mi>√</mi><mi>x</mi>", @"{\surd}x", "<mo>√</mo><mi>x</mi>")]
        [Arguments("<mo>√</mo><mi>x</mi>", @"{\surd}x", "<mo>√</mo><mi>x</mi>")]
        public async Task StandardLaTeXWritesAStructureOperatorAsItsSymbol(string mathML, string latex, string read)
        {
            var equation = MathML(mathML);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(latex);
            var back = RoundTrip(equation, EquationFormat.LaTeX);
            await Assert.That(back).IsEqualTo(MathML(read));
            await Assert.That(RoundTrip(back, EquationFormat.LaTeX)).IsEqualTo(back);
        }

        [Test]
        [Arguments(@"\square")]
        [Arguments(@"\Box")]
        public async Task LaTeXReadsTheSquareSymbol(string latex)
        {
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX)).IsEqualTo(MathML("<mo intent=\":text\">□</mo>"));
        }

        [Test]
        [Arguments("<mi>□</mi>", "ⓡ(□&U+25A1)", "<mo>□</mo>")]
        [Arguments("<mi>□</mi><mi>x</mi>", "ⓡ(□&U+25A1)𝑥", "<mo>□</mo><mi>x</mi>")]
        [Arguments("<mo>□</mo><mi>x</mi>", "ⓡ(□&U+25A1)𝑥", "<mo>□</mo><mi>x</mi>")]
        [Arguments("<mi>√</mi><mi>x</mi>", "ⓡ(√&U+221A)𝑥", "<mo>√</mo><mi>x</mi>")]
        public async Task UnicodeMathWritesAStructureOperatorAsARemark(string mathML, string unicodeMath, string read)
        {
            var equation = MathML(mathML);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(unicodeMath);
            var back = RoundTrip(equation, EquationFormat.UnicodeMath);
            await Assert.That(back).IsEqualTo(MathML(read));
            await Assert.That(RoundTrip(back, EquationFormat.UnicodeMath)).IsEqualTo(back);
        }

        [Test]
        [Arguments("<mi href=\"#t1\">sharet1</mi>", "<mi>sharet1</mi>")]
        [Arguments("<mi>x1</mi>", "<mi>x1</mi>")]
        public async Task OmmlKeepsAnUprightNameWithDigits(string mathML, string read)
        {
            var back = RoundTrip(MathML(mathML), EquationFormat.Omml);
            await Assert.That(back).IsEqualTo(MathML(read));
            await Assert.That(RoundTrip(back, EquationFormat.Omml)).IsEqualTo(back);
        }
    }
}
