namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using SharpEquations.Internal.Text;
    using static UnicodeMathText;

    /// <summary>
    /// What UnicodeMathML (src/unicodemathml.js) does to the input before and after the grammar: control words
    /// are resolved, and the characters outside the basic plane that the grammar names (math alphanumerics,
    /// emoji, Arabic math letters) are mapped to the private use area and back.
    /// </summary>
    internal static class UnicodeMathInput
    {
        // a quoted text (kept as it is, with its escaped quotes), an escaped quote, or a control word
        private static readonly Regex ControlWord = new Regex(@"""(?:\\""|[^""])*""|\\""|\\([A-Za-z0-9]+) ?", RegexOptions.CultureInvariant);

        /// <summary>
        /// The bar of a set written with braces, {𝑥|𝑥>0}, as the separator │: a bar alone would open an absolute
        /// value that the closing brace ends. Only a single bar directly between the braces is one; quoted text and
        /// escaped characters are left as they are.
        /// </summary>
        public static string SetBuilderBars(string unicodemath)
        {
            var chars = Characters(unicodemath);
            var frames = new Stack<(string Open, List<int> Bars)>();
            var separators = new List<int>();
            for (int i = 0; i < chars.Count; i++)
            {
                var ch = chars[i];
                if (ch == "\\")
                {
                    i++;
                    continue;
                }
                if (ch == "\"")
                {
                    for (i++; i < chars.Count && chars[i] != "\""; i++)
                    {
                        if (chars[i] == "\\")
                            i++;
                    }
                    continue;
                }
                if (ch == "(" || ch == "[" || ch == "{" || ch == "⟨" || ch == "〖")
                {
                    frames.Push((ch, new List<int>()));
                }
                else if (ch == ")" || ch == "]" || ch == "}" || ch == "⟩" || ch == "〗")
                {
                    if (frames.Count == 0)
                        continue;
                    var (open, bars) = frames.Pop();
                    if (open == "{" && ch == "}" && bars.Count == 1)
                        separators.Add(bars[0]);
                }
                else if (ch == "|" && frames.Count > 0)
                {
                    frames.Peek().Bars.Add(i);
                }
            }
            if (separators.Count == 0)
                return unicodemath;
            foreach (var i in separators)
                chars[i] = "│";
            return string.Concat(chars);
        }

        /// <summary>
        /// Closing brackets without an opening one escaped (𝑎\)), and opening ones that the end of their group 〖…〗 would
        /// close; the brackets after ├ and ┤ are left as they are. UnicodeMath reads no closing bracket alone.
        /// </summary>
        public static string EscapeUnpairedClosers(string unicodemath)
        {
            var chars = Characters(unicodemath);
            var opens = new Stack<(string Open, int At)>();
            var escaped = new List<int>();
            for (int i = 0; i < chars.Count; i++)
            {
                var ch = chars[i];
                if (ch == "\\" || ch == "├" || ch == "┤")
                {
                    i++;
                    continue;
                }
                if (ch == "\"")
                {
                    for (i++; i < chars.Count && chars[i] != "\""; i++)
                    {
                        if (chars[i] == "\\")
                            i++;
                    }
                    continue;
                }
                // the fractions that the reader of LaTeX writes, ⍁𝑎&𝑏〗, end as a group does
                if (ch == "〖" || ch == "⍁" || ch == "⒝")
                {
                    opens.Push(("〖", i));
                }
                else if (OpeningBrackets.Contains(ch))
                {
                    opens.Push((ch, i));
                }
                else if (ch == "〗")
                {
                    // the brackets left open in the group would close with it: they are characters too
                    while (opens.Count > 0 && opens.Peek().Open != "〖")
                        escaped.Add(opens.Pop().At);
                    if (opens.Count > 0)
                        opens.Pop();
                }
                else if (ClosingBrackets.Contains(ch))
                {
                    if (opens.Count > 0 && opens.Peek().Open != "〖")
                        opens.Pop();
                    else
                        escaped.Add(i);
                }
            }
            if (escaped.Count == 0)
                return unicodemath;
            foreach (var i in escaped)
                chars[i] = "\\" + chars[i];
            return string.Concat(chars);
        }

        private static readonly HashSet<string> OpeningBrackets = new HashSet<string>(Characters("([{⟨⌈⌊❲⟦⟪⟬⟮⦃⦅⦇⦉⦋⦍⦏⦑⦓⦕⦗⧘⧚⧼"));

        private static readonly HashSet<string> ClosingBrackets = new HashSet<string>(Characters(")]}⟩⌉⌋❳⟧⟫⟭⟯⦄⦆⦈⦊⦌⦎⦐⦒⦔⦖⦘⧙⧛⧽"));

        /// <summary>resolveCW: replaces control words (\alpha) with the characters they stand for.</summary>
        /// <param name="unknown">Called with each control word that is not one (as \foo), which is kept as text.</param>
        public static string ResolveControlWords(string unicodemath, Action<string> unknown = null)
        {
            string previous = "";
            return ControlWord.Replace(unicodemath, match =>
            {
                if (!match.Groups[1].Success)
                    return match.Value;
                string cw = match.Groups[1].Value;
                if (previous == "def" || previous == "newcommand")
                {
                    previous = "";
                    return match.Value;
                }
                previous = cw;
                if (IsFunctionName(cw))
                    return " " + cw;

                // \uXXXXX: a code point
                if (Js.Len(cw) >= 5 && Js.Ch(cw, 0) == "u"
                    && int.TryParse(Js.Sub(cw, 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code)
                    && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
                {
                    return FromCodePoint(code);
                }

                // math alphanumeric control words: \mscrH, \doubleX, \scriptX, \boldX
                int cchMS = 6;
                if (Js.Len(cw) >= 3)
                {
                    string mathStyle = "";
                    if (Js.Ch(cw, 0) == "m")
                    {
                        // the longest style: \mbfitsansA is mbfitsans, not mbfit
                        foreach (var style in MathStyles)
                        {
                            if (cw.StartsWith(style, StringComparison.Ordinal) && Js.Len(style) > Js.Len(mathStyle))
                            {
                                mathStyle = style;
                                cchMS = Js.Len(style);
                            }
                        }
                    }
                    else if (cw.StartsWith("Bbb", StringComparison.Ordinal))
                    {
                        mathStyle = "Bbb";
                        cchMS = 3;
                    }
                    else if (cw.StartsWith("double", StringComparison.Ordinal))
                    {
                        mathStyle = "Bbb";
                    }
                    else if (cw.StartsWith("script", StringComparison.Ordinal))
                    {
                        mathStyle = "mscr";
                    }
                    else if (cw.StartsWith("bold", StringComparison.Ordinal))
                    {
                        mathStyle = "mbf";
                        cchMS = 4;
                    }
                    if (mathStyle.Length > 0)
                    {
                        string c = Js.Sub(cw, cchMS);
                        if (Js.Len(c) > 0)
                        {
                            if (Js.Len(c) > 1)
                                c = ControlWords.Table.TryGetValue(c, out var named) ? named : null;
                            if (c != null && mathStyle == "mup")
                                return "\"" + c + "\"";
                            // \boldsymbol is not \bold with a symbol named "symbol": the control words below
                            if (c != null)
                                return GetMathAlphanumeric(c, mathStyle);
                        }
                        else
                        {
                            int n = Array.IndexOf(MathStyles, mathStyle) + 0x61;
                            return "Ⅎ" + FromCodePoint(n);
                        }
                    }
                }

                if (ControlWords.Table.TryGetValue(cw, out var symbol) && !InRange("①", symbol, "⒇"))
                    return symbol;
                if (Js.Len(cw) == 6 && cw.StartsWith("frac", StringComparison.Ordinal) && IsAsciiDigit(Js.Ch(cw, 4)) && IsAsciiDigit(Js.Ch(cw, 5)))
                    return GetUnicodeFraction(Js.Ch(cw, 4), Js.Ch(cw, 5));
                // not a control word: upright text
                unknown?.Invoke("\\" + cw);
                return "\"" + match.Value + "\"";
            });
        }

        private static readonly (int AstralBegin, int AstralEnd, int PrivateBegin, int PrivateEnd)[] AstralPrivateMap =
        {
            (0, 0, 0, 0),
            (0x1D400, 0x1D7FF, 0xE000, 0xE3FF),
            (0x1F004, 0x1F004, 0xE400, 0xE400), (0x1F0CF, 0x1F0CF, 0xE401, 0xE401), (0x1F18E, 0x1F18E, 0xE402, 0xE402),
            (0x1F191, 0x1F19A, 0xE403, 0xE40C), (0x1F1E6, 0x1F1FF, 0xE40D, 0xE426), (0x1F201, 0x1F201, 0xE427, 0xE427),
            (0x1F21A, 0x1F21A, 0xE428, 0xE428), (0x1F22F, 0x1F22F, 0xE429, 0xE429), (0x1F232, 0x1F236, 0xE42A, 0xE42E),
            (0x1F238, 0x1F23A, 0xE42F, 0xE431), (0x1F250, 0x1F251, 0xE432, 0xE433), (0x1F300, 0x1F320, 0xE434, 0xE454),
            (0x1F32D, 0x1F335, 0xE455, 0xE45D), (0x1F337, 0x1F37C, 0xE45E, 0xE4A3), (0x1F37E, 0x1F393, 0xE4A4, 0xE4B9),
            (0x1F3A0, 0x1F3CA, 0xE4BA, 0xE4E4), (0x1F3CF, 0x1F3D3, 0xE4E5, 0xE4E9), (0x1F3E0, 0x1F3F0, 0xE4EA, 0xE4FA),
            (0x1F3F4, 0x1F3F4, 0xE4FB, 0xE4FB), (0x1F3F8, 0x1F43E, 0xE4FC, 0xE542), (0x1F440, 0x1F440, 0xE543, 0xE543),
            (0x1F442, 0x1F4FC, 0xE544, 0xE5FE), (0x1F4FF, 0x1F53D, 0xE5FF, 0xE63D), (0x1F54B, 0x1F54E, 0xE63E, 0xE641),
            (0x1F550, 0x1F567, 0xE642, 0xE659), (0x1F57A, 0x1F57A, 0xE65A, 0xE65A), (0x1F595, 0x1F596, 0xE65B, 0xE65C),
            (0x1F5A4, 0x1F5A4, 0xE65D, 0xE65D), (0x1F5FB, 0x1F64F, 0xE65E, 0xE6B2), (0x1F680, 0x1F6C5, 0xE6B3, 0xE6F8),
            (0x1F6CC, 0x1F6CC, 0xE6F9, 0xE6F9), (0x1F6D0, 0x1F6D2, 0xE6FA, 0xE6FC), (0x1F6D5, 0x1F6D5, 0xE6FD, 0xE6FD),
            (0x1F6EB, 0x1F6EC, 0xE6FE, 0xE6FF), (0x1F6F4, 0x1F6FA, 0xE700, 0xE706), (0x1F7E0, 0x1F7EB, 0xE707, 0xE712),
            (0x1F90D, 0x1F93A, 0xE713, 0xE740), (0x1F93C, 0x1F945, 0xE741, 0xE74A), (0x1F947, 0x1F971, 0xE74B, 0xE775),
            (0x1F973, 0x1F976, 0xE776, 0xE779), (0x1F97A, 0x1F9A2, 0xE77A, 0xE7A2), (0x1F9A5, 0x1F9AA, 0xE7A3, 0xE7A8),
            (0x1F9AE, 0x1F9CA, 0xE7A9, 0xE7C5), (0x1F9CD, 0x1F9FF, 0xE7C6, 0xE7F8), (0x1FA70, 0x1FA73, 0xE7F9, 0xE7FC),
            (0x1FA78, 0x1FA7A, 0xE7FD, 0xE7FF), (0x1FA80, 0x1FA82, 0xE800, 0xE802), (0x1FA90, 0x1FA95, 0xE803, 0xE808),
            (0x1EE00, 0x1EEFF, 0xE900, 0xE9FF),
        };

        /// <summary>
        /// mapToPrivate: maps the characters outside the basic plane to the private use area, and Unicode numeric
        /// fractions such as ¹²/₃₄₅ to small fractions 12⊘345. Macros (ⓜ, \newcommand) are not supported.
        /// </summary>
        public static string MapToPrivate(string s)
        {
            var cps = CodePoints.Of(s).Select(CodePoints.Value).ToList();
            var u = new List<int>();
            for (int i = 0; i < cps.Count; i++)
            {
                int cp = cps[i];
                if (cp <= 0xFFFF)
                {
                    if (cp >= 0x2080 && cp <= 0x2089 && i >= 2 && (cps[i - 1] == '/' || cps[i - 1] == '⁄'))
                    {
                        int j = i - 2;
                        string numerator = "";
                        while (j >= 0)
                        {
                            string digit = FoldSupDigit(FromCodePoint(cps[j]));
                            if (digit.Length == 0)
                                break;
                            numerator = digit + numerator;
                            j--;
                        }
                        j++;
                        int k = i;
                        string denominator = "";
                        if (i - j >= 2)
                        {
                            while (k < cps.Count && cps[k] >= '₀' && cps[k] <= '₉')
                            {
                                denominator += FromCodePoint(cps[k] - 0x2050);
                                k++;
                            }
                            if (k - i >= 1)
                            {
                                u.RemoveRange(u.Count - (i - j), i - j);
                                u.AddRange(CodePoints.Of(" " + numerator + "⊘" + denominator + " ").Select(CodePoints.Value));
                                i = k - 1;
                                continue;
                            }
                        }
                    }
                    u.Add(cp);
                    continue;
                }

                var mapped = cp;
                foreach (var m in AstralPrivateMap)
                {
                    if (m.AstralBegin <= cp && cp <= m.AstralEnd)
                    {
                        mapped = m.PrivateBegin + (cp - m.AstralBegin);
                        break;
                    }
                }
                u.Add(mapped);
            }
            return string.Concat(u.Select(FromCodePoint));
        }

        /// <summary>mapFromPrivate: the inverse of <see cref="MapToPrivate"/> for the private use area.</summary>
        public static string MapFromPrivate(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in Characters(s))
            {
                int cp = CodePointAt(c, 0);
                string mapped = c;
                if (cp >= 0xE000 && cp <= 0xF8FF)
                {
                    foreach (var m in AstralPrivateMap)
                    {
                        if (m.PrivateBegin <= cp && cp <= m.PrivateEnd)
                        {
                            mapped = FromCodePoint(m.AstralBegin + (cp - m.PrivateBegin));
                            break;
                        }
                    }
                }
                sb.Append(mapped);
            }
            return sb.ToString();
        }

        /// <summary>astMapFromPrivate: <see cref="MapFromPrivate"/> over every string of a syntax tree.</summary>
        public static object AstMapFromPrivate(object ast)
        {
            switch (ast)
            {
                case null:
                    return null;
                case List<object> list:
                    for (int i = 0; i < list.Count; i++)
                        list[i] = AstMapFromPrivate(list[i]);
                    return list;
                case string s:
                    return MapFromPrivate(s);
                case JsObject o:
                    foreach (var key in new List<string>(o.Keys))
                        o[key] = AstMapFromPrivate(o[key]);
                    return o;
                default:
                    return ast;
            }
        }
    }
}
