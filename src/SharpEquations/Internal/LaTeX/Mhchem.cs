namespace SharpEquations.Internal.LaTeX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using static SharpEquations.Internal.UnicodeMath.UnicodeMathText;

    /// <summary>
    /// The \ce and \pu commands of the mhchem package, rewritten as plain LaTeX before the LaTeX is read: upright
    /// element symbols with their counts and charges as scripts, mass and atomic numbers as prescripts, reaction arrows
    /// with their labels over and under them, bonds, states and hydrate dots. The rules for \ce are those of mhchem.go
    /// of treeblood (https://github.com/Wyatt915/treeblood, Copyright (c) 2024 Wyatt Sheffield, MIT License), which
    /// writes MathML, here writing LaTeX. Text is a list of code points.
    /// </summary>
    internal static class Mhchem
    {
        private enum Kind
        {
            Letter,
            Digits,
            Space,
            Command,
            Group,
            Math,
            Open,
            Close,
            Sub,
            Sup,
            Symbol,
        }

        private enum State
        {
            Start,
            Coefficient,
            Species,
            Subscript,
            Group,
            Symbol,
        }

        private sealed class Token
        {
            public Token(Kind kind, string value)
            {
                Kind = kind;
                Value = value;
            }

            public Kind Kind { get; }

            /// <summary>The text of the token; the text inside the braces or the dollars of a group or of math.</summary>
            public string Value { get; }

            /// <summary>For an opening bracket, how many tokens after it the closing one is; 0 if it has none.</summary>
            public int Offset { get; set; }
        }

        /// <summary>A species: its name with the scripts of its count and charge, and the prescripts of its mass and atomic number.</summary>
        private sealed class Atom
        {
            public string Name;
            public string Count;
            public string Charge;
            public string Mass;
            public string Z;

            public bool OnlyPrescripts => Name == null && Count == null && Charge == null;

            public string ToLaTeX()
            {
                var latex = Name;
                if (Count != null)
                    latex += "_{" + Count + "}";
                if (Charge != null)
                    latex += "^{" + Charge + "}";
                if (Mass != null || Z != null)
                    latex = "{" + (Mass != null ? "^{" + Mass + "}" : "") + (Z != null ? "_{" + Z + "}" : "") + latex + "}";
                return latex;
            }
        }

        /// <summary>The LaTeX with its \ce{...} and \pu{...} rewritten as plain LaTeX; the LaTeX itself if it has none.</summary>
        public static string Rewrite(string latex)
        {
            if (latex == null || latex.IndexOf("\\ce", StringComparison.Ordinal) < 0 && latex.IndexOf("\\pu", StringComparison.Ordinal) < 0)
                return latex;
            var tex = Characters(latex);
            var result = new StringBuilder();
            for (int i = 0; i < tex.Count;)
            {
                if (tex[i] != "\\")
                {
                    result.Append(tex[i++]);
                    continue;
                }
                int j = i + 1;
                while (j < tex.Count && IsAsciiLetter(tex[j]))
                    j++;
                if (j == i + 1)
                {
                    // a control symbol, \\ or \{: the backslash and the character it escapes
                    result.Append(tex[i++]);
                    if (i < tex.Count)
                        result.Append(tex[i++]);
                    continue;
                }
                var name = Concat(tex, i + 1, j);
                int k = j;
                while (k < tex.Count && tex[k] == " ")
                    k++;
                if (name is "ce" or "pu" && k < tex.Count && tex[k] == "{")
                {
                    int end = ClosingBrace(tex, k);
                    var argument = Concat(tex, k + 1, end);
                    var rewritten = name == "ce" ? Chemistry(argument) : Units(argument);
                    i = end + 1;
                    int after = i;
                    while (after < tex.Count && tex[after] == " ")
                        after++;
                    // a script after \ce is the script of the whole formula
                    if (rewritten.Length > 0 && after < tex.Count && tex[after] is "^" or "_")
                        rewritten = "{" + rewritten + "}";
                    result.Append(rewritten);
                    if (rewritten.Length > 0 && IsAsciiLetter(rewritten[rewritten.Length - 1].ToString()) && i < tex.Count && IsAsciiLetter(tex[i]))
                        result.Append(' ');
                    continue;
                }
                result.Append(Concat(tex, i, j));
                i = j;
            }
            return result.ToString();
        }

        /// <summary>The plain LaTeX of the argument of \ce.</summary>
        public static string Chemistry(string formula) => Formula.Write(Tokenize(formula), false);

        private static bool IsAsciiLetter(string ch) => ch != null && ch.Length == 1 && (ch[0] >= 'A' && ch[0] <= 'Z' || ch[0] >= 'a' && ch[0] <= 'z');

        private static bool IsDigit(string ch) => ch.Length == 1 && ch[0] >= '0' && ch[0] <= '9';

        private static string Concat(List<string> chars, int start, int end) => string.Concat(chars.Skip(start).Take(end - start));

        /// <summary>The index of the brace that closes the one at the index.</summary>
        private static int ClosingBrace(List<string> chars, int open)
        {
            int depth = 0;
            for (int i = open; i < chars.Count; i++)
            {
                switch (chars[i])
                {
                    case "\\":
                        i++;
                        break;
                    case "{":
                        depth++;
                        break;
                    case "}" when --depth == 0:
                        return i;
                }
            }
            throw new FormatException("A brace of the chemical formula is not closed.");
        }

        private static List<Token> Tokenize(string text)
        {
            var chars = Characters(text);
            var tokens = new List<Token>();
            for (int i = 0; i < chars.Count;)
            {
                var ch = chars[i];
                int j = i + 1;
                switch (ch)
                {
                    case "\\":
                        while (j < chars.Count && IsAsciiLetter(chars[j]))
                            j++;
                        if (j == i + 1 && j < chars.Count)
                        {
                            j++;
                            var escaped = chars[i + 1];
                            tokens.Add(new Token(escaped == "{" ? Kind.Open : escaped == "}" ? Kind.Close : Kind.Command, "\\" + escaped));
                        }
                        else
                        {
                            tokens.Add(new Token(j == i + 1 ? Kind.Symbol : Kind.Command, Concat(chars, i, j)));
                        }
                        break;
                    case "{":
                        j = ClosingBrace(chars, i);
                        tokens.Add(new Token(Kind.Group, Concat(chars, i + 1, j)));
                        j++;
                        break;
                    case "}":
                        throw new FormatException("A brace of the chemical formula closes nothing.");
                    case "$":
                        while (j < chars.Count && chars[j] != "$")
                            j += chars[j] == "\\" ? 2 : 1;
                        if (j >= chars.Count)
                            throw new FormatException("The math of the chemical formula has no closing $.");
                        tokens.Add(new Token(Kind.Math, Concat(chars, i + 1, j)));
                        j++;
                        break;
                    case "(" or "[":
                        tokens.Add(new Token(Kind.Open, ch));
                        break;
                    case ")" or "]":
                        tokens.Add(new Token(Kind.Close, ch));
                        break;
                    case "^":
                        tokens.Add(new Token(Kind.Sup, ch));
                        break;
                    case "_":
                        tokens.Add(new Token(Kind.Sub, ch));
                        break;
                    case var _ when string.IsNullOrWhiteSpace(ch):
                        while (j < chars.Count && string.IsNullOrWhiteSpace(chars[j]))
                            j++;
                        tokens.Add(new Token(Kind.Space, " "));
                        break;
                    case var _ when IsDigit(ch):
                        while (j < chars.Count && IsDigit(chars[j]))
                            j++;
                        tokens.Add(new Token(Kind.Digits, Concat(chars, i, j)));
                        break;
                    case var _ when char.IsLetter(ch, 0):
                        tokens.Add(new Token(Kind.Letter, ch));
                        break;
                    default:
                        tokens.Add(new Token(Kind.Symbol, ch));
                        break;
                }
                i = j;
            }

            var open = new Stack<int>();
            for (int i = 0; i < tokens.Count; i++)
            {
                switch (tokens[i].Kind)
                {
                    case Kind.Open:
                        open.Push(i);
                        break;
                    case Kind.Close when open.Count > 0 && Closing(tokens[open.Peek()].Value) == tokens[i].Value:
                        int at = open.Pop();
                        tokens[at].Offset = i - at;
                        break;
                }
            }
            return tokens;
        }

        private static string Closing(string open) => open switch
        {
            "(" => ")",
            "[" => "]",
            _ => "\\}",
        };

        /// <summary>Text with math between dollars, as \text and the math.</summary>
        private static string TextWithMath(string text)
        {
            var chars = Characters(text);
            var result = new StringBuilder();
            for (int i = 0; i < chars.Count;)
            {
                int j = i;
                while (j < chars.Count && chars[j] != "$")
                    j += chars[j] == "\\" ? 2 : 1;
                if (j > i)
                    result.Append("\\text{").Append(Concat(chars, i, j)).Append('}');
                if (j >= chars.Count)
                    break;
                int k = j + 1;
                while (k < chars.Count && chars[k] != "$")
                    k += chars[k] == "\\" ? 2 : 1;
                if (k >= chars.Count)
                    throw new FormatException("The math of the chemical formula has no closing $.");
                result.Append(Math(Concat(chars, j + 1, k)));
                i = k + 1;
            }
            return result.ToString();
        }

        /// <summary>Math inside a formula: LaTeX, where \ce starts a formula again.</summary>
        private static string Math(string latex) => latex.Trim().Length == 0 ? "" : "{" + Rewrite(latex) + "}";

        private static string Bond(string bond) => bond.Trim() switch
        {
            "1" or "-" => "-",
            "2" or "=" => "=",
            "3" or "#" => "\\equiv",
            "~" or "..." => "\\cdots",
            "...." => "{\\cdot}{\\cdot}{\\cdot}{\\cdot}",
            "~-" => "\\overset{\\cdots}{-}",
            "~--" or "~=" => "\\overset{\\cdots}{=}",
            "-~-" => "\\overset{-}{\\underset{-}{\\cdots}}",
            "->" => "\\rightarrow",
            "<-" => "\\leftarrow",
            _ => throw new FormatException("Unknown chemical bond \\bond{" + bond + "}."),
        };

        /// <summary>The arguments the commands that take some take, which mhchem passes on.</summary>
        private static int Arity(string command) => command switch
        {
            "frac" or "dfrac" or "tfrac" or "cfrac" or "binom" or "overset" or "underset" or "stackrel" => 2,
            "text" or "textrm" or "textup" or "textnormal" or "textit" or "textbf" or "textsf" or "texttt" or "mbox" or "mathrm"
                or "mathit" or "mathbf" or "mathsf" or "mathtt" or "mathcal" or "mathbb" or "mathfrak" or "mathscr" or "boldsymbol"
                or "operatorname" or "sqrt" or "overline" or "underline" or "hat" or "bar" or "vec" or "dot" or "ddot" or "tilde"
                or "widehat" or "widetilde" or "overbrace" or "underbrace" or "boxed" or "phantom" or "hphantom" or "vphantom"
                or "ce" or "pu" or "bond" => 1,
            _ => 0,
        };

        private static bool IsTextCommand(string command) => command.StartsWith("text", StringComparison.Ordinal) ||
            command.StartsWith("math", StringComparison.Ordinal) || command is "mbox" or "operatorname" or "boldsymbol";

        /// <summary>The ends of a LaTeX string written one after another, with a blank after a control word before a letter.</summary>
        private static string Join(IEnumerable<string> parts)
        {
            var result = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0)
                    continue;
                if (IsAsciiLetter(part[0].ToString()) && EndsWithControlWord(result))
                    result.Append(' ');
                result.Append(part);
            }
            return result.ToString();
        }

        private static bool EndsWithControlWord(StringBuilder latex)
        {
            int i = latex.Length - 1;
            while (i >= 0 && IsAsciiLetter(latex[i].ToString()))
                i--;
            return i < latex.Length - 1 && i >= 0 && latex[i] == '\\';
        }

        /// <summary>One formula of \ce, or the script of one, written as LaTeX.</summary>
        private sealed class Formula
        {
            private readonly List<Token> tokens;
            private readonly bool script;
            private readonly List<string> result = new List<string>();
            private Atom atom;
            private State state = State.Start;
            private Kind? promoted;
            private int i;

            private Formula(List<Token> tokens, bool script)
            {
                this.tokens = tokens;
                this.script = script;
            }

            public static string Write(List<Token> tokens, bool script) => new Formula(tokens, script).Write();

            private Token Peek(int ahead = 0) => i + ahead < tokens.Count ? tokens[i + ahead] : null;

            private string Write()
            {
                while (i < tokens.Count)
                {
                    var t = tokens[i++];
                    var next = Peek();
                    if (promoted != null)
                    {
                        Script(t, next);
                        continue;
                    }
                    switch (t.Kind)
                    {
                        case Kind.Group:
                            Flush();
                            if (t.Value.Trim().Length > 0)
                                result.Add(TextWithMath(t.Value));
                            state = State.Group;
                            break;
                        case Kind.Math:
                            Flush();
                            result.Add(Math(t.Value));
                            break;
                        case Kind.Space:
                            Flush();
                            state = State.Start;
                            break;
                        case Kind.Sup when state == State.Start && (next == null || next.Kind == Kind.Space):
                            // a caret alone is a gas evolving
                            Flush();
                            result.Add("\\uparrow");
                            break;
                        case Kind.Sup or Kind.Sub:
                            promoted = t.Kind;
                            break;
                        case Kind.Command:
                            Command(t);
                            break;
                        case Kind.Open:
                            Open(t);
                            break;
                        case Kind.Letter:
                            Letters(t);
                            break;
                        case Kind.Digits:
                            Number(t);
                            break;
                        case Kind.Symbol:
                            Symbol(t, next);
                            break;
                        default:
                            Flush();
                            result.Add(t.Value);
                            state = State.Group;
                            break;
                    }
                }
                Flush();
                if (promoted != null)
                    throw new FormatException("A script of the chemical formula is missing.");
                return Join(result);
            }

            /// <summary>The script that a caret or an underscore has promoted the token to.</summary>
            private void Script(Token t, Token next)
            {
                string scr;
                switch (t.Kind)
                {
                    case Kind.Symbol when t.Value == "-" && next?.Kind == Kind.Digits:
                        i++;
                        scr = "-" + next.Value;
                        break;
                    case Kind.Math:
                        scr = Rewrite(t.Value);
                        break;
                    case Kind.Group:
                        scr = Write(Tokenize(t.Value), true);
                        break;
                    case Kind.Space:
                        throw new FormatException("A script of the chemical formula is missing.");
                    default:
                        var single = new List<Token> { t };
                        for (int n = t.Kind == Kind.Command ? Arity(t.Value.Substring(1)) : 0; n > 0 && Peek()?.Kind == Kind.Group; n--)
                            single.Add(tokens[i++]);
                        scr = Write(single, true);
                        break;
                }
                var superscript = promoted == Kind.Sup;
                promoted = null;
                if (superscript)
                    Superscript(scr);
                else
                    Subscript(scr);
            }

            private void Subscript(string scr)
            {
                if (atom == null)
                    atom = new Atom { Z = scr };
                else if (atom.Z == null && atom.Name == null)
                    atom.Z = scr;
                else if (atom.Count == null && atom.Name != null)
                    atom.Count = scr;
                else
                {
                    Flush();
                    atom = new Atom { Z = scr };
                }
            }

            private void Superscript(string scr)
            {
                if (atom == null)
                    atom = new Atom { Mass = scr };
                else if (atom.Mass == null && atom.Name == null)
                    atom.Mass = scr;
                else if (atom.Charge == null && atom.Name != null)
                    atom.Charge = scr;
                else
                {
                    Flush();
                    atom = new Atom { Mass = scr };
                }
            }

            /// <summary>Writes the current species; scripts without one go to what was written last.</summary>
            private void Flush()
            {
                if (atom == null)
                    return;
                if (atom.Name != null)
                {
                    result.Add(atom.ToLaTeX());
                }
                else
                {
                    var low = atom.Z ?? atom.Count;
                    var high = atom.Mass ?? atom.Charge;
                    var scripts = (low != null ? "_{" + low + "}" : "") + (high != null ? "^{" + high + "}" : "");
                    if (result.Count == 0)
                        result.Add("{}" + scripts);
                    else
                        result[result.Count - 1] = "{" + result[result.Count - 1] + "}" + scripts;
                }
                atom = null;
            }

            /// <summary>A new species, which takes the prescripts written before it.</summary>
            private void Species(string name)
            {
                if (atom != null && atom.OnlyPrescripts)
                {
                    atom.Name = name;
                    return;
                }
                Flush();
                atom = new Atom { Name = name };
            }

            private string Argument()
            {
                int j = i;
                while (j < tokens.Count && tokens[j].Kind == Kind.Space)
                    j++;
                if (j >= tokens.Count || tokens[j].Kind != Kind.Group)
                    return null;
                i = j + 1;
                return tokens[j].Value;
            }

            private void Command(Token t)
            {
                var name = t.Value.Substring(1);
                if (!name.All(c => IsAsciiLetter(c.ToString())))
                {
                    // \\, \, and the other control symbols
                    Flush();
                    result.Add(t.Value);
                    state = State.Start;
                    return;
                }
                switch (name)
                {
                    case "bond":
                        var bond = Argument() ?? throw new FormatException("\\bond has no argument.");
                        Flush();
                        result.Add(Bond(bond));
                        state = State.Start;
                        return;
                    case "ce":
                        Species(Write(Tokenize(Argument() ?? throw new FormatException("\\ce has no argument.")), script));
                        state = State.Species;
                        return;
                    case "pu":
                        Species(Units(Argument() ?? throw new FormatException("\\pu has no argument.")));
                        state = State.Species;
                        return;
                    case "left" or "right" or "middle":
                        Flush();
                        result.Add(t.Value + (i < tokens.Count ? tokens[i++].Value : ""));
                        state = State.Start;
                        return;
                }
                var latex = new StringBuilder(t.Value);
                int arity = Arity(name);
                for (int n = 0; n < arity; n++)
                {
                    var argument = Argument();
                    if (argument == null)
                        break;
                    var rewritten = name is "frac" or "dfrac" or "tfrac" or "cfrac" ? Write(Tokenize(argument), script)
                        : IsTextCommand(name) ? argument : Rewrite(argument);
                    latex.Append('{').Append(rewritten).Append('}');
                }
                Species(latex.ToString());
                state = arity == 0 ? State.Symbol : State.Species;
            }

            private void Open(Token t)
            {
                int open = i - 1;
                if (script || t.Offset <= 0 || open + t.Offset >= tokens.Count)
                {
                    Flush();
                    result.Add(t.Value);
                    return;
                }
                int close = open + t.Offset;
                var inner = tokens.GetRange(open + 1, close - open - 1);
                if (t.Value == "(" && inner.Count == 3 && inner[0].Kind == Kind.Digits && inner[1].Value == "/" && inner[2].Kind == Kind.Digits)
                {
                    // (1/2)H2O: a coefficient in parentheses
                    Flush();
                    result.Add("(" + inner[0].Value + "/" + inner[2].Value + ")");
                    i = close + 1;
                    state = State.Coefficient;
                    return;
                }

                if (t.Value == "(" && inner.Count == 1 && state == State.Start && (inner[0].Value == "v" || inner[0].Kind == Kind.Sup))
                {
                    // (v) a precipitate, (^) a gas
                    Flush();
                    result.Add(inner[0].Value == "v" ? "\\downarrow" : "\\uparrow");
                    i = close + 1;
                    return;
                }
                // a letter alone in parentheses is a state, (s), (l), (g)
                var content = inner.Count == 1 && inner[0].Kind == Kind.Letter ? "\\mathrm{" + inner[0].Value + "}" : Write(inner, false);
                Species(t.Value + content + tokens[close].Value);
                i = close + 1;
                state = State.Species;
            }

            private void Letters(Token t)
            {
                var name = t.Value;
                if (script)
                {
                    // in a script a letter alone is a variable, Fe^n+, letters together a word, Fe^{III}
                    while (Peek()?.Kind == Kind.Letter)
                        name += tokens[i++].Value;
                    Flush();
                    result.Add(name == t.Value ? name : "\\mathrm{" + name + "}");
                    return;
                }
                // an element: a letter and the lowercase letters after it
                while (Peek()?.Kind == Kind.Letter && char.IsLower(Peek().Value, 0))
                    name += tokens[i++].Value;
                if (name == t.Value && (Peek() == null || Peek().Kind == Kind.Space) && !(atom != null && atom.OnlyPrescripts))
                {
                    // a letter alone: v a precipitate, a lowercase letter a variable
                    Flush();
                    result.Add(name == "v" ? "\\downarrow" : char.IsLower(name, 0) ? name : "\\mathrm{" + name + "}");
                    state = State.Start;
                    return;
                }
                Species("\\mathrm{" + name + "}");
                state = State.Species;
            }

            private void Number(Token t)
            {
                var number = t.Value;
                if (script)
                {
                    result.Add(number);
                    return;
                }
                if (Peek()?.Value == "/" && Peek(1)?.Kind == Kind.Digits)
                {
                    number = "\\frac{" + number + "}{" + Peek(1).Value + "}";
                    i += 2;
                }
                else if (state is State.Start or State.Coefficient && Peek()?.Value == "." && Peek(1)?.Kind == Kind.Digits)
                {
                    number += "." + Peek(1).Value;
                    i += 2;
                }
                if (state is State.Start or State.Coefficient)
                {
                    Flush();
                    result.Add(number);
                    state = State.Coefficient;
                    return;
                }
                // a count, which a point after does not continue: KCr(SO4)2.12H2O
                if (atom != null)
                    atom.Count += number;
                else if (result.Count > 0)
                    result[result.Count - 1] = "{" + result[result.Count - 1] + "}_{" + number + "}";
                else
                    result.Add("{}_{" + number + "}");
                state = State.Subscript;
            }

            private void Symbol(Token t, Token next)
            {
                switch (t.Value)
                {
                    case "." or "*" when script:
                        // a radical dot
                        result.Add("\\bullet");
                        break;
                    case "." or "*":
                        // the dot of an adduct or a hydrate
                        Flush();
                        result.Add("\\cdot");
                        state = State.Start;
                        break;
                    case "#":
                        Flush();
                        result.Add("\\equiv");
                        state = State.Start;
                        break;
                    case "+":
                        if (state == State.Start || script)
                        {
                            Flush();
                            result.Add("+");
                            break;
                        }
                        atom ??= new Atom();
                        atom.Charge += "+";
                        break;
                    case "<":
                        Flush();
                        result.Add(Arrow() ?? "<");
                        state = State.Start;
                        break;
                    case "-" when next?.Kind == Kind.Symbol && next.Value == ">":
                        Flush();
                        result.Add(Arrow() ?? "-");
                        state = State.Start;
                        break;
                    case "-" when next != null && next.Kind != Kind.Space && next.Kind != Kind.Group:
                        // a bond, C6H5-CHO, or a hyphen after a symbol, \mu-Cl
                        Flush();
                        result.Add(state == State.Symbol ? "{-}" : "-");
                        state = State.Start;
                        break;
                    case "-" when atom != null && !script:
                        atom.Charge += "-";
                        Flush();
                        state = State.Start;
                        break;
                    case "-" when !script && state == State.Group && result.Count > 0 && (next == null || next.Kind == Kind.Space):
                        // a charge after a group: [AgCl2]-
                        Flush();
                        result[result.Count - 1] = "{" + result[result.Count - 1] + "}^{-}";
                        state = State.Start;
                        break;
                    default:
                        Flush();
                        result.Add(t.Value);
                        state = State.Start;
                        break;
                }
            }

            /// <summary>The arrow that starts at the token before the index, with the labels in brackets after it; null if none does.</summary>
            private string Arrow()
            {
                int start = i - 1;
                int end = start;
                while (end < tokens.Count && end - start < 4 && tokens[end].Kind == Kind.Symbol && tokens[end].Value is "-" or "=" or "<" or ">")
                    end++;
                for (int length = end - start; length >= 2; length--)
                {
                    var arrow = string.Concat(tokens.Skip(start).Take(length).Select(t => t.Value));
                    if (arrow is not ("->" or "<-" or "<->" or "<=>" or "<-->" or "<=>>" or "<<=>"))
                        continue;
                    i = start + length;
                    var above = Label();
                    var below = Label();
                    var latex = arrow switch
                    {
                        "->" => above != null ? "\\xrightarrow{" + above + "}" : "\\longrightarrow",
                        "<-" => above != null ? "\\xleftarrow{" + above + "}" : "\\longleftarrow",
                        _ => Over(above, arrow switch
                        {
                            "<->" => "\\longleftrightarrow",
                            "<=>" => "\\rightleftharpoons",
                            "<-->" => "\\rightleftarrows",
                            "<=>>" => "⥂",
                            _ => "⥄",
                        }),
                    };
                    return below != null ? "\\underset{" + below + "}{" + latex + "}" : latex;
                }
                return null;
            }

            private static string Over(string above, string arrow) => above != null ? "\\overset{" + above + "}{" + arrow + "}" : arrow;

            /// <summary>The label in brackets at the index, a formula; null if there is none or it is empty.</summary>
            private string Label()
            {
                var open = Peek();
                if (open == null || open.Kind != Kind.Open || open.Value != "[" || open.Offset <= 0 || i + open.Offset >= tokens.Count)
                    return null;
                var label = Write(tokens.GetRange(i + 1, open.Offset - 1), false);
                i += open.Offset + 1;
                return label.Length > 0 ? label : null;
            }
        }

        /// <summary>
        /// The plain LaTeX of the argument of \pu: a number, with its exponent as a power of ten, and its units upright,
        /// a thin space between them, the digits after a unit as its power (mol-1).
        /// </summary>
        public static string Units(string quantity)
        {
            var tokens = Tokenize(quantity);
            var parts = new List<string>();
            int i = 0;
            Token Peek(int ahead = 0) => i + ahead < tokens.Count ? tokens[i + ahead] : null;
            bool IsSign(Token t) => t?.Kind == Kind.Symbol && t.Value is "-" or "+" or "−";
            string SignedDigits()
            {
                var sign = "";
                if (IsSign(Peek()) && Peek(1)?.Kind == Kind.Digits)
                    sign = tokens[i++].Value == "+" ? "+" : "-";
                return Peek()?.Kind == Kind.Digits ? sign + tokens[i++].Value : null;
            }

            if (Peek()?.Kind == Kind.Digits || IsSign(Peek()) && Peek(1)?.Kind == Kind.Digits)
            {
                var number = SignedDigits();
                if (Peek()?.Value is "." or "," && Peek(1)?.Kind == Kind.Digits)
                {
                    number += (tokens[i].Value == "," ? "{,}" : ".") + tokens[i + 1].Value;
                    i += 2;
                }

                if (Peek()?.Value is "e" or "E" && (Peek(1)?.Kind == Kind.Digits || IsSign(Peek(1)) && Peek(2)?.Kind == Kind.Digits))
                {
                    i++;
                    number += "\\times 10^{" + SignedDigits() + "}";
                }
                parts.Add(number);
            }

            bool separate = parts.Count > 0;
            while (i < tokens.Count)
            {
                var t = tokens[i++];
                switch (t.Kind)
                {
                    case Kind.Space:
                        separate = parts.Count > 0;
                        continue;
                    case Kind.Letter:
                        var unit = t.Value;
                        while (Peek()?.Kind == Kind.Letter)
                            unit += tokens[i++].Value;
                        var power = SignedDigits();
                        parts.Add((separate ? "\\," : "") + "\\mathrm{" + unit + "}" + (power != null ? "^{" + power + "}" : ""));
                        break;
                    case Kind.Sup or Kind.Sub when parts.Count > 0:
                        var scr = Peek()?.Kind == Kind.Group ? Rewrite(tokens[i++].Value) : SignedDigits() ?? (i < tokens.Count ? tokens[i++].Value : "");
                        parts[parts.Count - 1] += t.Value + "{" + scr + "}";
                        break;
                    case Kind.Symbol when t.Value is "." or "*":
                        parts.Add("\\cdot");
                        break;
                    case Kind.Command when t.Value == "\\cdot":
                        parts.Add("\\cdot");
                        break;
                    case Kind.Group or Kind.Math:
                        parts.Add((separate ? "\\," : "") + Math(t.Value));
                        break;
                    case Kind.Command when t.Value.Skip(1).All(c => IsAsciiLetter(c.ToString())):
                        // a prefix, \mu m: the unit after it follows it without a space
                        parts.Add((separate ? "\\," : "") + t.Value);
                        if (Peek()?.Kind == Kind.Space)
                            i++;
                        separate = false;
                        continue;
                    default:
                        parts.Add(t.Value);
                        break;
                }
                separate = false;
            }
            return Join(parts);
        }
    }
}
