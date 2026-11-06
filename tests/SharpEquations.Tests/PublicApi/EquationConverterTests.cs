#nullable enable

namespace SharpEquations.Tests.PublicApi
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>The one-call conversions of <see cref="EquationConverter"/>.</summary>
    public class EquationConverterTests
    {
        [Test]
        [Arguments(@"\frac{a}{b}", EquationFormat.UnicodeMath, "𝑎/𝑏")]
        [Arguments(@"\sqrt[3]{x}", EquationFormat.UnicodeMath, "√(3&𝑥)")]
        [Arguments(@"\binom{n}{k}", EquationFormat.UnicodeMath, "𝑛⒞𝑘")]
        [Arguments(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}", EquationFormat.UnicodeMath, "⒨(𝑎&𝑏@𝑐&𝑑)")]
        [Arguments(@"\begin{aligned}a&=b\\c&=d\end{aligned}", EquationFormat.UnicodeMath, "█(𝑎&=𝑏@𝑐&=𝑑)")]
        [Arguments(@"\begin{cases}1&x>0\\0&\text{otherwise}\end{cases}", EquationFormat.UnicodeMath, "Ⓒ(1&𝑥>0@0&\"otherwise\")")]
        [Arguments(@"\left(\frac12\right)", EquationFormat.LaTeX, @"\left(\frac{1}{2}\right)")]
        [Arguments(@"\binom{n}{k}", EquationFormat.LaTeX, @"\binom{n}{k}")]
        [Arguments(@"\alpha\beta\Gamma\infty\le\ne\approx\cdot\times\to", EquationFormat.LaTeX, @"\alpha\beta\Gamma\infty\le\ne\approx\cdot\times\to")]
        [Arguments(@"\def\R{\mathbb{R}} x\in\R", EquationFormat.LaTeX, @"x\in\mathbb{R}")]
        [Arguments(@"\newcommand{\pd}[2]{\frac{\partial #1}{\partial #2}}\pd{f}{x}", EquationFormat.LaTeX, @"\frac{\partial f}{\partial x}")]
        [Arguments(@"\underset{x\to0}{\lim}", EquationFormat.LaTeX, @"\lim_{x\to0}")]
        [Arguments(@"\overset{n}{x+y}", EquationFormat.LaTeX, @"\overset{n}{x+y}")]
        [Arguments(@"\overset{n}{x+y}", EquationFormat.UnicodeMath, "(𝑥+𝑦)┴𝑛")]
        [Arguments(@"\overset{def}{=}", EquationFormat.LaTeX, @"\overset{def}{=}")]
        [Arguments(@"\stackrel{?}{=}", EquationFormat.LaTeX, @"\overset{?}{=}")]
        [Arguments(@"\color{red}{x}+y", EquationFormat.LaTeX, "x+y")]
        [Arguments(@"\begin{Vmatrix}1\end{Vmatrix}", EquationFormat.LaTeX, @"\begin{Vmatrix}1\end{Vmatrix}")]
        [Arguments(@"\begin{Vmatrix}1\end{Vmatrix}", EquationFormat.UnicodeMath, "⒩(1)")]
        public async Task ConvertsLaTeX(string latex, EquationFormat destination, string expected)
        {
            await Assert.That(EquationConverter.Convert(latex, EquationFormat.LaTeX, destination)).IsEqualTo(expected);
        }

        [Test]
        [Arguments("a/b", @"\frac{a}{b}")]
        [Arguments("x^2+1", "x^2+1")]
        [Arguments("𝑥²+1", "x^2+1")]
        [Arguments("√(3&x)", @"\sqrt[3]{x}")]
        [Arguments("n⒞k", @"\binom{n}{k}")]
        [Arguments("α+β", @"\alpha+\beta")]
        [Arguments(@"\alpha+\beta", @"\alpha+\beta")]
        [Arguments("∑_(i=1)^n i", @"\sum_{i=1}^ni")]
        public async Task ConvertsUnicodeMathToLaTeX(string unicodeMath, string expected)
        {
            await Assert.That(EquationConverter.Convert(unicodeMath, EquationFormat.UnicodeMath, EquationFormat.LaTeX)).IsEqualTo(expected);
        }

        [Test]
        public async Task ConvertsXml()
        {
            var math = XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msup><mi>x</mi><mn>2</mn></msup></math>");
            await Assert.That(EquationConverter.Convert(math, EquationFormat.MathML, EquationFormat.LaTeX)).IsEqualTo("x^2");
            var omml = XElement.Parse(EquationConverter.Convert(math, EquationFormat.MathML, EquationFormat.Omml));
            await Assert.That(EquationConverter.Convert(omml, EquationFormat.Omml, EquationFormat.LaTeX)).IsEqualTo("x^2");
        }

        [Test]
        public async Task UsesTheOptionsForReadingAndWriting()
        {
            var options = new EquationOptions { Display = true, LaTeXFlavor = LaTeXFlavor.Word };
            await Assert.That(EquationConverter.Convert(@"\frac{a}{b}", EquationFormat.LaTeX, EquationFormat.LaTeX, options)).IsEqualTo(@"\frac{𝑎}{𝑏}");
            await Assert.That(EquationConverter.Convert("x", EquationFormat.LaTeX, EquationFormat.MathML, options)).Contains("display=\"block\"");
            await Assert.That(EquationConverter.Convert("x", EquationFormat.LaTeX, EquationFormat.Omml, options)).StartsWith("<m:oMathPara");
        }

        [Test]
        public async Task TryConvertReturnsTheResult()
        {
            await Assert.That(EquationConverter.TryConvert("a/b", EquationFormat.UnicodeMath, EquationFormat.LaTeX, out var result)).IsTrue();
            await Assert.That(result).IsEqualTo(@"\frac{a}{b}");
            await Assert.That(EquationConverter.TryConvert("<math>", EquationFormat.MathML, EquationFormat.LaTeX, out result)).IsFalse();
            await Assert.That(result).IsNull();
            await Assert.That(EquationConverter.TryConvert(null, EquationFormat.LaTeX, EquationFormat.MathML, out result)).IsFalse();
            await Assert.That(result).IsNull();
            await Assert.That(EquationConverter.TryConvert(@"\foo", EquationFormat.LaTeX, EquationFormat.MathML, out _, new EquationOptions { Strict = true })).IsFalse();
        }

        [Test]
        public async Task TryConvertThrowsForFormatsItCannotWrite()
        {
            await Assert.That(() => EquationConverter.TryConvert("x", EquationFormat.LaTeX, EquationFormat.Mtef, out _)).ThrowsExactly<NotSupportedException>();
            await Assert.That(() => EquationConverter.TryConvert(null, EquationFormat.LaTeX, EquationFormat.Mtef, out _)).ThrowsExactly<NotSupportedException>();
            await Assert.That(() => EquationConverter.TryConvert("x", EquationFormat.LaTeX, (EquationFormat)42, out _)).ThrowsExactly<ArgumentOutOfRangeException>();
            await Assert.That(() => EquationConverter.TryConvert("x", EquationFormat.Mtef, EquationFormat.LaTeX, out _)).ThrowsExactly<ArgumentException>();
        }

        [Test]
        public async Task ConvertThrowsForFormatsItCannotWrite()
        {
            await Assert.That(() => EquationConverter.Convert("x", EquationFormat.LaTeX, EquationFormat.Mtef)).ThrowsExactly<NotSupportedException>();
            await Assert.That(() => EquationConverter.Convert("x", EquationFormat.LaTeX, (EquationFormat)(-3))).ThrowsExactly<ArgumentOutOfRangeException>();
            await Assert.That(() => EquationConverter.Convert("x", (EquationFormat)7, EquationFormat.LaTeX)).ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task ConvertsMtefBytes()
        {
            byte[] mtef = { 3, 1, 1, 3, 0, 1, 2, 0x83, (byte)'x', 0, 0, 0 };
            await Assert.That(EquationConverter.Convert(mtef.AsSpan(), EquationFormat.Mtef, EquationFormat.LaTeX)).IsEqualTo("x");
            await Assert.That(() => EquationConverter.Convert(mtef.AsSpan(), EquationFormat.LaTeX, EquationFormat.MathML)).ThrowsExactly<ArgumentException>();
        }

        [Test]
        [Arguments(EquationFormat.MathML, true, true)]
        [Arguments(EquationFormat.Omml, true, true)]
        [Arguments(EquationFormat.Mtef, true, false)]
        [Arguments(EquationFormat.UnicodeMath, true, true)]
        [Arguments(EquationFormat.LaTeX, true, true)]
        [Arguments((EquationFormat)(-1), false, false)]
        [Arguments((EquationFormat)5, false, false)]
        public async Task TellsWhatItCanReadAndWrite(EquationFormat format, bool read, bool write)
        {
            await Assert.That(EquationConverter.CanRead(format)).IsEqualTo(read);
            await Assert.That(EquationConverter.CanWrite(format)).IsEqualTo(write);
        }

        [Test]
        public async Task RejectsMacroNamesThatAreNotControlWords()
        {
            foreach (var name in new[] { "", "a1", "\\R", "é" })
            {
                var options = new EquationOptions { LaTeXMacros = new Dictionary<string, string> { [name] = "x" } };
                await Assert.That(() => EquationConverter.Convert("x", EquationFormat.LaTeX, EquationFormat.MathML, options)).ThrowsExactly<ArgumentException>().Because(name);
            }
        }

        [Test]
        public async Task OptionsAreValues()
        {
            var options = new EquationOptions { Display = true };
            await Assert.That(options).IsEqualTo(new EquationOptions { Display = true });
            await Assert.That(options).IsNotEqualTo(EquationOptions.Default);
            var strict = options with { Strict = true };
            await Assert.That(strict.Display && strict.Strict).IsTrue();
            await Assert.That(options.Strict).IsFalse();
            await Assert.That(EquationOptions.Default).IsSameReferenceAs(EquationOptions.Default);
            await Assert.That(EquationOptions.Default.Display || EquationOptions.Default.Strict || EquationOptions.Default.PreserveFormatting).IsFalse();
            await Assert.That(EquationOptions.Default.LaTeXFlavor).IsEqualTo(LaTeXFlavor.Standard);
            await Assert.That(EquationOptions.Default.LaTeXMacros).IsNull();
            await Assert.That(typeof(EquationOptions).GetProperties()).All(
                p => p!.SetMethod is not { } setter || setter.ReturnParameter.GetRequiredCustomModifiers().Length == 1).Because("init only");
        }
    }
}
