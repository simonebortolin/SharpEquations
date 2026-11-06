#nullable enable

namespace SharpEquations.Tests.UnicodeMath
{
    using System.Threading.Tasks;
    using System;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// The bit masks of UnicodeMath: the options of n-ary operators (∑4: limits on the other side), abstract boxes
    /// (□), enclosures (▭: hidden borders and strikes) and phantoms (⟡).
    /// </summary>
    public class UnicodeMathMaskTests
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        private static XElement Read(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath).ToMathML();

        [Test]
        [Arguments(0, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(2, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(3, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(8, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(16, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(24, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(32, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(128, "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(4, "∑_𝑛^(𝑖=1) 𝑖")]
        [Arguments(12, "∑_𝑛^(𝑖=1) 𝑖")]
        [Arguments(20, "∑_𝑛^(𝑖=1) 𝑖")]
        [Arguments(28, "∑_𝑛^(𝑖=1) 𝑖")]
        [Arguments(159, "∑_𝑛^(𝑖=1) 𝑖")]
        public async Task ReadsNaryOptions(int mask, string expected)
        {
            var equation = Equation.Parse("∑" + mask + "_(i=1)^n i", EquationFormat.UnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expected);
            await Assert.That(equation.ToMathML().Descendants(Mml + "mo").First().Value).IsEqualTo("∑");
        }

        [Test]
        [Arguments("∑160_(i=1)^n i", "nary mask is not between 0 and 159")]
        [Arguments("∑200 i", "nary mask is not between 0 and 159")]
        [Arguments("▭(256&x)", "enclosure mask is not between 0 and 255")]
        [Arguments("▭(300&x)", "enclosure mask is not between 0 and 255")]
        public async Task RejectsMasksOutOfRange(string unicodeMath, string message)
        {
            await Assert.That(() => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath)).Throws<FormatException>().WithMessage(message);
            await Assert.That(Equation.TryParse(unicodeMath, EquationFormat.UnicodeMath, out _)).IsFalse();
        }

        [Test]
        [Arguments(0, "nAlignBaseline nSpaceDefault nSizeDefault")]
        [Arguments(1, "nAlignCenter nSpaceDefault nSizeDefault")]
        [Arguments(4, "nAlignBaseline nSpaceUnary nSizeDefault")]
        [Arguments(8, "nAlignBaseline nSpaceBinary nSizeDefault")]
        [Arguments(12, "nAlignBaseline nSpaceRelational nSizeDefault")]
        [Arguments(16, "nAlignBaseline nSpaceSkip nSizeDefault")]
        [Arguments(20, "nAlignBaseline nSpaceOrd nSizeDefault")]
        [Arguments(24, "nAlignBaseline nSpaceDifferential nSizeDefault")]
        [Arguments(32, "nAlignBaseline nSpaceDefault nSizeText")]
        [Arguments(64, "nAlignBaseline nSpaceDefault nSizeScript")]
        [Arguments(96, "nAlignBaseline nSpaceDefault nSizeScriptScript")]
        [Arguments(128, "nAlignBaseline nSpaceDefault nSizeDefault fBreakable")]
        [Arguments(256, "nAlignBaseline nSpaceDefault nSizeDefault fBreakable fXPositioning")]
        [Arguments(512, "nAlignBaseline nSpaceDefault nSizeDefault fBreakable fXPositioning fXSpacing")]
        public async Task ReadsAbstractBoxOptions(int mask, string classes)
        {
            var box = Read("□(" + mask + "&x)").Elements().Single();
            await Assert.That(box.Name).IsEqualTo(Mml + "mrow");
            await Assert.That((string?)box.Attribute("class")).IsEqualTo(classes);
            await Assert.That(box.Attribute("intent")).IsNull();
            var scriptLevel = mask == 32 ? "0" : mask == 64 ? "1" : mask == 96 ? "2" : null;
            await Assert.That((string?)box.Attribute("scriptlevel")).IsEqualTo(scriptLevel);
        }

        [Test]
        [Arguments("▭(0&x)", "right left bottom top")]
        [Arguments("▭(3&x)", "right left")]
        [Arguments("▭(5&x)", "right bottom")]
        [Arguments("▭(15&x)", "")]
        [Arguments("▭(1&x)", "right left bottom")]
        [Arguments("▭(2&x)", "right left top")]
        [Arguments("▭(4&x)", "right bottom top")]
        [Arguments("▭(8&x)", "left bottom top")]
        [Arguments("▭(79&x)", "downdiagonalstrike")]
        [Arguments("▭(143&x)", "updiagonalstrike")]
        [Arguments("▭(207&x)", "updiagonalstrike downdiagonalstrike")]
        [Arguments("\\cancel(x)", "updiagonalstrike")]
        [Arguments("\\bcancel(x)", "downdiagonalstrike")]
        [Arguments("\\xcancel(x)", "updiagonalstrike downdiagonalstrike")]
        public async Task ReadsEnclosures(string unicodeMath, string notation)
        {
            var enclosure = Read(unicodeMath).Descendants(Mml + "menclose").SingleOrDefault();
            await Assert.That((string?)enclosure?.Attribute("notation") ?? "").IsEqualTo(notation);
        }

        [Test]
        [Arguments("⟡(0&x)", @"\phantom{x}")]
        [Arguments("⟡(3&x)", @"\mathrlap{x}")]
        [Arguments("⟡(5&x)", @"\smash[t]{x}")]
        [Arguments("⟡(12&x)", @"\hphantom{x}")]
        [Arguments("⟡(2&x)", @"\vphantom{x}")]
        [Arguments("⬍x", @"\smash{x}")]
        [Arguments("⬆x", @"\smash[t]{x}")]
        [Arguments("⬇x", @"\smash[b]{x}")]
        [Arguments("⬌x", @"\mathrlap{x}")]
        [Arguments("⟡x", @"\phantom{x}")]
        public async Task ReadsPhantomsAndSmashes(string unicodeMath, string latex)
        {
            await Assert.That(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath).ToString(EquationFormat.LaTeX)).IsEqualTo(latex);
        }
    }
}
