namespace SharpEquations.MathTools.MathEngine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using AngouriMath;
    using MathNet.Symbolics;
    using SharpEquations.MathTools;

    /// <summary>
    /// What the adapter of mathenginen.dll writes and reads, without the library (so that it is tested everywhere): the
    /// linear input of an expression, the command of an action, the meaning of an error code, the values of a result
    /// and the processor a library is built for.
    /// </summary>
    public static class MathEngineText
    {
        /// <summary>The linear input of an expression of AngouriMath: <c>sin(x)^2+(1/2)*x</c>.</summary>
        /// <exception cref="NotSupportedException">Something the engine has no input for.</exception>
        public static string Linear(Entity value) => value switch
        {
            Entity.Number.Integer n => n.EInteger.ToString(),
            Entity.Number.Rational q => "(" + q.ERational.Numerator.ToString() + "/" + q.ERational.Denominator.ToString() + ")",
            Entity.Number.Real r => Real(r),
            Entity.Number.Complex c => "(" + Real(c.RealPart) + "+" + Real(c.ImaginaryPart) + "*i)",
            Entity.Variable v when v == MathS.pi => "pi",
            Entity.Variable v when v == MathS.e => "e",
            Entity.Variable v => MathReader.Symbol(v.Name),
            Entity.Sumf(var a, var b) when Linear(b) is var right && right.StartsWith('-') => Child(a, 1) + right,
            Entity.Sumf(var a, var b) => Child(a, 1) + "+" + Child(b, 1),
            Entity.Minusf(var a, var b) => Child(a, 1) + "-" + Child(b, 2),
            Entity.Mulf(Entity.Number.Integer n, var b) when n.EInteger.IsZero == false && n.EInteger.Sign < 0 =>
                n.EInteger.Abs().Equals(PeterO.Numbers.EInteger.One) ? "-" + Child(b, 2) : "-" + n.EInteger.Abs().ToString() + "*" + Child(b, 2),
            Entity.Mulf(var a, var b) => Child(a, 2) + "*" + Child(b, 2),
            Entity.Divf(var a, var b) => Child(a, 2) + "/" + Child(b, 3),
            Entity.Powf(var a, var b) => Child(a, 4) + "^" + Child(b, 4),
            Entity.Sinf(var a) => Call("sin", a),
            Entity.Cosf(var a) => Call("cos", a),
            Entity.Tanf(var a) => Call("tan", a),
            Entity.Cotanf(var a) => Call("cot", a),
            Entity.Secantf(var a) => Call("sec", a),
            Entity.Cosecantf(var a) => Call("csc", a),
            Entity.Arcsinf(var a) => Call("arcsin", a),
            Entity.Arccosf(var a) => Call("arccos", a),
            Entity.Arctanf(var a) => Call("arctan", a),
            Entity.Arccotanf(var a) => Call("arccot", a),
            Entity.Arcsecantf(var a) => Call("arcsec", a),
            Entity.Arccosecantf(var a) => Call("arccsc", a),
            Entity.Logf(var b, var x) when b == MathS.e => Call("ln", x),
            Entity.Logf(var b, var x) => Call("ln", x) + "/" + Call("ln", b),
            Entity.Absf(var a) => Call("abs", a),
            Entity.Factorialf(var a) => Child(a, 4) + "!",
            Entity.Signumf(var a) => Call("sign", a),
            Entity.Floorf(var a) => Call("floor", a),
            Entity.Ceilf(var a) => Call("ceiling", a),
            Entity.Binomialf(var n, var k) => "combination(" + Linear(n) + "," + Linear(k) + ")",
            Entity.Integralf(var f, var x, var range) when range is var (from, to) =>
                "integral(" + Linear(f) + "," + Linear(x) + "," + Linear(from) + "," + Linear(to) + ")",
            Entity.Summationf(var f, var k, var from, var to) => "seriesSum(" + Linear(f) + "," + Linear(k) + "," + Linear(from) + "," + Linear(to) + ")",
            Entity.Productf(var f, var k, var from, var to) => "seriesProduct(" + Linear(f) + "," + Linear(k) + "," + Linear(from) + "," + Linear(to) + ")",
            Entity.Equalsf(var a, var b) => Linear(a) + "=" + Linear(b),
            _ => throw new NotSupportedException("The engine of Microsoft Mathematics has no input for “" + value.ToString() + "”."),
        };

        private static string Real(Entity.Number.Real r) =>
            r.IsFinite ? r.EDecimal.ToDouble().ToString("R", CultureInfo.InvariantCulture) : r.EDecimal.IsNegative ? "-∞" : "∞";

        private static string Call(string name, Entity argument) => name + "(" + Linear(argument) + ")";

        // a child in brackets when it binds less than its place needs: 1 sums, 2 products, 3 powers, 4 the rest
        private static string Child(Entity value, int needed)
        {
            var text = Linear(value);
            return Binding(value, text) < needed ? "(" + text + ")" : text;
        }

        private static int Binding(Entity value, string text) => value switch
        {
            Entity.Sumf or Entity.Minusf => 1,
            Entity.Mulf or Entity.Divf => 2,
            Entity.Powf => 3,
            Entity.Equalsf => 0,
            _ when text.StartsWith('-') => 1,
            _ => 4,
        };

        /// <summary>
        /// The input of an action on a system of equations (or a single expression): the equation itself for
        /// <see cref="CasAction.Solve"/> (an expression is set equal to 0, the equations of a system are separated by
        /// commas) and for <see cref="CasAction.Evaluate"/>, a function of the engine for the others.
        /// </summary>
        public static string Command(CasAction action, IReadOnlyList<Entity> system, string? variable)
        {
            if (system.Count == 0)
                throw new ArgumentException("No equation.", nameof(system));
            if (action == CasAction.Solve)
                return string.Join(", ", Equations(system));
            if (system.Count > 1)
                throw new NotSupportedException("A system of equations: it can only be solved.");
            var expression = Linear(system[0]);
            var x = variable != null ? MathReader.Symbol(variable) : null;
            return action switch
            {
                CasAction.Evaluate => expression,
                CasAction.Simplify => expression,
                CasAction.Expand => "expand(" + expression + ")",
                CasAction.Factor => "factor(" + expression + ")",
                CasAction.Differentiate => "deriv(" + expression + "," + (x ?? "x") + ")",
                CasAction.Integrate => "integral(" + expression + "," + (x ?? "x") + ")",
                _ => throw new NotSupportedException("The engine of Microsoft Mathematics does not do “" + action + "”."),
            };
        }

        /// <summary>The linear input of each equation of a system to solve: an expression is set equal to 0.</summary>
        public static IReadOnlyList<string> Equations(IReadOnlyList<Entity> system) =>
            system.Select(e => e is Entity.Equalsf ? Linear(e) : Linear(e) + "=0").ToList();

        /// <summary>
        /// The command that solves equations in the format of formulas of the engine (as Microsoft Mathematics asks
        /// for a solution): <c>SymbolicSolve[List[…]]</c>.
        /// </summary>
        public static string SolveCommand(IEnumerable<string> formulas) => "SymbolicSolve[List[" + string.Join(",", formulas) + "]]";

        /// <summary>What an error code of the engine means, or null for 0.</summary>
        public static string? Error(int code)
        {
            if (code == 0)
                return null;
            var kind = (code >> 16) & 0xFFFF;
            var low = code & 0xFFFF;
            var number = low % 2 == 1 ? -(low / 2) : low / 2;
            return kind switch
            {
                2 => "The computation was stopped.",
                1 => "The engine of Microsoft Mathematics cannot read this (error " + number.ToString(CultureInfo.InvariantCulture) + ").",
                _ => "The engine of Microsoft Mathematics cannot compute this (error " + number.ToString(CultureInfo.InvariantCulture) + ").",
            };
        }

        /// <summary>
        /// The values of a result in MathML, one <c>math</c> element each: the pieces of its row between commas,
        /// semicolons and “or”, each the right side of its last <c>=</c>, without what follows <c>≈</c>.
        /// </summary>
        public static IReadOnlyList<XElement> Values(string mathML)
        {
            var root = XElement.Parse(DecodeEntities(mathML));
            XNamespace ns = root.Name.Namespace;
            var row = root;
            while (row.Elements().Count() == 1 && row.Elements().Single().Name.LocalName is "mrow" or "mstyle" or "semantics")
                row = row.Elements().Single();
            if (row.Name.LocalName == "semantics")
                row = row.Elements().First();

            var values = new List<XElement>();
            var piece = new List<XElement>();
            foreach (var item in row.Elements().Where(e => e.Name.LocalName is not ("annotation" or "annotation-xml")))
            {
                if (IsSeparator(item))
                {
                    Add();
                    continue;
                }
                piece.Add(item);
            }
            Add();
            return values;

            void Add()
            {
                if (IsNotValue(string.Concat(piece.Select(e => e.Value))))
                {
                    piece = new List<XElement>();
                    return;
                }

                var approximately = piece.FindIndex(e => IsOperator(e, "≈"));
                var kept = approximately >= 0 ? piece.Take(approximately).ToList() : piece;
                var equals = kept.FindLastIndex(e => IsOperator(e, "="));
                if (equals >= 0)
                    kept = kept.Skip(equals + 1).ToList();
                var value = new XElement(ns + "math", new XElement(ns + "mrow", kept.Select(e => new XElement(e))));
                if (kept.Count > 0 && !values.Any(v => XNode.DeepEquals(v, value)))
                    values.Add(value);
                piece = new List<XElement>();
            }
        }

        private static readonly Dictionary<string, string> Entities = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApplyFunction"] = "\u2061", ["af"] = "\u2061", ["InvisibleTimes"] = "\u2062", ["it"] = "\u2062",
            ["InvisibleComma"] = "\u2063", ["ic"] = "\u2063", ["CenterDot"] = "·", ["middot"] = "·", ["times"] = "×",
            ["ImaginaryI"] = "ⅈ", ["ii"] = "ⅈ", ["ExponentialE"] = "ⅇ", ["ee"] = "ⅇ", ["DifferentialD"] = "ⅆ", ["dd"] = "ⅆ",
            ["pi"] = "π", ["infin"] = "∞", ["minus"] = "−", ["PlusMinus"] = "±", ["pm"] = "±", ["MinusPlus"] = "∓",
            ["approx"] = "≈", ["ne"] = "≠", ["le"] = "≤", ["ge"] = "≥", ["Integral"] = "∫", ["Sum"] = "∑", ["Product"] = "∏",
            ["radic"] = "√", ["Sqrt"] = "√", ["or"] = "∨", ["and"] = "∧", ["deg"] = "°", ["nbsp"] = "\u00A0",
            ["ThinSpace"] = "\u2009", ["MediumSpace"] = "\u205F", ["NegativeThinSpace"] = "", ["ZeroWidthSpace"] = "",
            ["LeftBracketingBar"] = "|", ["RightBracketingBar"] = "|", ["VerticalBar"] = "|", ["DoubleRightArrow"] = "⇒",
            ["RightArrow"] = "→", ["rarr"] = "→", ["Element"] = "∈", ["isin"] = "∈", ["NotElement"] = "∉",
            ["lceil"] = "⌈", ["rceil"] = "⌉", ["lfloor"] = "⌊", ["rfloor"] = "⌋", ["lang"] = "⟨", ["rang"] = "⟩",
            ["NotEqual"] = "≠", ["LessEqual"] = "≤", ["GreaterEqual"] = "≥", ["leq"] = "≤", ["geq"] = "≥",
            ["Nscr"] = "𝒩", ["Zopf"] = "ℤ", ["integers"] = "ℤ", ["Ropf"] = "ℝ", ["reals"] = "ℝ", ["Copf"] = "ℂ",
            ["complexes"] = "ℂ", ["Nopf"] = "ℕ", ["naturals"] = "ℕ", ["Qopf"] = "ℚ",
        };

        // a piece that is a condition (a≠0, n∈ℤ) or a family of values with a parameter (2π𝒩₁): not a value
        private static readonly string[] NotValues = { "≠", "<", ">", "≤", "≥", "∈", "∉", "𝒩", "ℤ", "ℕ", "ℝ", "ℂ", "ℚ" };

        private static bool IsNotValue(string text) => NotValues.Any(n => text.Contains(n, StringComparison.Ordinal));

        /// <summary>
        /// The MathML of the engine with its named entities (<c>&amp;InvisibleTimes;</c>, <c>&amp;ImaginaryI;</c>…)
        /// written as their characters, since XML knows only five of them.
        /// </summary>
        public static string DecodeEntities(string mathML) =>
            Regex.Replace(mathML, "&([A-Za-z][A-Za-z0-9]*);", m => Entities.TryGetValue(m.Groups[1].Value, out var c) ? c : m.Value);

        private static readonly string[] Functions =
        {
            "arcsin", "arccos", "arctan", "arccot", "arcsec", "arccsc", "sinh", "cosh", "tanh", "coth", "sin", "cos", "tan",
            "cot", "sec", "csc", "ln", "log", "exp", "abs", "sqrt", "floor", "ceiling", "sign", "pi", "oo",
        };

        /// <summary>
        /// The values of a result in the linear format of the engine (<c>x=1, x=-4</c>, <c>xcos(x)+sin(x)</c>,
        /// <c>⦗⦗sin⦘^⦗-1⦘⦘(x)</c>), read by AngouriMath: the pieces between commas, semicolons, “or” and “and”, each
        /// the right side of its last <c>=</c>, without what follows <c>≈</c>; ± gives two values.
        /// </summary>
        /// <exception cref="FormatException">A piece AngouriMath cannot read.</exception>
        public static IReadOnlyList<Entity> ReadLinear(string text)
        {
            var values = new List<Entity>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var piece in Pieces(Plain(text)).SelectMany(Unwrapped))
            {
                if (IsNotValue(piece))
                    continue;
                var kept = piece;
                if (kept.IndexOf('≈') is >= 0 and var approximately)
                    kept = kept[..approximately];
                var name = "";
                if (kept.LastIndexOf('=') is >= 0 and var equals)
                    (name, kept) = (kept[..equals], kept[(equals + 1)..]);
                if (kept.Length == 0)
                    continue;
                foreach (var variant in Signs(kept))
                {
                    var input = Implicit(variant);
                    Entity value;
                    try
                    {
                        value = MathS.FromString(input, useCache: false);
                    }
#pragma warning disable CA1031 // any failure of the parser is the same: not a value
                    catch (Exception e) when (e is not OutOfMemoryException)
#pragma warning restore CA1031
                    {
                        throw new FormatException("Not a value: “" + variant + "” (" + input + ").", e);
                    }
                    if (seen.Add(name + "=" + value.ToString()))
                        values.Add(value);
                }
            }
            return values;
        }

        // the values of a solution of a system, (x=2,y=1), one by one
        private static IEnumerable<string> Unwrapped(string piece)
        {
            if (piece.Length > 1 && piece[0] == '(' && Closing(piece, 0) == piece.Length - 1)
            {
                var inner = Pieces(piece[1..^1]).ToList();
                if (inner.Count > 1 || inner[0].Contains('=', StringComparison.Ordinal))
                    return inner.SelectMany(Unwrapped);
            }
            return new[] { piece };
        }

        /// <summary>
        /// The expression with its products and powers of sines and cosines written as sines and cosines of multiple
        /// angles (<c>2 sin x cos x</c> is <c>sin(2x)</c>), by MathNet.Symbolics, when that is simpler; else the same (also when
        /// either library cannot read the other).
        /// </summary>
        public static Entity Contracted(Entity value)
        {
            if (!value.Nodes.Any(n => n is Entity.Sinf or Entity.Cosf))
                return value;
            try
            {
                var contracted = MathS.FromString(Infix.Format(Trigonometric.Contract(Infix.ParseOrThrow(Linear(value)))), useCache: false);
                return contracted.Complexity < value.Complexity ? contracted : value;
            }
            catch (Exception)
            {
                return value;
            }
        }

        /// <summary>The text with two letters swapped, where they are not part of a longer name.</summary>
        public static string Swapped(string text, string a, string b) =>
            Regex.Replace(text, "(?<!\\p{L})(" + Regex.Escape(a) + "|" + Regex.Escape(b) + ")(?!\\p{L})", m => m.Value == a ? b : a);

        /// <summary>MathML with the identifiers of two letters swapped.</summary>
        public static string SwappedMathML(string mathML, string a, string b) =>
            Regex.Replace(mathML, "(<mi[^>]*>)\\s*(" + Regex.Escape(a) + "|" + Regex.Escape(b) + ")\\s*(</mi>)",
                m => m.Groups[1].Value + (m.Groups[2].Value == a ? b : a) + m.Groups[3].Value);

        // the text without the invisible brackets and with the letters AngouriMath reads
        private static string Plain(string text)
        {
            // ⦗⦗sin⦘^⦗-1⦘⦘(x) is arcsin(x), ⦗⦗sin⦘^⦗2⦘⦘(x) is (sin(x))^2
            text = Regex.Replace(text, "⦗⦗(sinh|cosh|tanh|coth|sin|cos|tan|cot|sec|csc)⦘\\^⦗-1⦘⦘(?=\\()", "arc$1");
            // b⦗⦗c⦘^⦗-1⦘⦘ is b/c
            text = Regex.Replace(text, "(?<=[\\p{L}\\p{N})⦘!π∞])⦗⦗([^⦗⦘]+)⦘\\^⦗-([^⦗⦘]+)⦘⦘(?!\\()", m => "/(" + m.Groups[1].Value + (m.Groups[2].Value == "1" ? ")" : ")^(" + m.Groups[2].Value + ")"));
            text = Bars(text);
            for (var m = Regex.Match(text, "⦗⦗([a-z]+)⦘\\^⦗([^⦗⦘]+)⦘⦘\\("); m.Success; m = Regex.Match(text, "⦗⦗([a-z]+)⦘\\^⦗([^⦗⦘]+)⦘⦘\\("))
            {
                var open = m.Index + m.Length - 1;
                var close = Closing(text, open);
                if (close < 0)
                    break;
                text = text[..m.Index] + "((" + m.Groups[1].Value + text[open..(close + 1)] + ")^(" + m.Groups[2].Value + "))" + text[(close + 1)..];
            }
            var plain = new StringBuilder(text)
                .Replace("⦗", "(").Replace("⦘", ")").Replace(" or ", "∨").Replace(" and ", "∧")
                .Replace("π", "pi").Replace("ⅇ", "e").Replace("ⅈ", "i").Replace("∞", "oo").Replace("−", "-")
                .Replace("·", "*").Replace("×", "*").Replace("⋅", "*").Replace("\u2062", "*").Replace("\u2061", "")
                .Replace("\u00A0", "").Replace(" ", "").Replace("\u2009", "");
            return plain.ToString();
        }

        // |x| is abs(x): a bar after a value or a closing bracket closes, any other opens
        private static string Bars(string text)
        {
            var written = new StringBuilder();
            var open = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '|')
                {
                    written.Append(text[i]);
                    continue;
                }

                var before = written.ToString().TrimEnd();
                var closes = open > 0 && before.Length > 0 && (char.IsLetterOrDigit(before[^1]) || before[^1] is ')' or '⦘' or '!' or 'π' or 'ⅇ' or 'ⅈ' or '∞');
                written.Append(closes ? ")" : "abs(");
                open += closes ? -1 : 1;
            }
            return written.ToString();
        }

        private static int Closing(string text, int open)
        {
            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                if (text[i] is '(' or '⦗')
                    depth++;
                else if (text[i] is ')' or '⦘' && --depth == 0)
                    return i;
            }
            return -1;
        }

        // the pieces between separators outside of brackets
        private static IEnumerable<string> Pieces(string text)
        {
            var depth = 0;
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '(' or '[' or '{':
                        depth++;
                        break;
                    case ')' or ']' or '}':
                        depth--;
                        break;
                    case ',' or ';' or '∨' or '∧' when depth == 0:
                        yield return text[start..i];
                        start = i + 1;
                        break;
                }
            }
            yield return text[start..];
        }

        private static readonly System.Buffers.SearchValues<char> PlusMinus = System.Buffers.SearchValues.Create("±∓");

        // a ± (or ∓) gives the value with + and the one with -
        private static IEnumerable<string> Signs(string text)
        {
            var at = text.IndexOfAny(PlusMinus);
            if (at < 0)
                return new[] { text };
            var (first, second) = text[at] == '±' ? ('+', '-') : ('-', '+');
            return Signs(text[..at] + first + text[(at + 1)..]).Concat(Signs(text[..at] + second + text[(at + 1)..]));
        }

        private enum Token
        {
            Value,
            Function,
            Open,
            Close,
            Other,
        }

        // the products the engine writes without a sign (2x, xcos(x), (x-1)(x+1)) with *, for AngouriMath
        private static string Implicit(string text)
        {
            var tokens = new List<(Token Kind, string Text)>();
            for (var i = 0; i < text.Length;)
            {
                var c = text[i];
                if (char.IsDigit(c) || c == '.')
                {
                    var end = i;
                    while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
                        end++;
                    tokens.Add((Token.Value, text[i..end]));
                    i = end;
                }
                else if (char.IsLetter(c))
                {
                    var end = i;
                    while (end < text.Length && char.IsLetter(text[end]))
                        end++;
                    for (var p = i; p < end;)
                    {
                        var run = text[p..end];
                        var name = Functions.FirstOrDefault(f => run.StartsWith(f, StringComparison.Ordinal));
                        if (name is "pi" or "oo")
                            tokens.Add((Token.Value, name));
                        else if (name != null)
                            tokens.Add((Token.Function, name));
                        else
                            tokens.Add((Token.Value, run[..1]));
                        p += name?.Length ?? 1;
                    }
                    i = end;
                }
                else
                {
                    tokens.Add((c switch { '(' => Token.Open, ')' => Token.Close, '√' => Token.Function, _ => Token.Other }, c == '√' ? "sqrt" : c.ToString()));
                    i++;
                }
            }

            var written = new StringBuilder();
            var previous = Token.Other;
            var previousText = "";
            for (var k = 0; k < tokens.Count; k++)
            {
                var (kind, word) = tokens[k];
                var ends = previous is Token.Value or Token.Close || previousText == "!";
                if (ends && kind is Token.Value or Token.Function or Token.Open)
                    written.Append('*');
                // √2 and √x: the root of what follows
                if (kind == Token.Function && word == "sqrt" && k + 1 < tokens.Count && tokens[k + 1].Kind == Token.Value)
                {
                    written.Append("sqrt(").Append(tokens[k + 1].Text).Append(')');
                    k++;
                    (previous, previousText) = (Token.Close, ")");
                    continue;
                }
                written.Append(word);
                (previous, previousText) = (kind, word);
            }
            return written.ToString();
        }

        private static bool IsSeparator(XElement e) =>
            IsOperator(e, ",", ";", "∨", "or") || (e.Name.LocalName == "mtext" && e.Value.Trim() == "or");

        private static bool IsOperator(XElement e, params string[] texts) =>
            e.Name.LocalName == "mo" && texts.Contains(e.Value.Trim());

        /// <summary>The processor of a Windows library (0x8664 for x64, 0x14C for x86), or null if it is not one.</summary>
        public static ushort? Machine(Stream library)
        {
            using var reader = new BinaryReader(library, System.Text.Encoding.ASCII, leaveOpen: true);
            if (library.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
                return null;
            library.Position = 0x3C;
            var header = reader.ReadInt32();
            if (header < 0 || header + 6 > library.Length)
                return null;
            library.Position = header;
            if (reader.ReadUInt32() != 0x00004550)
                return null;
            return reader.ReadUInt16();
        }
    }
}
