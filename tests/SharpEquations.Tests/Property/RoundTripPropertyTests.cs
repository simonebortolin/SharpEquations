#nullable enable

namespace SharpEquations.Tests.Property
{
    using System.Threading.Tasks;
    using TUnit.Assertions.Enums;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using SharpEquations;

    /// <summary>
    /// Properties of the equations of the UnicodeMathML corpus (738 equations of every construct UnicodeMath has) and of
    /// the equations of Word documents: writing never throws, MathML reads back to the same equation, an equation
    /// written in its own format reads back to itself, and one written in another format loses the first time only
    /// what that format cannot hold, so that it reads back to itself from then on.
    /// </summary>
    public class RoundTripPropertyTests
    {
        private static readonly EquationOptions Word = new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word };

        private static List<(string Input, Equation Equation)>? corpus;

        // the examples that are UnicodeMath (a few are LaTeX or not valid, to test errors)
        private static List<(string Input, Equation Equation)> Corpus() => corpus ??= UnicodeMathCorpusTests.Examples()
            .Select(e => (e.input, Equation: Equation.TryParse(e.input, EquationFormat.UnicodeMath, new EquationOptions { Display = e.display }, out var equation) ? equation : null))
            .Where(e => e.Equation != null)
            .Select(e => (e.input, e.Equation!))
            .ToList();

        // the equations of Word documents, as Word writes them in OMML
        private static List<(string Input, Equation Equation)> WordEquations() => Directory
            .GetFiles(TestData.Path("omml"), "*.omml")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), Equation.Parse(File.ReadAllText(f), EquationFormat.Omml)))
            .ToList();

        [Test]
        public async Task CorpusIsLarge()
        {
            await Assert.That(Corpus()).Count().IsGreaterThan(700);
            await Assert.That(WordEquations()).Count().IsGreaterThan(100);
        }

        /// <summary>The inputs of the equations for which the check fails, with what failed.</summary>
        private static Dictionary<string, string> Failures(Func<Equation, string?> check, List<(string Input, Equation Equation)>? equations = null)
        {
            var failures = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (input, equation) in equations ?? Corpus())
            {
                string? failure;
                try
                {
                    failure = check(equation);
                }
                catch (Exception e)
                {
                    failure = e.GetType().Name + ": " + e.Message;
                }
                if (failure != null)
                    failures[input] = failure;
            }
            return failures;
        }

        private static List<string> Listed(Dictionary<string, string> failures) => failures.Select(f => f.Key + "\n  " + f.Value).ToList();

        /// <summary>
        /// The UnicodeMath writer reads back what it writes only when reading could give another equation
        /// (UnicodeMathSerializer.NeedsReadBack): what it writes without reading back is what it writes reading back.
        /// </summary>
        [Test]
        public async Task UnicodeMathWrittenWithoutReadingBackIsTheSame()
        {
            static string? Check(Equation equation)
            {
                var math = equation.ToMathML();
                var written = Internal.UnicodeMath.UnicodeMathSerializer.Serialize(math, false, false);
                var checkedBack = Internal.UnicodeMath.UnicodeMathSerializer.Serialize(math, false, true);
                return written == checkedBack ? null : written + " instead of " + checkedBack;
            }

            await Assert.That(Listed(Failures(Check))).IsEmpty();
            await Assert.That(Listed(Failures(Check, WordEquations()))).IsEmpty();
        }

        /// <summary>
        /// The same for an operator of each character of the basic plane that is not a letter or a digit, alone, between
        /// two identifiers, before one and in a script: the characters that reading gives identifiers with are read back.
        /// </summary>
        [Test]
        public async Task OperatorsWrittenWithoutReadingBackAreTheSame()
        {
            var failures = new List<string>();
            for (int codePoint = 0x21; codePoint <= 0xFFFF; codePoint++)
            {
                if (codePoint is >= 0xD800 and <= 0xF8FF || char.IsLetterOrDigit((char)codePoint) || char.IsControl((char)codePoint))
                    continue;
                var mo = new System.Xml.Linq.XElement(MathMLNamespaces.MathML + "mo", ((char)codePoint).ToString());
                var a = new System.Xml.Linq.XElement(MathMLNamespaces.MathML + "mi", "a");
                var b = new System.Xml.Linq.XElement(MathMLNamespaces.MathML + "mi", "b");
                foreach (var content in new object[] { mo, new object[] { a, mo, b }, new object[] { mo, a }, new object[] { a, mo },
                    new System.Xml.Linq.XElement(MathMLNamespaces.MathML + "msub", a, mo) })
                {
                    Equation equation;
                    try
                    {
                        equation = Equation.Parse(new System.Xml.Linq.XElement(MathMLNamespaces.MathML + "math", content), EquationFormat.MathML);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    var math = equation.ToMathML();
                    var written = Internal.UnicodeMath.UnicodeMathSerializer.Serialize(math, false, false);
                    var checkedBack = Internal.UnicodeMath.UnicodeMathSerializer.Serialize(math, false, true);
                    if (written != checkedBack)
                        failures.Add("U+" + codePoint.ToString("X4") + ": " + written + " instead of " + checkedBack);
                }
            }
            await Assert.That(failures).IsEmpty();
        }

        [Test]
        public async Task WritingNeverThrows()
        {
            foreach (var format in new[] { EquationFormat.MathML, EquationFormat.Omml, EquationFormat.UnicodeMath, EquationFormat.LaTeX })
            {
                // a corpus equation that is only a comment (⫷...⫸) writes nothing in UnicodeMath, which drops comments
                await Assert.That(Failures(e => e.ToString(format) is null ? "null" : null)).IsEmpty().Because(format.ToString());
                await Assert.That(Failures(e => e.ToString(format, Word) is null ? "null" : null)).IsEmpty().Because(format + ", Word");
            }
        }

        [Test]
        public async Task MathMLReadsBackToTheSameEquation()
        {
            await Assert.That(Failures(e => Equation.Parse(e.ToString(), EquationFormat.MathML) == e ? null : "differs")).IsEmpty();
            await Assert.That(Failures(e => Equation.Parse(e.ToMathML(), EquationFormat.MathML, new EquationOptions { Strict = true }) == e ? null : "differs")).IsEmpty();
        }

        /// <summary>The equation written in the format and read back, as an equation of the same display.</summary>
        private static Equation ReadBack(Equation equation, EquationFormat format, EquationOptions? options)
        {
            options = (options ?? new EquationOptions()) with { Display = equation.IsDisplay };
            return Equation.Parse(equation.ToString(format, options), format, options).WithDisplay(equation.IsDisplay);
        }

        /// <summary>An equation written in its own format reads back to the same equation.</summary>
        private static Dictionary<string, string> Changed(EquationFormat format, List<(string Input, Equation Equation)> equations) => Failures(e =>
        {
            var back = ReadBack(e, format, null);
            return back == e ? null : e.ToString(format) + "\n  " + back.ToString(format);
        }, equations);

        /// <summary>
        /// Once read back, an equation written in a format reads back to itself: writing loses what the format cannot
        /// hold the first time only.
        /// </summary>
        private static Dictionary<string, string> Unstable(EquationFormat format, EquationOptions? options = null) => Failures(e =>
        {
            var once = ReadBack(e, format, options);
            var twice = ReadBack(once, format, options);
            return once == twice ? null : once.ToString(format, options) + "\n  " + twice.ToString(format, options);
        });

        [Test]
        public async Task UnicodeMathReadsBackToTheSameEquation() => await Assert.That(Listed(Changed(EquationFormat.UnicodeMath, Corpus()))).IsEmpty();

        [Test]
        public async Task WordOmmlReadsBackToTheSameEquation() => await Assert.That(Listed(Changed(EquationFormat.Omml, WordEquations()))).IsEmpty();

        [Test]
        public async Task StandardLaTeXReadsBackStably() => await Assert.That(Listed(Unstable(EquationFormat.LaTeX))).IsEmpty();

        [Test]
        public async Task WordLaTeXReadsBackStably() => await Assert.That(Listed(Unstable(EquationFormat.LaTeX, Word))).IsEmpty();

        [Test]
        public async Task OmmlReadsBackStably() => await Assert.That(Listed(Unstable(EquationFormat.Omml))).IsEmpty();

        [Test]
        public async Task StrictOmmlReadsWhatItWrites()
        {
            var strict = new EquationOptions { Strict = true };
            await Assert.That(Failures(e => Equation.TryParse(e.ToString(EquationFormat.Omml), EquationFormat.Omml, strict, out _) ? null : "rejected")).IsEmpty();
        }

        /// <summary>Reading is deterministic and does not depend on the equations read before.</summary>
        [Test]
        public async Task ReadingIsDeterministic()
        {
            var inputs = Corpus().Select(c => c.Input).ToList();
            var first = inputs.Select(i => Equation.Parse(i, EquationFormat.UnicodeMath).ToString()).ToList();
            inputs.Reverse();
            var second = inputs.Select(i => Equation.Parse(i, EquationFormat.UnicodeMath).ToString()).ToList();
            second.Reverse();
            await Assert.That(second).IsEquivalentTo(first, CollectionOrdering.Matching);
        }
    }
}
