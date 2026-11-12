#nullable enable

namespace SharpEquations.Tests.PublicApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;

    /// <summary>The commands an editor offers to complete what is typed, which the parsers read.</summary>
    public class EquationCommandsTests
    {
        [Test]
        public async Task ListsAreLarge()
        {
            await Assert.That(EquationCommands.Get(EquationFormat.UnicodeMath).Count).IsGreaterThan(500);
            await Assert.That(EquationCommands.Get(EquationFormat.LaTeX).Count).IsGreaterThan(300);
            await Assert.That(() => EquationCommands.Get(EquationFormat.MathML)).Throws<ArgumentException>();
        }

        [Test]
        public async Task Completion()
        {
            var names = EquationCommands.Complete("\\al", EquationFormat.UnicodeMath).Select(c => c.Name).ToList();
            await Assert.That(names).Contains("alpha");
            await Assert.That(names).Contains("aleph");
            await Assert.That(EquationCommands.Complete("alpha", EquationFormat.LaTeX)[0].Symbol).IsEqualTo("α");
            var frac = EquationCommands.Complete("frac", EquationFormat.LaTeX)[0];
            await Assert.That(frac.Text).IsEqualTo("\\frac{}{}");
            await Assert.That(EquationCommands.Complete("sum", EquationFormat.UnicodeMath)[0].Symbol).IsEqualTo("∑");
        }

        /// <summary>The structures of LaTeX are read as commands, not left as their text.</summary>
        [Test]
        public async Task LaTeXStructuresAreRead()
        {
            var failures = new List<string>();
            foreach (var command in EquationCommands.Get(EquationFormat.LaTeX).Where(c => c.Symbol.Length == 0 && c.Name is not ("left" or "right" or "middle")))
            {
                var text = "\\" + command.Name + (command.Arguments == 0 ? " x" : string.Concat(Enumerable.Repeat("{x}", command.Arguments)));
                if (!Equation.TryParse(text, EquationFormat.LaTeX, out var equation) || equation!.ToString().Contains("\\" + command.Name, StringComparison.Ordinal))
                    failures.Add(text);
            }
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>
        /// A command with a symbol reads as its symbol, as Word's math autocorrect replaces it: but for a few, which stand
        /// for more than a character in UnicodeMath (the examples \\quadratic or \\Fourier, the functions \\det or \\arg)
        /// or that LaTeX reads as another symbol.
        /// </summary>
        [Test]
        [Arguments(EquationFormat.UnicodeMath, 97)]
        [Arguments(EquationFormat.LaTeX, 95)]
        public async Task CommandsAreTheirSymbols(EquationFormat format, int percent)
        {
            var commands = EquationCommands.Get(format).Where(c => c.Symbol.Length > 0).ToList();
            var same = commands.Count(c =>
            {
                var read = Equation.TryParse("a" + c.Text + " b", format, out var typed);
                return read == Equation.TryParse("a" + c.Symbol + "b", EquationFormat.UnicodeMath, out var symbol) && typed == symbol;
            });
            await Assert.That(same * 100).IsGreaterThanOrEqualTo(commands.Count * percent);
        }
    }
}
