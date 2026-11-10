#nullable enable

namespace SharpEquations.Tests.Regression
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// An equation that Parse accepts is one every writer writes: sources that were read to an equation which then
    /// threw from ToString, Equals or ToOmml, or that were not read to the equation they are.
    /// </summary>
    public class AcceptedEquationTests
    {
        private static readonly XNamespace M = MathMLNamespaces.Omml;

        private static readonly EquationOptions Word = new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word };

        private static string? WriteAll(Equation equation)
        {
            try
            {
                _ = equation.ToString() + equation.ToString(EquationFormat.UnicodeMath) + equation.ToString(EquationFormat.LaTeX)
                    + equation.ToString(EquationFormat.LaTeX, Word) + equation.ToString(EquationFormat.Omml);
                _ = equation.ToOmml();
                _ = equation.ToMathML();
                _ = equation.GetHashCode();
                return null;
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        [Test]
        public async Task MtefCharWithASurrogateCodeIsDropped()
        {
            var mtef = new byte[] { 5, 1, 0, 6, 9, (byte)'D', 0, 0, 1, 0, 2, 0, 0x81, 0x00, 0xD8, 0, 0 };
            var equation = Equation.Parse(mtef, EquationFormat.Mtef);
            await Assert.That(equation.ToString()).DoesNotContain("�");
            await Assert.That(equation.ToMathML().Value).IsEqualTo(string.Empty);
            await Assert.That(WriteAll(equation)).IsNull();
        }

        [Test]
        [Arguments(0xFF, 0xFF)]
        [Arguments(0xFE, 0xFF)]
        [Arguments(0x00, 0xDC)]
        [Arguments(0xFF, 0xDF)]
        public async Task MtefCharThatIsNoCharacterOfXmlIsDropped(int low, int high)
        {
            var mtef = new byte[] { 5, 1, 0, 6, 9, (byte)'D', 0, 0, 1, 0, 2, 0, 0x81, (byte)low, (byte)high, 0, 0 };
            var equation = Equation.Parse(mtef, EquationFormat.Mtef);
            await Assert.That(equation.ToMathML().Value).IsEqualTo(string.Empty);
        }

        [Test]
        public async Task MtefNestedTooDeeplyIsInvalidData()
        {
            // a line record holding a line record, 600 times
            var mtef = new List<byte> { 5, 1, 0, 6, 9, (byte)'D', 0, 0 };
            for (int i = 0; i < 600; i++)
                mtef.AddRange(new byte[] { 1, 0 });
            mtef.AddRange(Enumerable.Repeat((byte)0, 601));
            await Assert.That(() => Equation.Parse(mtef.ToArray(), EquationFormat.Mtef)).ThrowsExactly<InvalidDataException>();
        }

        public static IEnumerable<string> InvalidXmlText() => new[] { "\u0001", "a\u0000b", "\u000B", "\u001F", "￾", "￿", "\uD800", "\uDC00", "x\uDC00\uD800", "\uD835" };

        [Test]
        [MethodDataSource(nameof(InvalidXmlText))]
        public async Task RejectsMathMLElementWithTextXmlDoesNotAllow(string text)
        {
            await Assert.That(() => Equation.Parse(new XElement("math", new XElement("mi", text)), EquationFormat.MathML)).ThrowsExactly<FormatException>();
            await Assert.That(() => Equation.Parse(new XElement("math", new XElement("mi", new XCData(text))), EquationFormat.MathML)).ThrowsExactly<FormatException>();
            await Assert.That(() => Equation.Parse(new XElement("math", new XElement("mi", new XAttribute("mathvariant", text), "x")), EquationFormat.MathML)).ThrowsExactly<FormatException>();
            await Assert.That(() => Equation.Parse(new XElement("math", new XAttribute("alttext", text)), EquationFormat.MathML)).ThrowsExactly<FormatException>();
            await Assert.That(() => Equation.Parse(new XElement("math", new XProcessingInstruction("pi", text)), EquationFormat.MathML)).ThrowsExactly<FormatException>();
        }

        [Test]
        [MethodDataSource(nameof(InvalidXmlText))]
        public async Task RejectsOmmlElementWithTextXmlDoesNotAllow(string text)
        {
            XElement Run(object content) => new XElement(M + "oMath", new XElement(M + "r", new XElement(M + "t", content)));
            await Assert.That(() => Equation.Parse(Run(text), EquationFormat.Omml)).ThrowsExactly<FormatException>();
            await Assert.That(() => Equation.Parse(new XElement(M + "oMathPara", Run(text)), EquationFormat.Omml)).ThrowsExactly<FormatException>();
            var accent = new XElement(M + "oMath", new XElement(M + "acc", new XElement(M + "accPr", new XElement(M + "chr", new XAttribute(M + "val", text))), new XElement(M + "e")));
            await Assert.That(() => Equation.Parse(accent, EquationFormat.Omml)).ThrowsExactly<FormatException>();
        }

        [Test]
        public async Task AcceptsElementsWithTheCharactersXmlAllows()
        {
            var equation = Equation.Parse(new XElement("math", new XElement("mtext", "a\tb\n�"), new XElement("mi", "\U0001D465")), EquationFormat.MathML);
            await Assert.That(WriteAll(equation)).IsNull();
            await Assert.That(equation).IsEqualTo(Equation.Parse(equation.ToString(), EquationFormat.MathML));
            var omml = Equation.Parse(new XElement(M + "oMath", new XElement(M + "r", new XElement(M + "t", "\U0001D465+1"))), EquationFormat.Omml);
            await Assert.That(WriteAll(omml)).IsNull();
        }

        private static readonly string[] Structures =
        {
            "msub", "msup", "msubsup", "munder", "mover", "munderover", "mfrac", "mroot", "msqrt", "mmultiscripts", "mtable", "mtr",
            "mlabeledtr", "mtd", "menclose", "mrow", "mstyle", "mpadded", "mphantom", "merror",
        };

        private static readonly string[] Fillers =
        {
            "<mi>a</mi>", "<mo>∑</mo>", "<mo>∫</mo>", "<mn>2</mn>", "<none/>", "<mprescripts/>", "<mrow/>", "<mtr/>", "<mtd/>", "<mtext>t</mtext>",
            "<mo>|</mo>", "<mo>(</mo>", "<mo>&#x2061;</mo>", "<mo>&#x332;</mo>", "<mo>^</mo>", "<mi>sin</mi>", "<mtable/>", "<msup/>",
            "<msub><mo>∑</mo></msub>", "<munderover/>", "<mfrac/>", "<mfrac linethickness=\"0\"/>", "<mroot><mi>a</mi></mroot>", "<mmultiscripts/>",
        };

        /// <summary>The structures empty, with one to three children of each kind, and after or before what gives them a meaning.</summary>
        public static IEnumerable<string> UnderFilled()
        {
            foreach (var name in Structures)
            {
                var forms = new List<string> { "<" + name + "/>" };
                foreach (var filler in Fillers)
                {
                    for (int count = 1; count <= 3; count++)
                        forms.Add("<" + name + ">" + string.Concat(Enumerable.Repeat(filler, count)) + "</" + name + ">");
                    forms.Add("<" + name + ">" + filler + "<mi>b</mi></" + name + ">");
                    forms.Add("<" + name + "><mi>b</mi>" + filler + "</" + name + ">");
                }
                foreach (var form in forms)
                {
                    yield return form;
                    yield return form + "<none/>";
                    yield return form + "<mrow><mi>x</mi><mi>y</mi></mrow>";
                    yield return "<mrow>" + form + "<mrow><mi>x</mi><mi>y</mi></mrow></mrow>";
                    yield return "<mi>f</mi><mo>&#x2061;</mo>" + form;
                    yield return "<mrow><mo>(</mo>" + form + "<mo>)</mo></mrow>";
                    yield return "<mrow><mo>{</mo>" + form + "<mo>&#x200B;</mo></mrow>";
                    yield return "<msub>" + form + "<mn>1</mn></msub>";
                    yield return "<mover accent=\"true\">" + form + "<mo>^</mo></mover>";
                    yield return "<mfrac><mn>1</mn>" + form + "</mfrac>";
                    yield return "<mtable><mtr><mtd>" + form + "</mtd></mtr></mtable>";
                    yield return "<mtable columnspacing=\"0pt\">" + form + "</mtable>";
                    yield return "<msqrt>" + form + "</msqrt>";
                }
            }
        }

        [Test]
        public async Task WritesStructuresWithTooFewChildren()
        {
            var failures = new List<string>();
            int accepted = 0;
            foreach (var content in UnderFilled())
            {
                var source = "<math>" + content + "</math>";
                Equation equation;
                try
                {
                    equation = Equation.Parse(source, EquationFormat.MathML);
                }
                catch (FormatException)
                {
                    continue;
                }
                accepted++;
                if (WriteAll(equation) is { } failure)
                    failures.Add(source + "\n  " + failure);
            }
            await Assert.That(failures).IsEmpty();
            await Assert.That(accepted).IsGreaterThan(1000);
        }

        [Test]
        public async Task WritesAScriptWithoutChildrenBeforeAnElement()
        {
            var equation = Equation.Parse("<math><msup/><none/></math>", EquationFormat.MathML);
            await Assert.That(WriteAll(equation)).IsNull();
        }

        [Test]
        public async Task DisplayInlineIsTheDefault()
        {
            var plain = Equation.Parse("<math><mi>x</mi></math>", EquationFormat.MathML);
            var inline = Equation.Parse("<math display=\"inline\"><mi>x</mi></math>", EquationFormat.MathML);
            await Assert.That(inline).IsEqualTo(plain);
            await Assert.That(inline.ToString()).IsEqualTo(plain.ToString());
            await Assert.That(inline.IsDisplay).IsFalse();
            await Assert.That(inline.ToMathML().Attribute("display")).IsNull();
            var element = Equation.Parse(new XElement("math", new XAttribute("display", "inline"), new XElement("mi", "x")), EquationFormat.MathML);
            await Assert.That(element).IsEqualTo(plain);
            await Assert.That(Equation.Parse("<math display=\"block\"><mi>x</mi></math>", EquationFormat.MathML).IsDisplay).IsTrue();
        }

        [Test]
        [Arguments(" 3")]
        [Arguments("3")]
        [Arguments("0")]
        [Arguments("+1")]
        [Arguments("-1")]
        [Arguments("Omml,MathML")]
        [Arguments("Omml, MathML")]
        [Arguments(" LaTeX")]
        [Arguments("LaTeX ")]
        [Arguments("\tOmml")]
        [Arguments("Mtef")]
        [Arguments("mtef")]
        [Arguments("m")]
        [Arguments("l")]
        [Arguments("X")]
        [Arguments(" ")]
        public async Task FormatStringIsANameOfAFormatAndNothingElse(string format)
        {
            var equation = Equation.Parse("x", EquationFormat.UnicodeMath);
            await Assert.That(() => equation.ToString(format, null)).ThrowsExactly<FormatException>();
            await Assert.That(() => string.Format("{0:" + format + "}", equation)).ThrowsExactly<FormatException>();
        }

        [Test]
        public async Task FormatStringNamesAFormatIgnoringCase()
        {
            var equation = Equation.Parse(@"\frac{a}{b}", EquationFormat.LaTeX);
            await Assert.That(equation.ToString("latex", null)).IsEqualTo(equation.ToString(EquationFormat.LaTeX));
            await Assert.That(equation.ToString("LATEX", null)).IsEqualTo(equation.ToString(EquationFormat.LaTeX));
            await Assert.That(equation.ToString("unicodemath", null)).IsEqualTo(equation.ToString(EquationFormat.UnicodeMath));
            await Assert.That(equation.ToString("OMML", null)).IsEqualTo(equation.ToString(EquationFormat.Omml));
            await Assert.That(equation.ToString("mathml", null)).IsEqualTo(equation.ToString());
            await Assert.That(equation.ToString("W", null)).IsEqualTo(equation.ToString(EquationFormat.LaTeX, Word));
            await Assert.That(equation.ToString(null, null)).IsEqualTo(equation.ToString());
        }

        [Test]
        public async Task ParagraphWithoutAnEquationIsAnEmptyDisplayEquation()
        {
            var expected = Equation.Parse("<math display=\"block\"/>", EquationFormat.MathML);
            var empty = Equation.Parse("<m:oMathPara/>", EquationFormat.Omml);
            await Assert.That(empty.ToString()).IsEqualTo("<math display=\"block\" xmlns=\"http://www.w3.org/1998/Math/MathML\" />");
            await Assert.That(empty).IsEqualTo(expected);
            await Assert.That(empty.IsDisplay).IsTrue();
            await Assert.That(WriteAll(empty)).IsNull();
            var withProperties = Equation.Parse(new XElement(M + "oMathPara", new XElement(M + "oMathParaPr", new XElement(M + "jc", new XAttribute(M + "val", "center")))), EquationFormat.Omml);
            await Assert.That(withProperties).IsEqualTo(expected);
            await Assert.That(Equation.Parse(empty.ToOmml(), EquationFormat.Omml)).IsEqualTo(expected);
        }
    }
}
