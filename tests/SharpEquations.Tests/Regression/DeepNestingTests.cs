#nullable enable

namespace SharpEquations.Tests.Regression
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpEquations;

    /// <summary>
    /// Equations nested deeply enough to overflow the stack, which ended the process (issue 69): they are rejected with
    /// an exception that can be caught, on a thread with the 1 MB stack of Windows.
    /// </summary>
    public class DeepNestingTests
    {
        private const int Stack = 1024 * 1024;

        private static string Nest(string open, string inner, string close, int depth) =>
            string.Concat(Enumerable.Repeat(open, depth)) + inner + string.Concat(Enumerable.Repeat(close, depth));

        private static Exception? OnThread(Action action)
        {
            Exception? thrown = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            }, Stack);
            thread.Start();
            thread.Join();
            return thrown;
        }

        /// <summary>
        /// The equations of the issue: near the limit, whether they are read or refused depends on the runtime and its
        /// frames, but they never end the process.
        /// </summary>
        [Test]
        [Arguments(@"\left(", "x", @"\right)", 300)]
        [Arguments(@"\frac{1}{", "x", "}", 500)]
        [Arguments(@"\sqrt{", "x", "}", 1000)]
        [Arguments("x_{", "x", "}", 1000)]
        [Arguments("{", "x", "}", 1200)]
        [Arguments("", "x", "^x", 600)]
        [Arguments("", "x", "^x", 800)]
        [Arguments("", "x", "_x", 600)]
        [Arguments("", "x", "_x", 800)]
        [Arguments("", "x", @"\sqrt", 600)]
        [Arguments("", "x", @"\sqrt", 800)]
        public async Task ReadsOrRejectsTheEquationsOfTheIssue(string open, string inner, string close, int depth)
        {
            var latex = Nest(open, inner, close, depth);
            foreach (var strict in new[] { false, true })
            {
                var thrown = OnThread(() => Equation.Parse(latex, EquationFormat.LaTeX, new EquationOptions { Strict = strict }).ToOmml());
                if (thrown is not null and not FormatException)
                    await Assert.That(thrown).IsTypeOf<InsufficientExecutionStackException>();
            }
        }

        [Test]
        [Arguments(@"\left(", @"\right)", 5000)]
        [Arguments(@"\frac{1}{", "}", 1000)]
        [Arguments(@"\frac{1}{", "}", 5000)]
        [Arguments(@"\sqrt{", "}", 1000)]
        [Arguments("x_{", "}", 1000)]
        [Arguments("{", "}", 5000)]
        public async Task RejectsDeepLaTeX(string open, string close, int depth)
        {
            var latex = Nest(open, "x", close, depth);
            var thrown = OnThread(() => Equation.Parse(latex, EquationFormat.LaTeX).ToOmml());
            await Assert.That(thrown).IsTypeOf<FormatException>();
            var parsed = true;
            OnThread(() => parsed = Equation.TryParse(latex, EquationFormat.LaTeX, out _));
            await Assert.That(parsed).IsFalse();
        }

        [Test]
        [Arguments("^x")]
        [Arguments("_x")]
        [Arguments(@"\sqrt")]
        public async Task RejectsLongLaTeXChains(string link)
        {
            // no braces: the chain of scripts or commands nests what it builds
            var latex = "x" + string.Concat(Enumerable.Repeat(link, 5000));
            var thrown = OnThread(() => Equation.Parse(latex, EquationFormat.LaTeX, new EquationOptions { Strict = true }));
            await Assert.That(thrown).IsTypeOf<FormatException>();
            await Assert.That(thrown!.Message).DoesNotContain("UnicodeMath");
        }

        [Test]
        [Arguments("(", ")", 5000)]
        [Arguments("√(", ")", 5000)]
        [Arguments("1/(", ")", 1000)]
        [Arguments("x_(", ")", 5000)]
        [Arguments("├(", "┤)", 5000)]
        public async Task RejectsDeepUnicodeMath(string open, string close, int depth)
        {
            var thrown = OnThread(() => Equation.Parse(Nest(open, "x", close, depth), EquationFormat.UnicodeMath).ToOmml());
            await Assert.That(thrown).IsTypeOf<FormatException>();
        }

        [Test]
        [Arguments("<mrow>", "</mrow>", 1000)]
        [Arguments("<mfrac><mn>1</mn>", "</mfrac>", 1000)]
        [Arguments("<msqrt>", "</msqrt>", 20000)]
        public async Task RejectsDeepMathML(string open, string close, int depth)
        {
            var mathML = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\">" + Nest(open, "<mi>x</mi>", close, depth) + "</math>";
            var thrown = OnThread(() => Equation.Parse(mathML, EquationFormat.MathML));
            await Assert.That(thrown).IsTypeOf<FormatException>().And.HasMessageEqualTo("The equation is nested too deeply.");
        }

        // two levels of XML for each: deeper than MaxDepth whatever the stack
        [Test]
        [Arguments("<m:d><m:e>", "</m:e></m:d>", 1000)]
        [Arguments("<m:d><m:e>", "</m:e></m:d>", 20000)]
        [Arguments("<m:f><m:num><m:r><m:t>1</m:t></m:r></m:num><m:den>", "</m:den></m:f>", 400)]
        [Arguments("<m:rad><m:deg/><m:e>", "</m:e></m:rad>", 400)]
        [Arguments("<m:sSub><m:e><m:r><m:t>x</m:t></m:r></m:e><m:sub>", "</m:sub></m:sSub>", 400)]
        [Arguments("<m:nary><m:sub/><m:sup/><m:e>", "</m:e></m:nary>", 300)]
        public async Task RejectsDeepOmml(string open, string close, int depth)
        {
            var omml = "<m:oMath xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\">"
                + Nest(open, "<m:r><m:t>x</m:t></m:r>", close, depth) + "</m:oMath>";
            var thrown = OnThread(() => Equation.Parse(omml, EquationFormat.Omml));
            await Assert.That(thrown).IsTypeOf<FormatException>().And.HasMessageEqualTo("The equation is nested too deeply.");
        }

        [Test]
        [Arguments(EquationFormat.MathML)]
        [Arguments(EquationFormat.Omml)]
        [Arguments(EquationFormat.UnicodeMath)]
        [Arguments(EquationFormat.LaTeX)]
        public async Task WritesOrRejectsDeepEquations(EquationFormat format)
        {
            // read within the limit, but deeper than some writers can recurse on a 1 MB stack
            var mathML = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\">" + Nest("<mfrac><mn>1</mn>", "<mi>x</mi>", "</mfrac>", 400) + "</math>";
            var equation = Equation.Parse(mathML, EquationFormat.MathML);
            var thrown = OnThread(() => equation.ToString(format));
            if (thrown is not null)
                await Assert.That(thrown).IsTypeOf<InsufficientExecutionStackException>().And.HasMessageEqualTo("The equation is nested too deeply.");
        }
    }
}
