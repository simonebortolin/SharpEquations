#nullable enable

namespace SharpEquations.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using SharpEquations;

    /// <summary>
    /// The test cases of mml2tex: MathML with the LaTeX mml2tex writes for it (its XSpec scenarios), and the MathML of its
    /// examples, which have none.
    /// </summary>
    public class Mml2TexCorpusTests
    {
        public sealed class Case
        {
            public string source { get; set; } = "";
            public string? label { get; set; }
            public string mathml { get; set; } = "";
            public string? tex { get; set; }
        }

        private static List<Case> Cases() => JsonSerializer.Deserialize<List<Case>>(
            File.ReadAllText(TestData.Path("mathml", "mml2tex.json")))!;

        public static IEnumerable<System.Func<Case>> All() => Cases().Select(c => (System.Func<Case>)(() => c));

        public static IEnumerable<System.Func<Case>> WithTeX() => Cases().Where(c => c.tex != null).Select(c => (System.Func<Case>)(() => c));

        // the LaTeX SharpEquations writes reads as the LaTeX mml2tex writes
        [Test]
        [MethodDataSource(nameof(WithTeX))]
        public async Task WritesLaTeXThatReadsAsMml2TexLaTeX(Case c)
        {
            var equation = Equation.Parse(c.mathml, EquationFormat.MathML);
            var options = new EquationOptions { Display = equation.IsDisplay };
            var ours = Equation.Parse(equation.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX, options);
            var theirs = Equation.Parse(c.tex!, EquationFormat.LaTeX, options);
            await Assert.That(ours).IsEqualTo(theirs).Because(c.label + ": " + equation.ToString(EquationFormat.LaTeX) + " / " + c.tex);
        }

        // written once, the equation loses what LaTeX cannot hold (the alignment of the columns, <mi>11</mi> that is a
        // number, the italic of <mi>Γ</mi>, which TeX writes upright) and reads back to itself from then on
        [Test]
        [MethodDataSource(nameof(All))]
        public async Task WritesLaTeXThatReadsBack(Case c)
        {
            var equation = Equation.Parse(c.mathml, EquationFormat.MathML);
            var options = new EquationOptions { Display = equation.IsDisplay };
            var once = Equation.Parse(equation.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX, options);
            var twice = Equation.Parse(once.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX, options);
            await Assert.That(twice).IsEqualTo(once).Because(once.ToString(EquationFormat.LaTeX));
        }
    }
}
