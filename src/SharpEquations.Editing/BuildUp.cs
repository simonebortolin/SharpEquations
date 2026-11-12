namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using SharpEquations.Editing.Text;

    /// <summary>
    /// The build up of linear text into structures, and the build down of structures into linear text, through the
    /// parsers and the writers of SharpEquations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run is built up together with the nodes before it in its row, since the operand of an operator typed in the
    /// run can be one of them: <c>(a+b)/c</c> makes a fraction of a bracket built up earlier. Those nodes are written
    /// in the linear text as they were typed when they are tokens (<c>a</c>, <c>+</c>, <c>12</c>) or brackets of
    /// tokens, so that the parser reads them as it would have read the text; any other node (a structure, a token with
    /// attributes) is written as a placeholder, a character of the Yi syllables that the parsers read as an identifier,
    /// and the node is put back in place of the identifier in the result.
    /// </para>
    /// </remarks>
    internal static class BuildUp
    {
        private const int PlaceholderFirst = 0xA000;
        private const int PlaceholderLast = 0xA48C;

        private static readonly EquationOptions WordLaTeX = new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word };

        public static bool IsPlaceholder(int codePoint) => codePoint >= PlaceholderFirst && codePoint <= PlaceholderLast;

        /// <summary>The characters that end an argument and build up what is before them, besides the space.</summary>
        public static bool IsTrigger(Rune rune, EquationFormat format)
        {
            if (rune.Value == ' ')
                return true;
            switch (rune.Value)
            {
                case '+' or '-' or '=' or '<' or '>' or ',' or ';' or ':' or '!' or '?':
                case 0x2212 or 0x00B1 or 0x2213 or 0x00D7 or 0x00B7 or 0x00F7 or 0x2217 or 0x22C5:
                case 0x2264 or 0x2265 or 0x2260 or 0x2248 or 0x2261 or 0x2192 or 0x2190 or 0x2194 or 0x21D2 or 0x21D4:
                case 0x2208 or 0x2209 or 0x2282 or 0x2283 or 0x2286 or 0x2287 or 0x222A or 0x2229:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The characters after which an argument is still to come: an operator typed after them does not build up.</summary>
        public static bool ExpectsOperand(Rune rune, EquationFormat format)
        {
            if (format == EquationFormat.LaTeX)
                return rune.Value is '^' or '_' or '\\' or '{' or '(' or '[' or '&';
            return rune.Value is '^' or '_' or '/' or '\\' or '(' or '[' or '{' or '&' or '@'
                or 0x252C or 0x2534 or 0x221A or 0x221B or 0x221C or 0x2592 or 0x27E8 or 0x3016 or 0x251C or 0x00A6 or 0x25A0
                or 0x2061 or 0x2044 or 0x2215 or 0x2298;
        }

        /// <summary>
        /// Whether the text ends with a control word (<c>\mathring</c>) that the nodes built from it leave as text
        /// (<c>mtext</c>), or as the empty box of a style command (<c>\rm</c>): its arguments are still to come.
        /// </summary>
        public static bool EndsWithPendingCommand(string text, IReadOnlyList<MathNode> nodes)
        {
            var i = text.Length;
            while (i > 0 && text[i - 1] < 0x80 && char.IsLetter(text[i - 1]))
                i--;
            if (i == text.Length || i == 0 || text[i - 1] != '\\')
                return false;
            var command = text.Substring(i - 1);
            return nodes.Any(n => Pending(n, command));

            static bool Pending(MathNode node, string command) =>
                node.Name == "mtext" && node.Text.Contains(command) || node.Name == "mi" && node.Text == "\u2B1A" || node.Children.Any(c => Pending(c, command));
        }

        private static readonly Lazy<Dictionary<string, int>> LaTeXArguments = new Lazy<Dictionary<string, int>>(() =>
            EquationCommands.Get(EquationFormat.LaTeX).Where(c => c.Arguments > 0).ToDictionary(c => c.Name, c => c.Arguments, StringComparer.Ordinal));

        /// <summary>
        /// Whether the LaTeX text ends before the arguments of one of its commands: <c>\underset a</c>, <c>\xrightarrow[y]</c>,
        /// whose arguments may come without braces (<c>\underset ab</c>, <c>\not a</c>), after a space too.
        /// </summary>
        public static bool AwaitsArguments(string text)
        {
            var runes = CodePointText.Runes(text).Select(r => r.Value).ToList();
            var i = 0;
            while (true)
            {
                SkipSpaces(ref i);
                if (i >= runes.Count)
                    return false;
                if (!Item(ref i))
                    return true;
            }

            void SkipSpaces(ref int k)
            {
                while (k < runes.Count && runes[k] == ' ')
                    k++;
            }

            // a token, a group or a command with its arguments; false if the text ends before them
            bool Item(ref int k)
            {
                switch (runes[k])
                {
                    case '{':
                        var depth = 0;
                        for (; k < runes.Count; k++)
                        {
                            depth += runes[k] switch { '{' => 1, '}' => -1, _ => 0 };
                            if (runes[k] == '\\')
                                k++;
                            else if (depth == 0)
                                break;
                        }
                        k++;
                        return k <= runes.Count;
                    case '\\':
                        var start = ++k;
                        while (k < runes.Count && IsAsciiLetter(runes[k]))
                            k++;
                        if (k == start)
                        {
                            k = Math.Min(k + 1, runes.Count);
                            return true;
                        }
                        var name = string.Concat(runes.Skip(start).Take(k - start).Select(c => (char)c));
                        if (!LaTeXArguments.Value.TryGetValue(name, out var count))
                            return true;
                        var optional = k;
                        SkipSpaces(ref optional);
                        if (optional < runes.Count && runes[optional] == '[')
                        {
                            var close = runes.IndexOf(']', optional);
                            if (close < 0)
                                return false;
                            k = close + 1;
                        }
                        for (var argument = 0; argument < count; argument++)
                        {
                            SkipSpaces(ref k);
                            if (k >= runes.Count || !Item(ref k))
                                return false;
                        }
                        return true;
                    default:
                        k++;
                        return true;
                }
            }
        }

        /// <summary>Whether the brackets (UnicodeMath) or the groups and environments (LaTeX) of the text are closed.</summary>
        public static bool IsBalanced(string text, EquationFormat format)
        {
            var depth = 0;
            var runes = CodePointText.Runes(text);
            // UnicodeMath: the text between quotes is one piece of text, spaces included
            if (format == EquationFormat.UnicodeMath && runes.Count(r => r.Value == '"') % 2 == 1)
                return false;
            for (var i = 0; i < runes.Count; i++)
            {
                var c = runes[i].Value;
                if (format == EquationFormat.LaTeX)
                {
                    if (c == '\\')
                    {
                        var word = new StringBuilder();
                        var j = i + 1;
                        for (; j < runes.Count && IsAsciiLetter(runes[j].Value); j++)
                            word.Append((char)runes[j].Value);
                        switch (word.ToString())
                        {
                            case "begin" or "left":
                                depth++;
                                break;
                            case "end" or "right":
                                depth--;
                                break;
                            case "":
                                j++;
                                break;
                        }
                        i = j - 1;
                        continue;
                    }
                    depth += c switch { '{' => 1, '}' => -1, _ => 0 };
                }
                else
                {
                    depth += c switch
                    {
                        '(' or '[' or '{' or 0x27E8 or 0x3016 or 0x251C => 1,
                        ')' or ']' or '}' or 0x27E9 or 0x3017 or 0x2524 => -1,
                        _ => 0,
                    };
                }
                if (depth < 0)
                    depth = 0;
            }
            return depth == 0;
        }

        /// <summary>
        /// The nodes of the text, read in the format after the nodes before it in its row; null if the text does not
        /// parse, or if the result cannot hold the nodes before it as they were.
        /// </summary>
        public static IReadOnlyList<MathNode>? Parse(IReadOnlyList<MathNode> before, string text, EquationFormat format, bool display)
        {
            if (text.EnumerateRunes().Any(r => IsPlaceholder(r.Value)))
                return null;

            var placeholders = new List<MathNode>();
            var sb = new StringBuilder();
            foreach (var node in before)
                Append(sb, node, placeholders, format);
            // what was built up before ended with a space or an operator: the text starts a new operand
            if (sb.Length > 0 && text.Length > 0 && char.IsLetterOrDigit(text, 0))
                sb.Append(' ');
            AppendSeparated(sb, text);

            // a line of an equation array (its row starts with maligngroup), where & is an alignment mark, is read as the
            // line of an array of its own
            var line = before.Count > 0 && before[0].Name == "maligngroup";
            var source = sb.ToString();
            if (line)
                source = format == EquationFormat.LaTeX ? "\\begin{aligned}" + source + "\\end{aligned}" : "█(" + source + ")";
            if (!Equation.TryParse(source, format, new EquationOptions { Display = display }, out var equation) || equation == null)
                return null;

            var elements = equation.ToMathML().Elements().ToList();
            if (line)
            {
                if (elements.Count != 1 || elements[0].Elements().ToList() is not { Count: 1 } rows || rows[0].Elements().ToList() is not { Count: 1 } cells)
                    return null;
                elements = cells[0].Elements().ToList();
            }

            var used = new bool[placeholders.Count];
            var nodes = new List<MathNode>();
            foreach (var element in elements)
            {
                var node = Substitute(MathNode.FromMathML(element), placeholders, used);
                if (node == null)
                    return null;
                nodes.Add(node);
            }
            return used.All(u => u) ? nodes : null;
        }

        /// <summary>The nodes of the row with every run built up where it parses.</summary>
        public static MathNode BuildAll(MathNode node, EquationFormat format, bool display)
        {
            if (node.IsRun || node.IsOpaque || node.Children.Count == 0)
                return node;

            var children = node.Children.Select(c => BuildAll(c, format, display)).ToList();
            if (node.IsRow)
            {
                for (var j = 0; j < children.Count; j++)
                {
                    if (!children[j].IsRun)
                        continue;
                    var built = Parse(children.Take(j).ToList(), children[j].Text, format, display);
                    if (built == null)
                        continue;
                    var after = children.Skip(j + 1).ToList();
                    children = built.Concat(after).ToList();
                    j = built.Count - 1;
                }
            }
            return node.WithChildren(children);
        }

        /// <summary>The equation of nodes, with the runs that do not parse written as <c>merror</c>; null if it is not valid MathML.</summary>
        public static Equation? ToEquation(IEnumerable<MathNode> nodes, bool display) =>
            ToEquation(nodes, display ? new[] { new KeyValuePair<string, string>("display", "block") } : Array.Empty<KeyValuePair<string, string>>());

        /// <summary>The equation of nodes in a <c>math</c> element with the attributes (<c>display</c>, <c>dir</c>...).</summary>
        public static Equation? ToEquation(IEnumerable<MathNode> nodes, IEnumerable<KeyValuePair<string, string>> attributes)
        {
            var math = new XElement(MathMLNamespaces.MathML + "math", nodes.SelectMany(n => n.ToMathML(MathNode.UnbuiltRun)));
            foreach (var attribute in attributes)
                math.SetAttributeValue(XName.Get(attribute.Key), attribute.Value);
            try
            {
                return Equation.Parse(math, EquationFormat.MathML);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>The linear text of an equation in the format: UnicodeMath, or the LaTeX of Word.</summary>
        public static string ToLinear(Equation equation, EquationFormat format) =>
            format == EquationFormat.LaTeX ? equation.ToString(EquationFormat.LaTeX, WordLaTeX) : equation.ToString(EquationFormat.UnicodeMath);

        private static void Append(StringBuilder sb, MathNode node, List<MathNode> placeholders, EquationFormat format)
        {
            if (node.IsRun)
            {
                AppendSeparated(sb, node.Text);
                return;
            }

            switch (node.Name)
            {
                case "maligngroup":
                    return;
                case "malignmark":
                    sb.Append('&');
                    return;
            }

            if (node.IsToken && node.Attributes.Count == 0 && IsLiteral(node, format))
            {
                AppendSeparated(sb, node.Text);
                return;
            }

            // a word of LaTeX (a function: tan, max, or \operatorname{sn}) is one identifier
            if (format == EquationFormat.LaTeX && node.Name == "mi" && node.Attributes.Count == 0 && node.Text.Length > 1 && node.Text.All(c => c < 0x80 && char.IsLetter(c)))
            {
                sb.Append("\\operatorname{").Append(node.Text).Append('}');
                return;
            }

            if (node.IsRow && node.Attributes.Count == 0 && node.Children.Count >= 2
                && node.Children[0].Name == "mo" && node.Children[node.Children.Count - 1].Name == "mo"
                && IsFence(node.Children[0].Text) && IsFence(node.Children[node.Children.Count - 1].Text))
            {
                foreach (var child in node.Children)
                    Append(sb, child, placeholders, format);
                return;
            }

            if (placeholders.Count > PlaceholderLast - PlaceholderFirst)
                throw new InvalidOperationException("Too many structures in a row.");
            AppendSeparated(sb, char.ConvertFromUtf32(PlaceholderFirst + placeholders.Count));
            placeholders.Add(node);
        }

        private static bool IsLiteral(MathNode token, EquationFormat format)
        {
            if (token.Text.Length == 0 || token.Name is "mtext" or "ms")
                return false;
            foreach (var rune in token.Text.EnumerateRunes())
            {
                if (IsPlaceholder(rune.Value) || rune.Value < 0x20)
                    return false;
                if (format == EquationFormat.LaTeX && rune.Value is '\\' or '{' or '}' or '^' or '_' or '&' or '%' or '#' or '$' or '~')
                    return false;
                if (format == EquationFormat.UnicodeMath && rune.Value is '\\' or '"')
                    return false;
            }
            if (token.Name == "mi" && !CodePointText.IsSingle(token.Text, out _))
                return format == EquationFormat.UnicodeMath && token.Text.All(c => c < 0x80 && char.IsLetter(c));
            return true;
        }

        private static bool IsFence(string text) => text is "(" or ")" or "[" or "]" or "{" or "}" or "|" or "‖" or "⟨" or "⟩" or "⌊" or "⌋" or "⌈" or "⌉";

        /// <summary>Appends text, with a space where two letters or digits would otherwise make one token.</summary>
        private static void AppendSeparated(StringBuilder sb, string text)
        {
            if (sb.Length > 0 && text.Length > 0 && (IsWordChar(sb[sb.Length - 1]) || sb[sb.Length - 1] is ',' or ';') && IsWordChar(text[0]))
                sb.Append(' ');
            sb.Append(text);
        }

        private static bool IsWordChar(char c) => c < 0x80 && char.IsLetterOrDigit(c) || char.IsSurrogate(c) || c >= PlaceholderFirst && c <= PlaceholderLast;

        private static bool IsAsciiLetter(int c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

        private static MathNode? Substitute(MathNode node, List<MathNode> placeholders, bool[] used)
        {
            if (node.IsToken)
            {
                var hasPlaceholder = node.Text.EnumerateRunes().Any(r => IsPlaceholder(r.Value));
                if (!hasPlaceholder)
                    return node;
                if (node.Name != "mi" || !CodePointText.IsSingle(node.Text, out var rune))
                    return null;
                var index = rune.Value - PlaceholderFirst;
                if (index >= placeholders.Count || used[index])
                    return null;
                used[index] = true;
                return placeholders[index];
            }

            if (node.Children.Count == 0)
                return node;

            var children = new MathNode[node.Children.Count];
            var changed = false;
            for (var i = 0; i < children.Length; i++)
            {
                var child = Substitute(node.Children[i], placeholders, used);
                if (child == null)
                    return null;
                changed |= !ReferenceEquals(child, node.Children[i]);
                children[i] = child;
            }

            if (node.IsRow)
            {
                // a placeholder that was a row in the slot of a structure stands alone in the row the slot was given
                if (children.Length == 1 && children[0].IsRow && node.Attributes.Count == 0 && !ReferenceEquals(children[0], node.Children[0]))
                    return children[0];
            }
            return changed ? node.WithChildren(children) : node;
        }
    }
}
