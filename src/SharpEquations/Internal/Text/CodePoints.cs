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

        /// <summary>The number of runes of the text.</summary>
        public static int Count(string s)
        {
            int count = s.Length;
            for (int i = 1; i < s.Length; i++)
            {
                if (char.IsLowSurrogate(s[i]) && char.IsHighSurrogate(s[i - 1]))
                    count--;
            }
            return count;
        }

        /// <summary>The number of UTF-16 units of the rune at an index in UTF-16 units.</summary>
        public static int LengthAt(string s, int at) =>
            char.IsHighSurrogate(s[at]) && at + 1 < s.Length && char.IsLowSurrogate(s[at + 1]) ? 2 : 1;

        /// <summary>The index in UTF-16 units of the rune at an index in runes: the length of the text for the index after the last rune, -1 beyond.</summary>
        public static int Offset(string s, int index)
        {
            int at = 0;
            while (at < s.Length)
            {
                if (index-- == 0)
                    return at;
                at += LengthAt(s, at);
            }
            return index == 0 ? at : -1;
        }

        /// <summary>The value of the rune at an index in UTF-16 units: U+FFFD for a lone surrogate.</summary>
        public static int ValueAt(string s, int at)
        {
            var c = s[at];
            if (!char.IsSurrogate(c))
                return c;
            return LengthAt(s, at) == 2 ? char.ConvertToUtf32(c, s[at + 1]) : 0xFFFD;
        }

        private static readonly string[] Ascii = AsciiStrings();

        private static string[] AsciiStrings()
        {
            var strings = new string[128];
            for (int i = 0; i < strings.Length; i++)
                strings[i] = ((char)i).ToString();
            return strings;
        }

        /// <summary>The rune at an index in UTF-16 units, as a string: U+FFFD for a lone surrogate.</summary>
        public static string TextAt(string s, int at)
        {
            var c = s[at];
            if (c < 128)
                return Ascii[c];
            if (!char.IsSurrogate(c))
                return c.ToString();
            return LengthAt(s, at) == 2 ? s.Substring(at, 2) : "\uFFFD";
        }

        /// <summary>Whether the text holds a surrogate without its pair, which reads as U+FFFD.</summary>
        public static bool HasLoneSurrogate(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsSurrogate(s[i]))
                    continue;
                if (LengthAt(s, i) == 1)
                    return true;
                i++;
            }
            return false;
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
