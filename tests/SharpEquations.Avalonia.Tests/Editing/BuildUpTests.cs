namespace SharpEquations.Avalonia.Tests.Editing
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>What is typed builds up when a space or an operator ends an argument, as in Word.</summary>
    public class BuildUpTests
    {
        private static MathEditor Type(string text, EquationFormat format = EquationFormat.UnicodeMath)
        {
            var editor = new MathEditor { InputFormat = format };
            editor.Insert(text);
            return editor;
        }

        private static string Built(MathEditor editor) => string.Join(" ", editor.Root.Children[0].Children);

        [Test]
        [Arguments("a/b ", "a/b")]
        [Arguments("x^2+1 ", "x^2+1")]
        [Arguments("\\alpha ", "α")]
        [Arguments("(a+b)/c ", "(a+b)/c")]
        [Arguments("\\sqrt(x) ", "√x")]
        [Arguments("e^-x ", "e^(-x)")]
        [Arguments("x^2_3 ", "x_3^2")]
        [Arguments("a/b+c ", "a/b+c")]
        [Arguments("1/2+1/3 ", "1/2+1/3")]
        public async Task UnicodeMath(string typed, string expected)
        {
            var editor = Type(typed);
            await Assert.That(editor.Root.Children[0].Children.Any(c => c.IsRun)).IsFalse().Because(Built(editor));
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse(expected, EquationFormat.UnicodeMath));
        }

        [Test]
        [Arguments("\\frac{a}{b} ", "a/b")]
        [Arguments("x^{2}+", "x^2+")]
        [Arguments("\\alpha+", "α+")]
        [Arguments("\\sqrt{x} ", "√x")]
        public async Task LaTeX(string typed, string expected)
        {
            var editor = Type(typed, EquationFormat.LaTeX);
            await Assert.That(editor.Root.Children[0].Children.Any(c => c.IsRun)).IsFalse().Because(Built(editor));
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse(expected, EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task ArgumentStillToComeDoesNotBuildUp()
        {
            // the minus after ^ is the start of the exponent, not an operator that ends it
            var editor = Type("e^-");
            await Assert.That(editor.Root.Children[0].Children.Count).IsEqualTo(1);
            await Assert.That(editor.Root.Children[0].Children[0].IsRun).IsTrue();
        }

        [Test]
        public async Task OpenBracketDoesNotBuildUp()
        {
            var editor = Type("\\sqrt(a+");
            await Assert.That(editor.Root.Children[0].Children[0].Text).IsEqualTo("\\sqrt(a+");
        }

        [Test]
        public async Task StructureBeforeIsAnOperand()
        {
            // the fraction built up first is the base of the superscript typed after it
            var editor = Type("(a/b ");
            editor.Insert(")^2 ");
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("(a/b)^2", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task UndoGivesBackTheTextTyped()
        {
            var editor = Type("a/b ");
            editor.Undo();
            await Assert.That(editor.Root.Children[0].Children.Count).IsEqualTo(1);
            await Assert.That(editor.Root.Children[0].Children[0].Text).IsEqualTo("a/b ");
            editor.Redo();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("a/b", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task LinearAndBack()
        {
            var editor = new MathEditor(Equation.Parse("√(x^2+1)/2", EquationFormat.UnicodeMath));
            editor.SetLinear(true);
            await Assert.That(Equation.Parse(editor.GetText(EquationFormat.UnicodeMath), EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("√(x^2+1)/2", EquationFormat.UnicodeMath));
            await Assert.That(editor.Root.Children[0].Children[0].IsRun).IsTrue();
            editor.SetLinear(false);
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("√(x^2+1)/2", EquationFormat.UnicodeMath));
        }

        /// <summary>A command of LaTeX whose arguments come without braces, after a space, waits for them before it builds up.</summary>
        [Test]
        [Arguments("\\underset ab", "\\underset{a}{b}")]
        [Arguments("\\xrightarrow[y] x", "\\xrightarrow[y]{x}")]
        [Arguments("\\boxed \\Box", "\\boxed{\\Box}")]
        [Arguments("\\not a", "\\not{a}")]
        public async Task CommandWaitsForItsArguments(string typed, string expected)
        {
            var editor = Type(typed + " ", EquationFormat.LaTeX);
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse(expected, EquationFormat.LaTeX));
        }

        [Test]
        public async Task LinearInLaTeX()
        {
            var editor = new MathEditor(Equation.Parse("a/b", EquationFormat.UnicodeMath)) { InputFormat = EquationFormat.LaTeX };
            editor.SetLinear(true);
            await Assert.That(editor.GetText(EquationFormat.LaTeX)).StartsWith("\\frac{");
            await Assert.That(Equation.Parse(editor.GetText(EquationFormat.LaTeX), EquationFormat.LaTeX)).IsEqualTo(Equation.Parse("a/b", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task TextThatDoesNotParseIsAnError()
        {
            var editor = Type("\\frac{a", EquationFormat.LaTeX);
            await Assert.That(editor.ToMathML().ToString()).Contains("merror");
        }
    }
}
