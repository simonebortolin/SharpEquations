namespace SharpEquations.Internal.LaTeX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SharpEquations.Internal.UnicodeMath;
    using static SharpEquations.Internal.UnicodeMath.UnicodeMathText;

    /// <summary>
    /// LaTeX to UnicodeMath: a port of TeX2UnicodeMath() of UnicodeMathML (playground/assets/TeX.js). Pass 1 replaces
    /// control words with UnicodeMath characters (\frac → ⍁, \begin → 〖, \alpha → α), the macro pass defines and
    /// applies \def and \newcommand macros, pass 2 rewrites the result as UnicodeMath. Text is a list of code points.
    /// </summary>
    internal sealed partial class LaTeXReader
    {
        private static readonly Dictionary<string, string> Environments = new Dictionary<string, string>
        {
            ["cases"] = "Ⓒ", ["dcases"] = "Ⓒ", ["align"] = "█", ["align*"] = "█", ["aligned"] = "█", ["alignat"] = "█",
            ["alignat*"] = "█", ["alignedat"] = "█", ["gather"] = "█", ["gather*"] = "█", ["gathered"] = "█", ["split"] = "█",
            ["eqnarray"] = "█", ["eqnarray*"] = "█", ["pmatrix"] = "⒨", ["matrix"] = "■", ["smallmatrix"] = "■", ["array"] = "■",
            ["vmatrix"] = "⒱", ["bmatrix"] = "ⓢ", ["Vmatrix"] = "⒩", ["Bmatrix"] = "Ⓢ",
        };

        private const string OverBrackets = "⎴⏜⏞⏠¯";
        private const string UnderBrackets = "⎵⏝⏟⏡";

        private readonly IDictionary<string, string> macros;

        private readonly bool display;

        private LaTeXReader(IDictionary<string, string> macros, bool display)
        {
            this.macros = macros ?? new Dictionary<string, string>();
            this.display = display;
        }

        /// <summary>
        /// TeX2UnicodeMath: the UnicodeMath of LaTeX math (without its $ or \[ delimiters). Macros defined with \def
        /// or \newcommand, and environments defined with \newenvironment, go to <paramref name="macros"/>, where later conversions find them. In a display equation the
        /// scripts of a large operator other than an integral (\sum, \prod, \bigcup...) go under and over it, as TeX
        /// puts them. The \ce and \pu of mhchem are rewritten as plain LaTeX first (<see cref="Mhchem"/>).
        /// </summary>
        public static string ToUnicodeMath(string tex, IDictionary<string, string> macros = null, bool display = false) =>
            RestoreTextEscapes(new LaTeXReader(macros, display).Convert(Mhchem.Rewrite(tex)));

        // the characters that \text escapes ({, \_, ...), held as noncharacters through the passes, which would read them as LaTeX
        private static readonly (string Escape, char Held, char Character)[] TextEscapes =
        {
            ("\\textbackslash{}", '\uFDD9', '\\'), ("\\textbackslash", '\uFDD9', '\\'), ("\\{", '\uFDD0', '{'), ("\\}", '\uFDD1', '}'), ("\\_", '\uFDD2', '_'), ("\\#", '\uFDD3', '#'), ("\\$", '\uFDD4', '$'),
            ("\\%", '\uFDD5', '%'), ("\\&", '\uFDD6', '&'), ("\\^{}", '\uFDD7', '^'), ("\\~{}", '\uFDD8', '~'),
            ("\\textasciicircum{}", '\uFDD7', '^'), ("\\textasciitilde{}", '\uFDD8', '~'),
        };

        private static readonly HashSet<string> TextCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "text", "textrm", "textup", "textnormal", "textbf", "textit", "textsf", "texttt", "textmd", "mbox", "emph",
        };
        /// <summary>Text as a quoted UnicodeMath string, which keeps parentheses and backslashes as they are.</summary>
        private static string Quoted(string text) => text.Length == 0 ? "" : "\"" + text.Replace("\"", "\\\"") + "\"";

        private static string HoldTextEscapes(string text)
        {
            foreach (var (escape, held, _) in TextEscapes)
                text = text.Replace(escape, held.ToString());
            return text;
        }

        private static string RestoreTextEscapes(string unicodemath)
        {
            foreach (var (_, held, character) in TextEscapes)
                unicodemath = unicodemath.Replace(held, character);
            if (!Characters(unicodemath).Any(c => CodePointAt(c, 0) >= HeldMarks))
                return unicodemath;
            return string.Concat(Characters(unicodemath).Select(c => CodePointAt(c, 0) >= HeldMarks ? char.ConvertFromUtf32(CodePointAt(c, 0) - HeldMarks) : c));
        }

        // combining marks held in the supplementary private use area B, at their code point plus this
        private const int HeldMarks = 0x100000;

        private static string HoldMark(string mark) => char.ConvertFromUtf32(CodePointAt(mark, 0) + HeldMarks);

        private sealed class Text
        {
            private readonly List<string> chars;

            public Text(string s) => chars = Characters(s);

            public int Length => chars.Count;

            public string this[int i] => i >= 0 && i < chars.Count ? chars[i] : null;

            public string Sub(int start, int end = int.MaxValue)
            {
                start = System.Math.Max(0, System.Math.Min(start, chars.Count));
                end = System.Math.Max(start, System.Math.Min(end, chars.Count));
                return string.Concat(chars.Skip(start).Take(end - start));
            }

            public int IndexOf(string c, int from)
            {
                for (int i = System.Math.Max(0, from); i < chars.Count; i++)
                {
                    if (chars[i] == c)
                        return i;
                }
                return -1;
            }

            /// <summary>The index of a text made of several characters, or -1.</summary>
            public int Find(string text, int from)
            {
                var wanted = Characters(text);
                for (int i = System.Math.Max(0, from); i + wanted.Count <= chars.Count; i++)
                {
                    if (chars.Skip(i).Take(wanted.Count).SequenceEqual(wanted))
                        return i;
                }
                return -1;
            }

            public override string ToString() => string.Concat(chars);
        }

        private static bool IsAsciiAlphabetic(string ch) => ch != null && ch.Length == 1 && (ch[0] >= 'A' && ch[0] <= 'Z' || ch[0] >= 'a' && ch[0] <= 'z');

        /// <summary>The name of the control word at the index (the letters after a backslash), empty if none.</summary>
        private static string ControlWord(Text tex, int at)
        {
            var end = at;
            while (IsAsciiAlphabetic(tex[end]))
                end++;
            return tex.Sub(at, end);
        }

        private static bool IsAccent(string ch) => InRange("̀", ch, "ͯ") || InRange("⃐", ch, "⃰");

        private static string Last(StringBuilder sb) => Js.Ch(sb.ToString(), Js.Len(sb.ToString()) - 1);

        private static void RemoveLast(StringBuilder sb)
        {
            var s = sb.ToString();
            sb.Clear().Append(Js.Sub(s, 0, Js.Len(s) - 1));
        }

        private static int FindClosingBrace(Text text, int i)
        {
            var (j, _) = FindClosingBrace(text, i, null);
            return j;
        }

        private static (int, int) FindClosingBrace(Text text, int i, string chStop)
        {
            int cBrace = 1;
            int over = 0;
            // the environments inside (〖 to 〗), whose line breaks and end are not those of the text searched
            int environments = 0;
            for (; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case "〖":
                        environments++;
                        break;
                    case "{":
                        cBrace++;
                        break;
                    case "}":
                        cBrace--;
                        if (cBrace == 0)
                            return (i, over);
                        break;
                    case "⒞":
                    case "/":
                        over = over == 0 ? i : -1;
                        break;
                    case "\\":
                        if (text[i + 1] == "\\" && environments == 0)
                            return (i, over);
                        // an escaped brace, \{ or \}, is a character
                        if (text[i + 1] == "{" || text[i + 1] == "}")
                            i++;
                        break;
                    case "$":
                        if (cBrace == 1 && chStop == "$")
                            return (i, over);
                        break;
                    case "〗":
                        if (environments == 0)
                            return (i, over);
                        environments--;
                        break;
                }
            }
            return (-1, over);
        }

        private (string, int) GetArg(Text tex, int i, bool checkNeedParens)
        {
            var arg = "";
            while (tex[i] == " ")
                i++;
            if (tex[i] == "{")
            {
                i++;
                int j = FindClosingBrace(tex, i);
                if (j > 0)
                {
                    arg = Pass2(tex.Sub(i, j));
                    i = j + 1;
                }
            }
            else if (tex[i] != null)
            {
                arg = tex[i];
                i++;
            }
            // an argument in parentheses, ^{(2)}, keeps them: UnicodeMath would take them for the grouping ones
            if (checkNeedParens && (UnicodeMathSerializer.NeedParens(arg) || IsParenthesized(arg)))
                arg = "(" + arg + ")";
            return (arg, i);
        }

        private static bool IsPrime(string ch) => ch == "'" || ch == "′" || ch == "″" || ch == "‴" || ch == "⁗";

        private static bool IsParenthesized(string arg)
        {
            var chars = Characters(arg);
            if (chars.Count < 2 || chars[0] != "(" || chars[chars.Count - 1] != ")")
                return false;
            int depth = 0;
            for (int i = 0; i < chars.Count; i++)
            {
                if (chars[i] == "\\")
                    i++;
                else if (chars[i] == "(")
                    depth++;
                else if (chars[i] == ")" && --depth == 0)
                    return i == chars.Count - 1;
            }
            return false;
        }

        // \: and \> (medium), \; (thick) and \! (negative thin, which UnicodeMath does not have)
        private static readonly Dictionary<string, string> Spaces = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [":"] = " ", [">"] = " ", [";"] = " ", ["!"] = "", [" "] = "\u00A0",
        };

        // the large operators whose scripts TeX puts under and over them in a display equation; an integral keeps them
        // at its side unless \limits puts them under and over
        private static readonly HashSet<string> Summations = new HashSet<string>(StringComparer.Ordinal)
        {
            "sum", "prod", "coprod", "bigcup", "bigcap", "bigvee", "bigwedge", "bigoplus", "bigotimes", "bigodot", "biguplus", "bigsqcup",
            "∑", "∏", "∐", "⋃", "⋂", "⋁", "⋀", "⨁", "⨂", "⨀", "⨄", "⨆",
        };

        /// <summary>
        /// The scripts of large operators where TeX puts them: under and over after \limits, and after a summation in a
        /// display equation, as UnicodeMath's ┬ and ┴ (∑┬(𝑘=1)┴𝑛); at the side after \nolimits, the operator then
        /// grouped (〖∑〗_(𝑘=1)^𝑛), since UnicodeMath puts the scripts of a summation under and over it in a display
        /// equation. \limits and \nolimits are left out.
        /// </summary>
        private string Limits(string latex)
        {
            var tex = new Text(latex);
            var result = new StringBuilder();
            // where the last summation begins in the result, and whether it ends the result
            int summation = -1;
            for (int i = 0; i < tex.Length;)
            {
                var cw = tex[i] == "\\" ? ControlWord(tex, i + 1) : "";
                var next = tex[i] == "\\" ? i + 1 + Math.Max(1, cw.Length) : i + 1;
                if (cw is "limits" or "nolimits")
                {
                    var (under, over, afterScripts) = Scripts(tex, next);
                    if (afterScripts == next)
                    {
                        i = next;
                        continue;
                    }
                    if (cw == "limits")
                        result.Append(UnderOver(under, over));
                    else
                    {
                        if (display && summation >= 0)
                            result.Insert(summation, '{').Append('}');
                        result.Append(under == null ? "" : "_{" + under + "}").Append(over == null ? "" : "^{" + over + "}");
                    }
                    summation = -1;
                    i = afterScripts;
                    continue;
                }
                if (LimitName(tex, cw, next, display) is var (limitName, afterName) && limitName != null)
                {
                    // the scripts of an operator name with limits go under and over it in a display equation
                    result.Append(limitName);
                    i = afterName;
                    if (display && NonBlank(tex, i) is "_" or "^")
                    {
                        var (under, over, afterScripts) = Scripts(tex, i);
                        result.Append(UnderOver(under, over));
                        i = afterScripts;
                        // UnderOver ends the limits with a space
                        while (tex[i] == " ")
                            i++;
                    }
                    summation = -1;
                    continue;
                }
                var token = tex.Sub(i, next);
                var isSummation = Summations.Contains(cw.Length > 0 ? cw : tex[i]);
                result.Append(token);
                i = next;
                summation = isSummation ? result.Length - token.Length : summation >= 0 && tex[i - 1] == " " ? summation : -1;
                if (isSummation && display && NonBlank(tex, i) is "_" or "^")
                {
                    // the scripts of a summation in a display equation (\limits or \nolimits would come before them)
                    var (under, over, afterScripts) = Scripts(tex, i);
                    result.Append(UnderOver(under, over));
                    summation = -1;
                    i = afterScripts;
                }
            }
            return result.ToString();
        }

        // the operator names of amsmath with limits: two words with a thin space between them, as UnicodeMath reads them,
        // or lim with an arrow or a bar under or over it
        private static readonly Dictionary<string, string> LimitNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["liminf"] = "lim\u2009inf\u2061", ["limsup"] = "lim\u2009sup\u2061", ["injlim"] = "inj\u2009lim\u2061",
            ["projlim"] = "proj\u2009lim\u2061", ["varinjlim"] = "{\\underset{\\rightarrow}{\\lim}}",
            ["varprojlim"] = "{\\underset{\\leftarrow}{\\lim}}", ["varliminf"] = "{\\underline{\\lim}}",
            ["varlimsup"] = "{\\overline{\\lim}}",
        };

        /// <summary>
        /// An operator name with limits at the index after the control word (\liminf, \operatorname*{argmax},
        /// \operatornamewithlimits{argmax}), or \lim in a display equation, as the reader reads it, and the index after it;
        /// null if none.
        /// </summary>
        private static (string Name, int Next) LimitName(Text tex, string cw, int next, bool display)
        {
            // lim, whose limit goes under it in a display equation also with nothing after it (lim┬𝑥)
            if (cw == "lim" && display)
                return (" lim", next);
            if (LimitNames.TryGetValue(cw, out var name))
                return (name, next);
            if (!(cw == "operatornamewithlimits" || cw == "operatorname" && tex[next] == "*"))
                return (null, next);
            var (operatorName, afterName) = BracedArgument(tex, cw == "operatorname" ? next + 1 : next);
            if (afterName < 0)
                return (null, next);
            // a name that UnicodeMath knows (lim) is read without the function apply sign, which would part it from its limits
            return (UnicodeMathText.IsFunctionName(operatorName.Trim()) ? " " + operatorName.Trim() : "\\operatorname{" + operatorName + "}", afterName);
        }

        // the limits end at a space, which parts them from the operand: ∑┬𝑘 (−1)^𝑘, not ∑┬(𝑘(−1))
        private static string UnderOver(string under, string over) =>
            (under == null ? "" : "┬" + ScriptGroup(under)) + (over == null ? "" : "┴" + ScriptGroup(over)) + " ";

        /// <summary>
        /// A script braced unless it is one character (┴𝑛, ┬{𝑘=1}), as the scripts of _ and ^ are; a command is braced
        /// too (┴{\infty}), since the space that ends its name would not part the script from the operand.
        /// </summary>
        private static string ScriptGroup(string script) => new Text(script).Length == 1 ? script : "{" + script + "}";

        /// <summary>The subscript and the superscript at the index (in either order, after blanks), and the index after them.</summary>
        private static (string Under, string Over, int Next) Scripts(Text tex, int i)
        {
            string under = null, over = null;
            int next = i;
            while (true)
            {
                int j = next;
                while (j < tex.Length && tex[j] == " ")
                    j++;
                if (tex[j] is not ("_" or "^") || (tex[j] == "_" ? under : over) != null)
                    return (under, over, next);
                var (script, afterScript) = ScriptArgument(tex, j + 1);
                if (afterScript < 0)
                    return (under, over, next);
                if (tex[j] == "_")
                    under = script;
                else
                    over = script;
                next = afterScript;
            }
        }

        /// <summary>A script: a braced group, a command with its arguments, a command or a character; -1 if none.</summary>
        private static (string Text, int Next) ScriptArgument(Text tex, int i)
        {
            while (i < tex.Length && tex[i] == " ")
                i++;
            if (i >= tex.Length)
                return (null, -1);
            if (tex[i] == "{")
                return BracedArgument(tex, i);
            if (tex[i] != "\\")
                return (tex[i], i + 1);
            if (ScriptCommand(tex, i) is var (command, after) && command != null)
                return (command, after);
            var end = i + 1 + Math.Max(1, ControlWord(tex, i + 1).Length);
            return (tex.Sub(i, end), end);
        }

        // the commands that only size or style what follows, left out (\displaystyle makes a display equation, ⓓ)
        private static readonly HashSet<string> Dropped = new HashSet<string>(StringComparer.Ordinal)
        {
            "big", "Big", "bigg", "Bigg", "bigl", "bigr", "Bigl", "Bigr", "biggl", "biggr", "Biggl", "Biggr", "bigm", "Bigm", "biggm", "Biggm",
            "textstyle", "scriptstyle", "scriptscriptstyle", "nonumber", "notag", "hline", "hdashline", "hfil", "hfill",
        };

        // the commands that are others for MathML
        private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dfrac"] = "\\frac", ["tfrac"] = "\\frac", ["cfrac"] = "\\frac", ["dbinom"] = "\\binom", ["tbinom"] = "\\binom",
            ["overrightarrow"] = "\\vec", ["Vert"] = "‖", ["lVert"] = "‖", ["rVert"] = "‖", ["bmod"] = "\\operatorname{mod}",
            ["bm"] = "\\boldsymbol", ["lg"] = "\\operatorname{lg}",
            // the symbols of LaTeX, amsmath, amssymb and plain TeX that UnicodeMath has no control word for (the others of
            // LaTeXSymbols are read from it), and \square, \Box, \blacksquare, \surd and \oslash, which are symbols in LaTeX
            // and build a box, a root or a fraction in UnicodeMath
            ["oslash"] = "\\⊘", ["yen"] = "¥", ["circledR"] = "®", ["S"] = "§", ["P"] = "¶", ["mho"] = "℧", ["rightsquigarrow"] = "↝",
            ["leftrightsquigarrow"] = "↭", ["measuredangle"] = "∡", ["sphericalangle"] = "∢", ["eqsim"] = "≂", ["doteqdot"] = "≑",
            ["triangleq"] = "≜", ["square"] = "\\□", ["Box"] = "\\□", ["blacksquare"] = "\\■", ["surd"] = "{\\surd}",
            ["lt"] = "<", ["gt"] = ">", ["dotsb"] = "⋯", ["dotsi"] = "⋯", ["dotsm"] = "⋯", ["dotsc"] = "…", ["dotso"] = "…",
            ["ldotp"] = ".", ["gggtr"] = "⋙", ["llless"] = "⋘", ["gvertneqq"] = "≩", ["lvertneqq"] = "≨", ["ngeqq"] = "≱",
            ["nshortmid"] = "∤", ["nshortparallel"] = "∦", ["nsubseteqq"] = "⊈", ["omicron"] = "ο", ["shortmid"] = "∣",
            ["smallfrown"] = "⌢", ["smallsmile"] = "⌣", ["thicksim"] = "∼", ["thickapprox"] = "≈", ["varsubsetneqq"] = "⫋",
            ["varsupsetneq"] = "⊋", ["varsupsetneqq"] = "⫌", ["bigcirc"] = "\\◯", ["centerdot"] = "⬝", ["degree"] = "°",
            ["intop"] = "\\int", ["smallint"] = "\\int", ["diagup"] = "\\╱", ["diagdown"] = "\\╲", ["thickspace"] = "\\;",
            ["And"] = "\\&", ["lvert"] = "|", ["rvert"] = "|", ["idotsint"] = "\\∫⋯\\∫",
            ["mathscr"] = "\\mathcal", ["operatornamewithlimits"] = "\\operatorname", ["mathstrut"] = "\\vphantom{\\(}",
        };

        private static readonly HashSet<string> OpeningBrackets = new HashSet<string>(StringComparer.Ordinal) { "(", "[", "{", "⟨", "⌈", "⌊" };
        private static readonly HashSet<string> ClosingBrackets = new HashSet<string>(StringComparer.Ordinal) { ")", "]", "}", "⟩", "⌉", "⌋" };
        private static readonly HashSet<string> OpeningBracketCommands = new HashSet<string>(StringComparer.Ordinal) { "langle", "lceil", "lfloor", "lbrace" };
        private static readonly HashSet<string> ClosingBracketCommands = new HashSet<string>(StringComparer.Ordinal) { "rangle", "rceil", "rfloor", "rbrace" };

        // the commands that put a script over a base, \overset{script}{base}, as the UnicodeMath operator (\underset is ⓤ)
        private static readonly Dictionary<string, string> Stacks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["overset"] = "┴", ["stackrel"] = "┴",
        };

        // the commands that put a mark over a base, as \overset{mark}{base}
        private static readonly Dictionary<string, string> OverArrows = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mathring"] = "˚", ["overleftarrow"] = "\\leftarrow", ["overleftrightarrow"] = "\\leftrightarrow",
        };

        private static bool IsZeroLength(string length) => ZeroLength().IsMatch(length.Trim());

        [System.Text.RegularExpressions.GeneratedRegex(@"^0*(\.0*)?\s*[a-z]*$")]
        private static partial System.Text.RegularExpressions.Regex ZeroLength();

        /// <summary>Whether an angle closes after the index: a bra alone, ⟨𝜓|, keeps its bar.</summary>
        private static bool AngleCloses(Text tex, int i)
        {
            var rest = tex.Sub(i, tex.Length);
            return rest.Any(c => c == '⟩') || rest.Contains("\\rangle");
        }

        /// <summary>
        /// The commands of LaTeX that have no UnicodeMath control word, rewritten with UnicodeMath operators:
        /// \overset{A}{B} as 〖B〗┴(A), \color{c}{x} as ✎(c&amp;{x}) and \colorbox{c}{x} as ☁(c&amp;{x}), \left. and \right. as
        /// \left and \right before a space; \color{c} without a
        /// second argument colors the rest of its group.
        /// </summary>
        private static string RewriteTeXOnly(string latex)
        {
            var tex = new Text(latex);
            var result = new StringBuilder();
            // between ⟨ and ⟩ a bar parts a bra from a ket (│), as in UnicodeMath: ⟨a│b⟩
            int angles = 0;
            // in brackets \mid is the separator ∣ of UnicodeMath, {x∣x>0}; outside them the relation, escaped
            int brackets = 0;
            for (int i = 0; i < tex.Length;)
            {
                if (tex[i] == "|" && angles > 0)
                {
                    result.Append('│');
                    i++;
                    continue;
                }
                if (tex[i] == "⟨" && AngleCloses(tex, i + 1) || tex[i] == "⟩")
                    angles = Math.Max(0, angles + (tex[i] == "⟨" ? 1 : -1));
                if (OpeningBrackets.Contains(tex[i]) || ClosingBrackets.Contains(tex[i]))
                    brackets = Math.Max(0, brackets + (OpeningBrackets.Contains(tex[i]) ? 1 : -1));
                // a slash of LaTeX is a slash, (a+b)/2, where UnicodeMath would build a fraction: the escaped one of UnicodeMath
                if (tex[i] == "/")
                {
                    result.Append("\\/");
                    i++;
                    continue;
                }
                if (tex[i] != "\\")
                {
                    // the blanks around a script, x ^ { 2 }, which LaTeX ignores and UnicodeMath would part the script at;
                    // a script that is a command with arguments, x_\text{ab}: the script is the command and its arguments
                    if (tex[i] == "_" || tex[i] == "^")
                    {
                        while (result.Length > 0 && result[result.Length - 1] == ' ')
                            result.Length--;
                        // a script without a base (^3 at the start of a group) is on an empty one, as LaTeX puts it
                        if (HasNoBase(result))
                            result.Append("{}");
                        result.Append(tex[i]);
                        for (i++; i < tex.Length && tex[i] == " "; i++)
                        {
                        }
                        if (ScriptCommand(tex, i) is var (command, after) && command != null)
                        {
                            result.Append('{').Append(RewriteTeXOnly(command)).Append('}');
                            i = after;
                        }
                        continue;
                    }
                    // a prime without a base ('x) is on an empty one, as LaTeX puts it
                    if (tex[i] == "'" && HasNoBase(result))
                        result.Append("{}");
                    // a comma before a digit parts two numbers in LaTeX (a_{1,1}, a_{m,1}), where UnicodeMath reads one (1,1 or
                    // ,1): {,} is LaTeX's decimal comma
                    if (tex[i] == "," && tex[i + 1] is { Length: 1 } next && next[0] is >= '0' and <= '9')
                    {
                        result.Append(", ");
                        i++;
                        continue;
                    }
                    // a combining mark after its base (𝑎⃑), held: the passes read the marks of \vec and the like before theirs
                    result.Append(IsAccent(tex[i]) ? HoldMark(tex[i]) : tex[i]);
                    i++;
                    continue;
                }
                var cw = ControlWord(tex, i + 1);
                int j = i + 1 + cw.Length;
                if (cw.Length == 0)
                {
                    if (tex[i + 1] == "{" || tex[i + 1] == "}")
                        brackets = Math.Max(0, brackets + (tex[i + 1] == "{" ? 1 : -1));
                    // \| is the double bar ‖, not an escaped bar
                    if (tex[i + 1] == "|")
                    {
                        result.Append('‖');
                        i += 2;
                        continue;
                    }
                    // \\ outside an environment, a line break, which an equation does not keep: \!, no space
                    if (tex[i + 1] == "\\" && !InEnvironment(tex, i))
                    {
                        result.Append("\\!");
                        for (i += 2; i < tex.Length && tex[i] == " "; i++)
                        {
                        }
                        continue;
                    }
                    // \_ held, which the passes would read as a subscript of what follows (\_\{\})
                    if (tex[i + 1] == "_")
                    {
                        result.Append('\\').Append('\uFDD2');
                        i += 2;
                        continue;
                    }
                    result.Append(tex[i++]);
                    if (i < tex.Length)
                        result.Append(tex[i++]);
                    continue;
                }
                if (cw == "langle" && AngleCloses(tex, j) || cw == "rangle")
                    angles = Math.Max(0, angles + (cw == "langle" ? 1 : -1));
                if (OpeningBracketCommands.Contains(cw) || ClosingBracketCommands.Contains(cw))
                    brackets = Math.Max(0, brackets + (OpeningBracketCommands.Contains(cw) ? 1 : -1));
                if (cw == "mid" && brackets == 0)
                {
                    result.Append("\\∣");
                    i = j;
                    continue;
                }
                if (cw == "middle")
                {
                    // \middle| between \left and \right, the bar of UnicodeMath; UnicodeMath wants no spaces around the
                    // other delimiters
                    int k = j;
                    while (k < tex.Length && tex[k] == " ")
                        k++;
                    while (result.Length > 0 && result[result.Length - 1] == ' ')
                        result.Length--;
                    // another delimiter in the middle is the delimiter itself, a slash the slash of UnicodeMath
                    var (middle, afterMiddle) = tex[k] == "\\" && tex[k + 1] == "|" ? ("\\‖", k + 2)
                        : tex[k] == "|" ? ("│", k + 1)
                        : tex.Sub(k, k + 5) == "\\vert" && !IsAsciiAlphabetic(tex[k + 5]) ? ("│", k + 5)
                        : tex[k] == "/" ? ("\\/", k + 1)
                        : (null, k);
                    i = afterMiddle;
                    if (middle == null)
                        continue;
                    if (middle != "│")
                    {
                        while (i < tex.Length && tex[i] == " ")
                            i++;
                    }
                    // UnicodeMath wants something on each side of the bar (├{│┤} is no UnicodeMath): an empty side holds
                    // the placeholder, which the writer writes as {}
                    if (EndsWithOpening(result))
                        result.Append('⬚');
                    result.Append(middle);
                    int afterBlanks = i;
                    while (tex[afterBlanks] == " ")
                        afterBlanks++;
                    if (tex[afterBlanks] == "\\" && ControlWord(tex, afterBlanks + 1) is "right" or "middle")
                        result.Append('⬚');
                    continue;
                }
                if (cw == "vert" && angles > 0)
                {
                    result.Append('│');
                    i = j;
                    continue;
                }
                if (cw == "mid" || cw == "circledS")
                {
                    // the relation ∣, a bar that EscapeOddBars escapes when it has no pair (the separator of a bra-ket
                    // between angles), and Ⓢ, an operator of UnicodeMath
                    result.Append(cw == "circledS" ? "\\Ⓢ" : angles > 0 ? "│" : "∣");
                    i = j;
                    continue;
                }
                if (OverArrows.TryGetValue(cw, out var over) && Argument(tex, j) is var (overBase, afterOverBase) && afterOverBase >= 0)
                {
                    // \mathring{a}, \overleftarrow{a}: the mark over the base, as \overset puts it
                    result.Append(RewriteTeXOnly("\\overset{" + over + "}{" + overBase + "}"));
                    i = afterOverBase;
                    continue;
                }
                if (cw is "phantom" or "vphantom" or "hphantom" && BracedArgument(tex, j) is var (phantom, afterPhantom) && afterPhantom >= 0 &&
                    NonBlank(tex, afterPhantom) is "_" or "^")
                {
                    // a phantom with scripts, \vphantom{\sum}_1^2, braced: UnicodeMath would take the scripts into the phantom
                    result.Append("{\\").Append(cw).Append('{').Append(RewriteTeXOnly(phantom)).Append("}}");
                    i = afterPhantom;
                    continue;
                }
                if ((cw == "newcommand" || cw == "renewcommand") && j < tex.Length && tex[j] == "\\")
                {
                    // \newcommand\name{...}: the name in braces, as \newcommand{\name}{...}
                    int end = j + 1;
                    while (end < tex.Length && IsAsciiAlphabetic(tex[end]))
                        end++;
                    result.Append("\\newcommand{").Append(tex.Sub(j, end)).Append('}');
                    i = end;
                    continue;
                }
                if (cw == "renewcommand")
                {
                    result.Append("\\newcommand");
                    i = j;
                    continue;
                }
                if (ArgumentCommands.TryGetValue(cw, out var argumentCount) && argumentCount == 2 && Arguments(tex, j, 2) is { } fractionArguments)
                {
                    // \frac12 or \frac 1 2: the arguments braced, as the reader wants them
                    result.Append(Renamed.TryGetValue(cw, out var fraction) ? fraction : "\\" + cw);
                    foreach (var argument in fractionArguments.Arguments)
                        result.Append('{').Append(RewriteTeXOnly(argument)).Append('}');
                    i = fractionArguments.Next;
                    continue;
                }
                if (Dropped.Contains(cw))
                {
                    // sizes and styles that MathML leaves to the renderer: \bigl(, \limits, \displaystyle
                    i = j;
                    while (i < tex.Length && tex[i] == " ")
                        i++;
                    continue;
                }
                if (Renamed.TryGetValue(cw, out var renamed))
                {
                    result.Append(renamed);
                    i = j;
                    if (IsAsciiAlphabetic(Js.Ch(renamed, Js.Len(renamed) - 1)) && i < tex.Length && IsAsciiAlphabetic(tex[i]))
                        result.Append(' ');
                    continue;
                }
                if (cw == "pmod" && Argument(tex, j) is var (modulus, afterModulus) && afterModulus >= 0)
                {
                    result.Append("\\;(\\operatorname{mod}\\,").Append(RewriteTeXOnly(modulus)).Append(')');
                    i = afterModulus;
                    continue;
                }
                if (SpaceCommands.TryGetValue(cw, out var spaceCommand))
                {
                    result.Append(spaceCommand);
                    i = j;
                    while (i < tex.Length && tex[i] == " ")
                        i++;
                    continue;
                }
                if (LengthCommands.Contains(cw))
                {
                    // \hspace{10ex}, \kern-1.5pt, \mkern10mu: the space of UnicodeMath nearest to the length, an em one when
                    // the length is not a number with a unit
                    var (length, afterSpace) = LengthArgument(tex, cw == "hspace" && tex[j] == "*" ? j + 1 : j);
                    result.Append(Space(length ?? 18));
                    i = afterSpace;
                    continue;
                }
                if (cw == "genfrac" && Arguments(tex, j, 6) is var (genfrac, afterFraction))
                {
                    // \genfrac{left}{right}{thickness}{style}{numerator}{denominator}: a fraction without a bar is the ¦ of UnicodeMath
                    var bar = genfrac[2].Trim().Length == 0 || !IsZeroLength(genfrac[2])
                        ? "\\frac{" + genfrac[4] + "}{" + genfrac[5] + "}"
                        : "{{" + genfrac[4] + "}¦{" + genfrac[5] + "}}";
                    if (genfrac[0].Trim().Length > 0 || genfrac[1].Trim().Length > 0)
                        bar = "\\left" + (genfrac[0].Trim().Length > 0 ? genfrac[0] : ".") + bar + "\\right" + (genfrac[1].Trim().Length > 0 ? genfrac[1] : ".");
                    result.Append(RewriteTeXOnly(bar));
                    i = afterFraction;
                    continue;
                }
                if (ExtensibleArrows.TryGetValue(cw, out var arrow) && OptionalArgument(tex, j) is var (arrowUnder, afterUnder) &&
                    Argument(tex, afterUnder) is var (arrowLabel, afterArrowLabel) && afterArrowLabel >= 0)
                {
                    // \xrightarrow[under]{over}, its label without braces too (\xrightarrow x)
                    var arrowOver = "\\overset{" + arrowLabel + "}{" + arrow + "}";
                    result.Append(RewriteTeXOnly(arrowUnder == null ? arrowOver : "\\underset{" + arrowUnder + "}{" + arrowOver + "}"));
                    i = afterArrowLabel;
                    continue;
                }
                if (UnderArrows.TryGetValue(cw, out var underArrow) && Argument(tex, j) is var (underBase, afterUnderBase) && afterUnderBase >= 0)
                {
                    result.Append(RewriteTeXOnly("\\underset{" + underArrow + "}{" + underBase + "}"));
                    i = afterUnderBase;
                    continue;
                }
                if (cw is "overbrace" or "underbrace" && Argument(tex, j) is var (braced, afterBraced) && afterBraced >= 0 &&
                    Scripts(tex, afterBraced) is var (braceUnder, braceOver, afterBraceScripts) && braceUnder != null && braceOver != null)
                {
                    // \overbrace3^a_x: for TeX the brace is an operator with limits, both scripts under and over the whole construct
                    result.Append(RewriteTeXOnly("\\underset{" + braceUnder + "}{\\overset{" + braceOver + "}{\\" + cw + "{" + braced + "}}}"));
                    i = afterBraceScripts;
                    continue;
                }
                if (cw == "underset" && Arguments(tex, j, 2) is var (underArguments, afterUnderset) &&
                    new Text(underArguments[1].Trim()) is var overText && overText.Find("\\overset", 0) == 0 &&
                    Arguments(overText, 8, 2) is var (overArguments, afterOverset) && afterOverset == overText.Length)
                {
                    // \underset{a}{\overset{b}{X}}: the scripts under and over X, munderover, as the writer writes it
                    result.Append("〖〖").Append(RewriteTeXOnly(overArguments[1])).Append("〗┬(").Append(RewriteTeXOnly(underArguments[0]))
                        .Append(")┴(").Append(RewriteTeXOnly(overArguments[0])).Append(")〗");
                    i = afterUnderset;
                    continue;
                }
                if (cw is "underset" or "overset" or "stackrel" && !(NonBlank(tex, j) == "{" && NonBlank(tex, BracedArgument(tex, j).Next) == "{") &&
                    Arguments(tex, j, 2) is var (stackArguments, afterStack))
                {
                    // the arguments of a stack braced: \underset ab
                    result.Append(RewriteTeXOnly("\\" + cw + "{" + stackArguments[0] + "}{" + stackArguments[1] + "}"));
                    i = afterStack;
                    continue;
                }
                if (cw == "boxed" && NonBlank(tex, j) != "{" && Argument(tex, j) is var (boxed, afterBoxed) && afterBoxed >= 0)
                {
                    result.Append(RewriteTeXOnly("\\boxed{" + boxed + "}"));
                    i = afterBoxed;
                    continue;
                }
                if (cw == "substack" && BracedArgument(tex, j) is var (rows, afterSubstack) && afterSubstack >= 0)
                {
                    // the rows of a script, as a matrix
                    result.Append(RewriteTeXOnly("\\begin{smallmatrix}" + rows + "\\end{smallmatrix}"));
                    i = afterSubstack;
                    continue;
                }
                if (cw == "sideset" && Arguments(tex, j, 3) is var (sideset, afterSideset))
                {
                    // \sideset{_1^2}{_3^4}\sum: the scripts before the operator on its phantom, as MathML writes them
                    result.Append(RewriteTeXOnly("{\\vphantom{" + sideset[2] + "}}" + sideset[0] + "{" + sideset[2] + "}" + sideset[1]));
                    i = afterSideset;
                    continue;
                }
                if (cw == "prescript" && Arguments(tex, j, 3) is var (prescript, afterPrescript))
                {
                    // \prescript{sup}{sub}{base} of mathtools: the scripts at the start of the group of their base, where
                    // UnicodeMath reads them as prescripts
                    result.Append('{');
                    if (prescript[0].Trim().Length > 0)
                        result.Append("^{").Append(RewriteTeXOnly(prescript[0])).Append('}');
                    if (prescript[1].Trim().Length > 0)
                        result.Append("_{").Append(RewriteTeXOnly(prescript[1])).Append('}');
                    result.Append(RewriteTeXOnly(prescript[2])).Append('}');
                    i = afterPrescript;
                    continue;
                }
                if (cw == "pod" && Argument(tex, j) is var (pod, afterPod) && afterPod >= 0)
                {
                    result.Append("\\quad(").Append(RewriteTeXOnly(pod)).Append(')');
                    i = afterPod;
                    continue;
                }
                if (cw == "not")
                {
                    // \not over the next symbol, the long solidus of UnicodeMath (≠ for \not=), over a group when it is a command
                    // with arguments (\not\operatorname{R}); alone, a slash
                    var (negated, afterNegated) = Argument(tex, j);
                    if (afterNegated < 0)
                    {
                        result.Append("\\/");
                        i = j;
                        continue;
                    }
                    var negatedText = new Text(negated);
                    bool symbol = negatedText.Length == 1 || negatedText[0] == "\\" && ControlWord(negatedText, 1).Length == negatedText.Length - 1;
                    result.Append(symbol ? RewriteTeXOnly(negated) : "(" + RewriteTeXOnly(negated) + ")").Append(HoldMark("\u0338"));
                    i = afterNegated;
                    continue;
                }
                if (cw == "operatorname" && tex[j] == "*")
                {
                    // \operatorname*, a name with limits, read as \operatorname
                    result.Append("\\operatorname");
                    i = j + 1;
                    continue;
                }
                if (cw == "mathop" && BracedArgument(tex, j) is var (operatorText, afterOperator) && afterOperator >= 0)
                {
                    // \mathop{\mathrm{argmin}}: an operator name; \mathop{x}, of one symbol, a group, which takes the limits
                    var name = operatorText.Trim();
                    if (Js.Len(name) == 1)
                    {
                        result.Append('{').Append(RewriteTeXOnly(name)).Append('}');
                        i = afterOperator;
                        continue;
                    }
                    if (name.StartsWith("\\mathrm{", StringComparison.Ordinal) && name[name.Length - 1] == '}')
                        name = name.Substring(8, name.Length - 9);
                    result.Append(name.All(char.IsLetter) ? "\\operatorname{" + name + "}" : RewriteTeXOnly(operatorText));
                    i = afterOperator;
                    continue;
                }
                if (cw is "begin" or "end" && BracedArgument(tex, j) is var (environmentName, afterName) && afterName >= 0 &&
                    (environmentName.EndsWith("matrix*", StringComparison.Ordinal) && Environments.ContainsKey(environmentName.TrimEnd('*')) ||
                     cw == "begin" && (environmentName == "array" || environmentName.StartsWith("alignat", StringComparison.Ordinal))))
                {
                    // \begin{pmatrix*}[r] is the matrix, whose alignment an equation does not keep; the column
                    // specification of an array follows its name, where the passes take it: \begin{array}{l}
                    var plain = environmentName.EndsWith("matrix*", StringComparison.Ordinal) ? environmentName.TrimEnd('*') : environmentName;
                    result.Append('\\').Append(cw).Append('{').Append(plain).Append('}');
                    i = afterName;
                    while (i < tex.Length && tex[i] == " ")
                        i++;
                    if (plain != environmentName && cw == "begin" && tex[i] == "[")
                    {
                        while (i < tex.Length && tex[i] != "]")
                            i++;
                        i++;
                    }
                    continue;
                }
                // a combining mark over or under a base, \overset{̸}{b}, is its accent, as UnicodeMath writes it: (𝑏)̸
                if ((Stacks.ContainsKey(cw) || cw == "underset") && BracedArgument(tex, j) is var (mark, afterMark) && afterMark >= 0 &&
                    Js.Len(mark) == 1 && IsAccent(mark) && BracedArgument(tex, afterMark) is var (accentBase, afterBase) && afterBase >= 0)
                {
                    result.Append('(').Append(RewriteTeXOnly(accentBase)).Append(')').Append(HoldMark(mark));
                    i = afterBase;
                    continue;
                }
                if (Stacks.TryGetValue(cw, out var stack))
                {
                    var (script, afterScript) = BracedArgument(tex, j);
                    var (body, afterBody) = afterScript < 0 ? (null, -1) : BracedArgument(tex, afterScript);
                    if (afterBody >= 0)
                    {
                        // grouped whole: 〖a〗┴(→)〖b〗┴(→) is not UnicodeMath
                        result.Append("〖〖").Append(RewriteTeXOnly(body)).Append('〗').Append(stack).Append('(').Append(RewriteTeXOnly(script)).Append(")〗");
                        i = afterBody;
                        continue;
                    }
                }
                else if (cw == "begin" && BracedArgument(tex, j) is var (environment, afterBegin) && (environment == "equation" || environment == "equation*"))
                {
                    // Word's numbered equation, \begin{equation}\label{eqN}...\end{equation}: the equation with #(N)
                    var endMarker = "\\end{" + environment + "}";
                    var end = tex.Find(endMarker, afterBegin);
                    var bodyText = new Text(end < 0 ? tex.Sub(afterBegin) : tex.Sub(afterBegin, end));
                    var body = bodyText.ToString();
                    var label = "";
                    var labelAt = bodyText.Find("\\label", 0);
                    if (labelAt >= 0)
                    {
                        var (name, afterLabel) = BracedArgument(bodyText, labelAt + 6);
                        if (afterLabel >= 0)
                        {
                            label = name.StartsWith("eq:", StringComparison.Ordinal) ? name.Substring(3) : name.StartsWith("eq", StringComparison.Ordinal) ? name.Substring(2) : name;
                            body = bodyText.Sub(0, labelAt) + bodyText.Sub(afterLabel);
                        }
                    }
                    result.Append(RewriteTeXOnly(body));
                    if (label.Length > 0)
                        result.Append("#(").Append(label).Append(')');
                    i = end < 0 ? tex.Length : end + Js.Len(endMarker);
                    continue;
                }
                else if (TextCommands.Contains(cw))
                {
                    var (text, afterText) = BracedArgument(tex, j);
                    if (afterText >= 0)
                    {
                        result.Append("\\text{").Append(HoldTextEscapes(text)).Append('}');
                        i = afterText;
                        continue;
                    }
                }
                else if ((cw == "left" || cw == "right") && j < tex.Length && tex[j] == ".")
                {
                    // the empty delimiter: UnicodeMath's ├ or ┤ before a space
                    result.Append('\\').Append(cw).Append(' ');
                    i = j + 1;
                    continue;
                }
                else if (cw == "color" || cw == "colorbox")
                {
                    var (color, afterColor) = BracedArgument(tex, j);
                    if (afterColor >= 0)
                    {
                        var (body, afterBody) = BracedArgument(tex, afterColor);
                        if (afterBody < 0)
                        {
                            afterBody = GroupEnd(tex, afterColor);
                            body = tex.Sub(afterColor, afterBody);
                        }
                        // \color{} has no color: the body alone
                        if (color.Trim().Length == 0)
                            result.Append(RewriteTeXOnly(body));
                        else
                            result.Append(cw == "color" ? "✎(" : "☁(").Append(color.Trim()).Append("&{").Append(RewriteTeXOnly(body)).Append("})");
                        i = afterBody;
                        continue;
                    }
                }
                else if (FontSwitches.TryGetValue(cw, out var font))
                {
                    // an old font switch, {\rm rxn}, styles the rest of its group: \mathrm{rxn}
                    var end = GroupEnd(tex, j);
                    result.Append('\\').Append(font).Append('{').Append(RewriteTeXOnly(tex.Sub(j, end).Trim())).Append('}');
                    i = end;
                    continue;
                }
                result.Append(tex.Sub(i, j));
                i = j;
            }
            return result.ToString();
        }

        // the font switches of plain TeX, which style the rest of their group, as the LaTeX commands with an argument
        private static readonly Dictionary<string, string> FontSwitches = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rm"] = "mathrm", ["bf"] = "mathbf", ["it"] = "mathit", ["cal"] = "mathcal", ["sf"] = "mathsf", ["tt"] = "mathtt",
        };

        /// <summary>The index of the brace that closes the group the index is in, or the length of the text.</summary>
        private static int GroupEnd(Text tex, int i)
        {
            int depth = 0;
            for (; i < tex.Length; i++)
            {
                if (tex[i] == "\\")
                    i++;
                else if (tex[i] == "{")
                    depth++;
                else if (tex[i] == "}" && depth-- == 0)
                    break;
            }
            return Math.Min(i, tex.Length);
        }

        // the commands with one or two arguments that can be a script without braces, as x_\text{ab} or x^\frac12
        private static readonly Dictionary<string, int> ArgumentCommands = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["text"] = 1, ["textrm"] = 1, ["textup"] = 1, ["textnormal"] = 1, ["mbox"] = 1, ["mathrm"] = 1, ["mathbf"] = 1,
            ["mathit"] = 1, ["mathcal"] = 1, ["mathbb"] = 1, ["mathfrak"] = 1, ["mathsf"] = 1, ["mathtt"] = 1, ["mathscr"] = 1,
            ["boldsymbol"] = 1, ["operatorname"] = 1, ["boxed"] = 1, ["sqrt"] = 1, ["hat"] = 1, ["bar"] = 1, ["vec"] = 1, ["dot"] = 1,
            ["ddot"] = 1, ["tilde"] = 1, ["widehat"] = 1, ["widetilde"] = 1, ["overline"] = 1, ["underline"] = 1,
            ["frac"] = 2, ["dfrac"] = 2, ["tfrac"] = 2, ["cfrac"] = 2, ["binom"] = 2, ["dbinom"] = 2, ["tbinom"] = 2,
        };

        /// <summary>The command with its braced arguments at the index (after blanks), and the index after it.</summary>
        private static (string Command, int Next) ScriptCommand(Text tex, int i)
        {
            while (i < tex.Length && tex[i] == " ")
                i++;
            if (i >= tex.Length || tex[i] != "\\")
                return (null, -1);
            var cw = ControlWord(tex, i + 1);
            int j = i + 1 + cw.Length;
            if (!ArgumentCommands.TryGetValue(cw, out var count))
                return (null, -1);
            int next = j;
            for (int argument = 0; argument < count; argument++)
            {
                while (next < tex.Length && tex[next] == " ")
                    next++;
                // a braced argument, or a single character (\frac12)
                if (tex[next] == "{")
                    (_, next) = BracedArgument(tex, next);
                else if (next < tex.Length && tex[next] != "\\" && tex[next] != "}")
                    next++;
                else
                    next = -1;
                if (next < 0)
                    return (null, -1);
            }
            return (tex.Sub(i, next), next);
        }

        // the commands whose bar is a delimiter: \left|, \right|, \big|
        private static readonly HashSet<string> DelimiterCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "left", "right", "middle", "big", "Big", "bigg", "Bigg", "bigl", "bigr", "Bigl", "Bigr", "biggl", "biggr", "Biggl", "Biggr", "bigm", "Bigm",
        };

        /// <summary>
        /// A bar (| or ‖) left without its pair, as in 𝑎|𝑏, escaped: in UnicodeMath a bar opens an absolute value or a
        /// norm that a second one closes. Bars are paired within their brackets; one bar alone between brackets (𝑃(𝑐|𝑑),
        /// {𝑥|𝑥>0}) is the separator UnicodeMath makes of it, a bar before a closing angle with no opening one is a ket
        /// (|𝑟⟩), and the bars of \left, \right and the like are delimiters.
        /// </summary>
        private static string EscapeOddBars(string latex)
        {
            var tex = new Text(latex);
            var frames = new Stack<List<int>>();
            frames.Push(new List<int>());
            var escaped = new List<int>();
            var kets = new HashSet<int>();
            bool ZeroSpacesBetween(int first, int second)
            {
                bool zero = false;
                for (int k = first + 1; k < second; k++)
                {
                    if (tex[k] == "\\" && tex[k + 1] == "!")
                    {
                        zero = true;
                        k++;
                    }
                    else if (tex[k] != " ")
                        return false;
                }
                return zero;
            }
            void Close(List<int> bars, bool inBrackets)
            {
                foreach (var bar in new[] { "|", "‖", "∣" })
                {
                    var some = bars.Where(b => tex[b] == bar).ToList();
                    if (inBrackets && some.Count == 1 && bar != "‖")
                        continue;
                    // bars in pairs, but two with only zero spaces between them (|\!|, which UnicodeMath would read as
                    // ||, one double bar): the first is then a bar alone
                    for (int k = 0; k < some.Count; k++)
                    {
                        if (k + 1 < some.Count && !ZeroSpacesBetween(some[k], some[k + 1]))
                            k++;
                        else
                            escaped.Add(some[k]);
                    }
                }
            }
            for (int i = 0; i < tex.Length; i++)
            {
                var ch = tex[i];
                if (ch == "\\")
                {
                    var cw = ControlWord(tex, i + 1);
                    // the brackets among the commands (\{, \}, \langle, \rangle) become the bracket they stand for; an
                    // escaped character (\|) is skipped
                    ch = cw switch
                    {
                        "" => tex[i + 1] == "{" ? "(" : tex[i + 1] == "}" ? ")" : null,
                        "langle" => "⟨",
                        "rangle" => "⟩",
                        _ => "\\",
                    };
                    int j = i + 1 + Math.Max(1, cw.Length);
                    if (ch == "\\")
                    {
                        while (tex[j] == " ")
                            j++;
                        // the bar after \left, \right... is skipped with the command
                        i = DelimiterCommands.Contains(cw) && (tex[j] == "|" || tex[j] == "‖") ? j : j - 1;
                        continue;
                    }
                    i = j - 1;
                }
                switch (ch)
                {
                    case "(" or "[" or "⟨":
                        frames.Push(new List<int>());
                        break;
                    case ")" or "]" or "⟩" when frames.Count > 1:
                        Close(frames.Pop(), true);
                        break;
                    case "⟩" when frames.Peek().Count > 0:
                        // a ket (|𝑟⟩): its bar has no pair, and ∣ (\mid) there is the bar of the ket
                        var ket = frames.Peek()[frames.Peek().Count - 1];
                        if (tex[ket] == "∣")
                            kets.Add(ket);
                        frames.Peek().RemoveAt(frames.Peek().Count - 1);
                        break;
                    case "|" or "‖" or "∣":
                        frames.Peek().Add(i);
                        break;
                }
            }
            while (frames.Count > 0)
                Close(frames.Pop(), false);
            if (escaped.Count == 0 && kets.Count == 0)
                return latex;
            var result = new StringBuilder();
            for (int i = 0; i < tex.Length; i++)
            {
                if (escaped.Contains(i))
                    result.Append('\\');
                result.Append(kets.Contains(i) ? "|" : tex[i]);
            }
            return result.ToString();
        }

        /// <summary>The count arguments (braced or single characters) at the index, and the index after them; null if missing.</summary>
        private static (List<string> Arguments, int Next)? Arguments(Text tex, int i, int count)
        {
            var arguments = new List<string>();
            for (int n = 0; n < count; n++)
            {
                var (argument, next) = Argument(tex, i);
                if (next < 0)
                    return null;
                arguments.Add(argument);
                i = next;
            }
            return (arguments, i);
        }

        /// <summary>A braced argument or a single character (\pmod n), and the index after it; -1 if there is none.</summary>
        private static (string Text, int Next) Argument(Text tex, int i)
        {
            while (i < tex.Length && tex[i] == " ")
                i++;
            if (i < tex.Length && tex[i] == "{")
                return BracedArgument(tex, i);
            // a command with its arguments (\not\operatorname{R}), a command (\frac\alpha\beta, \not\in) or an escaped
            // character (\genfrac\{]...)
            if (i + 1 < tex.Length && tex[i] == "\\")
            {
                if (ScriptCommand(tex, i) is var (command, afterCommand) && command != null)
                    return (command, afterCommand);
                var end = i + 1 + Math.Max(1, ControlWord(tex, i + 1).Length);
                return (tex.Sub(i, end), end);
            }
            return i < tex.Length && tex[i] != "\\" && tex[i] != "}" ? (tex[i], i + 1) : (null, -1);
        }

        /// <summary>Whether a script written now would have no base: at the start of the equation, of a group or of a cell.</summary>
        private static bool HasNoBase(StringBuilder result)
        {
            int end = result.Length;
            while (end > 0 && result[end - 1] == ' ')
                end--;
            return end == 0 || result[end - 1] is '{' or '(' or '[' or '&' or '〖' || end > 1 && result[end - 1] == '\\' && result[end - 2] == '\\';
        }

        /// <summary>The optional [...] argument at the index (after blanks), null if there is none, and the index after it.</summary>
        private static (string Text, int Next) OptionalArgument(Text tex, int i)
        {
            int j = i;
            while (j < tex.Length && tex[j] == " ")
                j++;
            if (tex[j] != "[")
                return (null, i);
            int end = tex.IndexOf("]", j);
            return end < 0 ? (null, i) : (tex.Sub(j + 1, end), end + 1);
        }

        // the extensible arrows of amsmath and mathtools, \xrightarrow[under]{over}
        private static readonly Dictionary<string, string> ExtensibleArrows = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["xrightarrow"] = "\\rightarrow", ["xleftarrow"] = "\\leftarrow", ["xRightarrow"] = "\\Rightarrow", ["xLeftarrow"] = "\\Leftarrow",
            ["xleftrightarrow"] = "\\leftrightarrow", ["xLeftrightarrow"] = "\\Leftrightarrow", ["xhookleftarrow"] = "\\hookleftarrow",
            ["xhookrightarrow"] = "\\hookrightarrow", ["xmapsto"] = "\\mapsto",
        };

        // the arrows of amsmath under a base, \underleftarrow{a}
        private static readonly Dictionary<string, string> UnderArrows = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["underleftarrow"] = "\\leftarrow", ["underrightarrow"] = "\\rightarrow", ["underleftrightarrow"] = "\\leftrightarrow",
        };

        /// <summary>The text of the {...} argument at the index (after blanks) and the index after it; -1 if there is none.</summary>
        private static bool IsAsciiLetterOrDigit(char c) => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9';

        /// <summary>
        /// The argument of a font command in the font, as TeX sets it: its ASCII letters and digits, between escaped braces
        /// too (\mathrm{\{b\}}), but not the text (\text{var}), which has the text font, nor the argument of an inner font
        /// command, which has its own. Upright letters are quoted, as UnicodeMath writes them; a digit, which has no bold
        /// italic form, is bold upright in a bold italic style (\boldsymbol{1}, 𝟏).
        /// </summary>
        private static string InFont(string argument, int style)
        {
            var tex = new Text(argument);
            var mathStyle = MathStyles[style];
            var digitStyle = mathStyle switch
            {
                "mbfit" or "mbfscr" or "mbffrak" => "mbf",
                "mbfitsans" => "mbfsans",
                _ => mathStyle,
            };
            var result = new StringBuilder();
            for (int i = 0; i < tex.Length;)
            {
                var ch = tex[i];
                int end;
                switch (ch)
                {
                    case "ⓣ" when tex[i + 1] == "{" && FindClosingBrace(tex, i + 2) is var close && close != -1:
                        end = close + 1;
                        break;
                    case "Ⅎ" when IsAsciiAlphabetic(tex[i + 1]) && tex[i + 2] == "{" && FindClosingBrace(tex, i + 3) is var close && close != -1:
                        end = close + 1;
                        break;
                    case "\\" when IsAsciiAlphabetic(tex[i + 1]):
                        end = i + 1 + ControlWord(tex, i + 1).Length;
                        break;
                    case "\\":
                        end = System.Math.Min(i + 2, tex.Length);
                        break;
                    case var _ when IsAsciiAlphabetic(ch):
                        end = i;
                        while (IsAsciiAlphabetic(tex[end]))
                            end++;
                        var letters = tex.Sub(i, end);
                        // a function name (\sin, which the first pass leaves as its letters) is in its own font
                        if (IsFunctionName(letters))
                            result.Append(letters);
                        else if (mathStyle == "mup")
                            result.Append(Quoted(letters));
                        else
                            result.Append(string.Concat(Characters(letters).Select(c => GetMathAlphanumeric(c, mathStyle))));
                        i = end;
                        continue;
                    case var _ when ch.Length == 1 && char.IsLetter(ch[0]) && mathStyle != "mup":
                        result.Append(GetMathAlphanumeric(ch, mathStyle));
                        i++;
                        continue;
                    case var _ when ch.Length == 1 && ch[0] >= '0' && ch[0] <= '9':
                        result.Append(GetMathAlphanumeric(ch, digitStyle));
                        i++;
                        continue;
                    default:
                        end = i + 1;
                        break;
                }
                result.Append(tex.Sub(i, end));
                i = end;
            }
            return result.ToString();
        }

        private static (string Text, int Next) BracedArgument(Text tex, int i)
        {
            while (i < tex.Length && tex[i] == " ")
                i++;
            if (i >= tex.Length || tex[i] != "{")
                return (null, -1);
            int depth = 0;
            for (int j = i; j < tex.Length; j++)
            {
                if (tex[j] == "\\")
                    j++;
                else if (tex[j] == "{")
                    depth++;
                else if (tex[j] == "}" && --depth == 0)
                    return (tex.Sub(i + 1, j), j + 1);
            }
            return (null, -1);
        }

        /// <summary>
        /// The environments of \newenvironment{name}[n][default]{begin}{end} (and \renewenvironment) applied: the
        /// definitions removed and kept in the macros under {name}, where later conversions find them, and each
        /// \begin{name}{arguments}...\end{name} replaced by the begin code with its arguments (#1...), the body and the
        /// end code, as TeX expands them.
        /// </summary>
        private string NewEnvironments(string latex)
        {
            var tex = new Text(latex);
            var result = new StringBuilder();
            int expansions = 0;
            for (int i = 0; i < tex.Length;)
            {
                var cw = tex[i] == "\\" ? ControlWord(tex, i + 1) : "";
                int j = i + 1 + cw.Length;
                switch (cw)
                {
                    case "newenvironment" or "renewenvironment" when BracedArgument(tex, j) is var (name, afterName) && afterName >= 0 &&
                        EnvironmentDefinition(tex, afterName) is var (_, _, _, _, afterDefinition) && afterDefinition >= 0:
                        macros["{" + name.Trim() + "}"] = tex.Sub(afterName, afterDefinition);
                        i = afterDefinition;
                        continue;
                    case "begin" when BracedArgument(tex, j) is var (name, afterName) && afterName >= 0 &&
                        macros.TryGetValue("{" + name.Trim() + "}", out var definition) &&
                        EnvironmentDefinition(new Text(definition), 0) is var (count, optional, begin, end, _) &&
                        EnvironmentArguments(tex, afterName, count, optional) is var (arguments, afterArguments) && afterArguments >= 0 &&
                        EnvironmentEnd(tex, afterArguments, name.Trim()) is var (bodyEnd, afterEnd) && bodyEnd >= 0:
                        if (++expansions > 1000)
                            throw new FormatException("The LaTeX environment " + name.Trim() + " does not stop expanding.");
                        // the expansion read again, for the environments it uses
                        var expansion = new StringBuilder();
                        foreach (var part in new[] { Substitute(begin, arguments), tex.Sub(afterArguments, bodyEnd), end, tex.Sub(afterEnd) })
                            Join(expansion, part);
                        tex = new Text(expansion.ToString());
                        i = 0;
                        if (ContinuesControlWord(result, expansion.ToString()))
                            result.Append(' ');
                        continue;
                    default:
                        var next = cw.Length > 0 ? j : i + (tex[i] == "\\" ? 2 : 1);
                        result.Append(tex.Sub(i, next));
                        i = next;
                        continue;
                }
            }
            return result.ToString();
        }

        /// <summary>The [n][default]{begin}{end} of an environment definition at the index; Next -1 if there is none.</summary>
        private static (int Count, string Optional, string Begin, string End, int Next) EnvironmentDefinition(Text tex, int i)
        {
            var (countText, afterCount) = OptionalArgument(tex, i);
            int count = 0;
            if (countText != null && (!int.TryParse(countText.Trim(), out count) || count < 0 || count > 9))
                return (0, null, null, null, -1);
            var (optional, afterOptional) = countText == null ? (null, afterCount) : OptionalArgument(tex, afterCount);
            var (begin, afterBegin) = BracedArgument(tex, afterOptional);
            var (end, afterEnd) = afterBegin < 0 ? (null, -1) : BracedArgument(tex, afterBegin);
            return (count, optional, begin, end, afterEnd);
        }

        /// <summary>The arguments of an environment at the index (the first one optional when it has a default); Next -1 if missing.</summary>
        private static (List<string> Arguments, int Next) EnvironmentArguments(Text tex, int i, int count, string optional)
        {
            var arguments = new List<string>();
            if (optional != null && count > 0)
            {
                var (given, afterGiven) = OptionalArgument(tex, i);
                arguments.Add(given ?? optional);
                i = afterGiven;
            }
            return Arguments(tex, i, count - arguments.Count) is var (rest, next) ? (arguments.Concat(rest).ToList(), next) : (null, -1);
        }

        /// <summary>The index of the \end{name} that closes an environment whose body starts at the index, and the index after it; -1 if none.</summary>
        private static (int End, int Next) EnvironmentEnd(Text tex, int i, string name)
        {
            int depth = 1;
            for (; i < tex.Length; i++)
            {
                if (tex[i] != "\\")
                    continue;
                var cw = ControlWord(tex, i + 1);
                if (cw is "begin" or "end" && BracedArgument(tex, i + 1 + cw.Length) is var (environment, after) && after >= 0 && environment.Trim() == name)
                {
                    depth += cw == "begin" ? 1 : -1;
                    if (depth == 0)
                        return (i, after);
                }
                i += Math.Max(1, cw.Length);
            }
            return (-1, -1);
        }

        /// <summary>The begin code of an environment with its #1... replaced by the arguments, and ## by #.</summary>
        private static string Substitute(string code, List<string> arguments)
        {
            var tex = new Text(code);
            var result = new StringBuilder();
            for (int i = 0; i < tex.Length; i++)
            {
                switch (tex[i])
                {
                    case "#" when tex[i + 1] == "#":
                        result.Append('#');
                        i++;
                        break;
                    case "#" when tex[i + 1] is { Length: 1 } digit && digit[0] is >= '1' and <= '9' && digit[0] - '1' < arguments.Count:
                        result.Append(arguments[digit[0] - '1']);
                        i++;
                        break;
                    default:
                        result.Append(tex[i]);
                        break;
                }
            }
            return result.ToString();
        }

        /// <summary>A part of an expansion appended, after a space if it would continue the control word before it (\alpha x).</summary>
        private static void Join(StringBuilder expansion, string part)
        {
            if (ContinuesControlWord(expansion, part))
                expansion.Append(' ');
            expansion.Append(part);
        }

        private static bool ContinuesControlWord(StringBuilder before, string after)
        {
            var text = new Text(before.ToString());
            int i = text.Length;
            while (i > 0 && IsAsciiAlphabetic(text[i - 1]))
                i--;
            return i < text.Length && text[i - 1] == "\\" && IsAsciiAlphabetic(new Text(after)[0]);
        }

        private string Convert(string latex)
        {
            var tex = new Text(EscapeOddBars(RewriteTeXOnly(Limits(NewEnvironments(latex)))));
            var cwPrev = "";
            bool needMacroPass = false;
            var uniTeX = new StringBuilder();

            for (int i = 0; i < tex.Length;)
            {
                switch (tex[i])
                {
                    case "\\":
                    {
                        i++;
                        // a backslash that ends the LaTeX escapes nothing
                        if (i >= tex.Length)
                            break;
                        if (tex[i] == "]" || tex[i] == ")")
                        {
                            i = tex.Length;
                            break;
                        }
                        if (tex[i] == "{" || tex[i] == "}")
                        {
                            uniTeX.Append('\\').Append(tex[i]);
                            i++;
                            break;
                        }
                        if (tex[i] == "$" || tex[i] == "%")
                        {
                            uniTeX.Append(tex[i]);
                            i++;
                            break;
                        }
                        // a line break, \\, which the space after it does not make \ (a space)
                        if (tex[i] == "\\")
                        {
                            uniTeX.Append("\\\\");
                            i++;
                            break;
                        }
                        var cw = ControlWord(tex, i);
                        i += cw.Length;
                        if (cwPrev == "def")
                        {
                            cwPrev = "";
                            uniTeX.Append('\\').Append(cw);
                            if (tex[i] == "}")
                                i++;
                            needMacroPass = true;
                            macros[cw] = "";
                            continue;
                        }
                        cwPrev = cw;
                        string symbol;
                        if (cw.Length > 0 && macros.TryGetValue(cw, out var macro))
                        {
                            symbol = macro;
                            if (Js.Ch(symbol, 0) == "ⓜ")
                                needMacroPass = true;
                        }
                        else if (cw.Length == 0 && tex[i] == ",")
                        {
                            symbol = "  ";
                            i++;
                        }
                        else if (cw.Length == 0 && Spaces.TryGetValue(tex[i], out var space))
                        {
                            symbol = space;
                            i++;
                        }
                        else if (cw == "qquad")
                        {
                            symbol = "  ";
                            if (tex[i] == " ")
                                i++;
                        }
                        else
                        {
                            symbol = UnicodeMathInput.ResolveControlWords("\\" + cw);
                            // the symbols of LaTeX and amssymb that UnicodeMath has no control word for, or another one
                            // (\succeq is ⪰, ≽ in UnicodeMath), as the writer writes them
                            if (!IsFunctionName(cw) && LaTeXSymbols.TryGetCharacter(cw, out var character) && LaTeXSymbols.Math(symbol).TrimEnd() != "\\" + cw)
                                symbol = character;
                            if (tex[i] == " " && !IsFunctionName(cw))
                                i++;
                            // a function name with limits, as Word writes it (\lim _{x→0}): the space would end the name
                            else if (tex[i] == " " && IsFunctionName(cw))
                            {
                                int next = i;
                                while (tex[next] == " ")
                                    next++;
                                if (tex[next] == "_" || tex[next] == "^")
                                    i = next;
                            }
                        }
                        if (symbol.Length > 0 && Js.Ch(symbol, 0) != "\"")
                            uniTeX.Append(symbol);
                        // a control word not known ends where its letters do: a space ends it for UnicodeMath too, which
                        // would otherwise read the letters or digits after it as part of it (\sgn r, \sgn2)
                        else if (symbol.Length > 0)
                            uniTeX.Append('\\').Append(cw).Append(tex[i] != null && tex[i].Length == 1 && IsAsciiLetterOrDigit(tex[i][0]) ? " " : "");
                        else if (cw.Length > 0)
                            uniTeX.Append('\\').Append(cw);
                        break;
                    }
                    case "\t":
                    case "\n":
                    case "\v":
                    case "\r":
                        uniTeX.Append(' ');
                        i++;
                        break;
                    case "%":
                    {
                        int j = tex.IndexOf("\n", i + 1);
                        if (j == -1)
                            j = tex.Length;
                        i = j + 1;
                        break;
                    }
                    case "{":
                        if (cwPrev == "newcommand")
                        {
                            cwPrev = "def";
                            i++;
                            continue;
                        }
                        uniTeX.Append(tex[i++]);
                        break;
                    case "}":
                        if (i > 0 && tex[i - 1] == "{")
                            uniTeX.Append('⬚');
                        uniTeX.Append(tex[i++]);
                        break;
                    case "~":
                        uniTeX.Append('\u00A0');
                        i++;
                        break;
                    default:
                        uniTeX.Append(tex[i++]);
                        break;
                }
            }

            var text = ApplyMacros(uniTeX.ToString(), macros, needMacroPass);
            return Pass2(text);
        }

        /// <summary>
        /// The macros of UnicodeMath text applied: the definitions (ⓜ\name{...}, ⓜ{\name}[n]{...}, as \def and \newcommand
        /// read) removed, and their uses (\name and their arguments) replaced by their bodies.
        /// </summary>
        [System.Text.RegularExpressions.GeneratedRegex("ⓜ\\{\\\\([A-Za-z]+)\\}")]
        private static partial System.Text.RegularExpressions.Regex BracedMacroName();

        [System.Text.RegularExpressions.GeneratedRegex("ⓜ\\\\([A-Za-z]+)")]
        private static partial System.Text.RegularExpressions.Regex MacroName();

        internal static string ExpandMacros(string unicodemath)
        {
            var text = BracedMacroName().Replace(unicodemath, "ⓜ\\$1");
            // the uses of a macro, which control words not known are written as text ("\\f") until then
            foreach (System.Text.RegularExpressions.Match name in MacroName().Matches(text))
                text = text.Replace("\"\\" + name.Groups[1].Value + " \"", "\\" + name.Groups[1].Value + " ").Replace("\"\\" + name.Groups[1].Value + "\"", "\\" + name.Groups[1].Value);
            return ApplyMacros(text, new Dictionary<string, string>(StringComparer.Ordinal), true, true);
        }

        /// <summary>Whether UnicodeMath text defines macros.</summary>
        internal static bool HasMacros(string unicodemath) => SharpEquations.Internal.Text.CodePoints.Contains(unicodemath, MacroMark);

        // \u24DC, which starts the definition of a macro
        private static readonly Rune MacroMark = new Rune(0x24DC);

        private static string ApplyMacros(string text, IDictionary<string, string> macros, bool needMacroPass, bool accentsAfter = false)
        {
            bool macroDefined = false;
            if (needMacroPass)
            {
                var t = new Text(text);
                for (int i = 0; i < t.Length;)
                {
                    int m = t.IndexOf("ⓜ", i);
                    if (m == -1)
                        break;
                    i = m;
                    if (t[i + 1] == "\\")
                    {
                        var (cw, body, k) = GetMacro(t, i);
                        if (cw.Length > 0)
                        {
                            macros[cw] = body;
                            t = new Text(t.Sub(0, m) + t.Sub(k));
                            macroDefined = true;
                            continue;
                        }
                        i++;
                    }
                    else
                    {
                        var (val, k) = ApplyMacro(t, i + 1, accentsAfter);
                        t = new Text(t.Sub(0, i) + val + t.Sub(k));
                        i += Js.Len(val);
                    }
                }
                text = t.ToString();
            }
            if (macroDefined)
            {
                var t = new Text(text);
                for (int i = 0; ;)
                {
                    i = t.IndexOf("\\", i);
                    if (i == -1)
                        break;
                    var cw = "";
                    int j;
                    for (j = i + 1; j < t.Length && IsAsciiAlphabetic(t[j]); j++)
                        cw += t[j];
                    if (cw.Length == 0 || !macros.TryGetValue(cw, out var body))
                    {
                        i++;
                        continue;
                    }
                    t = new Text(t.Sub(0, i) + body + t.Sub(i + 1 + cw.Length));
                    if (Js.Ch(body, 0) == "ⓜ")
                    {
                        var (val, k) = ApplyMacro(t, i + 1, accentsAfter);
                        t = new Text(t.Sub(0, i) + val + t.Sub(k));
                        // a body that is not a macro with arguments stays as it is, not read again for its own name
                        i += k == i + 1 ? Js.Len(body) - 1 : Js.Len(val);
                    }
                    else
                    {
                        i += Js.Len(body);
                    }
                }
                text = t.ToString();
            }
            return text;
        }

        /// <summary>getMacro: the control word, body and end of a macro definition ⓜ\cw...{body} at i.</summary>
        private static (string, string, int) GetMacro(Text s, int i)
        {
            if (s[i] == "ⓜ" && s[i + 1] == "\\")
            {
                var cw = "";
                int j;
                for (j = i + 2; j < s.Length && IsAsciiAlphabetic(s[j]); j++)
                    cw += s[j];
                while (s[j] == " ")
                    j++;
                int k = s.IndexOf("{", j);
                if (k != -1)
                    k = FindClosingBrace(s, k + 1);
                if (k != -1)
                {
                    var body = s[j] == "#" || s[j] == "[" ? "ⓜ" + s.Sub(j, k + 1) : s.Sub(j + 1, k);
                    return (cw, body, k + 1);
                }
            }
            return ("", "", i);
        }

        /// <summary>applyMacro: the macro whose body starts at i, with its arguments substituted, and the end of its arguments.</summary>
        private static (string, int) ApplyMacro(Text tex, int i, bool accentsAfter)
        {
            int k;
            int cArg = 0;
            if (tex[i] == "[")
            {
                cArg = Js.CodeAt(tex[i + 1], 0) - '0';
                if (tex[i + 2] != "]" || cArg < 0 || cArg > 9)
                    return ("", i);
                k = i + 3;
            }
            else
            {
                for (k = i; k < tex.Length && tex[k] != "{"; k++)
                {
                    if (tex[k] == "#")
                    {
                        if (cArg < 9)
                            cArg++;
                        k++;
                    }
                }
            }
            if (tex[k] != "{")
                return ("", i);
            int j = FindClosingBrace(tex, k + 1);
            if (j == -1)
                return ("", i);

            var macro = new Text(tex.Sub(k + 1, j));
            i = j + 1;
            var args = new List<string>();
            for (j = 0; j < cArg && i < tex.Length; j++)
            {
                var arg = "";
                while (tex[i] == " ")
                    i++;
                if (tex[i] == "{")
                {
                    k = FindClosingBrace(tex, i + 1);
                    if (k == -1)
                        return ("", i);
                    arg = tex.Sub(i + 1, k);
                    i = k + 1;
                }
                else if (tex[i] == "ⓝ")
                {
                    i++;
                }
                else if (tex[i] != null)
                {
                    arg = tex[i];
                    i++;
                }
                args.Add(arg);
            }
            while (args.Count < cArg)
                args.Add("");

            var val = new StringBuilder();
            // an accent passed as an argument (\hat, a combining mark) goes on the character after it, as in LaTeX
            string accent = null;
            for (k = 0; k < macro.Length; k++)
            {
                if (macro[k] == "#")
                {
                    k++;
                    int n = Js.CodeAt(macro[k], 0) - '1';
                    if (n >= 0 && n < args.Count)
                    {
                        if (accentsAfter && Js.Len(args[n]) == 1 && IsAccent(args[n]) && accent == null)
                        {
                            accent = args[n];
                            continue;
                        }
                        val.Append(args[n]);
                    }
                }
                else
                {
                    val.Append(macro[k]);
                }
                if (accent != null && val.Length > 0)
                {
                    val.Append(accent);
                    accent = null;
                }
            }
            if (accent != null)
                val.Append(accent);
            return (val.ToString(), i);
        }

        /// <summary>TeX2UMath: pass 2, LaTeX with UnicodeMath characters to UnicodeMath.</summary>
        private string Pass2(string latex)
        {
            var tex = new Text(latex);
            var uniTeX = new StringBuilder();
            int j, k;
            string val;

            for (int i = 0; i < tex.Length;)
            {
                var ch = tex[i];
                i++;
                uniTeX.Append(ch);
                if (i == tex.Length)
                    break;

                switch (ch)
                {
                    case "ⓗ":
                        if (tex[i] != "{")
                            continue;
                        j = FindClosingBrace(tex, i + 1);
                        if (j == -1)
                            continue;
                        uniTeX.Append('(').Append(tex.Sub(i + 1, j));
                        i = j + 1;
                        if (tex[i] == "{")
                        {
                            j = FindClosingBrace(tex, i + 1);
                            if (j == -1)
                                continue;
                            uniTeX.Append('&').Append(Pass2(tex.Sub(i + 1, j)));
                            i = j + 1;
                        }
                        uniTeX.Append(')');
                        continue;

                    case "⒝":
                    case "⍁":
                        if (tex[i] == "{")
                        {
                            j = FindClosingBrace(tex, i + 1);
                            if (j == -1)
                                continue;
                            uniTeX.Append(Pass2(tex.Sub(i + 1, j))).Append('&');
                            i = j + 1;
                            if (tex[i] == " ")
                                i++;
                            if (tex[i] != "{")
                                continue;
                            j = FindClosingBrace(tex, i + 1);
                            // an argument that is not closed runs to the end (j of -1 would read the LaTeX again, forever)
                            if (j == -1)
                                j = tex.Length;
                            uniTeX.Append(Pass2(tex.Sub(i + 1, j))).Append("〗 ");
                            i = j + 1;
                        }
                        else
                        {
                            uniTeX.Append(tex[i++]).Append('&').Append(tex[i++]).Append("〗 ");
                        }
                        continue;

                    case "⒭":
                        j = tex.IndexOf("▒", i);
                        if (j != -1)
                        {
                            uniTeX.Append(tex.Sub(i, j)).Append('▒');
                            j++;
                            (val, i) = GetArg(tex, j, true);
                            uniTeX.Append(val);
                        }
                        break;

                    case "Ⅎ":
                        if (tex[i + 1] == "{")
                        {
                            j = FindClosingBrace(tex, i + 2);
                            if (j == -1)
                                j = tex.Length;
                            var argument = tex.Sub(i + 2, j);
                            var style = CodePointAt(tex[i], 0) - 'a';
                            // letters and digits in a font (\mathcal{G}) are its math alphanumerics, which take scripts
                            if (style == 0 && argument.Length > 0 && argument.All(IsAsciiLetterOrDigit))
                            {
                                // upright: a letter is an upright identifier ("d"), a word upright text
                                RemoveLast(uniTeX);
                                uniTeX.Append(Quoted(argument));
                            }
                            else if (style > 0 && style < MathStyles.Length && argument.Length > 0 && argument.All(char.IsLetterOrDigit))
                            {
                                RemoveLast(uniTeX);
                                uniTeX.Append(InFont(argument, style));
                            }
                            else if (argument.All(char.IsLetterOrDigit))
                            {
                                uniTeX.Append(tex[i]).Append('(').Append(argument).Append(')');
                            }
                            else
                            {
                                // other mathematics in a font (\mathbf{b\times c}, \mathrm{x_1}): its ASCII letters and digits in
                                // the font, the rest as it is
                                RemoveLast(uniTeX);
                                uniTeX.Append(Pass2(style >= 0 && style < MathStyles.Length ? InFont(argument, style) : argument));
                            }
                            i = j + 1;
                        }
                        break;

                    case "√":
                    case "ⓑ":
                    case "ⓓ":
                    case "▭":
                    case "^":
                    case "_":
                        if (ch == "√" && tex[i] == "[")
                        {
                            j = tex.IndexOf("]", i + 1);
                            if (j != -1)
                            {
                                uniTeX.Append('(').Append(Pass2(tex.Sub(i + 1, j))).Append('&');
                                j++;
                                (val, i) = GetArg(tex, j, false);
                                uniTeX.Append(val).Append(')');
                            }
                            break;
                        }
                        if ((ch == "√" || ch == "ⓑ" || ch == "ⓓ") && NonBlank(tex, i) != "{")
                            break;
                        (val, i) = GetArg(tex, i, true);
                        if (tex[i] == "^" && Js.Ch(val, 0) != "(" && Characters(val).Contains(" "))
                            val = "(" + val + ")";
                        // a script with its own scripts, a_{3_4}: a_(3_4), which UnicodeMath would read as a_3 with _4
                        else if ((ch == "^" || ch == "_") && Js.Ch(val, 0) != "(" && Js.Len(val) > 1 && Characters(val).Any(c => c == "^" || c == "_"))
                            val = "(" + val + ")";
                        uniTeX.Append(val);
                        if (i < tex.Length && !"+-=/^_ )".Contains(tex[i]))
                            uniTeX.Append(' ');
                        break;

                    case "\\":
                        if (tex[i] == "{" || tex[i] == "}")
                        {
                            RemoveLast(uniTeX);
                            uniTeX.Append(tex[i]);
                            i++;
                        }
                        else if (LaTeXSerializer.IsStructureOperator(tex[i]))
                        {
                            // an operator of UnicodeMath escaped, \▭: the character itself, which UnicodeMath reads escaped
                            uniTeX.Append(tex[i]);
                            i++;
                        }
                        break;

                    case "{":
                    {
                        (j, k) = FindClosingBrace(tex, i, "/");
                        if (j == -1)
                            break;
                        if (k > 0)
                        {
                            var op = tex[k] == "/" ? "⍁" : "⒝";
                            RemoveLast(uniTeX);
                            uniTeX.Append(op).Append(Pass2(tex.Sub(i, k))).Append('&').Append(Pass2(tex.Sub(k + 1, j))).Append('〗');
                            i = j + 1;
                            break;
                        }
                        RemoveLast(uniTeX);
                        // a group with a script ({a+b}^2) is in invisible brackets, which a space would part from it
                        uniTeX.Append('〖').Append(Pass2(tex.Sub(i, j))).Append(tex[j + 1] == "_" || tex[j + 1] == "^" || tex[j + 1] == "┬" || tex[j + 1] == "┴" || IsPrime(tex[j + 1]) ? "〗" : "〗 ");
                        i = j + 1;
                        break;
                    }

                    case "Ⓣ":
                    case "ⓞ":
                    {
                        val = tex[i];
                        if (val == "*")
                            i++;
                        if (tex[i] != "{")
                            break;
                        j = FindClosingBrace(tex, i + 1);
                        if (j == -1)
                            break;
                        RemoveLast(uniTeX);
                        if (ch == "Ⓣ")
                        {
                            uniTeX.Append('#');
                            if (val == "*")
                                uniTeX.Append(tex.Sub(i + 1, j));
                            else
                                uniTeX.Append('(').Append(tex.Sub(i + 1, j)).Append(')');
                        }
                        else
                        {
                            if (IsAsciiAlphabetic(Last(uniTeX)))
                                uniTeX.Append(' ');
                            uniTeX.Append(tex.Sub(i + 1, j)).Append('⁡');
                        }
                        i = j + 1;
                        break;
                    }

                    case "ⓤ":
                    {
                        if (tex[i] != "{")
                            break;
                        (val, j) = GetArg(tex, i, true);
                        if (tex[j] == "{")
                        {
                            RemoveLast(uniTeX);
                            var (basE, end) = GetArg(tex, j, false);
                            // a base with its own scripts (\lim^2, \int^b) is grouped, which the limit goes under
                            if (Js.Len(basE) > 1 && Characters(basE).Any(c => c == "^" || c == "_" || c == "┬" || c == "┴"))
                                basE = "〖" + basE + "〗";
                            uniTeX.Append(basE).Append('┬').Append(val).Append(' ');
                            i = end;
                        }
                        break;
                    }

                    case "〖":
                    {
                        if (tex[i] != "{")
                            break;
                        j = FindClosingBrace(tex, i + 1);
                        if (j == -1)
                            break;
                        var environment = tex.Sub(i + 1, j);
                        if (!Environments.TryGetValue(environment, out var label))
                            throw new FormatException("Unknown LaTeX environment " + environment + ".");
                        i = j + 1;
                        // the column specification of an array ({ll}, {r|c}) and the column count of alignat
                        if ((environment == "array" || environment.StartsWith("alignat", StringComparison.Ordinal)) && i < tex.Length && tex[i] == "{")
                        {
                            j = FindClosingBrace(tex, i + 1);
                            if (j == -1)
                                break;
                            i = j + 1;
                        }
                        RemoveLast(uniTeX);
                        uniTeX.Append(label).Append('(');
                        for (; ; )
                        {
                            j = FindClosingBrace(tex, i);
                            if (j == -1)
                                break;
                            uniTeX.Append(Pass2(tex.Sub(i, j)));
                            if (tex[j] == "〗")
                            {
                                uniTeX.Append(')');
                                j = tex.IndexOf("}", j + 2);
                                if (j == -1)
                                    return uniTeX.ToString();
                                i = j + 1;
                                break;
                            }
                            if (tex[j] == "\\")
                                j++;
                            uniTeX.Append('@');
                            i = j + 1;
                        }
                        break;
                    }

                    case "ⓣ":
                        if (tex[i] != "{")
                            break;
                        for (; ; )
                        {
                            (j, _) = FindClosingBrace(tex, i + 1, "$");
                            if (j == -1)
                                break;

                            if (tex[j] == "$")
                            {
                                (k, _) = FindClosingBrace(tex, j + 1, "$");
                                if (tex[k] != "$")
                                    break;
                                RemoveLast(uniTeX);
                                uniTeX.Append(Quoted(tex.Sub(i + 1, j)));
                                uniTeX.Append(Pass2(tex.Sub(j + 1, k) + " "));
                                if (tex[k + 1] == "}")
                                {
                                    i = k + 2;
                                    break;
                                }
                                i = k;
                                uniTeX.Append('ⓣ');
                            }
                            else
                            {
                                RemoveLast(uniTeX);
                                uniTeX.Append(Quoted(tex.Sub(i + 1, j)));
                                i = j + 1;
                                break;
                            }
                        }
                        break;

                    default:
                        if (IsAccent(ch))
                        {
                            (val, i) = GetArg(tex, i, true);
                            RemoveLast(uniTeX);
                            // the accent of more than one character (\vec{AB}) is over all of them
                            if (Js.Len(val) > 1 && !(Js.Ch(val, 0) == "(" && Js.Ch(val, Js.Len(val) - 1) == ")"))
                                val = "(" + val + ")";
                            uniTeX.Append(val).Append(ch);
                        }
                        else if (OverBrackets.Contains(ch) || UnderBrackets.Contains(ch))
                        {
                            (val, i) = GetArg(tex, i, false);
                            uniTeX.Append('(').Append(val).Append(')');
                            // a space ends the operand of the bar, which would take what follows too (\overline{x}y)
                            if (i < tex.Length && !"^_┴┬ ".Contains(tex[i]))
                                uniTeX.Append(' ');
                        }
                        break;
                }
            }
            return uniTeX.ToString();
        }

        private static string NonBlank(Text tex, int i)
        {
            while (tex[i] == " ")
                i++;
            return tex[i];
        }

        /// <summary>Whether the index is between a \begin and its \end, where \\ parts rows.</summary>
        private static bool InEnvironment(Text tex, int i)
        {
            int depth = 0;
            for (int k = 0; k < i; k++)
            {
                if (tex[k] == "\\")
                {
                    var cw = ControlWord(tex, k + 1);
                    depth += cw switch { "begin" => 1, "end" => -1, _ => 0 };
                    k += Math.Max(1, cw.Length);
                }
            }
            return depth > 0;
        }

        /// <summary>Whether the result ends with \left and its delimiter, with nothing after them.</summary>
        private static bool EndsWithOpening(StringBuilder result)
        {
            var text = result.ToString();
            var at = text.LastIndexOf("\\left", StringComparison.Ordinal);
            if (at < 0)
                return false;
            var delimiter = new Text(text.Substring(at + 5).Trim());
            return delimiter.Length <= 1 || delimiter[0] == "\\" && (delimiter.Length == 2 || ControlWord(delimiter, 1).Length == delimiter.Length - 1);
        }

        // the commands of a space whose length follows them
        private static readonly HashSet<string> LengthCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "hspace", "hskip", "kern", "mkern", "mskip", "mspace",
        };

        // the spaces of plain TeX and amsmath that UnicodeMath has no control word for; the negative ones none, as \!
        private static readonly Dictionary<string, string> SpaceCommands = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["thinspace"] = "\\,", ["negthinspace"] = "\\!", ["negmedspace"] = "\\!", ["negthickspace"] = "\\!", ["nobreakspace"] = "~",
            ["space"] = "\\ ",
        };

        /// <summary>The space of UnicodeMath nearest to a length in mu (18 to the em), \! (none) for a negative one.</summary>
        private static string Space(double mu) => mu switch
        {
            <= 0 => "\\!",
            < 3.5 => "\\,",
            < 4.5 => "\\:",
            < 7 => "\\;",
            < 13.5 => "\\enspace ",
            < 27 => "\\quad ",
            _ => "\\qquad ",
        };

        /// <summary>
        /// The length after a space command, in mu, braced ({10ex}) or not (-1.5pt, 1em plus 2em), and the index after
        /// it; null if it is no number with a unit (\hspace{\fill}).
        /// </summary>
        private static (double? Mu, int Next) LengthArgument(Text tex, int i)
        {
            while (tex[i] == " ")
                i++;
            if (tex[i] == "{")
            {
                var (braced, afterBraced) = BracedArgument(tex, i);
                if (afterBraced < 0)
                    return (null, i);
                var inner = new Text(braced);
                var (mu, afterLength) = Dimension(inner, 0);
                return (NonBlank(inner, afterLength) == null ? mu : null, afterBraced);
            }
            var (length, next) = Dimension(tex, i);
            if (length == null)
                return (null, i);
            // the stretch and the shrink of glue, \hskip 1em plus 1fil minus 2pt
            while (true)
            {
                int word = next;
                while (tex[word] == " ")
                    word++;
                var glue = tex.Sub(word, word + 4) == "plus" ? 4 : tex.Sub(word, word + 5) == "minus" ? 5 : 0;
                if (glue == 0 || Dimension(tex, word + glue) is not (not null, var afterGlue))
                    return (length, next);
                next = afterGlue;
            }
        }

        /// <summary>A number with its unit (-1.5pt, 10 mu, 1fil) in mu, and the index after it; null if none.</summary>
        private static (double? Mu, int Next) Dimension(Text tex, int i)
        {
            while (tex[i] == " ")
                i++;
            int start = i;
            if (tex[i] is "-" or "+")
                i++;
            int digits = i;
            while (tex[i] is { Length: 1 } d && (d[0] is >= '0' and <= '9' or '.'))
                i++;
            if (i == digits || !double.TryParse(tex.Sub(start, i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
                return (null, start);
            while (tex[i] == " ")
                i++;
            // a unit is two letters (1ptx is 1pt and x), but for the infinite ones of glue (fil, fill, filll)
            int unit = i;
            i += 2;
            if (tex.Sub(unit, i) == "fi")
            {
                while (i < unit + 5 && tex[i] == "l")
                    i++;
            }
            double? mu = tex.Sub(unit, i) switch
            {
                "em" => 18,
                "ex" => 7.75,
                "mu" => 1,
                "pt" => 1.8,
                "bp" => 1.81,
                "dd" => 1.93,
                "px" => 1.35,
                "mm" => 5.12,
                "pc" => 21.6,
                "cc" => 23.1,
                "cm" => 51.2,
                "in" => 130.1,
                "sp" => 1.8 / 65536,
                "fil" or "fill" or "filll" => 0,
                _ => null,
            };
            return mu == null ? (null, start) : (number * mu, i);
        }
    }
}
