namespace SharpEquations.Avalonia.Tests.Editing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The commands of an application: structures, tables, copy in any format, undo and redo.</summary>
    public class CommandTests
    {
        private static Equation U(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);

        [Test]
        public async Task StructureTakesTheSelection()
        {
            var editor = new MathEditor(U("x+a"));
            editor.SetCaret(new MathPosition(new[] { 0 }, 2));
            editor.SetCaret(new MathPosition(new[] { 0 }, 3), extend: true);
            editor.InsertStructure("mfrac");
            await Assert.That(editor.Caret.Path).IsEquivalentTo(new[] { 0, 2, 1 });
            editor.Insert("b ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("x+a/b"));
        }

        [Test]
        public async Task MatrixAndCells()
        {
            var editor = new MathEditor();
            editor.InsertMatrix(2, 2, "(", ")");
            var cells = new List<string>();
            foreach (var text in new[] { "a", "b", "c", "d" })
            {
                editor.Insert(text);
                cells.Add(string.Join("/", editor.Caret.Path.Take(editor.Caret.Path.Count - 1)));
                if (text != "d")
                    await Assert.That(editor.MoveToNextCell()).IsTrue();
            }
            await Assert.That(editor.MoveToNextCell()).IsFalse();
            await Assert.That(cells.Distinct().Count()).IsEqualTo(4);
            await Assert.That(editor.ToEquation()).IsEqualTo(U("(■(a&b@c&d))"));
            await Assert.That(editor.MoveToNextCell(backward: true)).IsTrue();
        }

        [Test]
        public async Task FencesAroundTheSelection()
        {
            var editor = new MathEditor(U("a+b"));
            editor.SelectAll();
            editor.InsertFences("[", "]");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("[a+b]"));
        }

        /// <summary>What is typed between fences builds up without the opening fence, which alone does not parse (<c>|x</c>).</summary>
        [Test]
        [Arguments("|", "|", "|x+1/2|")]
        [Arguments("‖", "‖", "‖x+1/2‖")]
        [Arguments("(", ")", "(x+1/2)")]
        [Arguments("[", "]", "[x+1/2]")]
        [Arguments("⌊", "⌋", "⌊x+1/2⌋")]
        public async Task TypedBetweenFencesBuildsUp(string open, string close, string expected)
        {
            var editor = new MathEditor();
            editor.InsertFences(open, close);
            editor.Insert("x+1/2 ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U(expected));
            editor.Insert("-y");
            editor.BuildUpAll();
            await Assert.That(editor.ToEquation()).IsEqualTo(U(expected.Substring(0, expected.Length - close.Length) + "-y" + close));
        }

        [Test]
        public async Task SelectedTextInEveryFormat()
        {
            var editor = new MathEditor(U("x+a/b"));
            await Assert.That(editor.GetSelectedText(EquationFormat.MathML)).IsNull();
            editor.SelectAll();
            await Assert.That(U(editor.GetSelectedText(EquationFormat.UnicodeMath)!)).IsEqualTo(U("x+a/b"));
            await Assert.That(editor.GetSelectedText(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Standard })).IsEqualTo("x+\\frac{a}{b}");
            await Assert.That(editor.GetSelectedText(EquationFormat.MathML)).Contains("<mfrac>");
            await Assert.That(editor.GetSelectedText(EquationFormat.Omml)).Contains("m:f");
        }

        /// <summary>Everything typed and deleted undoes back to the empty equation, and redoes to the end.</summary>
        [Test]
        public async Task UndoAndRedoEverything()
        {
            var editor = new MathEditor();
            var states = new List<Equation?> { editor.ToEquation() };
            void Record() => states.Add(editor.ToEquation());

            editor.Insert("a/b ");
            Record();
            editor.Insert("+\\sqrt(x) ");
            Record();
            editor.Backspace(); // selects the radical
            editor.Backspace(); // deletes it
            Record();
            editor.MoveHome();
            editor.MoveHome();
            editor.Delete(); // selects the fraction
            editor.Delete();
            Record();
            await Assert.That(states[^1]).IsEqualTo(U("+"));

            var undone = new List<Equation?>();
            while (editor.CanUndo)
            {
                editor.Undo();
                undone.Add(editor.ToEquation());
            }
            await Assert.That(editor.IsEmpty).IsTrue();
            foreach (var state in states.Take(states.Count - 1))
                await Assert.That(undone).Contains(state);

            while (editor.CanRedo)
                editor.Redo();
            await Assert.That(editor.ToEquation()).IsEqualTo(U("+"));
        }

        [Test]
        public async Task TypingIsOneUndo()
        {
            var editor = new MathEditor();
            editor.Insert("x");
            editor.Insert("y");
            editor.Insert("z");
            editor.Undo();
            await Assert.That(editor.IsEmpty).IsTrue();
        }

        [Test]
        public async Task UndoLimitForgetsTheOldest()
        {
            var editor = new MathEditor { UndoLimit = 3 };
            for (var i = 0; i < 10; i++)
                editor.Insert(i + " ");
            var count = 0;
            while (editor.CanUndo)
            {
                editor.Undo();
                count++;
            }
            await Assert.That(count).IsEqualTo(3);
        }

        [Test]
        public async Task DeleteInsideARunAndAcrossTokens()
        {
            var editor = new MathEditor();
            editor.Insert("abc+def");
            editor.MoveLeft();
            editor.MoveLeft();
            editor.Backspace();
            editor.Delete();
            editor.Insert(" ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("abc+f"));
        }

        [Test]
        public async Task EquationNumber()
        {
            var editor = new MathEditor(Equation.Parse("E=mc^2", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            await Assert.That(editor.EquationNumber).IsNull();
            editor.SetEquationNumber("(1)");
            await Assert.That(editor.EquationNumber).IsEqualTo("(1)");
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("E=mc^2#(1)", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            await Assert.That(editor.GetText(EquationFormat.LaTeX)).IsEqualTo(Equation.Parse("E=mc^2#(1)", EquationFormat.UnicodeMath, new EquationOptions { Display = true }).ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word }));
            editor.Insert("+1 ");
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("E=mc^2+1#(1)", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            editor.SetEquationNumber(null);
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("E=mc^2+1", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            editor.Undo();
            await Assert.That(editor.EquationNumber).IsEqualTo("(1)");
        }

        [Test]
        public async Task EquationNumberTyped()
        {
            var editor = new MathEditor { IsDisplay = true };
            editor.Insert("E=mc^2#(1) ");
            await Assert.That(editor.EquationNumber).IsEqualTo("(1)");
            var latex = new MathEditor { IsDisplay = true, InputFormat = EquationFormat.LaTeX };
            latex.Insert("E=mc^2\\tag{2} ");
            await Assert.That(latex.EquationNumber).IsEqualTo("(2)");
        }

        private static Equation D(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        [Test]
        public async Task LinesOfAnEquationArray()
        {
            var editor = new MathEditor { IsDisplay = true };
            editor.Insert("a&=b+c");
            editor.InsertLineBreak();
            await Assert.That(editor.LineCount).IsEqualTo(2);
            editor.Insert("&=d ");
            await Assert.That(editor.ToEquation()).IsEqualTo(D("█(a&=b+c@&=d)"));
            editor.InsertLineBreak();
            editor.Insert("&=e ");
            await Assert.That(editor.ToEquation()).IsEqualTo(D("█(a&=b+c@&=d@&=e)"));

            // Backspace at the start of a line joins it to the line before
            editor.MoveHome();
            editor.Backspace();
            await Assert.That(editor.LineCount).IsEqualTo(2);
            editor.Undo();
            await Assert.That(editor.LineCount).IsEqualTo(3);
        }

        [Test]
        public async Task LineBreakInTheMiddle()
        {
            var editor = new MathEditor(D("x+y=1"));
            editor.SetCaret(new MathPosition(new[] { 0 }, 3));
            editor.InsertLineBreak();
            await Assert.That(editor.ToEquation()).IsEqualTo(D("█(x+y@=1)"));
            editor.MoveUp();
            editor.Delete();
            await Assert.That(editor.LineCount).IsEqualTo(1);
            await Assert.That(editor.ToEquation()).IsEqualTo(D("x+y=1"));
        }

        [Test]
        public async Task LinesFromLaTeX()
        {
            var editor = new MathEditor(Equation.Parse("\\begin{aligned}a&=b\\\\&=c\\end{aligned}", EquationFormat.LaTeX, new EquationOptions { Display = true }));
            await Assert.That(editor.LineCount).IsEqualTo(2);
        }

        [Test]
        public async Task CompletionOfCommands()
        {
            var editor = new MathEditor();
            editor.Insert("x+\\alp");
            await Assert.That(editor.CommandPrefix).IsEqualTo("\\alp");
            var alpha = editor.GetCompletions().First(c => c.Name == "alpha");
            editor.Complete(alpha);
            editor.Insert(" ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("x+α"));

            var latex = new MathEditor { InputFormat = EquationFormat.LaTeX };
            latex.Insert("\\fra");
            latex.Complete(latex.GetCompletions().First(c => c.Name == "frac"));
            latex.Insert("1");
            latex.MoveRight();
            latex.MoveRight();
            latex.Insert("2");
            latex.BuildUpAll();
            await Assert.That(latex.ToEquation()).IsEqualTo(U("1/2"));
        }

        [Test]
        public async Task RowsAndColumnsOfAMatrix()
        {
            var editor = new MathEditor(U("(■(a&b@c&d))"));
            await Assert.That(editor.IsInTable).IsFalse();
            editor.SetCaret(new MathPosition(new[] { 0, 0, 1, 0, 0, 0 }, 1));
            await Assert.That(editor.IsInTable).IsTrue();
            editor.InsertTableRow();
            editor.Insert("e");
            editor.MoveToNextCell();
            editor.Insert("f ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("(■(a&b@e&f@c&d))"));
            editor.InsertTableColumn();
            editor.Insert("g ");
            // an empty cell, which UnicodeMath reads as an empty operator
            await Assert.That(U(editor.GetText(EquationFormat.UnicodeMath))).IsEqualTo(U("(■(a&b&@e&f&g@c&d&))"));
            editor.DeleteTableColumn();
            editor.DeleteTableRow();
            await Assert.That(editor.ToEquation()).IsEqualTo(U("(■(a&b@c&d))"));
            editor.DeleteTableRow();
            editor.DeleteTableRow();
            // the last row deleted, the table goes, and the brackets stay
            await Assert.That(editor.Root.Children[0].Children[0].Children.Select(c => c.Text)).IsEquivalentTo(new[] { "(", ")" });
            await Assert.That(editor.InsertTableRow()).IsFalse();
        }
    }
}
