#nullable enable

namespace SharpEquations.Tests.Property
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// LaTeX and UnicodeMath that no one wrote: the corpus and the tests mutated at random (code points deleted, swapped
    /// or repeated, the tokens of the format and pieces of other equations inserted) are read with a bounded work of the
    /// grammar (<see cref="ParserWork"/>) to an equation or a FormatException, never another exception, and what is read
    /// is written in every format and read back from its UnicodeMath and its OMML. The seeds are fixed, so a failure
    /// repeats: the message holds the input.
    /// </summary>
    public class FuzzTests
    {
        private const int Cases = 1000;

        private static readonly string[] LaTeXTokens =
        {
            "{", "}", "[", "]", "(", ")", "|", "^", "_", "&", "'", "#", "#2", "$", " ", "\\", "\\\\", "\\\\[2pt]", "\\[", "\\]",
            "\\,", "\\!", "\\|", "\\frac", "\\over", "\\sqrt", "\\left(", "\\right)", "\\left.", "\\right.", "\\middle|",
            "\\begin{matrix}", "\\end{matrix}", "\\begin{cases}", "\\end{cases}", "\\begin{array}{c}", "\\end{array}",
            "\\begin{aligned}", "\\end{aligned}", "\\text{", "\\mathrm{", "\\mathbf", "\\operatorname", "\\def\\a{",
            "\\newcommand{\\b}[2]{#1", "\\a", "\\b", "\\overset", "\\underbrace", "\\color{red}", "\\sum", "\\int", "\\lim",
            "\\binom", "\\hat", "\\vec", "\\xrightarrow", "\\label{", "\\prime", "\\boxed", "\\phantom", "\\not", "\\foo",
            "𝑥", "​", "⁡", "",
        };

        private static readonly string[] UnicodeMathTokens =
        {
            "(", ")", "[", "]", "{", "}", "〖", "〗", "├", "┤", "|", "‖", "│", "∣", "^", "_", "/", "∕", "⊘", "¦", "⒞", "■", "█",
            "@", "&", "√", "∛", "▒", "┬", "┴", "∑", "∫", "〈", "〉", "\\", "\\frac", "\\sqrt", "\\matrix", "\\cases", "\\a",
            "ⓜ", "#", "⒨", "Ⓒ", "ⓐ", "Ⓐ", "¯", "̂", "̸", "⁡", " ", "\"", "\"text", "▭", "⬌", "→", "─",
            "#(1)", ":", "!", "′", " ", "\n", "𝑥", "​", "", "￿",
        };

        private static List<string> CodePoints(string text)
        {
            var codePoints = new List<string>();
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
                codePoints.Add(text.Substring(i, char.IsSurrogatePair(text, i) ? 2 : 1));
            return codePoints;
        }

        /// <summary>count sources, each one of the seeds changed one to three times.</summary>
        private static IEnumerable<string> Mutations(List<string> seeds, string[] tokens, int seed, int count)
        {
            var random = new Random(seed);
            for (int n = 0; n < count; n++)
            {
                var text = CodePoints(seeds[random.Next(seeds.Count)]);
                for (int changes = random.Next(1, 4); changes > 0; changes--)
                {
                    var at = random.Next(text.Count + 1);
                    switch (random.Next(5))
                    {
                        case 0:
                            if (at < text.Count)
                                text.RemoveAt(at);
                            break;
                        case 1:
                            text.Insert(at, tokens[random.Next(tokens.Length)]);
                            break;
                        case 2:
                            if (text.Count > 1)
                            {
                                var from = random.Next(text.Count);
                                text.InsertRange(at, text.GetRange(from, random.Next(1, Math.Min(8, text.Count - from) + 1)));
                            }
                            break;
                        case 3:
                            var other = CodePoints(seeds[random.Next(seeds.Count)]);
                            text.InsertRange(at, other.Skip(random.Next(other.Count)).Take(random.Next(1, 10)));
                            break;
                        default:
                            if (text.Count > 1)
                            {
                                int a = random.Next(text.Count), b = random.Next(text.Count);
                                (text[a], text[b]) = (text[b], text[a]);
                            }
                            break;
                    }
                }
                yield return string.Concat(text);
            }
        }

        private static string? Check(string source, EquationFormat format)
        {
            try
            {
                var equation = Equation.Parse(source, format);
                var unicodeMath = equation.ToString(EquationFormat.UnicodeMath);
                _ = equation.ToString(EquationFormat.LaTeX) + equation.ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word });
                Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
                Equation.Parse(equation.ToOmml(), EquationFormat.Omml);
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        internal static List<string> Failures(IEnumerable<string> sources, EquationFormat format)
        {
            var failures = new List<string>();
            foreach (var source in sources.Distinct())
            {
                string? result = null;
                var work = ParserWork.Of(() => result = Check(source, format));
                if (result != null)
                    failures.Add(Quote(source) + "\n  " + result);
                else if (work > ParserWork.Budget(source))
                    failures.Add(Quote(source) + "\n  takes " + work + " steps of the grammar, more than " + ParserWork.Budget(source));
            }
            return failures;
        }

        private static string Quote(string source) =>
            string.Concat(CodePoints(source).Select(c => c.Length == 1 && (char.IsControl(c[0]) || char.IsWhiteSpace(c[0]) && c != " " || c[0] >= '' && c[0] <= '' || c[0] == '￿' || char.GetUnicodeCategory(c[0]) == System.Globalization.UnicodeCategory.Format)
                ? $"\\u{(int)c[0]:X4}" : c));

        private static bool IsLaTeX(string input) => input.StartsWith("$") || input.StartsWith("\\[");

        [Test]
        [Arguments(1)]
        [Arguments(2)]
        [Arguments(3)]
        [Arguments(4)]
        public async Task ReadsMutatedLaTeX(int seed)
        {
            var corpus = UnicodeMathCorpusTests.Examples().Select(e => e.input).Where(IsLaTeX);
            var seeds = TruncatedInputTests.LaTeX.Concat(corpus).ToList();
            await Assert.That(Failures(Mutations(seeds, LaTeXTokens, seed, Cases), EquationFormat.LaTeX)).IsEmpty();
        }

        [Test]
        [Arguments(1)]
        [Arguments(2)]
        [Arguments(3)]
        [Arguments(4)]
        public async Task ReadsMutatedUnicodeMath(int seed)
        {
            var seeds = UnicodeMathCorpusTests.Examples().Select(e => e.input).Where(e => !IsLaTeX(e) && !e.StartsWith("\\(")).ToList();
            await Assert.That(Failures(Mutations(seeds, UnicodeMathTokens, seed, Cases), EquationFormat.UnicodeMath)).IsEmpty();
        }

        private static string Nest(string open, string close, int depth) =>
            string.Concat(Enumerable.Repeat(open, depth)) + "x" + string.Concat(Enumerable.Repeat(close, depth));

        /// <summary>Constructs nested a hundred deep, as no one writes them, are read and written.</summary>
        [Test]
        [Arguments("{", "}")]
        [Arguments("(", ")")]
        [Arguments(@"\sqrt{", "}")]
        [Arguments(@"\left(", @"\right)")]
        [Arguments("x^{", "}")]
        [Arguments("x_{", "}")]
        [Arguments(@"\text{", "}")]
        public async Task ReadsDeeplyNestedLaTeX(string open, string close)
        {
            await Assert.That(Failures(new[] { Nest(open, close, 100) }, EquationFormat.LaTeX)).IsEmpty();
        }

        [Test]
        [Arguments("(", ")")]
        [Arguments("[", "]")]
        [Arguments("〖", "〗")]
        [Arguments("√(", ")")]
        [Arguments("x^(", ")")]
        [Arguments("x_(", ")")]
        [Arguments("▭(", ")")]
        public async Task ReadsDeeplyNestedUnicodeMath(string open, string close)
        {
            await Assert.That(Failures(new[] { Nest(open, close, 100) }, EquationFormat.UnicodeMath)).IsEmpty();
        }

        [Test]
        [Arguments("a^{bc}^d", EquationFormat.UnicodeMath)]
        [Arguments(@"\newcommand{\h}[{\h}]{x}\h", EquationFormat.LaTeX)]
        [Arguments("〖", EquationFormat.UnicodeMath)]
        public async Task ReadsPreviouslyCrashingInputs(string source, EquationFormat format)
        {
            await Assert.That(Failures(new[] { source }, format)).IsEmpty();
        }

        /// <summary>A \begin not followed by { leaves an unclosed bracket, an empty operand that UnicodeMath writes as 〖〗.</summary>
        [Test]
        [Arguments(@"\begin\not{eqution\label{eq1}a\end{equation}")]
        [Arguments(@"\[(𝑎+𝑏)^𝑛=∑_{𝑘=0}^𝑛\binom\begin{matrix}{𝑛}{𝑘}𝑎a^2+b^2}^𝑘𝑏^{𝑛−𝑘}\]")]
        public async Task ReadsBeginWithoutBrace(string latex)
        {
            await Assert.That(Failures(new[] { latex }, EquationFormat.LaTeX)).IsEmpty();
        }

        /// <summary>
        /// The UnicodeMath grammar backtracks without memoization: these nestings, which an alternative would parse
        /// again at each level (the operand of a fraction, an unclosed bracket, a bracket with a diacritic), are parsed
        /// once, so twice the depth takes at most about four times the work (quadratic, where exponential would take
        /// thousands of times more).
        /// </summary>
        private static async Task AssertWorkIsPolynomial(string open, string close, EquationFormat format)
        {
            long Work(int depth) => ParserWork.Of(() => Check(Nest(open, close, depth), format));
            long half = Work(15), full = Work(30);
            // the grammar counts its work (UnicodeMathGrammarWork.Step in the entity rule): without it, nothing is measured
            await Assert.That(half).IsGreaterThan(0);
            await Assert.That(full).IsLessThanOrEqualTo(5 * half).Because($"{half} steps at depth 15, {full} at depth 30");
            await Assert.That(Failures(new[] { Nest(open, close, 30) }, format)).IsEmpty();
        }

        [Test]
        [Arguments("1/(", ")")]
        [Arguments("(", "")]
        public async Task ReadsDeeplyNestedUnicodeMathInLinearTime(string open, string close)
        {
            await AssertWorkIsPolynomial(open, close, EquationFormat.UnicodeMath);
        }

        [Test]
        [Arguments(@"\hat{", "}")]
        [Arguments(@"\frac{1}{", "}")]
        [Arguments("{", "")]
        public async Task ReadsDeeplyNestedLaTeXInLinearTime(string open, string close)
        {
            await AssertWorkIsPolynomial(open, close, EquationFormat.LaTeX);
        }

        /// <summary>The MathML of a nesting deeper than OMML holds is refused, not a stack overflow.</summary>
        [Test]
        public async Task RefusesMathMLNestedDeeperThanOmmlHolds()
        {
            var math = new XElement(MathMLNamespaces.MathML + "mi", "x");
            for (int i = 0; i < 2000; i++)
                math = new XElement(MathMLNamespaces.MathML + "msqrt", math);
            var equation = Equation.Parse(new XElement(MathMLNamespaces.MathML + "math", math), EquationFormat.MathML);
            await Assert.That(() => equation.ToOmml()).ThrowsException();
        }
    }
}
