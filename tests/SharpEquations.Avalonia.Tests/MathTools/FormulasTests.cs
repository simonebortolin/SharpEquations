#if NET10_0_OR_GREATER
namespace SharpEquations.Avalonia.Tests.MathTools
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Avalonia.MathTools;

    /// <summary>The formulas of the library parse without errors, and their UnicodeMath and LaTeX read back.</summary>
    public class FormulasTests
    {
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        public static System.Collections.Generic.IEnumerable<string> Names() => Formulas.All.Select(f => f.Name);

        [Test]
        [MethodDataSource(nameof(Names))]
        public async Task FormulaParses(string name)
        {
            var formula = Formulas.All.Single(f => f.Name == name);
            var equation = Equation.Parse(formula.UnicodeMath, EquationFormat.UnicodeMath, Display);
            await Assert.That(equation.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror")).IsFalse();
            await Assert.That(Equation.Parse(equation.ToString(EquationFormat.UnicodeMath), EquationFormat.UnicodeMath, Display)).IsEqualTo(equation);
            await Assert.That(Equation.TryParse(equation.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX, out _)).IsTrue();
        }

        [Test]
        public async Task NamesAreUnique() => await Assert.That(Formulas.All.Select(f => f.Name).Distinct().Count()).IsEqualTo(Formulas.All.Count);

        [Test]
        public async Task FindBySubjectAndName()
        {
            await Assert.That(Formulas.Find(null, null).Count()).IsEqualTo(Formulas.All.Count);
            await Assert.That(Formulas.Find("Geometry", null).All(f => f.Subject == "Geometry")).IsTrue();
            await Assert.That(Formulas.Find(null, "  PYTHAGOREAN ").Select(f => f.Name)).IsEquivalentTo(new[] { "Pythagorean theorem", "Pythagorean identity" });
            await Assert.That(Formulas.Find("Trigonometry", "pythagorean").Single().Name).IsEqualTo("Pythagorean identity");
            await Assert.That(Formulas.Find(null, "calculus").Count()).IsEqualTo(Formulas.Find("Calculus", null).Count());
        }
    }
}
#endif
