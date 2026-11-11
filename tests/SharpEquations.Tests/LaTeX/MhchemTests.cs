#nullable enable

namespace SharpEquations.Tests.LaTeX
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// The \ce and \pu of mhchem: the chemistry test cases of treeblood read strictly, and the MathML of some checked
    /// by hand.
    /// </summary>
    public class MhchemTests
    {
        private static readonly EquationOptions Strict = new EquationOptions { Strict = true };

        public static IEnumerable<string> Formulas() => JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(TestData.Path("latex", "treeblood-mhchem.json")))!;

        private static string Compact(Equation equation) => string.Concat(equation.ToMathML().Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)))
            .Replace(" xmlns=\"http://www.w3.org/1998/Math/MathML\"", "");

        [Test]
        public async Task TheCorpusHasAllTheFormulas()
        {
            await Assert.That(Formulas().Count()).IsEqualTo(76);
        }

        [Test]
        [MethodDataSource(nameof(Formulas))]
        public async Task EveryFormulaParsesStrictly(string latex)
        {
            var equation = Equation.Parse(latex, EquationFormat.LaTeX, Strict);
            var mathml = Compact(equation);
            await Assert.That(mathml.Length).IsGreaterThan(0);
            await Assert.That(mathml).DoesNotContain("⬚");
            await Assert.That(mathml).DoesNotContain("[object Object]");
            await Assert.That(mathml).DoesNotContain("\\");
        }

        [Test]
        [Arguments(@"\ce{H2O}", "<msub><mi mathvariant=\"normal\">H</mi><mn>2</mn></msub><mi mathvariant=\"normal\">O</mi>")]
        [Arguments(@"\ce{CrO4^2-}", "<mtext>Cr</mtext><msubsup><mi mathvariant=\"normal\">O</mi><mn>4</mn><mrow><mn>2</mn><mo>−</mo></mrow></msubsup>")]
        [Arguments(@"\ce{[AgCl2]-}", "<msup><mrow><mo>[</mo><mtext>Ag</mtext><msub><mtext>Cl</mtext><mn>2</mn></msub><mo>]</mo></mrow><mo>−</mo></msup>")]
        [Arguments(@"\ce{(NH4)2S}", "<msub><mrow><mo>(</mo><mi mathvariant=\"normal\">N</mi><msub><mi mathvariant=\"normal\">H</mi><mn>4</mn></msub><mo>)</mo></mrow><mn>2</mn></msub><mi mathvariant=\"normal\">S</mi>")]
        [Arguments(@"\ce{^{227}_{90}Th+}", "<mmultiscripts><mtext>Th</mtext><none /><mo>+</mo><mprescripts /><mn>90</mn><mn>227</mn></mmultiscripts>")]
        [Arguments(@"\ce{^0_-1n-}", "<mmultiscripts><mi mathvariant=\"normal\">n</mi><none /><mo>−</mo><mprescripts /><mrow><mo>−</mo><mn>1</mn></mrow><mn>0</mn></mmultiscripts>")]
        [Arguments(@"\ce{H{}^3HO}", "<mi mathvariant=\"normal\">H</mi><mmultiscripts><mi mathvariant=\"normal\">H</mi><mprescripts /><none /><mn>3</mn></mmultiscripts><mi mathvariant=\"normal\">O</mi>")]
        [Arguments(@"\ce{1/2H2O}", "<mfrac><mn>1</mn><mn>2</mn></mfrac><msub><mi mathvariant=\"normal\">H</mi><mn>2</mn></msub><mi mathvariant=\"normal\">O</mi>")]
        [Arguments(@"\ce{KCr(SO4)2.12H2O}", "<mi mathvariant=\"normal\">K</mi><mtext>Cr</mtext><msub><mrow><mo>(</mo><mi mathvariant=\"normal\">S</mi><msub><mi mathvariant=\"normal\">O</mi><mn>4</mn></msub><mo>)</mo></mrow><mn>2</mn></msub><mo>⋅</mo><mn>12</mn><msub><mi mathvariant=\"normal\">H</mi><mn>2</mn></msub><mi mathvariant=\"normal\">O</mi>")]
        [Arguments(@"\ce{A <=> B}", "<mi mathvariant=\"normal\">A</mi><mo>⇌</mo><mi mathvariant=\"normal\">B</mi>")]
        [Arguments(@"\ce{A ->[H2O] B}", "<mi mathvariant=\"normal\">A</mi><mover><mo>→</mo><mrow><msub><mi mathvariant=\"normal\">H</mi><mn>2</mn></msub><mi mathvariant=\"normal\">O</mi></mrow></mover><mi mathvariant=\"normal\">B</mi>")]
        [Arguments(@"\ce{A ->[{text above}][{text below}] B}", "<mi mathvariant=\"normal\">A</mi><munder><mover><mo>→</mo><mtext>text above</mtext></mover><mtext>text below</mtext></munder><mi mathvariant=\"normal\">B</mi>")]
        [Arguments(@"\ce{SO4^2- + Ba^2+ -> BaSO4 v}", "<mi mathvariant=\"normal\">S</mi><msubsup><mi mathvariant=\"normal\">O</mi><mn>4</mn><mrow><mn>2</mn><mo>−</mo></mrow></msubsup><mo>+</mo><msup><mtext>Ba</mtext><mrow><mn>2</mn><mo>+</mo></mrow></msup><mo>⟶</mo><mtext>Ba</mtext><mi mathvariant=\"normal\">S</mi><msub><mi mathvariant=\"normal\">O</mi><mn>4</mn></msub><mo>↓</mo>")]
        [Arguments(@"\ce{A-B=C#D}", "<mi mathvariant=\"normal\">A</mi><mo>−</mo><mi mathvariant=\"normal\">B</mi><mo>=</mo><mi mathvariant=\"normal\">C</mi><mo>≡</mo><mi mathvariant=\"normal\">D</mi>")]
        [Arguments(@"\ce{NO_x}", "<mi mathvariant=\"normal\">N</mi><msub><mi mathvariant=\"normal\">O</mi><mi>x</mi></msub>")]
        [Arguments(@"\ce{Ca(OH)2(s)}", "<mtext>Ca</mtext><msub><mrow><mo>(</mo><mi mathvariant=\"normal\">O</mi><mi mathvariant=\"normal\">H</mi><mo>)</mo></mrow><mn>2</mn></msub><mrow><mo>(</mo><mi mathvariant=\"normal\">s</mi><mo>)</mo></mrow>")]
        [Arguments(@"\ce{H2O}^2", "<msup><mrow><msub><mi mathvariant=\"normal\">H</mi><mn>2</mn></msub><mi mathvariant=\"normal\">O</mi></mrow><mn>2</mn></msup>")]
        [Arguments(@"\pu{1.2e3 kJ mol-1}", "<mn>1.2</mn><mo>×</mo><msup><mn>10</mn><mn>3</mn></msup><mo> </mo><mtext>kJ</mtext><mo> </mo><msup><mtext>mol</mtext><mrow><mo>−</mo><mn>1</mn></mrow></msup>")]
        public async Task FormulaIsReadAsMathML(string latex, string expected)
        {
            await Assert.That(Compact(Equation.Parse(latex, EquationFormat.LaTeX, Strict))).IsEqualTo(expected);
        }

        [Test]
        [Arguments(@"\ce{A\bond{?}B}")]
        [Arguments(@"\ce{H2O")]
        [Arguments(@"\ce{A $x B}")]
        public async Task MalformedFormulaIsRejected(string latex)
        {
            await Assert.That(Equation.TryParse(latex, EquationFormat.LaTeX, Strict, out _)).IsFalse();
        }
    }
}
