#nullable enable

namespace SharpEquations.Tests.Property
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using SharpEquations;

    /// <summary>
    /// Text that is not a whole equation, as a user types it or a document cuts it: every prefix of the LaTeX and
    /// UnicodeMath of the corpus and of the tests is read with a bounded work of the grammar (<see cref="ParserWork"/>),
    /// to an equation or a FormatException, never another exception.
    /// </summary>
    public class TruncatedInputTests
    {

        private static IEnumerable<string> Prefixes(string text)
        {
            var codePoints = new List<string>();
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
                codePoints.Add(text.Substring(i, char.IsSurrogatePair(text, i) ? 2 : 1));
            for (int n = 1; n < codePoints.Count; n++)
                yield return string.Concat(codePoints.Take(n));
        }

        private static string? Check(string source, EquationFormat format)
        {
            try
            {
                var equation = Equation.Parse(source, format);
                _ = equation.ToString(EquationFormat.LaTeX) + equation.ToString(EquationFormat.UnicodeMath) + equation.ToString(EquationFormat.Omml);
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

        private static List<string> Failures(IEnumerable<string> sources, EquationFormat format)
        {
            var failures = new List<string>();
            foreach (var source in sources.Distinct())
            {
                string? result = null;
                var work = ParserWork.Of(() => result = Check(source, format));
                if (result != null)
                    failures.Add(source + "\n  " + result);
                else if (work > ParserWork.Budget(source))
                    failures.Add(source + "\n  takes " + work + " steps of the grammar, more than " + ParserWork.Budget(source));
            }
            return failures;
        }

        internal static readonly string[] LaTeX =
        {
            @"\frac{a^2+b^2}{c}", @"\sqrt[3]{x+1}", @"\left(\frac{1}{2}\right)", @"\begin{pmatrix}a&b\\c&d\end{pmatrix}",
            @"\begin{cases}1&x>0\\0&\text{otherwise}\end{cases}", @"\sum_{i=1}^{n} i^2", @"\int_0^1 f(x)\,dx", @"\lim_{x\to0}\frac{\sin x}{x}",
            @"\overset{def}{=}", @"\underset{x}{\lim}", @"\text{if $x>0$}", @"\mathbf{b\times c}", @"\color{red}{x}+y", @"x_\text{ab}",
            @"\newcommand{\pd}[2]{\frac{\partial #1}{\partial #2}}\pd{f}{x}", @"\def\R{\mathbb{R}}x\in\R", @"\begin{equation}\label{eq1}a\end{equation}",
            @"\left\langle a \middle| b \right\rangle", @"\begin{array}{ll}1&2\end{array}", @"\binom{n}{k}", @"\overbrace{a+b}^{n}",
            @"\operatorname{rank}A", @"\hat{x}\vec{AB}", @"\left.\frac{d}{dx}\right|_{x=0}", @"{a \over b}", @"\xrightarrow{f}",
        };

        [Test]
        public async Task ReadsEveryPrefixOfLaTeX()
        {
            var corpus = UnicodeMathCorpusTests.Examples().Where(e => e.input.StartsWith("$") || e.input.StartsWith("\\[")).Select(e => e.input);
            var failures = Failures(LaTeX.Concat(corpus).SelectMany(Prefixes), EquationFormat.LaTeX);
            await Assert.That(failures).IsEmpty();
        }

        [Test]
        public async Task ReadsEveryPrefixOfUnicodeMath()
        {
            // every third example of the corpus, to keep the test short: they share most constructs
            var corpus = UnicodeMathCorpusTests.Examples().Where((e, i) => i % 3 == 0 && !e.input.StartsWith("$") && !e.input.StartsWith("\\[")).Select(e => e.input);
            var failures = Failures(corpus.SelectMany(Prefixes), EquationFormat.UnicodeMath);
            await Assert.That(failures).IsEmpty();
        }

        [Test]
        public async Task ReadsEveryPrefixOfMathML()
        {
            const string mathML = "<math><mfrac><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><msqrt><mn>2</mn></msqrt></mfrac></math>";
            await Assert.That(Failures(Prefixes(mathML), EquationFormat.MathML)).IsEmpty();
        }
    }
}
