namespace SharpEquations.Internal.LaTeX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations.Internal.UnicodeMath;
    using static SharpEquations.Internal.UnicodeMath.Js;
    using static SharpEquations.Internal.UnicodeMath.UnicodeMathText;
    using static SharpEquations.Internal.UnicodeMath.UnicodeMathSerializer;

    /// <summary>
    /// MathML to LaTeX as Word's LaTeX math mode reads it: a port of TeX() of UnicodeMathML (playground/assets/TeX.js),
    /// on XElement. Letters keep their math alphanumeric characters (𝑥), and constructs without a LaTeX command keep
    /// their UnicodeMath (¦, ✎(...)). Strings are indexed by code points.
    /// </summary>
    internal sealed partial class LaTeXSerializer
    {
        private static readonly Dictionary<string, string> AccentsAbove = new Dictionary<string, string>
        {
            ["¯"] = "overbar", ["\u0300"] = "grave", ["\u0301"] = "acute", ["\u0302"] = "hat", ["\u0303"] = "tilde", ["\u0305"] = "bar",
            ["\u0306"] = "breve", ["\u0307"] = "dot", ["\u0308"] = "ddot", ["\u030C"] = "check", ["\u20D7"] = "vec", ["\u20DB"] = "dddot",
            ["\u20DC"] = "ddddot", ["⎴"] = "overbracket", ["⏜"] = "overparen", ["⏞"] = "overbrace", ["⏠"] = "overshell",
        };

        private static readonly Dictionary<string, string> AccentsBelow = new Dictionary<string, string>
        {
            ["⎵"] = "underbracket", ["⏝"] = "underparen", ["⏟"] = "underbrace", ["⏡"] = "undershell", ["▁"] = "underbar",
        };

        private static readonly Dictionary<string, string> Enclosures = new Dictionary<string, string>
        {
            ["box"] = "boxed", ["top"] = "", ["bottom"] = "", ["roundedbox"] = "", ["circle"] = "circle", ["longdiv"] = "", ["actuarial"] = "",
            ["cancel"] = "cancel", ["bcancel"] = "bcancel", ["xcancel"] = "xcancel", ["updiagonalstrike"] = "cancel",
            ["downdiagonalstrike"] = "bcancel", ["updiagonalstrike downdiagonalstrike"] = "xcancel", ["downdiagonalstrike updiagonalstrike"] = "xcancel",
        };

        private static readonly (string Environment, string Intent)[] MatrixIntents =
        {
            ("pmatrix", ":parenthesized-matrix"), ("vmatrix", ":determinant"), ("Vmatrix", ":normed-matrix"), ("bmatrix", ":bracketed-matrix"),
            ("Bmatrix", ":curly-braced-matrix"),
        };

        private static readonly Dictionary<string, string> OperatorMap = new Dictionary<string, string>
        {
            ["&"] = "&", [">"] = ">", ["<"] = "<", ["\u00A0"] = " ", ["\u2061"] = "", ["⋯"] = "⋅⋅⋅",
        };

        private readonly bool standard;

        private LaTeXSerializer(bool standard)
        {
            this.standard = standard;
        }

        /// <summary>
        /// MathMLtoTeX: the LaTeX of a MathML element, for Word's LaTeX math mode; with standard set, LaTeX that
        /// LaTeX itself, KaTeX and MathJax read (see <see cref="LaTeXSymbols"/>).
        /// </summary>
        public static string Serialize(XElement math, bool standard = false)
        {
            var latex = new LaTeXSerializer(standard).TeX(MathML.DefaultIntents.Restore(math));
            return standard ? LaTeXSymbols.Tidy(latex) : latex;
        }

        /// <summary>
        /// A base or a script braced unless it is one unit; in standard LaTeX a command with its arguments is one as a
        /// base (\mathbb{R}^n), not as a script, which LaTeX would part from its arguments (x_{\text{ab}}).
        /// </summary>
        private string CheckBracing(string str, bool isBase = false)
        {
            if (standard)
            {
                if (Len(str) == 0)
                    return "{}";
                if (Len(str) == 1 || SingleCommand().IsMatch(str) || IsGroup(str) || isBase && Ch(str, 0) == "\\" && IsUnit(str))
                    return str;
                return "{" + str + "}";
            }
            int length = Len(str);
            if (length > 2 && !IsUnit(str) || length == 2)
                str = "{" + str + "}";
            return str;
        }

        /// <summary>
        /// Whether the text is one unit that a script takes whole: a group between brackets, or a command with its
        /// arguments (\frac{a}{b}).
        /// </summary>
        private static bool IsUnit(string str)
        {
            var chars = Characters(str);
            int i = 0;
            if (chars[0] == "\\")
            {
                for (i = 1; i < chars.Count && chars[i].Length == 1 && char.IsLetter(chars[i][0]); i++)
                {
                }
                if (i == 1)
                    return false;
                // the space that ends a command name (\sin )
                if (i == chars.Count - 1 && chars[i] == " ")
                    return true;
                // braced arguments, and the optional one of \sqrt (\left[ is no argument)
                bool optional = string.Concat(chars.Skip(1).Take(i - 1)) == "sqrt";
                while (i < chars.Count && (chars[i] == "{" || optional && chars[i] == "["))
                {
                    i = Closing(chars, i);
                    if (i < 0)
                        return false;
                    i++;
                }
                return i == chars.Count;
            }
            // brackets that UnicodeMath pairs, but not as a group: (⟨)…] is not one
            return IsOpenDelimiter(chars[0]) && Closing(chars, 0) == chars.Count - 1 &&
                "([{⟨⌈⌊".IndexOf(chars[0], StringComparison.Ordinal) == ")]}⟩⌉⌋".IndexOf(chars[chars.Count - 1], StringComparison.Ordinal);
        }

        /// <summary>The index of the bracket that closes the one at the index, -1 if none does.</summary>
        private static int Closing(List<string> chars, int i)
        {
            int depth = 0;
            for (; i < chars.Count; i++)
            {
                if (chars[i] == "\\")
                {
                    i++;
                    continue;
                }

                if (IsOpenDelimiter(chars[i]))
                    depth++;
                else if (UnicodeMathSerializer.IsCloseDelimiter(chars[i]) && --depth == 0)
                    return i;
            }
            return -1;
        }

        /// <summary>Whether the text is one brace group: {...} whose first brace closes at its end.</summary>
        private static bool IsGroup(string str)
        {
            var chars = Characters(str);
            if (chars.Count < 2 || chars[0] != "{" || chars[chars.Count - 1] != "}")
                return false;
            int depth = 0;
            for (int i = 0; i < chars.Count; i++)
            {
                if (chars[i] == "\\")
                {
                    i++;
                    continue;
                }

                if (chars[i] == "{")
                    depth++;
                else if (chars[i] == "}" && --depth == 0 && i < chars.Count - 1)
                    return false;
            }
            return depth == 0;
        }

        private string Unary(XElement node, string op)
        {
            var ret = Nary(node, "", ChildCount(node));
            if (op.Length == 0)
                ret = RemoveOuterParens(ret);
            return op + "{" + ret + "}";
        }

        private string Binary(XElement node, string op) => CheckBracing(TeX(First(node)), true) + op + CheckBracing(TeX(Last(node)));

        private string Ternary(XElement node, string op1, string op2)
        {
            return CheckBracing(TeX(First(node)), true) + op1 + CheckBracing(TeX(Child(node, 1))) + op2 + CheckBracing(TeX(Last(node)));
        }

        private string Nary(XElement node, string op, int cNode)
        {
            var parts = new List<string>();
            for (int i = 0; i < cNode; i++)
                parts.Add(TeX(Child(node, i)));
            return string.Join(op, parts);
        }

        private static string EquationNumber(string eq)
        {
            if (Ch(eq, 0) == "(")
                eq = Sub(eq, 1, Len(eq) - 1);
            return eq;
        }

        private string TeX(XElement value, bool noAddParens = false)
        {
            if (value == null)
                return "";

            int cNode = ChildCount(value);
            string intent;
            var ret = "";
            string val;
            string op;
            var parent = value.Parent;

            switch (Name(value))
            {
                case "mtable":
                {
                    var symbol = "matrix";
                    intent = Attr(value, "intent");
                    if (intent == ":system-of-equations")
                        symbol = standard ? "aligned" : "align";

                    if (Text(First(parent)) == "{" && (ChildCount(parent) == 2 || EmptyFence(Text(Last(parent)))) && Child(parent, 1) == value)
                    {
                        ret = "\\begin{cases}" + Nary(value, "\\\\", cNode) + "\\end{cases}";
                        break;
                    }
                    else if (HasAttr(parent, "intent"))
                    {
                        intent = Attr(parent, "intent");
                        foreach (var (environment, matrixIntent) in MatrixIntents)
                        {
                            if (matrixIntent == intent)
                            {
                                symbol = environment;
                                break;
                            }
                        }
                    }
                    else if (intent == ":math-paragraph" && standard)
                    {
                        var lines = new List<string>();
                        foreach (var node in Children(value))
                        {
                            if (Name(node) == "mlabeledtr" || Attr(First(node), "intent") == ":equation-label")
                            {
                                var label = Text(First(node));
                                var line = ChildCount(node) > 2 ? TeX(Child(node, 1)) + "&" : "";
                                lines.Add(line + TeX(Last(node)) + "\\qquad\\text{" + label + "}");
                            }
                            else
                            {
                                lines.Add(TeX(node));
                            }
                        }
                        ret = "\\begin{aligned}" + string.Join("\\\\", lines) + "\\end{aligned}";
                        break;
                    }
                    else if (intent == ":math-paragraph")
                    {
                        var rows = Children(value);
                        for (int i = 0; i < rows.Count; i++)
                        {
                            var node = rows[i];
                            if (Name(node) == "mlabeledtr" || Attr(First(node), "intent") == ":equation-label")
                            {
                                ret += "\\begin{equation}\\label{eq" + EquationNumber(Text(First(node))) + "}";
                                if (ChildCount(node) > 2)
                                    ret += TeX(Child(node, 1)) + "&";
                                ret += TeX(Last(First(value))) + "\\end{equation}";
                            }
                            else
                            {
                                ret += UnicodeMathSerializer.Serialize(node);
                            }
                            if (i < rows.Count - 1)
                                ret += "\\\\";
                        }
                        break;
                    }
                    else if (cNode == 1 && HasEqLabel(value))
                    {
                        if (standard)
                        {
                            ret = TeX(Last(First(value))) + "\\tag{" + EquationNumber(Text(First(First(First(value))))) + "}";
                            break;
                        }
                        ret = "\\begin{equation}\\label{eq" + EquationNumber(Text(First(First(First(value))))) + "}" +
                            TeX(Last(First(value))) + "\\end{equation}";
                        break;
                    }
                    ret = "\\begin{" + symbol + "}" + Nary(value, "\\\\", cNode) + "\\end{" + symbol + "}";
                    break;
                }

                case "mtr":
                    ret = standard
                        ? string.Join("&", Children(value).Where(c => Attr(c, "intent") != ":no-equation-label").Select(c => TeX(c)))
                        : Nary(value, "&", cNode);
                    break;

                case "mtd":
                    ret = Nary(value, "", cNode);
                    if (Ch(ret, 0) == "&")
                        ret = Sub(ret, 1);
                    break;

                case "maligngroup":
                case "malignmark":
                    ret = "&";
                    break;

                case "menclose":
                {
                    var notation = Attr(value, "notation");
                    if (notation == "top" || notation == "bottom")
                    {
                        ret = Unary(value, notation == "top" ? "\\overline" : "\\underline");
                        break;
                    }
                    if (standard && notation == "circle")
                        notation = "box";
                    if (!string.IsNullOrEmpty(notation) && Enclosures.TryGetValue(notation, out var enclosure) && enclosure.Length > 0)
                    {
                        ret = Unary(value, "\\" + enclosure);
                        break;
                    }
                    ret = Unary(value, "\\boxed");
                    break;
                }

                case "mphantom":
                    ret = Unary(value, "\\phantom");
                    break;

                case "mpadded":
                {
                    op = "";
                    int mask = 0;
                    if (Attr(value, "width") == "0")
                        mask = 2;
                    if (Attr(value, "height") == "0")
                        mask |= 4;
                    if (Attr(value, "depth") == "0")
                        mask |= 8;
                    if (Name(First(value)) == "mphantom")
                    {
                        if (mask == 2)
                            op = "\\vphantom";
                        else if (mask == 12)
                            op = "\\hphantom";
                        ret = op.Length > 0 ? op + Sub(TeX(First(value)), 8)
                            : standard ? TeX(First(value)) : "⟡(" + mask + "&" + TeX(First(First(value)), true) + ")";
                        break;
                    }
                    switch (mask)
                    {
                        case 2: op = standard ? "\\mathrlap" : "\\hsmash"; break;
                        case 4: op = standard ? "\\smash[t]" : "\\asmash"; break;
                        case 8: op = standard ? "\\smash[b]" : "\\dsmash"; break;
                        case 12: op = "\\smash"; break;
                    }
                    mask |= 1;
                    ret = op.Length > 0 ? Unary(value, op) : standard ? Nary(value, "", cNode) : "⟡(" + mask + "&" + TeX(First(value), true) + ")";
                    break;
                }

                case "mstyle":
                    ret = standard ? Nary(value, "", cNode) : TeX(First(value));
                    val = Attr(value, "mathcolor");
                    if (!string.IsNullOrEmpty(val))
                        ret = standard ? "\\textcolor{" + val + "}{" + ret + "}" : "✎(" + val + "&" + ret + ")";
                    val = Attr(value, "mathbackground");
                    if (!string.IsNullOrEmpty(val))
                        ret = standard ? "\\colorbox{" + val + "}{$" + ret + "$}" : "☁(" + val + "&" + ret + ")";
                    break;

                case "msqrt":
                    ret = Unary(value, "\\sqrt");
                    break;

                case "mroot":
                    ret = "\\sqrt[" + TeX(Last(value)) + "]{" + TeX(First(value), true) + "}";
                    break;

                case "mfrac":
                    op = "\\frac";
                    val = Attr(value, "linethickness");
                    if (val == "0" || val == "0.0pt" || standard && IsZeroThickness(val))
                    {
                        op = "\\genfrac{}{}{0pt}{}";
                        if (HasAttr(parent, "intent") && Attr(parent, "intent").StartsWith("binomial-coefficient", StringComparison.Ordinal) ||
                            Attr(First(parent), "title") == "binomial coefficient")
                            op = "\\binom";
                    }
                    ret = op + "{" + TeX(First(value)) + "}{" + TeX(Last(value)) + "}";
                    break;

                case "msup" when IsPrime(Text(Last(value))):
                    ret = CheckBracing(TeX(First(value)), true) + (standard ? LaTeXSymbols.Math(Text(Last(value))) : Text(Last(value)));
                    break;

                case "msup":
                    op = "^";
                    if (IsPrime(Text(Last(value))))
                        op = "";
                    ret = Binary(value, op);
                    if (Attr(Last(value), "intent") == "transpose")
                    {
                        ret = CheckBracing(TeX(First(value)), true) + (standard ? "^{\\top}" : "^⊺");
                    }
                    break;

                case "mover":
                    if (Name(Last(value)) == "mo" && AccentsAbove.TryGetValue(Text(Last(value)), out var above))
                    {
                        if (standard && above == "overbar")
                            above = "overline";
                        ret = "\\" + above + "{" + TeX(First(value)) + "}";
                        break;
                    }
                    if (standard && Name(Last(value)) == "mo" && (Attr(value, "accent") == "true" || Text(Last(value)) != "→") && SpacingAccents.TryGetValue(Text(Last(value)), out var spacing))
                    {
                        ret = "\\" + spacing + "{" + TeX(First(value)) + "}";
                        break;
                    }
                    if (standard)
                    {
                        ret = TakesLimits(First(value)) && !HasAttr(value, "accent")
                            ? CheckBracing(TeX(First(value)), true) + "^" + CheckBracing(TeX(Last(value)))
                            : "\\overset{" + TeX(Last(value)) + "}{" + TeX(First(value)) + "}";
                        break;
                    }
                    if (HasAttr(value, "accent") && IsCombiningMark(Text(Last(value))) && Len(TeX(First(value))) > 1)
                    {
                        ret = "\\overset{" + TeX(Last(value)) + "}{" + TeX(First(value)) + "}";
                        break;
                    }
                    op = HasAttr(value, "accent") ? "" : "^";
                    ret = Binary(value, op);
                    break;

                case "munder":
                    if (Name(Last(value)) == "mo" && AccentsBelow.TryGetValue(Text(Last(value)), out var below))
                    {
                        if (standard && below == "underbar")
                            below = "underline";
                        ret = "\\" + below + "{" + TeX(First(value)) + "}";
                        break;
                    }
                    if (standard && Name(Last(value)) == "mo" && (Text(Last(value)) == "\u0332" || Text(Last(value)) == "_"))
                    {
                        ret = "\\underline{" + TeX(First(value)) + "}";
                        break;
                    }
                    if (standard && !TakesLimits(First(value)))
                    {
                        ret = "\\underset{" + TeX(Last(value)) + "}{" + TeX(First(value)) + "}";
                        break;
                    }
                    if (standard)
                    {
                        ret = CheckBracing(TeX(First(value)), true) + "_" + CheckBracing(TeX(Last(value)));
                        break;
                    }
                    if ((HasAttr(value, "accentunder") || HasAttr(value, "accent")) && IsCombiningMark(Text(Last(value))))
                    {
                        ret = Len(TeX(First(value))) > 1 ? "\\underset{" + TeX(Last(value)) + "}{" + TeX(First(value)) + "}" : TeX(First(value)) + Text(Last(value));
                        break;
                    }
                    op = HasAttr(value, "accentunder") ? "" : "_";
                    if (Text(First(value)) == "lim" && !First(value).HasElements)
                        op = "_";
                    ret = Binary(value, op);
                    break;

                case "msub":
                    ret = Binary(value, "_");
                    break;

                case "munderover":
                case "msubsup":
                    if (Name(value) == "munderover")
                    {
                        intent = Attr(parent, "intent");
                        if (string.IsNullOrEmpty(intent) || !intent.StartsWith(":largeop", StringComparison.Ordinal))
                        {
                            if (standard && !TakesLimits(First(value)))
                            {
                                ret = "\\underset{" + TeX(Child(value, 1)) + "}{\\overset{" + TeX(Last(value)) + "}{" + TeX(First(value)) + "}}";
                                break;
                            }
                            ret = standard ? Ternary(value, "_", "^") : Ternary(value, "┬", "┴");
                            break;
                        }
                    }
                    ret = Ternary(value, "_", "^");
                    break;

                case "mmultiscripts":
                {
                    var children = Children(value);
                    int prescripts = children.FindIndex(c => Name(c) == "mprescripts");
                    if (prescripts >= 0)
                    {
                        if (prescripts + 1 < children.Count && Name(children[prescripts + 1]) != "none")
                            ret = "_" + Script(TeX(children[prescripts + 1]));
                        if (prescripts + 2 < children.Count && Name(children[prescripts + 2]) != "none")
                            ret += "^" + Script(TeX(children[prescripts + 2]));
                        if (ret.Length > 0)
                            ret = "{}" + ret;
                    }
                    ret += Script(TeX(Child(value, 0)));
                    int end = prescripts >= 0 ? prescripts : children.Count;
                    if (end > 1 && Name(children[1]) != "none")
                        ret += "_" + Script(TeX(children[1]));
                    if (end > 2 && Name(children[2]) != "none")
                        ret += "^" + Script(TeX(children[2]));
                    break;
                }

                case "mfenced":
                {
                    var opSeparators = Characters((Attr(value, "separators") ?? ",").Replace(" ", ""));
                    var children = Children(value);
                    ret = Attr(value, "open") ?? "(";
                    for (int i = 0; i < children.Count; i++)
                    {
                        ret += TeX(children[i]);
                        if (i < children.Count - 1 && opSeparators.Count > 0)
                            ret += i < opSeparators.Count - 1 ? opSeparators[i] : opSeparators[opSeparators.Count - 1];
                    }
                    ret += Attr(value, "close") ?? ")";
                    break;
                }

                case "mo":
                    val = Text(value);
                    if (standard && val == "⋯")
                        return "\\cdots ";
                    if (standard && val == "\u00A0")
                        return "~";
                    if (standard && val == "&")
                        return "\\&";
                    if (standard && IsFunctionName(val))
                        return "\\" + val + " ";
                    if (OperatorMap.TryGetValue(val, out var mapped))
                        return mapped;
                    // escaped for the reader, in standard LaTeX its command if it has one (Ⓢ, \circledS)
                    if (IsStructureOperator(val))
                        return standard && LaTeXSymbols.Math(val) != val ? LaTeXSymbols.Math(val) : "\\" + val;
                    if (val == "$")
                    {
                        ret = "\\$";
                        break;
                    }
                    if (Attr(value, "intent") == ":text" && !standard)
                    {
                        // a bracket or a bar that pairs with nothing: the reader escapes the brackets that close nothing and
                        // the odd bars; \{ and \} are braces, \| the double bar
                        ret = val == "{" || val == "}" ? "\\" + val : val == "‖" ? "\\|" : IsOpenDelimiter(val) || IsCloseDelimiter(val) || val == "|" ? val : "\\" + val;
                        break;
                    }
                    if (!standard && (val == "{" || val == "}"))
                        return "\\" + val;
                    if (HasAttr(value, "title"))
                    {
                        switch (Attr(value, "title"))
                        {
                            case "differential":
                            case "derivative":
                                ret = "𝑑";
                                break;
                            case "binomial coefficient":
                                val = "";
                                break;
                        }
                    }
                    if (ret.Length == 0)
                        ret = val;
                    if (standard)
                        ret = LaTeXSymbols.Math(ret);
                    break;

                case "mi":
                    if (standard)
                    {
                        ret = StandardIdentifier(value);
                        break;
                    }

                    if (Len(Text(value)) == 1 && !value.HasElements)
                    {
                        var c = Text(value);
                        if (!HasAttr(value, "mathvariant"))
                        {
                            ret = ItalicizeCharacter(c);
                            break;
                        }
                        MathVariants.TryGetValue(Attr(value, "mathvariant"), out var mathstyle);
                        if (mathstyle == "mup")
                        {
                            switch (Attr(value, "title"))
                            {
                                case "base of natural logarithm":
                                    ret = "ⅇ";
                                    break;
                                case "imaginary unit":
                                    ret = "ⅈ";
                                    break;
                            }
                            if (ret.Length > 0)
                                break;
                            if (c != "∞" && c != "⋯" && !InRange("Α", c, "Ω") && CodePointAt(c, 0) <= 0xFFFF)
                            {
                                ret = c.Length == 1 && (c[0] >= 'A' && c[0] <= 'Z' || c[0] >= 'a' && c[0] <= 'z') ? "\\mathrm{" + c + "}" : "\\textrm{" + c + "}";
                                break;
                            }
                        }
                        else if (mathstyle != null)
                        {
                            ret = GetMathAlphanumeric(c, mathstyle);
                            break;
                        }
                    }
                    else if (IsFunctionName(Text(value)))
                    {
                        ret = "\\" + Text(value) + " ";
                        break;
                    }
                    else if (Len(Text(value)) > 1 && SharpEquations.Internal.Text.AsciiPatterns.Letters().IsMatch(Text(value)))
                    {
                        ret = "\\operatorname{" + Text(value) + "}";
                        break;
                    }
                    ret = Text(value);
                    break;

                case "mn":
                    ret = standard ? LaTeXSymbols.Math(Text(value)) : Text(value);
                    break;

                case "mtext":
                    ret = standard ? LaTeXSymbols.Text(Text(value)) : "\\textrm{" + Text(value) + "}";
                    break;

                case "ms":
                {
                    // a string literal, shown between its quotes
                    var quoted = (Attr(value, "lquote") ?? "\"") + Text(value) + (Attr(value, "rquote") ?? "\"");
                    ret = standard ? LaTeXSymbols.Text(quoted) : "\\textrm{" + quoted.Replace("\"", "\\\"") + "}";
                    break;
                }

                case "mspace":
                {
                    var width = Attr(value, "width");
                    if (!string.IsNullOrEmpty(width))
                    {
                        int i = Array.IndexOf(SpaceWidths, width);
                        if (i >= 0)
                            ret = standard ? StandardSpaces[i] : UniSpaces[i];
                    }
                    break;
                }
            }

            if (ret.Length > 0 || IsToken(value))
                return ret;

            var nodes = Children(value);
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (i == 1 && (ret == "{" || ret == "\\{") && Name(node) == "mtable" && (ChildCount(value) == 2 || EmptyFence(Text(Last(value)))))
                    ret = "";
                ret += TeX(node);
            }

            var mrowIntent = Name(value) == "mrow" && HasAttr(value, "intent") ? Attr(value, "intent") : "";
            if (standard && IsBinomial(value))
                return "\\binom{" + TeX(First(Child(value, 1))) + "}{" + TeX(Last(Child(value, 1))) + "}";
            if (standard && IsTallFence(value, mrowIntent) || !standard && IsUnpairedFence(value, mrowIntent))
                return LeftRight(value);
            if (mrowIntent.Length > 0)
            {
                if (mrowIntent == ":fenced" && ChildCount(value) > 0 && Text(Last(value)).Length == 0)
                    return Text(First(value)).Length == 0 ? "{" + ret + "}" : ret;
                if (mrowIntent.StartsWith("absolute-value", StringComparison.Ordinal) || mrowIntent.StartsWith("cardinality", StringComparison.Ordinal))
                {
                    var abs = mrowIntent[0] == 'a' ? "\\abs" : "\\card";
                    ret = Sub(ret, 1, Len(ret) - 1);
                    // bars around something tall, around bars or within bars, which they would pair with
                    bool plain = !value.Elements().Skip(1).Take(ChildCount(value) - 2).Any(e => e.DescendantsAndSelf().Any(d => TallElements.Contains(Name(d)) || IsBar(d))) &&
                        !value.Ancestors().Any(a => IsBar(a.Elements().FirstOrDefault()));
                    if (!standard)
                        return abs + "{" + ret + "}";
                    return plain ? "|" + ret + "|" : "\\left|" + ret + "\\right|";
                }
                if (mrowIntent.StartsWith("binomial-coefficient", StringComparison.Ordinal) ||
                    mrowIntent.EndsWith("matrix", StringComparison.Ordinal) || mrowIntent.EndsWith("determinant", StringComparison.Ordinal))
                {
                    var chars = Characters(ret);
                    int last = chars.Count - 1;
                    if (chars.Count > 0 && chars[0] == "|")
                        return Sub(ret, 1, last);
                    // the brackets of the other matrices, which UnicodeMathML keeps around the environment
                    if (chars.Count > 3 && chars[0] == "\\" && (chars[1] == "{" && chars[last] == "}" || chars[1] == "|" && chars[last] == "|") && chars[last - 1] == "\\")
                        return Sub(ret, 2, last - 1);
                    if (chars.Count > 1 && (chars[0] == "[" && chars[last] == "]" || chars[0] == "‖" && chars[last] == "‖" || chars[0] == "{" && chars[last] == "}"))
                        return Sub(ret, 1, last);
                    if (chars.Count == 0 || chars[0] != "(")
                        return ret;
                    if (chars[last] == ")")
                        return Sub(ret, 1, last);
                    bool binomial = false;
                    int cParen = 1;
                    int k = 0;
                    for (int i = 1; i < chars.Count - 1; i++)
                    {
                        switch (chars[i])
                        {
                            case "(":
                                cParen++;
                                break;
                            case ")":
                                cParen--;
                                if (cParen == 0)
                                {
                                    if (!binomial)
                                        return ret;
                                    k = i;
                                }
                                break;
                            case "⒞":
                                binomial = true;
                                break;
                        }
                    }
                    return k > 0 ? Sub(ret, 1, k) + Sub(ret, k + 1) : ret;
                }
                if (mrowIntent == ":function" && Previous(value) != null && First(value) != null && Name(First(value)) == "mi" &&
                    CodePointAt(Text(First(value)), 0) < 0x2100 && Name(Previous(value)) == "mi")
                    return " " + ret;
            }
            return ret;
        }

        /// <summary>A script in standard LaTeX, braced unless it is a single character, command or group.</summary>
        private string Script(string script) => standard ? CheckBracing(script) : script;

        [System.Text.RegularExpressions.GeneratedRegex(@"^\\[A-Za-z]+ ?$")]
        private static partial System.Text.RegularExpressions.Regex SingleCommand();

        [System.Text.RegularExpressions.GeneratedRegex(@"^0*(\.0*)?[a-z]*$")]
        private static partial System.Text.RegularExpressions.Regex ZeroThickness();

        private static readonly string[] TallElements = { "mfrac", "mtable", "munderover", "msqrt", "mroot", "msubsup", "mover", "munder" };

        /// <summary>An mrow between fences around a fraction, a table or the like, which \\left and \\right make grow.</summary>
        private static bool IsTallFence(XElement value, string intent)
        {
            if (Name(value) != "mrow" || ChildCount(value) < 2 || Name(First(value)) != "mo" || Name(Last(value)) != "mo")
                return false;
            if (intent.StartsWith("absolute-value", StringComparison.Ordinal) || intent.StartsWith("cardinality", StringComparison.Ordinal) ||
                intent.StartsWith("binomial-coefficient", StringComparison.Ordinal) || intent.EndsWith("matrix", StringComparison.Ordinal) ||
                intent.EndsWith("determinant", StringComparison.Ordinal))
                return false;
            var open = Text(First(value));
            var close = Text(Last(value)).Replace("\u200B", "");
            if (open == "{" && Name(Child(value, 1)) == "mtable")
                return false;
            // a binomial coefficient: \\binom has its parentheses
            var middle = value.Elements().Skip(1).Take(ChildCount(value) - 2).ToList();
            if (middle.Count == 1 && Name(middle[0]) == "mfrac" && IsZeroThickness(Attr(middle[0], "linethickness")))
                return false;
            if (IsUnpairedFence(value, intent))
                return true;
            bool fences = (open.Length == 0 || IsOneOf(open, OpeningFences)) && (close.Length == 0 || IsOneOf(close, ClosingFences));
            return fences && (open.Length > 0 || close.Length > 0) &&
                middle.Any(e => e.DescendantsAndSelf().Any(d => TallElements.Contains(Name(d))));
        }

        private const string OpeningFences = "([{⟨|‖⌈⌊";
        private const string ClosingFences = ")]}⟩|‖⌉⌋";

        /// <summary>An mrow between fences that do not pair (]𝑎[, or one side only), read back only with \left and \right.</summary>
        private static bool IsUnpairedFence(XElement value, string intent)
        {
            if (intent != ":fenced" || Name(value) != "mrow" || ChildCount(value) < 2 || Name(First(value)) != "mo" || Name(Last(value)) != "mo")
                return false;
            var open = Text(First(value));
            var close = Text(Last(value)).Replace("\u200B", "");
            // a brace before a table is that of cases
            if (open == "{" && Name(Child(value, 1)) == "mtable")
                return false;
            return (open.Length > 0 || close.Length > 0) && !(IsOneOf(open, OpeningFences) && IsOneOf(close, ClosingFences));
        }

        /// <summary>The operators that build a structure in UnicodeMath (▭, √, ¦…), escaped when they are alone: \▭.</summary>
        internal static bool IsStructureOperator(string text) => text != null && Len(text) == 1 && "▭▁▢◯⟌⬭╱╲╳√∛∜⒭¦⒞⒜ⓒ□⟡⬄⇳⬍⬆⬇⬌■⒨⒩ⓢⓈ⒱█ⓘⓐⓡⓜⓓⓑ▒".Contains(text);

        private static bool IsBar(XElement element) => element != null && Name(element) == "mo" && (Text(element) == "|" || Text(element) == "‖");

        private static bool IsCombiningMark(string text) =>
            Len(text) == 1 && (CodePointAt(text, 0) >= 0x300 && CodePointAt(text, 0) <= 0x36F || CodePointAt(text, 0) >= 0x20D0 && CodePointAt(text, 0) <= 0x20F0);

        private static bool IsOneOf(string text, string set) => Len(text) == 1 && Characters(set).Contains(text);

        private string LeftRight(XElement value)
        {
            string Fence(string fence) => fence.Replace("\u200B", "").Length == 0 ? "." : LaTeXSymbols.Math(fence).TrimEnd();
            var inner = string.Concat(value.Elements().Skip(1).Take(ChildCount(value) - 2).Select(e => TeX(e)));
            return "\\left" + Fence(Text(First(value))) + inner + "\\right" + Fence(Text(Last(value)));
        }

        private static readonly string[] StandardSpaces =
            { "", "\\,", "\\,", "\\,", "\\:", "\\;", "\\;", "\\;", "", "\\enspace ", "", "", "", "", "", "", "", "", "\\quad " };

        // the accents that omml2mml and MathML writers put over a base as spacing characters
        private static readonly Dictionary<string, string> SpacingAccents = new Dictionary<string, string>
        {
            ["^"] = "hat", ["ˆ"] = "hat", ["~"] = "tilde", ["˜"] = "tilde", ["→"] = "vec", ["⃗"] = "vec", ["˙"] = "dot", ["¨"] = "ddot",
            ["ˇ"] = "check", ["˘"] = "breve", ["´"] = "acute", ["`"] = "grave", ["‾"] = "bar", ["¯"] = "bar", ["―"] = "overline",
            ["˚"] = "mathring", ["←"] = "overleftarrow", ["↔"] = "overleftrightarrow",
        };

        /// <summary>A closing fence that is not there: empty in Word mode, also a zero width space in standard LaTeX.</summary>
        private bool EmptyFence(string fence) => fence.Length == 0 || standard && fence == "\u200B";

        /// <summary>(n¦k): a fraction without a bar between parentheses, UnicodeMath's binomial coefficient.</summary>
        private static bool IsBinomial(XElement value)
        {
            return Name(value) == "mrow" && ChildCount(value) == 3 && Text(First(value)) == "(" && Text(Last(value)) == ")" &&
                Name(First(value)) == "mo" && Name(Last(value)) == "mo" && Name(Child(value, 1)) == "mfrac" &&
                IsZeroThickness(Attr(Child(value, 1), "linethickness"));
        }

        private static bool IsZeroThickness(string thickness) =>
            thickness != null && ZeroThickness().IsMatch(thickness.Trim()) && thickness.Trim().Length > 0;

        /// <summary>Whether limits under or over the element are its subscript and superscript: an operator or a function name.</summary>
        private static bool TakesLimits(XElement element)
        {
            while (Name(element) == "mrow" && ChildCount(element) == 1)
                element = First(element);
            if (Name(element) == "mo")
                return IsLargeOperator(element) || IsFunctionName(Text(element));
            // a name that is not a function's, \operatorname{argmax}, takes its scripts at its side: \underset puts them under
            return Name(element) == "mi" && IsFunctionName(Text(element));
        }

        /// <summary>Whether an operator is large (∑, ∫, ⋃…) or a horizontal brace, whose limits go under and over it.</summary>
        private static bool IsLargeOperator(XElement element)
        {
            if ((string)element.Attribute("largeop") == "true" || (string)element.Attribute("movablelimits") == "true")
                return true;
            var text = Text(element);
            if (Len(text) != 1)
                return false;
            var c = char.ConvertToUtf32(text, 0);
            return c >= 0x220F && c <= 0x2211 || c >= 0x222B && c <= 0x2233 || c >= 0x22C0 && c <= 0x22C3
                || c >= 0x2A00 && c <= 0x2A1C || c >= 0x23DC && c <= 0x23E1;
        }

        /// <summary>An identifier in standard LaTeX: a letter with its font, a function name, or a name as \operatorname.</summary>
        private static string StandardIdentifier(XElement value)
        {
            var text = Text(value);
            // the double-struck italic letters of UnicodeMath (ⅆ, ⅇ, ⅈ, ⅉ, ⅅ) are upright: \mathrm{d}
            var intent = Attr(value, "intent");
            if (intent is "ⅆ" or "ⅇ" or "ⅈ" or "ⅉ" or "ⅅ")
                return LaTeXSymbols.Math(intent);
            if (Len(text) != 1)
            {
                // an empty identifier, which holds a place (the base of {}^3, the left side of &=)
                if (text.Length == 0)
                    return "{}";
                if (IsFunctionName(text))
                    return "\\" + text + " ";
                var plain = string.Concat(Characters(text).Where(c => c.Length == 1 && (c[0] >= 'A' && c[0] <= 'Z' || c[0] >= 'a' && c[0] <= 'z')));
                return plain.Length == text.Length ? "\\operatorname{" + text + "}" : LaTeXSymbols.Math(text);
            }
            switch (Attr(value, "title"))
            {
                case "base of natural logarithm":
                    return "\\mathrm{e}";
                case "imaginary unit":
                    return "\\mathrm{i}";
            }
            if (!HasAttr(value, "mathvariant"))
                return LaTeXSymbols.Math(ItalicizeCharacter(text));
            MathVariants.TryGetValue(Attr(value, "mathvariant"), out var mathstyle);
            if (mathstyle == "mup")
                return CodePointAt(text, 0) <= 0xFFFF ? LaTeXSymbols.Upright(text) : LaTeXSymbols.Math(text);
            return LaTeXSymbols.Math(mathstyle != null ? GetMathAlphanumeric(text, mathstyle) : text);
        }

        private static bool IsToken(XElement value)
        {
            switch (Name(value))
            {
                case "mi":
                case "mn":
                case "mo":
                case "mtext":
                case "mspace":
                case "ms":
                    return true;
                default:
                    return false;
            }
        }
    }
}
