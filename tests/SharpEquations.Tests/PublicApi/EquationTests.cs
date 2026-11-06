#nullable enable

namespace SharpEquations.Tests.PublicApi
{
    using System.Threading.Tasks;
    using TUnit.Assertions.Enums;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>The <see cref="Equation"/> type: reading, writing, equality and immutability.</summary>
    public class EquationTests
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;
        private static readonly XNamespace M = MathMLNamespaces.Omml;

        private const string Fraction = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mfrac><mi>a</mi><mi>b</mi></mfrac></math>";

        [Test]
        public async Task ParsesEveryTextFormatToTheSameEquation()
        {
            var fromMathML = Equation.Parse(Fraction, EquationFormat.MathML);
            var fromUnicodeMath = Equation.Parse("a/b", EquationFormat.UnicodeMath);
            var fromLaTeX = Equation.Parse(@"\frac{a}{b}", EquationFormat.LaTeX);
            var fromOmml = Equation.Parse("<m:oMath><m:f><m:num><m:r><m:t>a</m:t></m:r></m:num><m:den><m:r><m:t>b</m:t></m:r></m:den></m:f></m:oMath>", EquationFormat.Omml);
            foreach (var equation in new[] { fromMathML, fromUnicodeMath, fromLaTeX, fromOmml })
            {
                var fraction = equation.ToMathML().Descendants(Mml + "mfrac").Single();
                await Assert.That(fraction.Elements().Select(e => e.Value.Normalize(System.Text.NormalizationForm.FormKC))).IsEquivalentTo(new[] { "a", "b" }, CollectionOrdering.Matching).Because(equation.ToString());
            }
            await Assert.That(fromUnicodeMath).IsEqualTo(fromLaTeX);
        }

        [Test]
        public async Task ParsesXmlElementsWithoutChangingThem()
        {
            var source = XElement.Parse("<math><mi mathcolor=\"red\">x</mi></math>");
            var before = source.ToString();
            var equation = Equation.Parse(source, EquationFormat.MathML);
            await Assert.That(source.ToString()).IsEqualTo(before);
            await Assert.That(equation.ToMathML().Name).IsEqualTo(Mml + "math");
            await Assert.That(equation.ToMathML().Element(Mml + "mi")?.Attribute("mathcolor")).IsNull();
        }

        [Test]
        public async Task ParsesOmmlElements()
        {
            var omml = new XElement(M + "oMath", new XElement(M + "r", new XElement(M + "t", "x")));
            var equation = Equation.Parse(omml, EquationFormat.Omml);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo("𝑥");
            await Assert.That(omml.Elements().Count()).IsEqualTo(1);
        }

        [Test]
        public async Task ParsesOmmlParagraphsAsDisplayEquations()
        {
            var equation = Equation.Parse("<m:oMathPara><m:oMath><m:r><m:t>x</m:t></m:r></m:oMath></m:oMathPara>", EquationFormat.Omml);
            await Assert.That(equation.IsDisplay).IsTrue();
            await Assert.That(equation.ToOmml().Name).IsEqualTo(M + "oMathPara");
            await Assert.That(equation.WithDisplay(false).ToOmml().Name).IsEqualTo(M + "oMath");
        }

        [Test]
        public async Task ParsesMtefBytes()
        {
            // MTEF 3: header, a line with the character 'x' (typeface 3, variable), end of line, end of equation
            byte[] mtef = { 3, 1, 1, 3, 0, 1, 2, 0x83, (byte)'x', 0, 0, 0 };
            var equation = Equation.Parse(mtef.AsSpan(), EquationFormat.Mtef);
            await Assert.That(equation.ToMathML().Descendants(Mml + "mi").Single().Value).IsEqualTo("x");
            await Assert.That(EquationConverter.Convert(mtef, EquationFormat.Mtef, EquationFormat.UnicodeMath)).IsEqualTo(equation.ToString(EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task RejectsNullSources()
        {
            await Assert.That(() => Equation.Parse((string)null!, EquationFormat.LaTeX)).ThrowsExactly<ArgumentNullException>();
            await Assert.That(() => Equation.Parse((XElement)null!, EquationFormat.MathML)).ThrowsExactly<ArgumentNullException>();
            await Assert.That(() => EquationConverter.Convert((string)null!, EquationFormat.LaTeX, EquationFormat.MathML)).ThrowsExactly<ArgumentNullException>();
        }

        [Test]
        [Arguments(EquationFormat.MathML)]
        [Arguments(EquationFormat.Omml)]
        [Arguments(EquationFormat.UnicodeMath)]
        [Arguments(EquationFormat.LaTeX)]
        public async Task RejectsBytesOfTextFormats(EquationFormat format)
        {
            var exception = await Assert.That(() => Equation.Parse(new byte[] { 1, 2 }.AsSpan(), format)).ThrowsExactly<ArgumentException>();
            await Assert.That(exception!.ParamName).IsEqualTo("format");
        }

        [Test]
        public async Task RejectsTextForMtef()
        {
            await Assert.That(() => Equation.Parse("x", EquationFormat.Mtef)).ThrowsExactly<ArgumentException>();
            await Assert.That(() => Equation.TryParse("x", EquationFormat.Mtef, out _)).ThrowsExactly<ArgumentException>();
        }

        [Test]
        [Arguments(EquationFormat.UnicodeMath)]
        [Arguments(EquationFormat.LaTeX)]
        [Arguments(EquationFormat.Mtef)]
        public async Task RejectsXmlForTextFormats(EquationFormat format)
        {
            await Assert.That(() => Equation.Parse(XElement.Parse(Fraction), format)).ThrowsExactly<ArgumentException>();
        }

        [Test]
        [Arguments((EquationFormat)(-1))]
        [Arguments((EquationFormat)5)]
        [Arguments((EquationFormat)100)]
        public async Task RejectsUndefinedFormats(EquationFormat format)
        {
            await Assert.That(() => Equation.Parse("x", format)).ThrowsExactly<ArgumentOutOfRangeException>();
            await Assert.That(() => Equation.Parse(XElement.Parse(Fraction), format)).ThrowsExactly<ArgumentOutOfRangeException>();
            await Assert.That(() => Equation.Parse(new byte[1].AsSpan(), format)).ThrowsExactly<ArgumentOutOfRangeException>();
            await Assert.That(() => Equation.TryParse("x", format, out _)).ThrowsExactly<ArgumentOutOfRangeException>();
            var equation = Equation.Parse("x", EquationFormat.LaTeX);
            await Assert.That(() => equation.ToString(format)).ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        [Arguments(EquationFormat.MathML, "<math><mi>x</mi>")]
        [Arguments(EquationFormat.MathML, "")]
        [Arguments(EquationFormat.MathML, "   ")]
        [Arguments(EquationFormat.MathML, "<!-- only a comment -->")]
        [Arguments(EquationFormat.MathML, "text")]
        [Arguments(EquationFormat.MathML, "<math/><math/>")]
        [Arguments(EquationFormat.MathML, "<mrow><mi>x</mi></mrow>")]
        [Arguments(EquationFormat.MathML, "<m:math xmlns:m=\"urn:other\"/>")]
        [Arguments(EquationFormat.MathML, "<!DOCTYPE math [<!ENTITY x \"y\">]><math>&x;</math>")]
        [Arguments(EquationFormat.Omml, "<m:oMath>")]
        [Arguments(EquationFormat.Omml, "<math/>")]
        [Arguments(EquationFormat.Omml, "<w:p/>")]
        public async Task RejectsMalformedXml(EquationFormat format, string source)
        {
            await Assert.That(() => Equation.Parse(source, format)).ThrowsExactly<FormatException>();
            await Assert.That(Equation.TryParse(source, format, out var equation)).IsFalse();
            await Assert.That(equation).IsNull();
        }

        [Test]
        public async Task TryParseReturnsFalseForNull()
        {
            await Assert.That(Equation.TryParse(null, EquationFormat.LaTeX, out var equation)).IsFalse();
            await Assert.That(equation).IsNull();
        }

        [Test]
        public async Task TryParseReturnsTheEquation()
        {
            await Assert.That(Equation.TryParse(@"\sqrt{x}", EquationFormat.LaTeX, out var equation)).IsTrue();
            await Assert.That(equation!.ToString(EquationFormat.LaTeX)).IsEqualTo(@"\sqrt{x}");
            await Assert.That(Equation.TryParse(@"\sqrt{x}", EquationFormat.LaTeX, new EquationOptions { Display = true }, out equation)).IsTrue();
            await Assert.That(equation!.IsDisplay).IsTrue();
        }

        [Test]
        public async Task TryParseHonoursStrictOptions()
        {
            var strict = new EquationOptions { Strict = true };
            await Assert.That(Equation.TryParse(@"\foo x", EquationFormat.LaTeX, out _)).IsTrue();
            await Assert.That(Equation.TryParse(@"\foo x", EquationFormat.LaTeX, strict, out _)).IsFalse();
            await Assert.That(Equation.TryParse(@"\foo x", EquationFormat.UnicodeMath, strict, out _)).IsFalse();
            await Assert.That(Equation.TryParse("<math><foo/></math>", EquationFormat.MathML, strict, out _)).IsFalse();
            await Assert.That(Equation.TryParse("<math><foo/></math>", EquationFormat.MathML, out _)).IsTrue();
        }

        [Test]
        public async Task HonoursCancellation()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            foreach (var format in new[] { EquationFormat.LaTeX, EquationFormat.UnicodeMath, EquationFormat.MathML })
            {
                await Assert.That(() => Equation.Parse(format == EquationFormat.MathML ? Fraction : "x", format, null, source.Token)).Throws<OperationCanceledException>();
                await Assert.That(() => EquationConverter.Convert("x", EquationFormat.LaTeX, format, null, source.Token)).Throws<OperationCanceledException>();
            }
        }

        [Test]
        public async Task ReadsDisplayFromTheOptionsOrTheDelimiters()
        {
            await Assert.That(Equation.Parse("x", EquationFormat.LaTeX).IsDisplay).IsFalse();
            await Assert.That(Equation.Parse("x", EquationFormat.LaTeX, new EquationOptions { Display = true }).IsDisplay).IsTrue();
            await Assert.That(Equation.Parse("x", EquationFormat.UnicodeMath, new EquationOptions { Display = true }).IsDisplay).IsTrue();
            await Assert.That(Equation.Parse("$$x$$", EquationFormat.LaTeX).IsDisplay).IsTrue();
            await Assert.That(Equation.Parse(@"\[x\]", EquationFormat.LaTeX).IsDisplay).IsTrue();
            await Assert.That(Equation.Parse("$x$", EquationFormat.LaTeX, new EquationOptions { Display = true }).IsDisplay).IsFalse();
            await Assert.That(Equation.Parse(@"\(x\)", EquationFormat.LaTeX, new EquationOptions { Display = true }).IsDisplay).IsFalse();
            await Assert.That(Equation.Parse("<math display=\"block\"><mi>x</mi></math>", EquationFormat.MathML).IsDisplay).IsTrue();
            await Assert.That(Equation.Parse("<math display=\"inline\"><mi>x</mi></math>", EquationFormat.MathML).IsDisplay).IsFalse();
        }

        [Test]
        public async Task WithDisplayReturnsANewEquation()
        {
            var inline = Equation.Parse("x", EquationFormat.LaTeX);
            await Assert.That(inline.WithDisplay(false)).IsSameReferenceAs(inline);
            var display = inline.WithDisplay(true);
            await Assert.That(display).IsNotSameReferenceAs(inline);
            await Assert.That(display.IsDisplay).IsTrue();
            await Assert.That(inline.IsDisplay).IsFalse();
            await Assert.That(display.WithDisplay(true)).IsSameReferenceAs(display);
            await Assert.That(display.WithDisplay(false)).IsEqualTo(inline);
            await Assert.That(display).IsNotEqualTo(inline);
            await Assert.That(display.ToMathML().Attribute("display")?.Value).IsEqualTo("block");
            await Assert.That(display.WithDisplay(false).ToMathML().Attribute("display")).IsNull();
        }

        [Test]
        public async Task SharesNoXmlWithTheCaller()
        {
            var source = XElement.Parse(Fraction);
            var equation = Equation.Parse(source, EquationFormat.MathML);
            source.Add(new XElement(Mml + "mi", "changed"));
            var copy = equation.ToMathML();
            copy.RemoveNodes();
            copy.SetAttributeValue("display", "block");
            await Assert.That(equation.ToMathML().Descendants(Mml + "mfrac").Count()).IsEqualTo(1);
            await Assert.That(equation.ToMathML().Descendants(Mml + "mi").Count()).IsEqualTo(2);
            await Assert.That(equation.IsDisplay).IsFalse();
            await Assert.That(equation.ToMathML()).IsNotSameReferenceAs(equation.ToMathML());
            await Assert.That(equation.ToOmml()).IsNotSameReferenceAs(equation.ToOmml());
        }

        [Test]
        public async Task HasNoSettableMembers()
        {
            await Assert.That(typeof(Equation).IsSealed).IsTrue();
            await Assert.That(typeof(Equation).GetProperties().Where(p => p.SetMethod?.IsPublic == true)).IsEmpty();
            await Assert.That(typeof(Equation).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)).IsEmpty();
            await Assert.That(typeof(Equation).GetConstructors()).IsEmpty();
        }

        [Test]
        public async Task EqualityComparesTheNormalizedMathML()
        {
            var a = Equation.Parse(@"\frac{a}{b}", EquationFormat.LaTeX);
            var b = Equation.Parse("a/b", EquationFormat.UnicodeMath);
            var c = Equation.Parse("a/c", EquationFormat.UnicodeMath);
            await Assert.That(a.Equals(b)).IsTrue();
            await Assert.That(a.Equals((object)b)).IsTrue();
            await Assert.That(a == b).IsTrue();
            await Assert.That(a != b).IsFalse();
            await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
            await Assert.That(a == c).IsFalse();
            await Assert.That(a != c).IsTrue();
            await Assert.That(a.Equals(null)).IsFalse();
            await Assert.That(a!.Equals((object?)"a/b")).IsFalse();
            await Assert.That(a == null!).IsFalse();
            await Assert.That(null! == a).IsFalse();
            await Assert.That((Equation?)null == null).IsTrue();
#pragma warning disable CS1718
            await Assert.That(a == a).IsTrue();
#pragma warning restore CS1718
            await Assert.That(new HashSet<Equation> { a!, b, c }).Count().IsEqualTo(2);
        }

        [Test]
        public async Task FormattingIsDroppedUnlessPreserved()
        {
            const string coloured = "<math><mstyle mathcolor=\"red\"><mi>x</mi></mstyle><mi style=\"font-size:20pt\" mathsize=\"2em\">y</mi></math>";
            var plain = Equation.Parse(coloured, EquationFormat.MathML);
            await Assert.That(plain).IsEqualTo(Equation.Parse("<math><mi>x</mi><mi>y</mi></math>", EquationFormat.MathML));
            var kept = Equation.Parse(coloured, EquationFormat.MathML, new EquationOptions { PreserveFormatting = true });
            await Assert.That(kept.ToMathML().Element(Mml + "mstyle")?.Attribute("mathcolor")?.Value).IsEqualTo("red");
            await Assert.That(kept).IsNotEqualTo(plain);
        }

        [Test]
        public async Task ToStringWritesEveryWritableFormat()
        {
            var equation = Equation.Parse(@"\frac{a^2+b^2}{c}", EquationFormat.LaTeX);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo("(𝑎²+𝑏²)/𝑐");
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(@"\frac{a^2+b^2}{c}");
            await Assert.That(equation.ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word })).IsEqualTo(@"\frac{𝑎^2+𝑏^2}{𝑐}");
            await Assert.That(equation.ToString(EquationFormat.MathML)).IsEqualTo(equation.ToString());
            await Assert.That(XElement.Parse(equation.ToString(EquationFormat.Omml)).Name).IsEqualTo(M + "oMath");
            await Assert.That(XElement.Parse(equation.ToString(EquationFormat.MathML)).Name).IsEqualTo(Mml + "math");
            await Assert.That(() => equation.ToString(EquationFormat.Mtef)).ThrowsExactly<NotSupportedException>();
        }

        [Test]
        [Arguments(null, EquationFormat.MathML, LaTeXFlavor.Standard)]
        [Arguments("", EquationFormat.MathML, LaTeXFlavor.Standard)]
        [Arguments("G", EquationFormat.MathML, LaTeXFlavor.Standard)]
        [Arguments("M", EquationFormat.MathML, LaTeXFlavor.Standard)]
        [Arguments("O", EquationFormat.Omml, LaTeXFlavor.Standard)]
        [Arguments("U", EquationFormat.UnicodeMath, LaTeXFlavor.Standard)]
        [Arguments("L", EquationFormat.LaTeX, LaTeXFlavor.Standard)]
        [Arguments("W", EquationFormat.LaTeX, LaTeXFlavor.Word)]
        [Arguments("mathml", EquationFormat.MathML, LaTeXFlavor.Standard)]
        [Arguments("OMML", EquationFormat.Omml, LaTeXFlavor.Standard)]
        [Arguments("UnicodeMath", EquationFormat.UnicodeMath, LaTeXFlavor.Standard)]
        [Arguments("latex", EquationFormat.LaTeX, LaTeXFlavor.Standard)]
        public async Task FormatsWithFormatStrings(string? format, EquationFormat expected, LaTeXFlavor flavor)
        {
            var equation = Equation.Parse(@"\sqrt[3]{x}", EquationFormat.LaTeX);
            var text = equation.ToString(expected, new EquationOptions { LaTeXFlavor = flavor });
            await Assert.That(equation.ToString(format, CultureInfo.InvariantCulture)).IsEqualTo(text);
            await Assert.That(((IFormattable)equation).ToString(format, null)).IsEqualTo(text);
            await Assert.That(string.Format(CultureInfo.InvariantCulture, "{0:" + format + "}", equation)).IsEqualTo(text);
        }

        [Test]
        public async Task FormatsInInterpolatedStrings()
        {
            var equation = Equation.Parse("x^2+1", EquationFormat.LaTeX);
            await Assert.That($"{equation:U}").IsEqualTo("𝑥²+1");
            await Assert.That($"{equation:L}").IsEqualTo("x^2+1");
            await Assert.That($"{equation}").IsEqualTo(equation.ToString());
        }

        [Test]
        [Arguments("X")]
        [Arguments("Mtef")]
        [Arguments("1")]
        [Arguments("3")]
        [Arguments("99")]
        [Arguments("m")]
        [Arguments("LaTeX2")]
        [Arguments("MathML,LaTeX")]
        public async Task RejectsUnknownFormatStrings(string format)
        {
            var equation = Equation.Parse("x", EquationFormat.LaTeX);
            await Assert.That(() => equation.ToString(format, null)).ThrowsExactly<FormatException>();
        }

        [Test]
        public async Task ToStringOfMathMLIsCachedAndStable()
        {
            var equation = Equation.Parse("x", EquationFormat.LaTeX);
            await Assert.That(equation.ToString()).IsSameReferenceAs(equation.ToString());
            await Assert.That(equation.ToString()).StartsWith("<math xmlns=\"http://www.w3.org/1998/Math/MathML\">");
            await Assert.That(equation.ToString()).DoesNotContain("\n");
        }

        [Test]
        public async Task LaTeXMacrosApplyToTheirParseOnly()
        {
            var macros = new Dictionary<string, string> { ["R"] = @"\mathbb{R}" };
            var options = new EquationOptions { LaTeXMacros = macros };
            await Assert.That(Equation.Parse(@"x\in\R", EquationFormat.LaTeX, options).ToString(EquationFormat.LaTeX)).IsEqualTo(@"x\in\mathbb{R}");
            await Assert.That(macros.Keys).IsEquivalentTo(new[] { "R" });
            await Assert.That(Equation.Parse(@"\def\N{\mathbb{N}}\N", EquationFormat.LaTeX, options).ToString(EquationFormat.LaTeX)).IsEqualTo(@"\mathbb{N}");
            await Assert.That(macros.Keys).IsEquivalentTo(new[] { "R" }).Because("a \\def of the LaTeX does not change the options");
            await Assert.That(() => Equation.Parse(@"\N", EquationFormat.LaTeX, options with { Strict = true })).Throws<FormatException>();
        }
    }
}
