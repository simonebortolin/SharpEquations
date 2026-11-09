#nullable enable

namespace SharpEquations.Tests.LaTeX
{
    using System.Collections.Generic;
    using System.Security;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Internal.LaTeX;

    /// <summary>Every command the standard LaTeX writer writes for a character is one the LaTeX reader reads.</summary>
    public class LaTeXSymbolsTests
    {
        private static readonly EquationOptions Strict = new EquationOptions { Strict = true };

        public static IEnumerable<string> Characters() => LaTeXSymbols.CharacterCommands.Keys;

        [Test]
        [MethodDataSource(nameof(Characters))]
        public async Task CharacterWrittenAsLaTeXReadsBack(string character)
        {
            var element = char.IsLetter(character, 0) ? "mi" : "mo";
            var equation = Equation.Parse("<math><mi>a</mi><" + element + ">" + SecurityElement.Escape(character) + "</" + element + "><mi>b</mi></math>", EquationFormat.MathML);
            var latex = equation.ToString(EquationFormat.LaTeX);

            // read back as the character (or one written as the same command: \circ for ° and ∘), not as the text "\geqslantb";
            // what UnicodeMath makes of the character (an identifier or an operator) is lost only the first time
            var read = Equation.Parse(latex, EquationFormat.LaTeX, Strict);
            await Assert.That(read.ToString(EquationFormat.LaTeX)).IsEqualTo(latex);
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX, Strict)).IsEqualTo(read);
            await Assert.That(Equation.Parse(read.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX, Strict)).IsEqualTo(read);
        }
    }
}
