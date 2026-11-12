namespace SharpEquations.Avalonia.Tests.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The editor on the 700 equations of the UnicodeMathML corpus.</summary>
    public class CorpusTests
    {
        [Test]
        public async Task CorpusIsLarge() => await Assert.That(Corpus.Examples()).Count().IsGreaterThan(700);

        /// <summary>An equation loaded in the editor (its MathML to the tree of nodes and back) is the same equation.</summary>
        [Test]
        public async Task LoadedEquationIsUnchanged()
        {
            var failures = new List<string>();
            foreach (var (input, _, equation) in Corpus.Examples())
            {
                if (new MathEditor(equation).ToEquation() != equation)
                    failures.Add(input);
            }
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>Linear and back: an equation built down to UnicodeMath and built up again is the same equation.</summary>
        [Test]
        public async Task BuiltDownAndUpIsUnchanged()
        {
            var failures = new List<string>();
            foreach (var (input, _, equation) in Corpus.Examples())
            {
                var editor = new MathEditor(equation);
                editor.SetLinear(true);
                editor.SetLinear(false);
                if (editor.ToEquation() != equation)
                    failures.Add(input);
            }
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>
        /// Every equation typed one character at a time, building up as it is typed, ends in an equation; and for most
        /// of them it is the equation of the whole text read at once. The others are text that UnicodeMath reads in
        /// another way when it is read a piece at a time: what builds up at an operator is final, as in Word (the
        /// prescripts of <c>_4 F_1</c>, <c>+-</c> after a superscript, the spaces in quoted text).
        /// </summary>
        [Test]
        public async Task TypedEquationIsMostlyTheEquationOfTheText()
        {
            var same = 0;
            var errors = new List<string>();
            foreach (var (input, display, equation) in Corpus.Examples())
            {
                var editor = new MathEditor { IsDisplay = display };
                try
                {
                    editor.Insert(input);
                    if (editor.ToEquation() == equation)
                        same++;
                }
                catch (Exception e)
                {
                    errors.Add(input + ": " + e.Message);
                }
            }
            await Assert.That(errors).IsEmpty();
            await Assert.That(same).IsGreaterThanOrEqualTo(660);
        }
    }
}
