namespace SharpEquations.Avalonia.Tests.Editing
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The math autocorrect: sequences replaced by their symbols as they are typed, as in Word.</summary>
    public class AutoCorrectTests
    {
        private static Equation U(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);

        private static MathEditor Type(string text, EquationFormat format = EquationFormat.UnicodeMath)
        {
            var editor = new MathEditor { InputFormat = format };
            editor.Insert(text);
            return editor;
        }

        // the row of the equation, built up nodes in brackets and runs as they are
        private static string Row(MathEditor editor) =>
            string.Concat(editor.Root.Children[0].Children.Select(c => c.IsRun ? c.Text : c.IsToken ? "[" + c.Text + "]" : "[" + c.Name + "]"));

        /// <summary>Each entry of Word, typed between two letters, gives the equation of its symbol.</summary>
        [Test]
        public async Task EveryDefault()
        {
            foreach (var entry in MathAutoCorrect.Defaults)
            {
                var editor = Type("a" + entry.Key + "b ");
                await Assert.That(editor.ToEquation()).IsEqualTo(U("a" + entry.Value + "b")).Because(entry.Key);
                await Assert.That(editor.Root.Children[0].Children.Any(c => c.IsRun)).IsFalse().Because(entry.Key);
            }
        }

        /// <summary>The start of a sequence waits, without building up, for what comes after it.</summary>
        [Test]
        public async Task LongestMatchWaitsForTheNextCharacter()
        {
            var editor = Type("a<-");
            await Assert.That(Row(editor)).IsEqualTo("a<-");
            editor.Insert(">");
            await Assert.That(Row(editor)).IsEqualTo("[a][↔]");

            await Assert.That(Type("a<-b ").ToEquation()).IsEqualTo(U("a←b"));
            await Assert.That(Type("p<=>q ").ToEquation()).IsEqualTo(U("p⇔q"));
        }

        /// <summary>An operator that starts a sequence waits for the next character to build up.</summary>
        [Test]
        public async Task OperatorWaitsForTheNextCharacter()
        {
            var editor = Type("x^2+");
            await Assert.That(Row(editor)).IsEqualTo("x^2+");
            editor.Insert("-");
            await Assert.That(Row(editor)).IsEqualTo("[msup][±]");

            var plus = Type("x^2+1");
            await Assert.That(Row(plus)).IsEqualTo("[msup][+]1");
            plus.Insert(" ");
            await Assert.That(Row(plus)).IsEqualTo("[msup][+][1]");
            await Assert.That(plus.ToEquation()).IsEqualTo(U("x^2+1"));
        }

        [Test]
        public async Task BuildsUpWithTheSymbol()
        {
            await Assert.That(Type("a_(x->0) ").ToEquation()).IsEqualTo(U("a_(x→0)"));
            await Assert.That(Type("a!=b ").ToEquation()).IsEqualTo(U("a≠b"));
        }

        [Test]
        public async Task SequenceAtTheEndIsReplacedByEnter()
        {
            var editor = Type("a<-");
            editor.BuildUpAll();
            await Assert.That(editor.ToEquation()).IsEqualTo(U("a←"));
            await Assert.That(Row(editor)).IsEqualTo("[a][←]");
            editor.Undo();
            await Assert.That(Row(editor)).IsEqualTo("a<-");
        }

        /// <summary>An undo right after the autocorrection gives back the sequence as it was typed, as in Word.</summary>
        [Test]
        public async Task UndoGivesBackTheSequence()
        {
            // the b that ends the sequence builds up what the < would have
            var editor = Type("a<-b");
            await Assert.That(Row(editor)).IsEqualTo("[a][←]b");
            editor.Undo();
            await Assert.That(Row(editor)).IsEqualTo("a<-b");
            editor.Redo();
            await Assert.That(Row(editor)).IsEqualTo("[a][←]b");

            // replaced and built up by the same character, one undo gives back the text
            var unequal = Type("a!=");
            await Assert.That(Row(unequal)).IsEqualTo("[a][≠]");
            unequal.Undo();
            await Assert.That(Row(unequal)).IsEqualTo("a!=");

            var alpha = Type("\\alpha ");
            await Assert.That(Row(alpha)).IsEqualTo("[α]");
            alpha.Undo();
            await Assert.That(Row(alpha)).IsEqualTo("\\alpha ");
        }

        /// <summary>A command followed by a space becomes its symbol, even where nothing builds up yet; the space is not kept.</summary>
        [Test]
        public async Task CommandsBecomeTheirSymbols()
        {
            await Assert.That(Row(Type("(\\alpha "))).IsEqualTo("(α");
            await Assert.That(Row(Type("(x\\in "))).IsEqualTo("(x∈");
            await Assert.That(Type("(\\alpha +\\beta ) ").ToEquation()).IsEqualTo(U("(α+β)"));
            await Assert.That(Row(Type("\\frac{\\alpha ", EquationFormat.LaTeX))).IsEqualTo("\\frac{α");

            // a structure is no symbol, and a command still being typed is not replaced
            await Assert.That(Row(Type("\\frac{", EquationFormat.LaTeX))).IsEqualTo("\\frac{");
            await Assert.That(Row(Type("(\\alph"))).IsEqualTo("(\\alph");

            var off = new MathEditor();
            off.AutoCorrect!.ReplaceCommands = false;
            off.Insert("(\\alpha ");
            await Assert.That(Row(off)).IsEqualTo("(\\alpha ");
        }

        [Test]
        public async Task ShortcutsOfTheApplication()
        {
            var editor = new MathEditor();
            editor.AutoCorrect!.Entries["\\R"] = "ℝ";
            editor.Insert("x\\in\\R ");
            await Assert.That(editor.ToEquation()).IsEqualTo(U("x∈ℝ"));

            // \Re is another command: the shortcut \R is a whole word
            var re = new MathEditor();
            re.AutoCorrect!.Entries["\\R"] = "ℝ";
            re.Insert("(\\Re");
            await Assert.That(Row(re)).IsEqualTo("(\\Re");

            var removed = new MathEditor();
            removed.AutoCorrect!.Entries.Remove("->");
            removed.Insert("(a->");
            await Assert.That(Row(removed)).IsEqualTo("(a->");
        }

        [Test]
        public async Task Disabled()
        {
            var editor = new MathEditor { AutoCorrect = null };
            editor.Insert("(a<-b");
            await Assert.That(Row(editor)).IsEqualTo("(a<-b");

            var empty = new MathEditor { AutoCorrect = new MathAutoCorrect(withDefaults: false) { ReplaceCommands = false } };
            empty.Insert("(a<-b\\alpha ");
            await Assert.That(Row(empty)).IsEqualTo("(a<-b\\alpha ");
        }

        [Test]
        public async Task QuotedTextIsLeftAsTyped()
        {
            await Assert.That(Row(Type("\"a->b"))).IsEqualTo("\"a->b");
            await Assert.That(Row(Type("\"\\alpha "))).IsEqualTo("\"\\alpha ");
        }

        /// <summary>In LaTeX, where &lt;= is two signs, only the backslash shortcuts of the application apply.</summary>
        [Test]
        public async Task LaTeXOnlyTheShortcuts()
        {
            var editor = new MathEditor { InputFormat = EquationFormat.LaTeX };
            editor.AutoCorrect!.Entries["\\R"] = "ℝ";
            editor.Insert("a<=b\\R+");
            editor.BuildUpAll();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse("a<=bℝ+", EquationFormat.LaTeX));
        }

        /// <summary>In the linear view the text is edited as it is typed.</summary>
        [Test]
        public async Task NotInTheLinearView()
        {
            var editor = new MathEditor();
            editor.SetLinear(true);
            editor.Insert("a->b");
            await Assert.That(Row(editor)).IsEqualTo("a->b");
        }
    }
}
