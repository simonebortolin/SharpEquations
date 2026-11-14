namespace SharpEquations.MathTools.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.MathTools;
    using SharpEquations.MathTools.Graphing;

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
            await Assert.That(Equation.Parse(equation.ToString(EquationFormat.MathML), EquationFormat.MathML, Display).ToString(EquationFormat.UnicodeMath))
                .IsEqualTo(equation.ToString(EquationFormat.UnicodeMath));
        }

        public static System.Collections.Generic.IEnumerable<string> Graphed() => Formulas.All.Where(f => f.Graph != null).Select(f => f.Name);

        [Test]
        [MethodDataSource(nameof(Graphed))]
        public async Task PresetIsDrawn(string name)
        {
            var preset = Formulas.All.Single(f => f.Name == name).Graph!;
            var plot = preset.Plot();
            // every value is of a parameter, and the curve has points in the region around the origin
            await Assert.That(preset.Values.Keys.All(plot.Parameters.Contains)).IsTrue();
            foreach (var (parameter, value) in preset.Values)
                await Assert.That(plot[parameter]).IsEqualTo(value);
            var points = plot.Kind switch
            {
                GraphKind.Function => Enumerable.Range(-100, 201).Count(i => double.IsFinite(plot.Y(i / 10.0))),
                GraphKind.Implicit => Contours.ZeroSet(plot.F, Viewport.Default, 80, 80).Count,
                _ => Enumerable.Range(0, 200).Count(i => plot.Point(plot.Range.Min + (plot.Range.Max - plot.Range.Min) * i / 199) is var (x, y) && double.IsFinite(x) && double.IsFinite(y)),
            };
            await Assert.That(points).IsGreaterThan(20);
        }

        [Test]
        public async Task CurvesAreASubject()
        {
            await Assert.That(Formulas.Subjects).Contains("Curves");
            await Assert.That(Formulas.Find("Curves", null).All(f => f.Graph != null)).IsTrue();
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
