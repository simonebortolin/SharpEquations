#nullable enable

namespace SharpEquations.Tests.MathML
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>Content MathML read as the presentation MathML it is written as (ctop.xsl).</summary>
    public class ContentMathMLTests
    {
        private const string Namespace = "http://www.w3.org/1998/Math/MathML";
        private static readonly XNamespace Mml = Namespace;
        private static readonly EquationOptions Strict = new EquationOptions { Strict = true };

        /// <summary>The elements of presentation MathML an equation may hold once read.</summary>
        private static readonly HashSet<string> Presentation = new HashSet<string>(StringComparer.Ordinal)
        {
            "math", "mi", "mn", "mo", "mtext", "mspace", "ms", "mglyph", "mrow", "mfrac", "msqrt", "mroot", "mstyle",
            "merror", "mpadded", "mphantom", "menclose", "msub", "msup", "msubsup", "munder", "mover",
            "munderover", "mmultiscripts", "mprescripts", "none", "mtable", "mtr", "mlabeledtr", "mtd", "maligngroup",
            "malignmark", "mstack", "mlongdiv", "msgroup", "msrow", "mscarries", "mscarry", "msline", "maction",
            "semantics", "annotation", "annotation-xml",
        };

        public static IEnumerable<string> CtopFiles() =>
            Directory.GetFiles(TestData.Path("contentmathml"), "*.mml").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal)!;

        private static Equation ReadFile(string file) =>
            Equation.Parse(File.ReadAllText(TestData.Path("contentmathml", file)), EquationFormat.MathML, Strict);

        private static string Read(string mathML) =>
            Equation.Parse(mathML, EquationFormat.MathML, Strict).ToString().Replace(" xmlns=\"" + Namespace + "\"", "");

        private static string LaTeX(string mathML) => Equation.Parse(mathML, EquationFormat.MathML, Strict).ToString(EquationFormat.LaTeX);

        [Test]
        [Arguments("<math><apply><plus/><ci>x</ci><cn>1</cn></apply></math>", "<math><mi>x</mi><mo>+</mo><mn>1</mn></math>")]
        [Arguments("<math><apply><power/><ci>x</ci><cn>2</cn></apply></math>", "<math><msup><mi>x</mi><mn>2</mn></msup></math>")]
        [Arguments("<math><apply><root/><degree><cn>3</cn></degree><ci>x</ci></apply></math>", "<math><mroot><mi>x</mi><mn>3</mn></mroot></math>")]
        [Arguments("<math><apply><abs/><ci>x</ci></apply></math>", "<math><mrow><mo>|</mo><mi>x</mi><mo>|</mo></mrow></math>")]
        [Arguments("<math><apply><factorial/><ci>n</ci></apply></math>", "<math><mi>n</mi><mo>!</mo></math>")]
        [Arguments("<math><apply><in/><ci>x</ci><reals/></apply></math>", "<math><mi>x</mi><mo>∈</mo><mi>ℝ</mi></math>")]
        [Arguments("<math><apply><ci>f</ci><ci>x</ci></apply></math>", "<math><mi>f</mi><mo>⁡</mo><mrow><mo>(</mo><mi>x</mi><mo>)</mo></mrow></math>")]
        [Arguments("<math><apply><minus/><ci>a</ci><apply><minus/><ci>b</ci><ci>c</ci></apply></apply></math>",
            "<math><mi>a</mi><mo>−</mo><mrow><mo>(</mo><mi>b</mi><mo>−</mo><mi>c</mi><mo>)</mo></mrow></math>")]
        [Arguments("<math><apply><times/><apply><plus/><ci>a</ci><ci>b</ci></apply><ci>c</ci></apply></math>",
            "<math><mrow><mo>(</mo><mi>a</mi><mo>+</mo><mi>b</mi><mo>)</mo></mrow><mo>⁢</mo><mi>c</mi></math>")]
        [Arguments("<math><apply><log/><logbase><cn>2</cn></logbase><ci>x</ci></apply></math>",
            "<math><msub><mi>log</mi><mn>2</mn></msub><mo>⁡</mo><mi>x</mi></math>")]
        public async Task ReadsContentMathMLAsPresentation(string content, string expected)
        {
            await Assert.That(Read(content)).IsEqualTo(expected);
        }

        [Test]
        [Arguments("<math><apply><divide/><ci>a</ci><ci>b</ci></apply></math>", "a/b")]
        [Arguments("<math><apply><sin/><ci>x</ci></apply></math>", @"\sin x")]
        [Arguments("<math><apply><int/><bvar><ci>x</ci></bvar><lowlimit><cn>0</cn></lowlimit><uplimit><infinity/></uplimit><apply><exp/><apply><minus/><ci>x</ci></apply></apply></apply></math>",
            @"\int_0^\infty e^{-x}dx")]
        [Arguments("<math><apply><sum/><bvar><ci>i</ci></bvar><lowlimit><cn>1</cn></lowlimit><uplimit><ci>n</ci></uplimit><ci>i</ci></apply></math>", @"\sum\limits_{i=1}^ni")]
        [Arguments("<math><apply><diff/><bvar><ci>x</ci></bvar><apply><sin/><ci>x</ci></apply></apply></math>", @"\frac{d \sin x}{dx}")]
        [Arguments("<math><apply><partialdiff/><bvar><ci>x</ci></bvar><bvar><ci>y</ci></bvar><apply><ci>f</ci><ci>x</ci><ci>y</ci></apply></apply></math>",
            @"\frac{\partial^2f(x,y)}{\partial x\partial y}")]
        [Arguments("<math><apply><partialdiff/><bvar><ci>x</ci><degree><cn>2</cn></degree></bvar><ci>f</ci></apply></math>", @"\frac{\partial^2f}{\partial x^2}")]
        [Arguments("<math><apply><limit/><bvar><ci>x</ci></bvar><condition><apply><tendsto/><ci>x</ci><cn>0</cn></apply></condition><ci>x</ci></apply></math>",
            @"\lim_{x\to0}x")]
        [Arguments("<math><matrix><matrixrow><cn>1</cn><cn>0</cn></matrixrow><matrixrow><cn>0</cn><cn>1</cn></matrixrow></matrix></math>",
            @"\begin{pmatrix}1&0\\0&1\end{pmatrix}")]
        [Arguments("<math><set><ci>a</ci><ci>b</ci></set></math>", @"\{a,b\}")]
        [Arguments("<math><interval closure=\"open-closed\"><cn>0</cn><cn>1</cn></interval></math>", "(0,1]")]
        [Arguments("<math><apply><and/><ci>p</ci><apply><not/><ci>q</ci></apply></apply></math>", @"p\wedge\neg q")]
        [Arguments("<math><apply><csymbol cd=\"arith1\">plus</csymbol><ci>x</ci><ci>y</ci></apply></math>", "x+y")]
        public async Task WritesContentMathMLInOtherFormats(string content, string latex)
        {
            await Assert.That(LaTeX(content)).IsEqualTo(latex);
        }

        [Test]
        public async Task ReadsTheSameEquationAsItsPresentation()
        {
            var content = Equation.Parse("<math><apply><eq/><apply><power/><ci>x</ci><cn>2</cn></apply><cn>4</cn></apply></math>", EquationFormat.MathML);
            var presentation = Equation.Parse("<math><msup><mi>x</mi><mn>2</mn></msup><mo>=</mo><mn>4</mn></math>", EquationFormat.MathML);
            await Assert.That(content).IsEqualTo(presentation);
        }

        [Test]
        public async Task KeepsThePresentationMathMLAroundIt()
        {
            await Assert.That(Read("<math><mi>f</mi><mo>=</mo><apply><plus/><ci>x</ci><cn>1</cn></apply></math>"))
                .IsEqualTo("<math><mi>f</mi><mo>=</mo><mrow><mi>x</mi><mo>+</mo><mn>1</mn></mrow></math>");
            await Assert.That(Read("<math><apply><plus/><ci>x</ci><ci><msub><mi>y</mi><mn>1</mn></msub></ci></apply></math>"))
                .IsEqualTo("<math><mi>x</mi><mo>+</mo><msub><mi>y</mi><mn>1</mn></msub></math>");
        }

        [Test]
        public async Task TakesThePresentationAnnotationOfSemantics()
        {
            await Assert.That(Read("<math><semantics><apply><plus/><ci>x</ci><cn>1</cn></apply><annotation-xml encoding=\"MathML-Presentation\"><mi>y</mi></annotation-xml></semantics></math>"))
                .IsEqualTo("<math><mi>y</mi></math>");
        }

        [Test]
        public async Task LeavesContentAnnotationsAlone()
        {
            const string annotated = "<math><semantics><mi>x</mi><annotation-xml encoding=\"MathML-Content\"><ci>x</ci></annotation-xml></semantics></math>";
            await Assert.That(Read(annotated)).IsEqualTo(annotated);
        }

        [Test]
        public async Task IgnoresWhiteSpaceBetweenElementsAndAroundTokens()
        {
            const string indented = "<m:math xmlns:m=\"http://www.w3.org/1998/Math/MathML\">\n  <m:apply>\n    <m:plus/>\n    <m:ci> x </m:ci>\n    <m:cn>\n      1\n    </m:cn>\n  </m:apply>\n</m:math>";
            await Assert.That(Equation.Parse(indented, EquationFormat.MathML)).IsEqualTo(Equation.Parse("<math><mi>x</mi><mo>+</mo><mn>1</mn></math>", EquationFormat.MathML));
        }

        [Test]
        public async Task ReadsContentMathMLGivenAsXml()
        {
            var math = new XElement(Mml + "math", new XElement(Mml + "apply", new XElement(Mml + "times"), new XElement(Mml + "cn", "2"), new XElement(Mml + "ci", "x")));
            await Assert.That(Equation.Parse(math, EquationFormat.MathML, Strict).ToString(EquationFormat.LaTeX)).IsEqualTo("2x");
        }

        [Test]
        public async Task RejectsWhatIsNeitherContentNorPresentationWhenStrict()
        {
            await Assert.That(() => Equation.Parse("<math><apply><plus/><ci>x</ci><foo/></apply></math>", EquationFormat.MathML, Strict))
                .Throws<FormatException>().WithMessageContaining("foo");
        }

        [Test]
        public async Task RejectsContentMathMLNestedTooDeeply()
        {
            var deep = string.Concat(Enumerable.Repeat("<apply><minus/>", 600)) + "<ci>x</ci>" + string.Concat(Enumerable.Repeat("</apply>", 600));
            await Assert.That(() => Equation.Parse("<math>" + deep + "</math>", EquationFormat.MathML)).ThrowsExactly<FormatException>();
        }

        [Test]
        [MethodDataSource(nameof(CtopFiles))]
        public async Task ReadsTheEquationsOfCtopAsPresentationMathML(string file)
        {
            var math = ReadFile(file).ToMathML();
            var others = math.DescendantsAndSelf().Where(e => e.Name.Namespace != Mml || !Presentation.Contains(e.Name.LocalName)).Select(e => e.Name.LocalName).ToList();
            await Assert.That(others).IsEmpty();
        }

        [Test]
        [MethodDataSource(nameof(CtopFiles))]
        public async Task WritesTheEquationsOfCtopAsMathMLThatReadsBack(string file)
        {
            var equation = ReadFile(file);
            await Assert.That(Equation.Parse(equation.ToString(), EquationFormat.MathML, Strict)).IsEqualTo(equation);
        }

        [Test]
        [MethodDataSource(nameof(CtopFiles))]
        public async Task WritesTheEquationsOfCtopAsLaTeXAndOmml(string file)
        {
            var equation = ReadFile(file);
            await Assert.That(() => equation.ToString(EquationFormat.LaTeX)).ThrowsNothing();
            await Assert.That(() => equation.ToOmml()).ThrowsNothing();
        }
    }
}
