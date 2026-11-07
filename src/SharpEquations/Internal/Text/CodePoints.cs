namespace SharpEquations.Internal.Text
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Text as Unicode scalar values (<see cref="Rune"/>, which <c>Rune.cs</c> provides on .NET Standard 2.0), never as
    /// UTF-16 units: a character outside the basic plane (𝑥, 𝔸, emoji) is one value. A lone surrogate reads as U+FFFD.
    /// </summary>
    internal static class CodePoints
    {
        public static IEnumerable<Rune> Of(string s)
        {
            foreach (var rune in s.EnumerateRunes())
                yield return rune;
        }

        /// <summary>Whether the text holds the rune.</summary>
        public static bool Contains(string s, Rune rune)
        {
            foreach (var r in s.EnumerateRunes())
            {
                if (r == rune)
                    return true;
            }
            return false;
        }

        /// <summary>The text as a single rune, if it is one.</summary>
        public static bool IsSingle(string s, out Rune rune)
        {
            rune = default;
            int count = 0;
            foreach (var r in s.EnumerateRunes())
            {
                if (++count > 1)
                    return false;
                rune = r;
            }
            return count == 1;
        }
    }
}
