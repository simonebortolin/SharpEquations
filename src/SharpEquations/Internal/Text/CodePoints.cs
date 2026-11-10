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

        /// <summary>Whether a code is a character XML 1.0 allows: not a control other than tab, line feed and return, a surrogate, U+FFFE or U+FFFF.</summary>
        public static bool IsXml(int code) =>
            code is 0x9 or 0xA or 0xD or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;

        /// <summary>Whether the text holds only characters XML 1.0 allows, and no surrogate without its pair.</summary>
        public static bool IsXml(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    i++;
                else if (char.IsSurrogate(s[i]) || !IsXml(s[i]))
                    return false;
            }
            return true;
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
