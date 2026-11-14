namespace SharpEquations.Avalonia.Tests.Editing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>What the floating bar at the caret shows, without a control.</summary>
    public class QuickBarModeTests
    {
        private static Equation U(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);

        private static MathQuickBarMode Mode(MathEditor editor) => editor.GetQuickBarMode(8, out _);

        [Test]
        public async Task CompletionsOfACommand()
        {
            var editor = new MathEditor();
            editor.Insert("x+\\al");
            var mode = editor.GetQuickBarMode(8, out var completions);
            await Assert.That(mode).IsEqualTo(MathQuickBarMode.Completions);
            await Assert.That(completions.Select(c => c.Name)).Contains("alpha");
            await Assert.That(completions.Count).IsLessThanOrEqualTo(8);
        }

        [Test]
        public async Task ABackslashAloneShowsNothing()
        {
            var editor = new MathEditor();
            editor.Insert("x+\\");
            var mode = editor.GetQuickBarMode(8, out var completions);
            await Assert.That(mode).IsEqualTo(MathQuickBarMode.Hidden);
            await Assert.That(completions).IsEmpty();
        }

        [Test]
        public async Task DismissedUntilTheCommandChanges()
        {
            var editor = new MathEditor();
            editor.Insert("\\al");
            editor.DismissCompletions();
            var dismissed = Mode(editor);
            editor.Insert("p");
            var again = Mode(editor);
            editor.Backspace();
            await Assert.That(dismissed).IsEqualTo(MathQuickBarMode.Hidden);
            await Assert.That(again).IsEqualTo(MathQuickBarMode.Completions);
            await Assert.That(Mode(editor)).IsEqualTo(MathQuickBarMode.Completions);
        }

        [Test]
        public async Task SelectionTableAndEmptySlot()
        {
            var editor = new MathEditor(U("a+b"));
            var nothing = Mode(editor);
            editor.SelectAll();
            var selection = Mode(editor);
            editor.MoveEnd();
            editor.InsertMatrix(2, 2, "(", ")");
            var table = Mode(editor);
            editor.Undo();
            editor.InsertStructure("mfrac");
            await Assert.That(nothing).IsEqualTo(MathQuickBarMode.Hidden);
            await Assert.That(selection).IsEqualTo(MathQuickBarMode.Selection);
            await Assert.That(table).IsEqualTo(MathQuickBarMode.Table);
            await Assert.That(Mode(editor)).IsEqualTo(MathQuickBarMode.Symbols);
        }

        [Test]
        public async Task NothingInTheLinearFormat()
        {
            var editor = new MathEditor(U("a+b"));
            editor.SetLinear(true);
            editor.SelectAll();
            await Assert.That(editor.IsLinear).IsTrue();
            await Assert.That(Mode(editor)).IsEqualTo(MathQuickBarMode.Hidden);
        }
    }
}
