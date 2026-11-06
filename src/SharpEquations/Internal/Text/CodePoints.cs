// A Unicode scalar value: System.Text.Rune on .NET Core 3.0 and later, its int value on .NET Framework and
// .NET Standard, which do not have Rune
#if NETCOREAPP3_0_OR_GREATER
global using CodePoint = System.Text.Rune;
#else
global using CodePoint = System.Int32;
#endif

namespace SharpEquations.Internal.Text
{
    using System.Collections.Generic;

    /// <summary>
    /// Text as Unicode scalar values (<c>CodePoint</c>), never as UTF-16 units: a character outside the basic plane
    /// (𝑥, 𝔸, emoji) is one value. A lone surrogate reads as U+FFFD.
    /// </summary>
    internal static class CodePoints
    {
        public static IEnumerable<CodePoint> Of(string s)
        {
#if NETCOREAPP3_0_OR_GREATER
            foreach (var rune in s.EnumerateRunes())
                yield return rune;
#else
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    yield return char.ConvertToUtf32(s[i], s[++i]);
                else
                    yield return char.IsSurrogate(s[i]) ? 0xFFFD : s[i];
            }
#endif
        }

        public static CodePoint From(int value)
        {
#if NETCOREAPP3_0_OR_GREATER
            return new CodePoint(value);
#else
            return value;
#endif
        }

        public static int Value(CodePoint codePoint)
        {
#if NETCOREAPP3_0_OR_GREATER
            return codePoint.Value;
#else
            return codePoint;
#endif
        }

        public static bool IsBmp(CodePoint codePoint) => Value(codePoint) <= 0xFFFF;

        public static string ToString(CodePoint codePoint) => char.ConvertFromUtf32(Value(codePoint));

        /// <summary>The text as a single code point, if it is one.</summary>
        public static bool IsSingle(string s, out CodePoint codePoint)
        {
            codePoint = default;
            int count = 0;
            foreach (var c in Of(s))
            {
                if (++count > 1)
                    return false;
                codePoint = c;
            }
            return count == 1;
        }
    }
}
