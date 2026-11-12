#nullable enable

namespace SharpEquations.Tests.Omml
{
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// Characters outside the basic plane (mathematical alphanumerics such as 𝒢, 𝟏, 𝑥, 𝜕, and emoji such as 🍕) to and
    /// from OMML, through the public API.
    /// </summary>
    /// <remarks>
    /// The mathematical alphanumerics are written as Word writes them, the plain letter with its style (𝒢 is a G with
    /// m:scr), before mml2omml.xsl sees them, so how the stylesheet counts characters does not matter for them. An
    /// emoji reaches the stylesheet as it is, and the stylesheet, compiled to C# by SharpEquations.XsltGenerator,
    /// counts code points as XPath does (XslCompiledTransform counted UTF-16 units, so that 🍕 counted as two
    /// characters and was written upright, in a run of its own): 🍕 is written as an italic identifier, without m:rPr,
    /// in one run with the characters around it, and reads back to the same equation. The tests marked as counting
    /// code points pin that behaviour.
    /// </remarks>
    public class OmmlAstralTests
    {
        private static readonly XNamespace M = MathMLNamespaces.Omml;

        private const string Namespaces = " xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";

        /// <summary>The OMML without its WordprocessingML run properties (the Cambria Math font) and namespace declarations.</summary>
        private static string Compact(XElement omml)
        {
            var copy = new XElement(omml);
            copy.Descendants(MathMLNamespaces.WordprocessingML + "rPr").Remove();
            return copy.ToString(SaveOptions.DisableFormatting)
                .Replace(" xmlns:m=\"" + M.NamespaceName + "\"", "")
                .Replace(" xmlns:w=\"" + MathMLNamespaces.WordprocessingML.NamespaceName + "\"", "");
        }

        /// <summary>The runs of the OMML as "properties:text", "-" for a run without m:rPr.</summary>
        private static string Runs(XElement omml) => string.Join(" ", omml.Descendants(M + "r").Select(r =>
            string.Join(",", r.Element(M + "rPr")?.Elements().Select(p => p.Name.LocalName + "=" + (string?)p.Attribute(M + "val")) ?? new[] { "-" })
            + ":" + r.Element(M + "t")?.Value));

        private static string MathML(string body) => "<math xmlns=\"http://www.w3.org/1998/Math/MathML\">" + body + "</math>";

        [Test]
        [Arguments("𝒢", "<m:oMath><m:r><m:rPr><m:scr m:val=\"script\" /></m:rPr><m:t>G</m:t></m:r></m:oMath>")]
        [Arguments("𝑥+𝒢", "<m:oMath><m:r><m:t>x+</m:t></m:r><m:r><m:rPr><m:scr m:val=\"script\" /></m:rPr><m:t>G</m:t></m:r></m:oMath>")]
        [Arguments("x𝒢", "<m:oMath><m:r><m:t>x</m:t></m:r><m:r><m:rPr><m:scr m:val=\"script\" /></m:rPr><m:t>G</m:t></m:r></m:oMath>")]
        [Arguments("𝟏", "<m:oMath><m:r><m:rPr><m:sty m:val=\"b\" /></m:rPr><m:t>1</m:t></m:r></m:oMath>")]
        public async Task WritesAlphanumericsAsPlainLetters(string unicodeMath, string omml)
        {
            await Assert.That(Compact(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath).ToOmml())).IsEqualTo(omml);
        }

        [Test]
        public async Task WritesTheGroupOperationOfTheCorpus()
        {
            var omml = Equation.Parse("+ : 𝒢 × 𝒢 → 𝒢", EquationFormat.UnicodeMath).ToOmml();
            await Assert.That(Runs(omml)).IsEqualTo("-:+: scr=script:G -:× scr=script:G -:→ scr=script:G");
        }

        [Test]
        public async Task WritesTheIndicatorFunctionsOfTheCorpus()
        {
            var omml = Equation.Parse("K_c (r) = 𝟏_[¼,¾] (r) + ½ × 𝟏_[0,¼] (r)", EquationFormat.UnicodeMath).ToOmml();
            await Assert.That(Runs(omml)).IsEqualTo("-:K -:c -:r -:= sty=b:1 -:1 -:4 -:, -:3 -:4 -:r -:+ -:1 -:2 -:× sty=b:1 -:0, -:1 -:4 -:r");
            await Assert.That(omml.Descendants(M + "sSub").Select(s => string.Concat(s.Element(M + "e")!.Descendants(M + "t").Select(t => t.Value))))
                .IsEquivalentTo(new[] { "K", "1", "1" });
        }

        [Test]
        public async Task WritesTheDerivativesOfTheCorpus()
        {
            var omml = Equation.Parse("ⅅ_𝑥 𝑓(𝑥)≠𝜕_𝑥𝑦 𝑓(𝑥,𝑦)", EquationFormat.UnicodeMath).ToOmml();
            await Assert.That(Runs(omml)).IsEqualTo("-:D -:x -:f -:x -:≠ -:∂ -:xy -:f -:x,y");
            await Assert.That(omml.Descendants(M + "t").Select(t => t.Value).Where(t => t.Any(char.IsSurrogate))).IsEmpty();
        }

        /// <summary>
        /// Counting code points: 🍕 is one character in mml2omml.xsl, an italic identifier written without m:rPr, in
        /// one run with the characters around it.
        /// </summary>
        [Test]
        [Arguments("🍕", "<m:oMath><m:r><m:t>🍕</m:t></m:r></m:oMath>")]
        [Arguments("𝑥+🍕", "<m:oMath><m:r><m:t>x+🍕</m:t></m:r></m:oMath>")]
        public async Task WritesEmojiItalicCountingCodePoints(string unicodeMath, string omml)
        {
            await Assert.That(Compact(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath).ToOmml())).IsEqualTo(omml);
        }

        /// <summary>
        /// Counting code points: each 🍕 of the corpus example is written as an italic identifier, without m:rPr.
        /// </summary>
        [Test]
        public async Task WritesThePizzaOfTheCorpusCountingCodePoints()
        {
            var omml = Equation.Parse("(\"pizza\"^🍕)^🍕", EquationFormat.UnicodeMath).ToOmml();
            await Assert.That(Runs(omml)).IsEqualTo("nor=:pizza -:🍕 -:🍕");
            await Assert.That(Compact(omml)).IsEqualTo("<m:oMath><m:sSup><m:e><m:d><m:dPr /><m:e><m:sSup>"
                + "<m:e><m:r><m:rPr><m:nor /></m:rPr><m:t>pizza</m:t></m:r></m:e>"
                + "<m:sup><m:r><m:t>🍕</m:t></m:r></m:sup>"
                + "</m:sSup></m:e></m:d></m:e>"
                + "<m:sup><m:r><m:t>🍕</m:t></m:r></m:sup></m:sSup></m:oMath>");
        }

        [Test]
        [Arguments("<m:r><m:t>𝑥+𝒢</m:t></m:r>", "<mi>x</mi><mo>+</mo><mi>𝒢</mi>")]
        [Arguments("<m:r><m:t>🍕</m:t></m:r>", "<mi>🍕</mi>")]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>🍕</m:t></m:r>", "<mi mathvariant=\"normal\">🍕</mi>")]
        [Arguments("<m:sSup><m:e><m:r><m:t>𝒢</m:t></m:r></m:e><m:sup><m:r><m:t>🍕</m:t></m:r></m:sup></m:sSup>", "<msup><mi>𝒢</mi><mi>🍕</mi></msup>")]
        [Arguments("<m:r><m:t>𝟏+𝜕𝑥</m:t></m:r>", "<mi>𝟏</mi><mo>+</mo><mi>𝜕</mi><mi>x</mi>")]
        [Arguments("<m:sSub><m:e><m:r><m:t>𝜕</m:t></m:r></m:e><m:sub><m:r><m:t>𝑥𝑦</m:t></m:r></m:sub></m:sSub><m:r><m:t>𝑓</m:t></m:r>",
            "<msub><mi>𝜕</mi><mrow><mi>x</mi><mi>y</mi></mrow></msub><mi>f</mi>")]
        public async Task ReadsAstralCharacters(string runs, string mathML)
        {
            var equation = Equation.Parse("<m:oMath" + Namespaces + ">" + runs + "</m:oMath>", EquationFormat.Omml);
            await Assert.That(equation.ToMathML().ToString(SaveOptions.DisableFormatting)).IsEqualTo(MathML(mathML));
            await Assert.That(equation.ToMathML().Value.Any(c => c >= '' && c <= '')).IsFalse().Because("no private use characters");
        }

        [Test]
        [Arguments("𝒢")]
        [Arguments("𝑥+𝒢")]
        [Arguments("x𝒢")]
        [Arguments("+ : 𝒢 × 𝒢 → 𝒢")]
        public async Task RoundTripsAlphanumericsThroughOmml(string unicodeMath)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back).IsEqualTo(equation);
            await Assert.That(back.ToString(EquationFormat.UnicodeMath)).IsEqualTo(equation.ToString(EquationFormat.UnicodeMath));
            await Assert.That(back.ToString(EquationFormat.UnicodeMath)).Contains("𝒢");
        }

        /// <summary>
        /// Counting code points: the italic 🍕 that mml2omml.xsl writes reads back as it was, so the first trip through
        /// OMML gives the same equation.
        /// </summary>
        [Test]
        [Arguments("🍕", "<mi>🍕</mi>")]
        [Arguments("𝑥+🍕", "<mi>x</mi><mo>+</mo><mi>🍕</mi>")]
        [Arguments("(\"pizza\"^🍕)^🍕", "<msup><mrow><mo>(</mo><msup><mtext>pizza</mtext><mi>🍕</mi></msup><mo>)</mo></mrow><mi>🍕</mi></msup>")]
        public async Task RoundTripsEmojiThroughOmmlCountingCodePoints(string unicodeMath, string mathML)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back.ToMathML().ToString(SaveOptions.DisableFormatting)).IsEqualTo(MathML(mathML));
            await Assert.That(back).IsEqualTo(equation);
            await Assert.That(back.ToString(EquationFormat.UnicodeMath)).IsEqualTo(equation.ToString(EquationFormat.UnicodeMath));
        }

        [Test]
        [Arguments("<m:r><m:t>𝑥+𝒢</m:t></m:r>")]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>🍕</m:t></m:r>")]
        [Arguments("<m:r><m:rPr><m:scr m:val=\"script\"/></m:rPr><m:t>G</m:t></m:r><m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>🍕</m:t></m:r>")]
        public async Task OmmlRoundTripsWithAstralCharacters(string runs)
        {
            var equation = Equation.Parse("<m:oMath" + Namespaces + ">" + runs + "</m:oMath>", EquationFormat.Omml);
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back).IsEqualTo(equation);
            await Assert.That(back.ToOmml().ToString()).IsEqualTo(equation.ToOmml().ToString());
        }

        /// <summary>
        /// The style of a digit, which omml2mml.xsl does not write on numbers, is kept: a styled digit is its
        /// mathematical alphanumeric, an identifier of its own, as UnicodeMath and LaTeX read it.
        /// </summary>
        [Test]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"b\"/></m:rPr><m:t>1</m:t></m:r>", "<mi>𝟏</mi>")]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"b\"/></m:rPr><m:t>12</m:t></m:r>", "<mi>𝟏</mi><mi>𝟐</mi>")]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"b\"/></m:rPr><m:t>1+x</m:t></m:r>", "<mi>𝟏</mi><mo>+</mo><mi>𝐱</mi>")]
        [Arguments("<m:r><m:rPr><m:scr m:val=\"double-struck\"/></m:rPr><m:t>1</m:t></m:r>", "<mi>𝟙</mi>")]
        [Arguments("<m:r><m:rPr><m:scr m:val=\"sans-serif\"/><m:sty m:val=\"p\"/></m:rPr><m:t>1</m:t></m:r>", "<mi>𝟣</mi>")]
        [Arguments("<m:r><m:rPr><m:scr m:val=\"sans-serif\"/><m:sty m:val=\"b\"/></m:rPr><m:t>1</m:t></m:r>", "<mi>𝟭</mi>")]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>12</m:t></m:r>", "<mn>12</mn>")]
        [Arguments("<m:r><m:rPr><m:sty m:val=\"bi\"/></m:rPr><m:t>1</m:t></m:r>", "<mn>1</mn>")]
        [Arguments("<m:r><m:rPr><m:nor/><m:sty m:val=\"b\"/></m:rPr><m:t>12</m:t></m:r>", "<mtext>12</mtext>")]
        public async Task ReadsStyledDigits(string runs, string mathML)
        {
            var equation = Equation.Parse("<m:oMath" + Namespaces + ">" + runs + "</m:oMath>", EquationFormat.Omml);
            await Assert.That(equation.ToMathML().ToString(SaveOptions.DisableFormatting)).IsEqualTo(MathML(mathML));
            await Assert.That(Equation.Parse(equation.ToOmml(), EquationFormat.Omml)).IsEqualTo(equation);
        }

        /// <summary>
        /// A monospace digit is read, but not written back: mml2omml.xsl writes no m:scr for monospace.
        /// </summary>
        [Test]
        public async Task ReadsMonospaceDigits()
        {
            var equation = Equation.Parse("<m:oMath" + Namespaces + "><m:r><m:rPr><m:scr m:val=\"monospace\"/></m:rPr><m:t>1</m:t></m:r></m:oMath>", EquationFormat.Omml);
            await Assert.That(equation.ToMathML().ToString(SaveOptions.DisableFormatting)).IsEqualTo(MathML("<mi>𝟷</mi>"));
        }

        [Test]
        [Arguments("𝟏", EquationFormat.UnicodeMath)]
        [Arguments("𝟏𝟐+2", EquationFormat.UnicodeMath)]
        [Arguments("𝟙", EquationFormat.UnicodeMath)]
        [Arguments("𝟣", EquationFormat.UnicodeMath)]
        [Arguments("𝟭", EquationFormat.UnicodeMath)]
        [Arguments(@"\mathbf{12}+\mathbb{1}", EquationFormat.LaTeX)]
        public async Task RoundTripsStyledDigitsThroughOmml(string source, EquationFormat format)
        {
            var equation = Equation.Parse(source, format);
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back).IsEqualTo(equation);
            await Assert.That(back.ToString(format)).IsEqualTo(equation.ToString(format));
        }

        [Test]
        public async Task IndicatorFunctionsOfTheCorpusKeepTheirBoldOne()
        {
            var equation = Equation.Parse("K_c (r) = 𝟏_[¼,¾] (r) + ½ × 𝟏_[0,¼] (r)", EquationFormat.UnicodeMath);
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back.ToMathML().Descendants(MathMLNamespaces.MathML + "msub").Select(s => s.Elements().First().ToString(SaveOptions.DisableFormatting)))
                .IsEquivalentTo(new[] { "<mi xmlns=\"http://www.w3.org/1998/Math/MathML\">K</mi>", "<mi xmlns=\"http://www.w3.org/1998/Math/MathML\">𝟏</mi>", "<mi xmlns=\"http://www.w3.org/1998/Math/MathML\">𝟏</mi>" });
        }

        /// <summary>
        /// Counting code points: an italic 🍕 of OMML (a run without m:sty) is written back as it was read.
        /// </summary>
        [Test]
        public async Task ItalicEmojiOfOmmlComesBackItalicCountingCodePoints()
        {
            var equation = Equation.Parse("<m:oMath" + Namespaces + "><m:r><m:t>🍕</m:t></m:r></m:oMath>", EquationFormat.Omml);
            await Assert.That(Runs(equation.ToOmml())).IsEqualTo("-:🍕");
            var back = Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
            await Assert.That(back).IsEqualTo(equation);
            await Assert.That(back.ToMathML().ToString(SaveOptions.DisableFormatting)).IsEqualTo(MathML("<mi>🍕</mi>"));
        }
    }
}
