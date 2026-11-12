namespace SharpEquations.Avalonia.Tests.Editing
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The caret, the selection and the deletion in the tree of an equation.</summary>
    public class EditorTests
    {
        private static MathEditor Load(string unicodeMath) => new MathEditor(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath));

        [Test]
        public async Task CaretGoesThroughTheSlots()
        {
            // a/b: the caret enters the numerator, goes to the denominator, then out of the fraction
            var editor = Load("a/b");
            editor.MoveHome();
            var positions = new System.Collections.Generic.List<string> { editor.Caret.ToString() };
            for (var i = 0; i < 6; i++)
            {
                editor.MoveRight();
                positions.Add(editor.Caret.ToString());
            }
            await Assert.That(positions).IsEquivalentTo(new[] { "/0:0", "/0/0/0:0", "/0/0/0:1", "/0/0/1:0", "/0/0/1:1", "/0:1", "/0:1" });

            for (var i = 0; i < 6; i++)
                editor.MoveLeft();
            await Assert.That(editor.Caret.ToString()).IsEqualTo("/0:0");
        }

        [Test]
        public async Task UpAndDownInAFraction()
        {
            var editor = Load("a/b");
            editor.SetCaret(new MathPosition(new[] { 0, 0, 0 }, 1));
            editor.MoveDown();
            await Assert.That(editor.Caret.Path).IsEquivalentTo(new[] { 0, 0, 1 });
            editor.MoveUp();
            await Assert.That(editor.Caret.Path).IsEquivalentTo(new[] { 0, 0, 0 });
        }

        [Test]
        public async Task TypingInASlot()
        {
            var editor = Load("a/b");
            editor.SetCaret(new MathPosition(new[] { 0, 0, 1 }, 1));
            editor.Insert("+c ");
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("a/(b+c)", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task BackspaceSelectsAStructureThenDeletesIt()
        {
            var editor = Load("x+a/b");
            editor.MoveEnd();
            editor.Backspace();
            await Assert.That(editor.Selection).IsNotNull();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("x+a/b", EquationFormat.UnicodeMath));
            editor.Backspace();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("x+", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task BackspaceInATokenMakesItText()
        {
            var editor = Load("123");
            editor.MoveEnd();
            editor.Backspace();
            await Assert.That(editor.Root.Children[0].Children.Single().Text).IsEqualTo("12");
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("12", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task SelectionOutOfASlotCoversTheStructure()
        {
            var editor = Load("x+a/b");
            editor.SetCaret(new MathPosition(new[] { 0, 2, 0 }, 1));
            editor.SetCaret(new MathPosition(new[] { 0 }, 0), extend: true);
            var selection = editor.Selection!;
            await Assert.That(selection.Path).IsEquivalentTo(new[] { 0 });
            await Assert.That(selection.Start).IsEqualTo(0);
            await Assert.That(selection.End).IsEqualTo(3);
            await Assert.That(editor.GetSelectedEquation()).IsEqualTo(Equation.Parse("x+a/b", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task SelectAllAndType()
        {
            var editor = Load("x+y");
            editor.SelectAll();
            editor.Insert("z");
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("z", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task PasteInsertsTheEquation()
        {
            var editor = Load("x+");
            editor.MoveEnd();
            await Assert.That(editor.Paste("\\frac{1}{2}", EquationFormat.LaTeX)).IsTrue();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("x+1/2", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task BuildDownTheSelection()
        {
            var editor = Load("x+a/b");
            editor.SetCaret(new MathPosition(new[] { 0 }, 2));
            editor.SetCaret(new MathPosition(new[] { 0 }, 3), extend: true);
            editor.BuildDown();
            await Assert.That(editor.Root.Children[0].Children[2].IsRun).IsTrue();
            editor.BuildUpAll();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("x+a/b", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task EmptyEquation()
        {
            var editor = new MathEditor();
            await Assert.That(editor.IsEmpty).IsTrue();
            await Assert.That(editor.ToEquation()).IsNull();
            editor.Backspace();
            editor.MoveLeft();
            editor.MoveUp();
            await Assert.That(editor.Caret.ToString()).IsEqualTo("/0:0");
        }

        // the MathML Word puts on the clipboard: an XML declaration, the mml prefix, and the namespace of OMML
        private const string WordMathML = "<?xml version=\"1.0\"?><mml:math xmlns:mml=\"http://www.w3.org/1998/Math/MathML\" "
            + "xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"><mml:mi>A</mml:mi><mml:mo>=</mml:mo>"
            + "<mml:mi>π</mml:mi><mml:msup><mml:mrow><mml:mi>r</mml:mi></mml:mrow><mml:mrow><mml:mn>2</mml:mn></mml:mrow></mml:msup></mml:math>";

        [Test]
        public async Task PasteTheMathMLOfWord()
        {
            var editor = new MathEditor();
            await Assert.That(editor.Paste(WordMathML, EquationFormat.MathML)).IsTrue();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("A=πr^2", EquationFormat.UnicodeMath));
        }

        /// <summary>MathML that does not read (the UTF-8 of Word read as UTF-16) inserts nothing, rather than its text.</summary>
        [Test]
        public async Task MathMLThatDoesNotReadInsertsNothing()
        {
            var editor = new MathEditor();
            var garbled = System.Text.Encoding.Unicode.GetString(System.Text.Encoding.UTF8.GetBytes(WordMathML + " "));
            await Assert.That(editor.Paste(garbled, EquationFormat.MathML)).IsFalse();
            await Assert.That(editor.IsEmpty).IsTrue();
        }
    }
}
