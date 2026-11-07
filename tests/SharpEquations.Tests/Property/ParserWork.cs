#nullable enable

namespace SharpEquations.Tests.Property
{
    using System;
    using SharpEquations.Internal.UnicodeMath;

    /// <summary>
    /// The work of the UnicodeMath grammar, as the entities it tries (LaTeX is read through it too): a measure that is the
    /// same on every machine, unlike the time a parse takes, and grows exponentially when an alternative parses again
    /// what another one parsed.
    /// </summary>
    internal static class ParserWork
    {
        /// <summary>The entities the grammar tries while the action runs, on this thread.</summary>
        public static long Of(Action action)
        {
            var before = UnicodeMathGrammarWork.Steps;
            action();
            return UnicodeMathGrammarWork.Steps - before;
        }

        /// <summary>The work a source of this length may take: quadratic in its code points, far below exponential.</summary>
        public static long Budget(string source)
        {
            long n = CodePointCount(source);
            return 2000 + 20 * n * n;
        }

        private static int CodePointCount(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
                count++;
            return count;
        }
    }
}
