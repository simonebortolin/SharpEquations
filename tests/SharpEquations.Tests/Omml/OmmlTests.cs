namespace SharpEquations.Tests
{
    using System.Threading.Tasks;
    using TUnit.Assertions.Enums;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations;
    using SharpEquations.Internal.Omml;

    public class OmmlTests
    {
        private static readonly XNamespace M = MathMLNamespaces.Omml;
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        private static XElement Omml(string body)
        {
            return XElement.Parse("<m:oMath xmlns:m=\"" + M.NamespaceName + "\" xmlns:w=\"" + MathMLNamespaces.WordprocessingML.NamespaceName + "\">" + body + "</m:oMath>");
        }

        private static string R(string text) => "<m:r><m:t>" + text + "</m:t></m:r>";

        /// <summary>
        /// The element structure of a MathML tree, ignoring mrow grouping, as "name" or "name=text" per element.
        /// </summary>
        private static List<string> Shape(XElement math)
        {
            return math.DescendantsAndSelf()
                .Where(e => e.Name.LocalName != "mrow" && e.Name.LocalName != "maligngroup")
                .Select(e => e.HasElements ? e.Name.LocalName : e.Name.LocalName + "=" + e.Value)
                .ToList();
        }

        [Test]
        public async Task Fraction()
        {
            var math = OmmlParser.Parse(Omml("<m:f><m:num>" + R("a+1") + "</m:num><m:den>" + R("2") + "</m:den></m:f>"));
            await Assert.That(math.Name).IsEqualTo(Mml + "math");
            await Assert.That(math.Attribute("display")).IsNull();
            await Assert.That(Shape(math)).IsEquivalentTo(new[] { "math", "mfrac", "mi=a", "mo=+", "mn=1", "mn=2" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task CharactersOutsideTheBasicPlane()
        {
            var math = OmmlParser.Parse(Omml("<m:sSup><m:e>" + R("𝑥\uE000") + "</m:e><m:sup>" + R("2") + "</m:sup></m:sSup>" + R("+𝔸😀")));
            await Assert.That(math.Value).Contains("𝑥");
            await Assert.That(math.Value).Contains("\uE000");
            await Assert.That(math.Value).Contains("𝔸😀");
        }

        [Test]
        public async Task ParenthesesAreDelimiters()
        {
            var math = XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msup><mrow><mo>(</mo><mi>a</mi><mo>+</mo><mi>b</mi><mo>)</mo></mrow><mi>n</mi></msup>"
                + "<mo>+</mo><mrow><mo>(</mo><mi>a</mi><mo>)</mo><mo>+</mo><mo>(</mo><mi>b</mi><mo>)</mo></mrow>"
                + "<mrow><mo>⟨</mo><mi>ψ</mi><mo>|</mo><mi>H</mi><mo>|</mo><mi>ψ</mi><mo>⟩</mo></mrow></math>");
            var omml = OmmlSerializer.Serialize(math);
            var delimiters = omml.Descendants(M + "d").ToList();
            await Assert.That(delimiters.Count).IsEqualTo(2);
            await Assert.That(delimiters[0].Element(M + "dPr")?.Element(M + "sepChr")).IsNull();
            await Assert.That(string.Concat(delimiters[0].Element(M + "e").Descendants(M + "t").Select(t => t.Value))).IsEqualTo("a+b");
            await Assert.That(delimiters[1].Elements(M + "e").Count()).IsEqualTo(3);
            // "|" is the default separator of m:d
            await Assert.That((string)delimiters[1].Element(M + "dPr")?.Element(M + "sepChr")?.Attribute(M + "val") ?? "|").IsEqualTo("|");
        }

        [Test]
        public async Task HorizontalBracesAreGroupCharacters()
        {
            var math = XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><munder><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mo stretchy=\"true\">⏟</mo></munder></math>");
            var omml = OmmlSerializer.Serialize(math);
            await Assert.That(omml.Descendants(M + "groupChr").SingleOrDefault()).IsNotNull();
        }

        [Test]
        public async Task RadicalWithHiddenDegreeIsSquareRoot()
        {
            foreach (var hide in new[] { "<m:degHide/>", "<m:degHide m:val=\"1\"/>", "<m:degHide m:val=\"on\"/>", "<m:degHide m:val=\"true\"/>" })
            {
                var math = OmmlParser.Parse(Omml("<m:rad><m:radPr>" + hide + "</m:radPr><m:deg/><m:e>" + R("x") + "</m:e></m:rad>"));
                await Assert.That(Shape(math)).IsEquivalentTo(new[] { "math", "msqrt", "mi=x" }, CollectionOrdering.Matching).Because(hide);
            }
            var root = OmmlParser.Parse(Omml("<m:rad><m:radPr><m:degHide m:val=\"0\"/></m:radPr><m:deg>" + R("3") + "</m:deg><m:e>" + R("x") + "</m:e></m:rad>"));
            await Assert.That(Shape(root)).IsEquivalentTo(new[] { "math", "mroot", "mi=x", "mn=3" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task NaryWithLimits()
        {
            var math = OmmlParser.Parse(Omml("<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:limLoc m:val=\"undOvr\"/></m:naryPr>"
                + "<m:sub>" + R("i=1") + "</m:sub><m:sup>" + R("n") + "</m:sup><m:e>" + R("i") + "</m:e></m:nary>"));
            await Assert.That(Shape(math)).IsEquivalentTo(new[] { "math", "munderover", "mo=∑", "mi=i", "mo==", "mn=1", "mi=n", "mi=i" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task AccentUsesSpacingCharacter()
        {
            var math = OmmlParser.Parse(Omml("<m:acc><m:accPr><m:chr m:val=\"̇\"/></m:accPr><m:e>" + R("a") + "</m:e></m:acc>"));
            await Assert.That(Shape(math)).IsEquivalentTo(new[] { "math", "mover", "mi=a", "mo=˙" }, CollectionOrdering.Matching);
            await Assert.That((string)math.Descendants(Mml + "mover").Single().Attribute("accent")).IsEqualTo("true");

            var omml = OmmlSerializer.Serialize(math);
            await Assert.That(omml.Name).IsEqualTo(M + "oMath");
            await Assert.That(omml.Descendants(M + "acc").SingleOrDefault()).IsNotNull();
        }

        [Test]
        public async Task GroupCharacterDefault()
        {
            var math = OmmlParser.Parse(Omml("<m:groupChr><m:e>" + R("d") + "</m:e></m:groupChr>"));
            await Assert.That(math.Descendants(Mml + "mo").Single().Value).IsEqualTo("⏟");
        }

        [Test]
        public async Task DisplayEquation()
        {
            var para = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath>" + R("x") + "</m:oMath></m:oMathPara>");
            var math = OmmlParser.Parse(para);
            await Assert.That((string)math.Attribute("display")).IsEqualTo("block");
            await Assert.That((string)OmmlParser.Parse(para.Element(M + "oMath")).Attribute("display")).IsEqualTo("block");

            var back = OmmlSerializer.Serialize(math);
            await Assert.That(back.Name).IsEqualTo(M + "oMathPara");
            await Assert.That(back.Descendants(M + "t").Single().Value).IsEqualTo("x");
        }

        [Test]
        public async Task DisplayEquationWithSeveralLines()
        {
            var para = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath>" + R("a") + "</m:oMath><m:oMath>" + R("b") + "</m:oMath></m:oMathPara>");
            var math = OmmlParser.Parse(para);
            await Assert.That((string)math.Attribute("display")).IsEqualTo("block");
            var rows = math.Element(Mml + "mtable").Elements(Mml + "mtr").ToList();
            await Assert.That(rows.Select(r => r.Value)).IsEquivalentTo(new[] { "a", "b" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task FromMathML()
        {
            var math = XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msup><mi>x</mi><mn>2</mn></msup><mo>+</mo>"
                + "<mfrac><mn>1</mn><msqrt><mi>y</mi></msqrt></mfrac></math>");
            var omml = OmmlSerializer.Serialize(math);
            await Assert.That(omml.Name).IsEqualTo(M + "oMath");
            await Assert.That(omml.GetPrefixOfNamespace(M)).IsEqualTo("m");
            await Assert.That(omml.Element(M + "sSup")).IsNotNull();
            await Assert.That(omml.Element(M + "f")).IsNotNull();
            await Assert.That(omml.Descendants(M + "rad").SingleOrDefault()).IsNotNull();
            await Assert.That(omml.DescendantsAndSelf().Attributes(M + "val").Any(a => a.Value.Length == 0)).IsFalse();
        }

        [Test]
        public async Task NaryOperandWithoutMrow()
        {
            var math = XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><munderover><mo>∑</mo><mrow><mi>i</mi><mo>=</mo><mn>1</mn></mrow>"
                + "<mi>n</mi></munderover><msup><mi>i</mi><mn>2</mn></msup></math>");
            var nary = OmmlSerializer.Serialize(math).Element(M + "nary");
            await Assert.That(nary).IsNotNull();
            await Assert.That(nary.Element(M + "e").Element(M + "sSup")).IsNotNull();
        }

        [Test]
        public async Task MathHasOneRow()
        {
            var math = OmmlParser.Parse(Omml(R("x") + "<m:sSup><m:e>" + R("y") + "</m:e><m:sup>" + R("2") + "</m:sup></m:sSup>"));
            await Assert.That(math.Elements().Count()).IsEqualTo(1);
            await Assert.That(math.Elements().Single().Name).IsEqualTo(Mml + "mrow");
        }

        [Test]
        public async Task FromMathMLWithoutNamespaceAndWithAnnotation()
        {
            var math = XElement.Parse("<math display=\"block\"><semantics><mrow><mi>a</mi><mo>=</mo><mn>1</mn></mrow>"
                + "<annotation encoding=\"StarMath 5.0\">a = 1</annotation></semantics></math>");
            var omml = OmmlSerializer.Serialize(math);
            await Assert.That(omml.Name).IsEqualTo(M + "oMathPara");
            await Assert.That(string.Concat(omml.Descendants(M + "t").Select(t => t.Value))).IsEqualTo("a=1");
        }

        /// <summary>
        /// Word numbers equations with "#" and the number ("x=2#(1)"); they become MathML labeled rows,
        /// and the "#" comes back converting to OMML.
        /// </summary>
        [Test]
        public async Task NumberedEquations()
        {
            var para = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath><m:eqArr>"
                + "<m:e>" + R("x=2") + "<m:r><m:t xml:space=\"preserve\"> #</m:t></m:r><m:d><m:e>" + R("1") + "</m:e></m:d></m:e>"
                + "<m:e>" + R("y=3") + "</m:e></m:eqArr></m:oMath></m:oMathPara>");
            var math = OmmlParser.Parse(para);
            var rows = math.Element(Mml + "mtable").Elements().ToList();
            await Assert.That(rows.Select(r => r.Name.LocalName)).IsEquivalentTo(new[] { "mlabeledtr", "mtr" }, CollectionOrdering.Matching);
            var cells = rows[0].Elements(Mml + "mtd").ToList();
            await Assert.That(cells[0].Value).IsEqualTo("1");
            await Assert.That(cells[1].Value).IsEqualTo("x=2");
            await Assert.That(math.Descendants(Mml + "mo").Any(o => o.Value == "#")).IsFalse();

            var back = OmmlSerializer.Serialize(math);
            var lines = back.Descendants(M + "eqArr").Single().Elements(M + "e").ToList();
            await Assert.That(lines.Count).IsEqualTo(2);
            await Assert.That(string.Concat(lines[0].Descendants(M + "t").Select(t => t.Value))).IsEqualTo("x=2#1");
            await Assert.That(lines[0].Element(M + "d")).IsNotNull();

            var single = OmmlParser.Parse(XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath>"
                + R("E=mc") + R("#") + "<m:d><m:e>" + R("3") + "</m:e></m:d></m:oMath></m:oMathPara>"));
            await Assert.That(single.Descendants(Mml + "mlabeledtr").Single().Elements(Mml + "mtd").First().Value).IsEqualTo("3");
        }

        /// <summary>
        /// A "#" not followed by a number, or inside a matrix, is kept.
        /// </summary>
        [Test]
        public async Task HashWithoutNumber()
        {
            var math = OmmlParser.Parse(Omml(R("a#")));
            await Assert.That(math.Descendants(Mml + "mlabeledtr")).IsEmpty();
            await Assert.That(math.Descendants(Mml + "mo").Last().Value).IsEqualTo("#");
        }

        /// <summary>
        /// An integral without limits is a plain operator in MathML; it has to become an m:nary again, with
        /// the limits hidden, or Word shows it as a small character.
        /// </summary>
        [Test]
        public async Task NaryWithoutLimitsStaysNary()
        {
            var math = OmmlParser.Parse(Omml("<m:nary><m:naryPr><m:subHide m:val=\"1\"/><m:supHide m:val=\"1\"/></m:naryPr><m:sub/><m:sup/><m:e>" + R("x") + "</m:e></m:nary>"));
            await Assert.That(Shape(math)).IsEquivalentTo(new[] { "math", "mo=∫", "mi=x" }, CollectionOrdering.Matching);

            var nary = OmmlSerializer.Serialize(math).Descendants(M + "nary").Single();
            await Assert.That((string)nary.Element(M + "naryPr").Element(M + "subHide").Attribute(M + "val")).IsEqualTo("on");
            await Assert.That((string)nary.Element(M + "naryPr").Element(M + "supHide").Attribute(M + "val")).IsEqualTo("on");
            await Assert.That(nary.Element(M + "e").Value).IsEqualTo("x");
        }

        [Test]
        public async Task SetOperatorsBetweenOperandsAreNotNary()
        {
            var math = XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>A</mi><mo>∪</mo><mrow><mi>B</mi></mrow></math>");
            await Assert.That(OmmlSerializer.Serialize(math).Descendants(M + "nary")).IsEmpty();
        }

        [Test]
        public async Task TextKeepsItsSpaces()
        {
            const string nor = "<m:r><m:rPr><m:nor/></m:rPr><m:t xml:space=\"preserve\">{0}</m:t></m:r>";
            var math = OmmlParser.Parse(Omml(string.Format(nor, "sia ") + R("x") + string.Format(nor, " e  poi ")));
            await Assert.That(math.Descendants(Mml + "mtext").Select(t => t.Value)).IsEquivalentTo(new[] { "sia\u00A0", "\u00A0e\u00A0\u00A0poi\u00A0" }, CollectionOrdering.Matching);

            var back = OmmlSerializer.Serialize(math);
            await Assert.That(back.Descendants(M + "t").Select(t => t.Value)).IsEquivalentTo(new[] { "sia\u00A0", "x", "\u00A0e\u00A0\u00A0poi\u00A0" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task NumbersSplitInRunsAreOneNumber()
        {
            var math = OmmlParser.Parse(Omml(R("200") + R("00") + R("+0") + R(".6") + R(",1")
                + "<m:sSub><m:e>" + R("1") + "</m:e><m:sub>" + R("2") + "</m:sub></m:sSub>"));
            await Assert.That(Shape(math)).IsEquivalentTo(new[] { "math", "mn=20000", "mo=+", "mn=0.6", "mo=,", "mn=1", "msub", "mn=1", "mn=2" }, CollectionOrdering.Matching);
        }

        /// <summary>
        /// Lines of a display equation aligned in Word (m:aln, "align at this character") keep their
        /// alignment points; lines without them are an equation array all the same.
        /// </summary>
        [Test]
        public async Task DisplayEquationWithAlignedLines()
        {
            var para = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\" xmlns:w=\"" + MathMLNamespaces.WordprocessingML.NamespaceName + "\"><m:oMath>"
                + R("a") + "<m:r><m:rPr><m:aln/></m:rPr><m:t>=b</m:t></m:r><m:r><w:br/></m:r></m:oMath>"
                + "<m:oMath><m:r><m:rPr><m:aln/></m:rPr><m:t>=c</m:t></m:r></m:oMath></m:oMathPara>");
            var math = OmmlParser.Parse(para);
            await Assert.That(math.Descendants(Mml + "mtr").Select(r => r.Value)).IsEquivalentTo(new[] { "a=b", "=c" }, CollectionOrdering.Matching);
            await Assert.That(math.Descendants(Mml + "maligngroup")).IsNotEmpty();

            // back to lines of their own, each aligned at the same run as before
            var back = OmmlSerializer.Serialize(math);
            await Assert.That(back.Name).IsEqualTo(M + "oMathPara");
            await Assert.That(back.Descendants(M + "eqArr")).IsEmpty();
            var lines = back.Elements(M + "oMath").ToList();
            await Assert.That(lines.Select(l => l.Value)).IsEquivalentTo(new[] { "a=b", "=c" }, CollectionOrdering.Matching);
            await Assert.That(lines.Select(l => l.Descendants(M + "aln").Single().Parent.Parent.Element(M + "t").Value)).IsEquivalentTo(new[] { "=b", "=c" }, CollectionOrdering.Matching);

            var plain = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath>" + R("a") + "</m:oMath><m:oMath>" + R("b") + "</m:oMath></m:oMathPara>");
            var plainBack = OmmlSerializer.Serialize(OmmlParser.Parse(plain));
            await Assert.That(plainBack.Elements(M + "oMath").Select(l => l.Value)).IsEquivalentTo(new[] { "a", "b" }, CollectionOrdering.Matching);
        }

        /// <summary>
        /// A genuine equation array (m:eqArr) stays one: only lines aligned with m:aln become lines again.
        /// </summary>
        [Test]
        public async Task EquationArrayIsNotSplitIntoLines()
        {
            var para = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath><m:eqArr><m:e>" + R("a&amp;=b")
                + "</m:e><m:e>" + R("&amp;=c") + "</m:e></m:eqArr></m:oMath></m:oMathPara>");
            var back = OmmlSerializer.Serialize(OmmlParser.Parse(para));
            await Assert.That(back.Elements(M + "oMath").Count()).IsEqualTo(1);
            await Assert.That(back.Descendants(M + "eqArr").Single().Elements(M + "e").Count()).IsEqualTo(2);
        }

        /// <summary>
        /// A brace without its closing one (a system, a function by cases) or a delimiter without separator:
        /// an empty character means none, and must not come back as the default parenthesis or bar.
        /// </summary>
        [Test]
        public async Task EmptyDelimitersStayEmpty()
        {
            var omml = Omml("<m:d><m:dPr><m:begChr m:val=\"{\"/><m:endChr m:val=\"\"/></m:dPr><m:e>" + R("a") + "</m:e></m:d>"
                + "<m:d><m:dPr><m:begChr m:val=\"\"/><m:endChr m:val=\"}\"/></m:dPr><m:e>" + R("b") + "</m:e></m:d>"
                + "<m:d><m:dPr><m:sepChr m:val=\"\"/></m:dPr><m:e>" + R("c") + "</m:e><m:e>" + R("d") + "</m:e></m:d>");
            var math = OmmlParser.Parse(omml);
            var back = OmmlSerializer.Serialize(math);
            var properties = back.Descendants(M + "dPr").ToList();
            await Assert.That((string)properties[0].Element(M + "endChr").Attribute(M + "val")).IsEqualTo("");
            await Assert.That((string)properties[1].Element(M + "begChr").Attribute(M + "val")).IsEqualTo("");
            await Assert.That((string)properties[2].Element(M + "sepChr").Attribute(M + "val")).IsEqualTo("");
            await Assert.That(XNode.DeepEquals(math, OmmlParser.Parse(back))).IsTrue();
        }

        /// <summary>
        /// The alignment points of a system in braces belong to the system: the row holding it got them as "&amp;" text.
        /// </summary>
        [Test]
        public async Task AlignmentOfANestedSystemStaysInTheSystem()
        {
            var para = XElement.Parse("<m:oMathPara xmlns:m=\"" + M.NamespaceName + "\"><m:oMath><m:eqArr><m:e>" + R("x") + "</m:e><m:e>"
                + R("W=") + "<m:d><m:dPr><m:begChr m:val=\"{\"/><m:endChr m:val=\"\"/></m:dPr><m:e><m:eqArr><m:e>" + R("a=0") + "</m:e><m:e>"
                + R("⋮") + "</m:e><m:e>" + R("b=0") + "</m:e></m:eqArr></m:e></m:d></m:e></m:eqArr></m:oMath></m:oMathPara>");
            var back = OmmlSerializer.Serialize(OmmlParser.Parse(para));
            await Assert.That(back.Descendants(M + "t").Any(t => t.Value.Contains("&"))).IsFalse().Because(back.ToString());
            await Assert.That(back.Descendants(M + "eqArr").Count()).IsEqualTo(2);
        }

        /// <summary>
        /// A matrix without column alignment gets no m:mcJc: its empty value was read back as "left".
        /// </summary>
        [Test]
        public async Task MatrixColumnsStayCentered()
        {
            var math = OmmlParser.Parse(Omml("<m:d><m:e><m:m><m:mr><m:e>" + R("1") + "</m:e><m:e>" + R("2") + "</m:e></m:mr></m:m></m:e></m:d>"));
            var back = OmmlSerializer.Serialize(math);
            await Assert.That(back.Descendants(M + "mcJc")).IsEmpty();
            await Assert.That(OmmlParser.Parse(back).Descendants(Mml + "mtable").Single().Attribute("columnalign")).IsNull();
        }

        [Test]
        public async Task MatrixWithOneColumnStaysMatrix()
        {
            var math = OmmlParser.Parse(Omml("<m:d><m:e><m:m><m:mr><m:e>" + R("1") + "</m:e></m:mr><m:mr><m:e>" + R("x") + "</m:e></m:mr></m:m></m:e></m:d>"));
            var back = OmmlSerializer.Serialize(math);
            await Assert.That(back.Descendants(M + "eqArr")).IsEmpty();
            await Assert.That(back.Descendants(M + "m").Single().Elements(M + "mr").Count()).IsEqualTo(2);
        }

        [Test]
        public async Task EquationArrayInDelimitersStaysEquationArray()
        {
            var math = OmmlParser.Parse(Omml("<m:d><m:dPr><m:begChr m:val=\"{\"/><m:endChr m:val=\"\"/></m:dPr><m:e><m:eqArr><m:e>" + R("a") + "</m:e><m:e>" + R("b") + "</m:e></m:eqArr></m:e></m:d>"));
            await Assert.That(OmmlSerializer.Serialize(math).Descendants(M + "eqArr").SingleOrDefault()).IsNotNull();
        }

        [Test]
        public async Task MathRunPropertiesComeFirst()
        {
            var omml = OmmlSerializer.Serialize(XElement.Parse("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mtext>t</mtext></math>"));
            await Assert.That(omml.Descendants(M + "r").Single().Elements().First().Name).IsEqualTo(M + "rPr");
        }

        [Test]
        public async Task RejectsDeeplyNestedEquations()
        {
            var inner = new XElement(Mml + "mi", "x");
            for (var i = 0; i < 2000; i++)
                inner = new XElement(Mml + "mrow", inner);
            await Assert.That(() => OmmlSerializer.Serialize(new XElement(Mml + "math", inner))).ThrowsExactly<ArgumentException>();
        }

        [Test]
        public async Task RejectsOtherElements()
        {
            await Assert.That(() => OmmlParser.Parse(new XElement("p"))).ThrowsExactly<ArgumentException>();
            await Assert.That(() => OmmlSerializer.Serialize(new XElement(Mml + "mrow"))).ThrowsExactly<ArgumentException>();
        }

        /// <summary>
        /// Equations written by Word, from the test suite of https://github.com/scienceai/omml2mathml (Apache-2.0):
        /// each converts to MathML, and converting that to OMML and back gives the same MathML.
        /// </summary>
        [Test]
        public async Task WordEquationsRoundTrip()
        {
            var dir = TestData.Path("omml");
            var files = Directory.GetFiles(dir, "*.omml");
            await Assert.That(files.Length).IsGreaterThan(100);
            foreach (var file in files)
            {
                var math = OmmlParser.Parse(XElement.Load(file));
                await Assert.That(math.Name).IsEqualTo(Mml + "math").Because(file);
                await Assert.That(math.HasElements).IsTrue().Because(file);
                var back = OmmlParser.Parse(OmmlSerializer.Serialize(math));
                await Assert.That(Shape(back)).IsEquivalentTo(Shape(math), CollectionOrdering.Matching).Because(Path.GetFileName(file));
            }
        }

        /// <summary>
        /// Converting to MathML and back keeps the OMML structures: an integral or a matrix does not become plain text.
        /// </summary>
        [Test]
        public async Task WordEquationsKeepTheirStructure()
        {
            foreach (var file in Directory.GetFiles(TestData.Path("omml"), "*.omml"))
            {
                var omml = XElement.Load(file);
                var back = OmmlSerializer.Serialize(OmmlParser.Parse(omml));
                foreach (var kind in new[] { "nary", "f", "rad", "sSub", "sSup", "sSubSup", "m" })
                    await Assert.That(back.Descendants(M + kind).Count()).IsEqualTo(omml.Descendants(M + kind).Count()).Because(kind + " in " + Path.GetFileName(file));
            }
        }
    }
}
