namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SharpEquations.Internal.Text;

    /// <summary>
    /// Character helpers of UnicodeMathML (src/unicodemathml.js): math alphanumerics, function names, Unicode
    /// fractions and scripts. Strings are UTF-16, as in JavaScript.
    /// </summary>
    internal static class UnicodeMathText
    {
        public static readonly string[] MathStyles =
        {
            "mup", "mbfit", "mscr", "mbfscr", "mfrak", "Bbb", "mbffrak", "mitBbb",
            "mbfsans", "mitsans", "mbfitsans", "mbf", "mit", "msans", "mtt", "misol",
            "minit", "mtail", "mloop", "mstrc", "mrhnd", "mchan",
        };

        public const string DigitSuperscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        public const string DigitSubscripts = "₀₁₂₃₄₅₆₇₈₉";

        public static readonly (string Fraction, int Numerator, int Denominator)[] UnicodeFractions =
        {
            ("½", 1, 2), ("⅓", 1, 3), ("⅔", 2, 3), ("¼", 1, 4), ("¾", 3, 4), ("⅕", 1, 5),
            ("⅖", 2, 5), ("⅗", 3, 5), ("⅘", 4, 5), ("⅙", 1, 6), ("⅚", 5, 6), ("⅐", 1, 7),
            ("⅛", 1, 8), ("⅜", 3, 8), ("⅝", 5, 8), ("⅞", 7, 8), ("⅑", 1, 9), ("↉", 0, 3),
            ("⅒", 1, 10),
        };

        private static readonly string[] FunctionNames =
        {
            "Im", "Pr", "Re", "arg", "cos", "cot", "csc", "ctg", "deg", "det", "dim", "erf", "exp", "gcd", "hom",
            "inf", "ker", "lg", "lim", "log", "ln", "max", "min", "mod", "sec", "sin", "sup", "tan", "tg",
        };

        /// <summary>inRange(ch0, ch, ch1): ch a single code point between those of ch0 and ch1.</summary>
        public static bool InRange(string ch0, string ch, string ch1)
        {
            return ch != null && CodePoints.IsSingle(ch, out var c) && CodePoints.IsSingle(ch0, out var c0) && CodePoints.IsSingle(ch1, out var c1)
                && c.Value >= c0.Value && c.Value <= c1.Value;
        }

        public static bool IsAsciiDigit(string ch) => InRange("0", ch, "9");

        /// <summary>String.fromCodePoint.</summary>
        public static string FromCodePoint(int code) => char.ConvertFromUtf32(code);

        /// <summary>The value of the code point at index i, counted in code points; -1 out of range.</summary>
        public static int CodePointAt(string s, int i) => Js.CodeAt(s, i);

        public static bool IsFunctionName(string fn)
        {
            if (string.IsNullOrEmpty(fn))
                return false;
            if (Js.Len(fn) >= 4 && Js.Ch(fn, 0) == "a")
            {
                // remove the 'a' or 'arc' trigonometric prefix
                int i = Js.Sub(fn, 1, 3) == "rc" ? 3 : 1;
                fn = Js.Sub(fn, i);
            }
            if (Js.Len(fn) == 4 && Js.Ch(fn, 3) == "h")
                fn = Js.Sub(fn, 0, 3);
            return Array.IndexOf(FunctionNames, fn) >= 0;
        }

        public static string FoldMathItalic(int code)
        {
            if (code == 0x210E)
                return "h";

            if (code >= 0x1D434 && code <= 0x1D467)
            {
                code += 0x0041 - 0x1D434;
                if (code > 0x005A)
                    code += 0x0061 - 0x005A - 1;
            }
            else if (code >= 0x1D6FC && code <= 0x1D71B)
            {
                code += 0x03B1 - 0x1D6FC;
            }
            else
            {
                return "";
            }
            return FromCodePoint(code);
        }

        public static string FoldMathItalics(string chars)
        {
            var sb = new StringBuilder();
            foreach (var codePoint in CodePoints.Of(chars))
            {
                int code = codePoint.Value;
                sb.Append(code >= 0x2102 ? FoldMathItalic(code) : codePoint.ToString());
            }
            return sb.ToString();
        }

        public static string FoldSupDigit(string ch)
        {
            switch (ch)
            {
                case "¹": return "1";
                case "²": return "2";
                case "³": return "3";
            }
            if (InRange("⁰", ch, "⁹") && !InRange("ⁱ", ch, "⁳"))
                return FromCodePoint(CodePointAt(ch, 0) - 0x2040);
            return "";
        }

        public static string ItalicizeCharacter(string c)
        {
            return string.CompareOrdinal(c, "Α") < 0 || string.CompareOrdinal(c, "Ω") > 0 && c != "∇" ? GetMathAlphanumeric(c, "mit") : c;
        }

        /// <summary>Array.from(chars): the code points of a string, as strings.</summary>
        public static List<string> Characters(string chars) => CodePoints.Of(chars).Select(c => c.ToString()).ToList();

        public static string ItalicizeCharacters(string chars) => string.Concat(Characters(chars).Select(ItalicizeCharacter));

        public static string GetUnicodeFraction(string chNum, string chDenom)
        {
            if (Js.Len(chNum) == 1)
            {
                if (chDenom == "10" && chNum == "1")
                    return "⅒";
                if (Js.Len(chDenom) <= 2)
                {
                    foreach (var (fraction, numerator, denominator) in UnicodeFractions)
                    {
                        if (chNum == numerator.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            && chDenom == denominator.ToString(System.Globalization.CultureInfo.InvariantCulture))
                            return fraction;
                    }
                }
            }
            var result = new StringBuilder();
            foreach (var c in CodePoints.Of(chNum))
            {
                int n = c.Value - '0';
                if (n < 0 || n > 9)
                    return "";
                result.Append(DigitSuperscripts[n]);
            }
            result.Append('⁄');
            foreach (var c in CodePoints.Of(chDenom))
            {
                int n = c.Value - '0';
                if (n < 0 || n > 9)
                    return "";
                result.Append(DigitSubscripts[n]);
            }
            return result.ToString();
        }

        private static readonly int[] Abjad =
        {
            0, 1, -1, 21, 22, 2, 7, 23, 3, 24, 19, 6, 14, 20, 17, 25, 8,
            26, 15, 27, -1, -1, -1, -1, -1, -1, 16, 18, 10, 11, 12, 13, 4, 5, -1, 9,
        };
        private const string DottedChars = "ٮںڡٯ";
        private static readonly Dictionary<string, string> LetterlikeDoubleStruck = new Dictionary<string, string>
            { ["C"] = "ℂ", ["H"] = "ℍ", ["N"] = "ℕ", ["P"] = "ℙ", ["Q"] = "ℚ", ["R"] = "ℝ", ["Z"] = "ℤ" };
        private static readonly Dictionary<string, string> LetterlikeFraktur = new Dictionary<string, string>
            { ["C"] = "ℭ", ["H"] = "ℌ", ["I"] = "ℑ", ["R"] = "ℜ", ["Z"] = "ℨ" };
        private static readonly Dictionary<string, string> LetterlikeScript = new Dictionary<string, string>
            { ["B"] = "ℬ", ["E"] = "ℰ", ["F"] = "ℱ", ["H"] = "ℋ", ["I"] = "ℐ", ["L"] = "ℒ", ["M"] = "ℳ", ["R"] = "ℛ", ["e"] = "ℯ", ["g"] = "ℊ", ["o"] = "ℴ" };
        private static readonly Dictionary<string, string> MitBbb = new Dictionary<string, string>
            { ["D"] = "ⅅ", ["d"] = "ⅆ", ["e"] = "ⅇ", ["i"] = "ⅈ", ["j"] = "ⅉ" };
        private static readonly uint[] MissingCharMask = { 0xF5080169, 0x5569157B, 0xA1080869, 0xF0000000, 0xF0000000 };
        private static readonly Dictionary<string, int> OffsetsGr = new Dictionary<string, int>
            { ["∂"] = 51, ["∇"] = 25, ["ϴ"] = 17, ["ϵ"] = 52, ["ϑ"] = 53, ["ϰ"] = 54, ["ϕ"] = 55, ["ϱ"] = 56, ["ϖ"] = 57 };
        private static readonly string[] SetsAr = { "misol", "minit", "mtail", "mstrc", "mloop", "Bbb" };
        private static readonly string[] SetsDigit = { "mbf", "Bbb", "msans", "mbfsans", "mtt" };
        private static readonly string[] SetsEn = { "mbf", "mit", "mbfit", "mscr", "mbfscr", "mfrak", "Bbb", "mbffrak", "msans", "mbfsans", "mitsans", "mbfitsans", "mtt" };
        private static readonly string[] SetsGr = { "mbf", "mit", "mbfit", "mbfsans", "mbfitsans" };

        /// <summary>
        /// The math alphanumeric character of the unstyled character ch in the math style; ch itself when there is none.
        /// </summary>
        public static string GetMathAlphanumeric(string ch, string mathStyle)
        {
            if (string.IsNullOrEmpty(mathStyle) || mathStyle == "mup")
                return ch;
            if (ch == null)
                throw new InvalidOperationException("No character to style.");
            int code = Math.Max(0, Js.CodeAt(ch, 0));
            int n;

            if (code > 0x6BA && ch != "∂" && ch != "∇")
                return ch;

            if (string.CompareOrdinal(ch, "0") >= 0 && string.CompareOrdinal(ch, "9") <= 0)
            {
                code += 0x1D7CE - 0x30;
                n = Array.IndexOf(SetsDigit, mathStyle);
                return n != -1 ? FromCodePoint(code + n * 10) : ch;
            }

            if (code >= 'A' && code <= 'Z' || code >= 'a' && code <= 'z')
            {
                string varsel = "";
                if (mathStyle == "mchan" || mathStyle == "mrhnd")
                {
                    varsel = mathStyle == "mchan" ? "\uFE00" : "\uFE01";
                    mathStyle = "mscr";
                }
                string chT = null;
                switch (mathStyle)
                {
                    case "mit":
                        if (ch == "h")
                            return "ℎ";
                        break;
                    case "mfrak":
                        LetterlikeFraktur.TryGetValue(ch, out chT);
                        break;
                    case "mscr":
                        LetterlikeScript.TryGetValue(ch, out chT);
                        break;
                    case "Bbb":
                        LetterlikeDoubleStruck.TryGetValue(ch, out chT);
                        break;
                    case "mitBbb":
                        MitBbb.TryGetValue(ch, out chT);
                        break;
                }
                if (!string.IsNullOrEmpty(chT))
                    return chT + varsel;

                n = Array.IndexOf(SetsEn, mathStyle);
                if (n == -1)
                    return ch;
                code -= 0x41;
                if (code > 26)
                    code -= 6;
                return FromCodePoint(code + 52 * n + 0x1D400) + varsel;
            }

            if (string.CompareOrdinal(ch, "Α") >= 0 && string.CompareOrdinal(ch, "ϵ") <= 0 || ch == "∂" || ch == "∇")
            {
                if (mathStyle == "mbf")
                {
                    if (ch == "Ϝ")
                        return "𝟊";
                    if (ch == "ϝ")
                        return "𝟋";
                }
                n = Array.IndexOf(SetsGr, mathStyle);
                if (n == -1)
                    return ch;

                if (OffsetsGr.TryGetValue(ch, out int code0) && code0 != 0)
                {
                    code = code0;
                }
                else
                {
                    code -= 0x391;
                    if (code > 25)
                        code -= 6;
                }
                return FromCodePoint(code + 58 * n + 0x1D6A8);
            }
            if (code < 0x627)
                return ch == "ı" ? "𝚤" : ch == "ȷ" ? "𝚥" : ch;

            n = Array.IndexOf(SetsAr, mathStyle);
            if (n == -1)
                return ch;

            if (code <= 0x64A)
            {
                code = Abjad[code - 0x0627];
                if (code == -1)
                    return ch;
            }
            else
            {
                code = Characters(DottedChars).IndexOf(ch);
                if (code == -1)
                    return ch;
                code += 28;
            }
            if (mathStyle == "misol")
            {
                if (code == 4)
                    n = 1;
            }
            else if (((1u << code) & MissingCharMask[n - 1]) != 0)
            {
                return ch;
            }
            return FromCodePoint(32 * n + code + 0x1EE00);
        }
    }
}
