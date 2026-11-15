namespace SharpEquations.Avalonia.Tests.Editing
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The keys, given to the editor and to the floating bar with no control.</summary>
    public class KeyTests
    {
        private static Equation U(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);

        [Test]
        public async Task ShiftArrowsSelectAndBackspaceDeletes()
        {
            var editor = new MathEditor(U("a+b"));
            editor.MoveEnd();
            await Assert.That(editor.ProcessKey(MathKey.Left, MathKeyModifiers.Shift)).IsTrue();
            await Assert.That(editor.Selection).IsNotNull();
            await Assert.That(editor.ProcessKey(MathKey.Backspace, MathKeyModifiers.None)).IsTrue();
            await Assert.That(editor.ToEquation()).IsEqualTo(U("a+"));
        }

        [Test]
        public async Task ReadOnlyMovesButDoesNotEdit()
        {
            var editor = new MathEditor(U("a+b"));
            editor.MoveEnd();
            await Assert.That(editor.ProcessKey(MathKey.Backspace, MathKeyModifiers.None, readOnly: true)).IsFalse();
            await Assert.That(editor.ProcessKey(MathKey.Delete, MathKeyModifiers.None, readOnly: true)).IsFalse();
            await Assert.That(editor.ProcessKey(MathKey.Enter, MathKeyModifiers.None, readOnly: true)).IsFalse();
            await Assert.That(editor.ProcessKey(MathKey.Home, MathKeyModifiers.None, readOnly: true)).IsTrue();
            await Assert.That(editor.Caret.Offset).IsEqualTo(0);
            await Assert.That(editor.ToEquation()).IsEqualTo(U("a+b"));
        }

        [Test]
        public async Task TabMovesThroughTheCellsOnly()
        {
            var editor = new MathEditor();
            await Assert.That(editor.ProcessKey(MathKey.Tab, MathKeyModifiers.None)).IsFalse();
            editor.InsertMatrix(2, 2, "(", ")");
            var first = editor.Caret.Path.ToArray();
            await Assert.That(editor.ProcessKey(MathKey.Tab, MathKeyModifiers.Control)).IsFalse();
            await Assert.That(editor.ProcessKey(MathKey.Tab, MathKeyModifiers.None)).IsTrue();
            await Assert.That(editor.Caret.Path).IsNotEquivalentTo(first);
            await Assert.That(editor.ProcessKey(MathKey.Tab, MathKeyModifiers.Shift)).IsTrue();
            await Assert.That(editor.Caret.Path).IsEquivalentTo(first);
        }

        [Test]
        public async Task EnterBuildsUpAndShiftEnterBreaksTheLine()
        {
            var editor = new MathEditor();
            editor.Insert("a/b");
            await Assert.That(editor.ProcessKey(MathKey.Enter, MathKeyModifiers.None)).IsTrue();
            await Assert.That(editor.ToEquation()).IsEqualTo(U("a/b"));
            await Assert.That(editor.ProcessKey(MathKey.Escape, MathKeyModifiers.None)).IsFalse();
        }

        [Test]
        public async Task TheBarChoosesAndTakesACompletion()
        {
            var editor = new MathEditor();
            var bar = new MathQuickBarState(editor);
            editor.Insert("x+\\al");
            await Assert.That(bar.Update(true)).IsTrue();
            await Assert.That(bar.Mode).IsEqualTo(MathQuickBarMode.Completions);
            var index = bar.Completions.Select(c => c.Name).ToList().IndexOf("alpha");
            await Assert.That(index).IsGreaterThanOrEqualTo(0);
            await Assert.That(bar.ProcessKey(MathKey.Up, MathKeyModifiers.None)).IsTrue();
            await Assert.That(bar.SelectedIndex).IsEqualTo(bar.Completions.Count - 1);
            await Assert.That(bar.Update(true)).IsFalse();
            await Assert.That(bar.SelectedIndex).IsEqualTo(bar.Completions.Count - 1);
            bar.SelectedIndex = index;
            await Assert.That(bar.ProcessKey(MathKey.Down, MathKeyModifiers.Shift)).IsFalse();
            await Assert.That(bar.ProcessKey(MathKey.Tab, MathKeyModifiers.None)).IsTrue();
            editor.Insert(" ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("x+α"));
        }

        [Test]
        public async Task EscapeClosesTheCompletions()
        {
            var editor = new MathEditor();
            var bar = new MathQuickBarState(editor);
            editor.Insert("\\su");
            bar.Update(true);
            await Assert.That(bar.ProcessKey(MathKey.Escape, MathKeyModifiers.None)).IsTrue();
            await Assert.That(bar.Mode).IsEqualTo(MathQuickBarMode.Hidden);
            await Assert.That(bar.ProcessKey(MathKey.Escape, MathKeyModifiers.None)).IsFalse();
        }

        [Test]
        public async Task ClosedWhenNotEnabled()
        {
            var editor = new MathEditor(U("a+b"));
            var bar = new MathQuickBarState(editor);
            editor.SelectAll();
            bar.Update(false);
            await Assert.That(bar.Mode).IsEqualTo(MathQuickBarMode.Hidden);
            bar.Update(true);
            await Assert.That(bar.Mode).IsEqualTo(MathQuickBarMode.Selection);
        }
    }
}
