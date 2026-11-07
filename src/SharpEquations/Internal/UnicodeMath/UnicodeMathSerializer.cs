namespace SharpEquations.Internal.UnicodeMath
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using static Js;
    using static UnicodeMathText;

    /// <summary>
    /// MathML to UnicodeMath: a port of dump() and getUnicodeMath() of UnicodeMathML (src/unicodemathml.js), on
    /// XElement where the JavaScript walks the DOM. Strings are indexed by code points.
    /// </summary>
    internal sealed class UnicodeMathSerializer
    {
        private const string OverBrackets = "⎴⏜⏞⏠¯";
        private const string UnderBrackets = "⎵⏝⏟⏡";

        internal static readonly string[] UniSpaces =
            { "\u200B", "\u200A", "\u200A\u200A", "\u2009", "\u205F", "\u2005", "\u2004", "\u2004\u200A", "", "\u2002", "", "", "", "", "", "", "", "", "\u2003" };
        internal static readonly string[] SpaceWidths =
        {
            "0", "veryverythinmathspace", "verythinmathspace", "thinmathspace", "mediummathspace", "thickmathspace", "verythickmathspace",
            "veryverythickmathspace", null, "0.5em", null, null, null, null, null, null, null, null, "1em",
        };

        internal static readonly Dictionary<string, string> MathVariants = new Dictionary<string, string>
        {
            ["normal"] = "mup", ["bold"] = "mbf", ["italic"] = "mit", ["bold-italic"] = "mbfit", ["double-struck"] = "Bbb",
            ["bold-fraktur"] = "mbffrak", ["script"] = "mscr", ["bold-script"] = "mbfscr", ["fraktur"] = "mfrak", ["sans-serif"] = "msans",
            ["bold-sans-serif"] = "mbfsans", ["sans-serif-italic"] = "mitsans", ["sans-serif-bold-italic"] = "mbfitsans", ["monospace"] = "mtt",
            ["isolated"] = "misol", ["initial"] = "minit", ["tailed"] = "mtail", ["looped"] = "mloop", ["stretched"] = "mstrc",
            ["roundhand"] = "mrhnd", ["chancery"] = "mchan",
        };

        private static readonly (string Symbol, string Intent)[] MatrixIntents =
        {
            ("⒨", ":parenthesized-matrix"), ("⒱", ":determinant"), ("⒩", ":normed-matrix"), ("ⓢ", ":bracketed-matrix"), ("Ⓢ", ":curly-braced-matrix"),
        };

        private static readonly (string Symbol, string Notation)[] SymbolClasses =
        {
            ("▭", "box"), ("\u0304", "top"), ("▁", "bottom"), ("▢", "roundedbox"), ("◯", "circle"), ("⟌", "longdiv"), ("\u20E7", "actuarial"),
            ("⬭", "circle"), ("╱", "cancel"), ("╲", "bcancel"), ("╳", "xcancel"),
        };

        private static readonly (int Bit, string Notation)[] MaskClasses =
        {
            (1, "top"), (2, "bottom"), (4, "left"), (8, "right"), (16, "horizontalstrike"), (32, "verticalstrike"), (64, "downdiagonalstrike"),
            (128, "updiagonalstrike"),
        };

        private static readonly string[] NeedPreSpace = { "mfrac", "mover", "msub", "msubsup", "msup", "munder", "munderover" };

        private readonly bool keepSelectionInfo;
        private bool display;

        private UnicodeMathSerializer(bool keepSelectionInfo)
        {
            this.keepSelectionInfo = keepSelectionInfo;
        }

        /// <summary>getUnicodeMath: the UnicodeMath of a MathML element, with the spaces that build-up does not need removed.</summary>
        /// <summary>
        /// A copy with the single Latin and Greek letters of mi elements as the italic mathematical alphanumerics (x as
        /// 𝑥), the form UnicodeMathML writes and this port decides spaces and parentheses by.
        /// </summary>
        private static XElement Italicized(XElement math)
        {
            if (!math.Descendants().Any(e => e.Name.LocalName == "mi" && IsPlainLetter(e)))
                return math;
            var copy = new XElement(math);
            foreach (var letter in copy.Descendants().Where(e => e.Name.LocalName == "mi" && IsPlainLetter(e)))
                letter.Value = GetMathAlphanumeric(letter.Value, "mit");
            return copy;
        }

        private static bool IsPlainLetter(XElement mi)
        {
            if (mi.HasElements || mi.Attribute("mathvariant") != null || Len(mi.Value) != 1)
                return false;
            var c = CodePointAt(mi.Value, 0);
            return c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= 0x3B1 && c <= 0x3C9;
        }

        public static string Serialize(XElement math, bool keepSelectionInfo = false)
        {
            var italicized = MathML.DefaultIntents.Restore(Italicized(math));
            var serializer = new UnicodeMathSerializer(keepSelectionInfo) { display = (string)math.Attribute("display") == "block" };
            var text = serializer.Write(italicized);
            // the intents and argument names that reading does not give back are written: ⓘ("name"…) and ⓐ(name …)
            bool display = (string)math.Attribute("display") == "block";
            for (int pass = 0; pass < 3; pass++)
            {
                XElement back;
                try
                {
                    back = MathML.MathMLParser.Normalize(UnicodeMathParser.Parse(text, display), new EquationOptions(), false);
                }
                catch (System.FormatException)
                {
                    break;
                }
                int count = serializer.explicitIntents.Count + serializer.explicitArgs.Count + serializer.rawOperators.Count;
                serializer.FindLostIntents(italicized, back);
                if (serializer.explicitIntents.Count + serializer.explicitArgs.Count + serializer.rawOperators.Count == count)
                    break;
                text = serializer.Write(italicized);
            }
            return text;
        }

        private readonly HashSet<XElement> explicitIntents = new HashSet<XElement>();
        private readonly HashSet<XElement> explicitArgs = new HashSet<XElement>();
        private readonly HashSet<XElement> rawOperators = new HashSet<XElement>();

        /// <summary>
        /// The intent of an element, or the one UnicodeMath gives its structure by default (an equation holds none):
        /// :function for a function application, :largeop for an n-ary operation, :fenced for content between fences.
        /// </summary>
        internal static string Role(XElement e)
        {
            var intent = Attr(e, "intent");
            if (intent != null)
                return intent;
            if (Name(e) == "mtable" && Attr(e, "columnspacing") == "0pt")
                return ":system-of-equations";
            if (Name(e) != "mrow")
                return null;
            int n = ChildCount(e);
            if (n == 3 && Text(First(e)) == "{" && Text(Last(e)) == "\u200B" && Role(Child(e, 1)) == ":system-of-equations")
                return ":piecewise";
            if (n >= 2 && Name(Child(e, 1)) == "mo" && Text(Child(e, 1)) == "\u2061")
                return ":function";
            if (n == 2 && IsNaryHead(First(e)))
                return ":largeop";
            if (n >= 2 && IsFence(First(e)) && IsFence(Last(e)))
                return ":fenced";
            return null;
        }

        private static bool IsFenced(XElement row) => Name(row) == "mrow" && Role(row) == ":fenced";

        private static bool IsFenceDelimited(XElement row) => Name(row) == "mrow" && ChildCount(row) > 1 && IsFence(First(row)) && IsFence(Last(row));

        private static XElement IsNaryHeadBase(XElement head)
        {
            while (Name(head) is "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover")
                head = First(head);
            return head;
        }

        private static bool IsNaryHead(XElement head)
        {
            while (Name(head) is "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover")
                head = First(head);
            return Name(head) == "mo" && Len(Text(head)) == 1 && IsNaryOperator(Text(head));
        }

        private static bool IsFence(XElement mo)
        {
            if (Name(mo) != "mo" || mo.HasElements)
                return false;
            var text = Text(mo);
            return text.Length == 0 || text == "\u200B" || HasAttr(mo, "minsize") || IsOpening(text) || IsClosing(text) || IsOpenDelimiter(text) ||
                Len(text) == 1 && ")]}⟩⌉⌋⟧⟫⦄〗❳〕⁆⟭⟯⦆⦈⦊⦌⦎⦐⦒⦔⦖⦘⧙⧛⧽".Contains(text);
        }

        /// <summary>
        /// An operator that reading would take for a structure: an enclosure or a root sign without its operand, or an
        /// opening bracket that nothing closes inside a row.
        /// </summary>
        private static bool IsLoneStructure(XElement mo)
        {
            var text = Text(mo);
            if (Len(text) != 1)
                return false;
            if ("▭√∛∜⟡▢◯⬭".Contains(text))
                return true;
            var parent = mo.Parent;
            return (IsOpening(text) || IsOpenDelimiter(text)) && text != "|" && text != "‖" && Name(parent) == "mrow" && First(parent) != mo &&
                !(Last(parent) == mo && IsFence(First(parent))) && Role(parent) != ":fenced" && !mo.ElementsAfterSelf().Any(e => Name(e) == "mo" && IsClosing(Text(e)));
        }

        private static bool IsNaryOperator(string op) => "∑∏∐∫∬∭∮∯∰∱∲∳⨌⨀⨁⨂⨃⨄⨅⨆⋀⋁⋂⋃".Contains(op);

        /// <summary>The elements whose intent or argument name reading the UnicodeMath does not give back, in a parallel walk.</summary>
        /// <summary>
        /// An intent or argument name that an equation holds: not one UnicodeMath gives by default (found again for writing,
        /// or in MathML not read as an equation), and an argument name that such an intent refers to.
        /// </summary>
        private static bool IsHeld(XAttribute attribute) =>
            attribute != null && (attribute.Name == "intent" ? !MathML.DefaultIntents.IsRestored(attribute) && !MathML.MathMLParser.IsDefaultIntent(attribute.Value)
                : MathML.MathMLParser.IsReferenced(attribute));

        private void FindLostIntents(XElement written, XElement read)
        {
            if (Name(written) != Name(read))
            {
                if (Name(written) == "mo" && Name(read) == "mi" && Text(written) == Text(read))
                    rawOperators.Add(written);
                // a row of one element whose intent reading does not give back: ⓘ("time derivative"𝜌̇)
                if (Name(written) == "mrow" && ChildCount(written) == 1 && Name(First(written)) == Name(read) &&
                    (IsHeld(written.Attribute("intent")) && !HasAttr(read, "intent") || IsHeld(written.Attribute("arg")) && !HasAttr(read, "arg")))
                {
                    if (IsHeld(written.Attribute("intent")))
                        explicitIntents.Add(written);
                    if (IsHeld(written.Attribute("arg")))
                        explicitArgs.Add(written);
                }
                return;
            }
            // an intent that reading gives in another form (:largeop(k,…) for :largeop($l,…)) is not written: ⓘ would
            // add one more
            if (IsHeld(written.Attribute("intent")) && Attr(written, "intent") != Attr(read, "intent") && Attr(read, "intent") is null or ":fenced")
                explicitIntents.Add(written);
            if (IsHeld(written.Attribute("arg")) && Attr(read, "arg") == null)
                explicitArgs.Add(written);
            var writtenChildren = Children(written);
            var readChildren = Children(read);
            if (writtenChildren.Count != readChildren.Count)
            {
                // an n-ary operator that reading takes for the operator of what follows it (∫ ⅆ𝑥)
                foreach (var mo in writtenChildren.Where(c => Name(c) == "mo" && Len(Text(c)) == 1 && IsNaryOperator(Text(c))))
                    rawOperators.Add(mo);
                // a row of an intent or an argument name that reading does not give back is not read as a row either
                // (ⓘ("derivative"𝑓′(𝑥)): its elements are read as elements of its parent
                foreach (var row in writtenChildren.Where(c => Name(c) == "mrow" && (IsHeld(c.Attribute("intent")) || IsHeld(c.Attribute("arg"))) &&
                    writtenChildren.Count - 1 + ChildCount(c) == readChildren.Count))
                {
                    if (IsHeld(row.Attribute("intent")))
                        explicitIntents.Add(row);
                    if (IsHeld(row.Attribute("arg")))
                        explicitArgs.Add(row);
                }
                return;
            }
            for (int i = 0; i < writtenChildren.Count; i++)
                FindLostIntents(writtenChildren[i], readChildren[i]);
        }

        private string Write(XElement math)
        {
            var unicodeMath = Dump(math, false);
            var chars = Characters(unicodeMath);
            for (int i = 0; ; i++)
            {
                i = chars.IndexOf(" ", i);
                if (i < 0)
                    break;
                if (i == chars.Count - 1)
                {
                    chars.RemoveAt(i);
                    break;
                }
                // the space that ends a script of an operator (𝐴^∗ =) is needed: the script would go on otherwise
                if (i >= 2 && chars[i - 2] is "^" or "_" or "┬" or "┴" && !IsAlphanumeric(chars[i - 1]) && chars[i - 1] != ")")
                    continue;
                if ("=+−/ )]}〗".Contains(chars[i + 1]))
                {
                    int j = 1;
                    if (chars[i + 1] == " " && i < chars.Count - 2 && "=+−/)]}".Contains(chars[i + 2]))
                        j = 2;
                    chars.RemoveRange(i, j);
                }
            }
            return string.Concat(chars);
        }

        /// <summary>
        /// MathML text as an element: the "mml:" or "m:" prefix that some producers write without declaring it is
        /// removed first (removeMmlPrefixes).
        /// </summary>
        public static XElement ParseMathML(string mathML)
        {
            foreach (var prefix in new[] { "mml:", "m:" })
            {
                if (mathML.StartsWith("<" + prefix, System.StringComparison.Ordinal))
                {
                    mathML = mathML.Replace("<" + prefix, "<").Replace("</" + prefix, "</");
                    break;
                }
            }
            return XElement.Parse(mathML, LoadOptions.PreserveWhitespace);
        }

        // DOM of the JavaScript on XElement
        internal static string Name(XElement e) => e?.Name.LocalName;

        internal static List<XElement> Children(XElement e) => e.Elements().ToList();

        internal static XElement Child(XElement e, int i) => e?.Elements().ElementAtOrDefault(i);

        internal static XElement First(XElement e) => e?.Elements().FirstOrDefault();

        internal static XElement Last(XElement e) => e?.Elements().LastOrDefault();

        internal static XElement Previous(XElement e) => e?.ElementsBeforeSelf().LastOrDefault();

        private static bool IsCombining(string text) => Len(text) == 1 && (InRange("\u0300", text, "\u036F") || InRange("\u20D0", text, "\u20FF"));

        private static bool IsOpening(string fence) => Len(fence) == 1 && "([{⟨⌈⌊⟦⟪⦃〖❲〔⁅|‖".Contains(fence);

        private static bool IsClosing(string fence) => Len(fence) == 1 && ")]}⟩⌉⌋⟧⟫⦄〗❳〕⁆|‖".Contains(fence);

        internal static string Attr(XElement e, string name) => (string)e?.Attribute(name);

        internal static bool HasAttr(XElement e, string name) => e?.Attribute(name) != null;

        internal static string Text(XElement e) => e?.Value ?? "";

        internal static int ChildCount(XElement e) => e?.Elements().Count() ?? 0;

        internal static bool IsAsciiAlphabetic(string ch) => ch != null && Regex.IsMatch(ch, "[A-Za-z]");

        internal static bool IsAlphanumeric(string ch)
        {
            if (ch == null)
                return false;
            int code = CodePointAt(ch, 0);
            return Regex.IsMatch(ch, "^[A-Za-z0-9]") || code >= 0x3018 || InRange("Α", ch, "ϵ") || InRange("ℂ", ch, "ⅉ");
        }

        internal static bool IsCloseDelimiter(string op) => op.Length > 0 && Len(op) == 1 && ")]}⟩〗⌉⌋❳⟧⟫⟭⟯⦄⦆⦈⦊⦌⦎⦐⦒⦔⦖⦘⧙⧛⧽".Contains(op);

        internal static bool IsOpenDelimiter(string op) => op.Length > 0 && Len(op) == 1 && "([{⟨〖⌈⌊❲⟦⟨⟪⟬⟮⦃⦅⦇⦉⦋⦍⦏⦑⦓⦕⦗⧘⧚⧼".Contains(op);

        /// <summary>Primes only (′, ″, ‴, ⁗ or '), one or more.</summary>
        internal static bool IsPrime(string ch) => Len(ch) > 0 && Characters(ch).All(c => "′″‴⁗'".Contains(c));

        internal static bool IsDoubleStruck(string ch) => InRange("ⅅ", ch, "ⅉ");

        internal static string RemoveOuterParens(string ret)
        {
            var chars = Characters(ret);
            if (chars.Count > 0 && chars[0] == "(")
            {
                int cParen = 1;
                for (int i = 1; i < chars.Count - 1; i++)
                {
                    if (chars[i] == "(")
                        cParen++;
                    else if (chars[i] == ")")
                        cParen--;
                    if (cParen == 0)
                        break;
                }
                if (cParen == 1 && chars[chars.Count - 1] == ")")
                    ret = Sub(ret, 1, chars.Count - 1);
            }
            return ret;
        }

        internal static bool NeedParens(string ret)
        {
            var chars = Characters(ret);
            string ch1 = null;
            for (int i = 0; i < chars.Count; i++)
            {
                var c = chars[i];
                if (c == "(" && i < chars.Count - 1)
                {
                    int j = chars.IndexOf(")", i + 1);
                    if (j > 0)
                    {
                        i = j;
                        continue;
                    }
                    return true;
                }
                if (!CodePointsAreBmp(c))
                    continue;
                if (c == " " && (ch1 == "^" || ch1 == "_") || IsAlphanumeric(c) || c == "∑")
                    continue;
                if (!DigitSuperscripts.Contains(c) && !IsPrime(c) && !DigitSubscripts.Contains(c) && !"\u2061∞⬌!^_ⒶⒻ".Contains(c) && (i != 0 || c != "−"))
                    return true;
                ch1 = c;
            }
            return false;
        }

        private static bool CodePointsAreBmp(string c) => CodePointAt(c, 0) <= 0xFFFF;

        private static bool NeedBeginEnd(XElement node)
        {
            int cChild = ChildCount(node);
            if (cChild <= 1)
                return false;
            bool prevOpHat = false;
            foreach (var child in Children(node))
            {
                if (Name(child) == "mo")
                {
                    var op = Text(child);
                    if (op == "^")
                    {
                        prevOpHat = true;
                        continue;
                    }
                    if (op != "(" && op != ")" && (op != "−" || !prevOpHat))
                        return true;
                }
                prevOpHat = false;
            }
            return false;
        }

        private static readonly string[] MathMLObjects =
            { "mfrac", "msqrt", "mroot", "menclose", "msup", "msub", "munderover", "msubsup", "mover", "munder", "mpadded", "mphantom", "multiscripts" };

        private static bool IsMathMLObject(XElement value, bool ignoreIntent)
        {
            if (value != null && Name(value) == "mrow")
            {
                if (!ignoreIntent && Role(value) != null)
                {
                    var intent = Role(value);
                    if (intent == ":function" || intent == ":fenced" || intent.Contains("integral") ||
                        intent.StartsWith(":n-ary", System.StringComparison.Ordinal) || intent.StartsWith("binomial-coefficient", System.StringComparison.Ordinal))
                        return true;
                }
                if (ChildCount(value) == 1)
                    value = value.Parent;
            }
            return value != null && MathMLObjects.Contains(Name(value));
        }

        internal static bool HasEqLabel(XElement node)
        {
            if (Name(node) != "mtable")
                return false;
            node = First(node);
            return Name(node) == "mlabeledtr" || Attr(First(node), "intent") == ":equation-label";
        }

        internal static bool IsDigitArg(XElement node)
        {
            if (node == null || Last(node) == null)
                return false;
            return Name(Last(node)) == "mn" && Child(node, 1) != null && IsAsciiDigit(Text(Child(node, 1)));
        }

        private static string CheckSpace(int i, XElement node, string ret)
        {
            if (i > 0 && Name(node) == "mrow" && First(node) != null && Name(First(node)) == "mi" &&
                IsAsciiAlphabetic(Ch(Text(First(node)), 0)) && IsAlphanumeric(Ch(ret, Len(ret) - 1)))
                return " ";
            return "";
        }

        private static string DigitScript(string digits, string table)
        {
            return string.Concat(Characters(digits).Select(d => IsAsciiDigit(d) ? Ch(table, d[0] - '0') : d));
        }

        private static string CheckSelAttr(XElement value, string op)
        {
            var selattr = Attr(value, op == "Ⓐ" ? "selanchor" : "selfocus");
            if (string.IsNullOrEmpty(selattr))
                return "";
            if (selattr == "0")
                selattr = "";
            return op + "(" + selattr + ")";
        }

        private string GetSelectionCodes(XElement value) => keepSelectionInfo ? CheckSelAttr(value, "Ⓐ") + CheckSelAttr(value, "Ⓕ") : "";

        private string Unary(XElement node, string op)
        {
            int cNode = ChildCount(node);
            var ret = Nary(node, "", cNode);
            if (op.Length == 0)
                ret = RemoveOuterParens(ret);
            else if (cNode > 1 || cNode == 1 && Name(First(node)) is "mfrac" or "mover" or "munder" && !(Ch(ret, 0) == "(" && RemoveOuterParens(ret) != ret))
                ret = "(" + ret + ")";
            return op + ret;
        }

        private string Binary(XElement node, string op)
        {
            var ret = Dump(First(node));
            var retd = Dump(Last(node));
            // an empty operand (the <mo/> of an unclosed bracket) is an empty group, as nothing would not read back
            if (ret.Length == 0)
                ret = "〖〗";
            if (retd.Length == 0)
                retd = "〖〗";

            // a scripted base (𝑎_𝑏 under ^𝑐) is grouped, as 𝑎_𝑏^𝑐 is one subsup
            if ((op == "^" || op == "_" || IsPrime(Text(Last(node)))) && IsScripted(First(node)) && functionName == null)
                ret = "〖" + ret.TrimEnd(' ') + "〗";
            if (op == "^" && Name(Last(node)) == "msub" || op == "_" && Name(Last(node)) == "msup")
                return ret + op + "(" + retd + ")";
            // a script with a limit (𝑎_(𝑏┬𝑐)) and the denominator of a fraction that is a fraction are parenthesized
            if ((op == "^" || op == "_") && Name(Last(node)) is "munder" or "mover" or "munderover" && HasLimit(retd) ||
                Name(node) == "mfrac" && (Name(Last(node)) == "mfrac" || Name(Last(node)) == "mo" && Text(Last(node)).Length > 0 && !char.IsLetter(Text(Last(node)), 0)))
                retd = "(" + retd.Trim(' ') + ")";
            // an operator alone over a fraction bar ((+̸)/2) is in parentheses, which the fraction drops
            if (Name(node) == "mfrac" && Name(First(node)) == "mo" && Text(First(node)).Length > 0 && !char.IsLetter(Text(First(node)), 0))
                ret = "(" + ret.Trim(' ') + ")";
            // a script that applies a function (log_(𝑏⁡𝑎)): the space before the function name would end the script
            if ((op == "^" || op == "_") && Name(Last(node)) == "mrow" && Role(Last(node)) == ":function")
                retd = "(" + retd.Trim(' ') + ")";
            if (IsMathMLObject(node, false) && ChildCount(node) > 0)
            {
                var attr = Role(Last(node));
                if (attr == ":fenced" && Ch(retd, 0) == "(" && Ch(retd, Len(retd) - 1) == ")")
                    retd = "(" + retd + ")";
                if (Name(node) == "mfrac")
                {
                    attr = Role(First(node));
                    if (attr == ":fenced" && Ch(ret, 0) == "(" && Ch(ret, Len(ret) - 1) == ")")
                        ret = "(" + ret + ")";
                }
            }
            if (op == "⊘")
            {
                var ch = GetUnicodeFraction(ret, retd);
                if (ch.Length > 0)
                    return ch;
            }
            if (op == "/" && (ret.EndsWith("^∗ )", System.StringComparison.Ordinal) || ret.EndsWith("^† )", System.StringComparison.Ordinal)))
                ret = Sub(ret, 1, Len(ret) - 2);
            // a script of a scripted base (a^b^c): the space that ends the inner script would end the outer one too
            if (op == "^" || op == "_")
                ret = ret.TrimEnd(' ');
            // an accent applies to the character before it: a longer base ("text", 𝑥_𝑖) is parenthesized
            if (op.Length == 0 && (Name(node) == "mover" || Name(node) == "munder") && Len(ret.TrimEnd(' ')) > 1 &&
                !(Ch(ret, 0) == "(" && Ch(ret, Len(ret) - 1) == ")"))
                ret = "(" + ret.TrimEnd(' ') + ")";
            ret += op + retd;
            if (op.Length > 0)
                ret += " ";
            return ret;
        }

        private string Ternary(XElement node, string op1, string op2)
        {
            var ret = new[] { Dump(Child(node, 1)), Dump(Child(node, 2)) };
            if (Name(node) == "msubsup" || Name(node) == "munderover")
            {
                for (int i = 0; i < 2; i++)
                {
                    var attr = Role(Child(node, i + 1));
                    if (attr == ":fenced" && Ch(ret[i], 0) == "(" && Ch(ret[i], Len(ret[i]) - 1) == ")")
                        ret[i] = "(" + ret[i] + ")";
                    // a scripted script (𝑎_(𝑏_𝑐)^𝑑): the space that ends the inner script would end both
                    else if (Name(Child(node, i + 1)) is "msub" or "msup" or "msubsup" && Len(ret[i]) > 0 && Ch(ret[i], Len(ret[i]) - 1) == " ")
                        ret[i] = "(" + ret[i].TrimEnd(' ') + ")";
                }
            }
            var bas = Dump(First(node));
            if (op1 == "_" && IsScripted(First(node)))
                bas = "〖" + bas.TrimEnd(' ') + "〗";
            for (int i = 0; i < 2; i++)
            {
                if (Name(Child(node, i + 1)) is "munder" or "mover" or "munderover" && HasLimit(ret[i]))
                    ret[i] = "(" + ret[i].Trim(' ') + ")";
            }
            return bas + op1 + ret[0] + op2 + ret[1] + " ";
        }

        private static bool HasLimit(string text) => text.Any(c => c == '┬' || c == '┴');

        /// <summary>A base with scripts of its own, which a script after it would join.</summary>
        private static bool IsScripted(XElement node) => Name(node) is "msub" or "msup" or "msubsup" or "mmultiscripts";

        /// <summary>
        /// The operator of an n-ary operation with its limits, and the options of UnicodeMath (∑1, ∮16) for where the
        /// limits are when they are not where they go by default, and for the placeholder of a limit (⬚).
        /// </summary>
        private XElement naryHeadBase;

        private string NaryOperator(XElement node, XElement naryand)
        {
            naryHeadBase = IsNaryHeadBase(node);
            try
            {
                return NaryOperatorCore(node, naryand);
            }
            finally
            {
                naryHeadBase = null;
            }
        }

        private string NaryOperatorCore(XElement node, XElement naryand)
        {
            var name = Name(node);
            bool underOver = name is "munder" or "mover" or "munderover";
            bool scripts = underOver || name is "msub" or "msup" or "msubsup";
            int smash = IsSmashed(naryand) ? 64 : 0;
            // the upper limit as a superscript of the operator, the lower one under it (∫3)
            if (name is "munder" && Name(First(First(node))) == "mo" && Name(First(node)) is "msup" && ChildCount(First(node)) == 2)
            {
                return Dump(First(First(node))) + (3 + smash).ToString(System.Globalization.CultureInfo.InvariantCulture) + "┬" + LimitText(Child(node, 1)) + "┴" +
                    LimitText(Child(First(node), 1));
            }
            if (!scripts || ChildCount(node) < 2 || Name(First(node)) != "mo")
                return Dump(node);
            var op = Text(First(node));
            // one limit under or over the operator: ┬ and ┴ put it there in either mode, as UnicodeMathML writes it
            if (name is "munder" or "mover" && ChildCount(node) == 2 && !(Name(Last(node)) == "mo" && Text(Last(node)) == "⬚"))
                return Dump(First(node)) + (smash > 0 ? smash.ToString(System.Globalization.CultureInfo.InvariantCulture) : "") + (name == "munder" ? "┬" : "┴") +
                    LimitText(Last(node));
            XElement low = null, high = null;
            if (name is "munderover" or "msubsup")
            {
                low = Child(node, 1);
                high = Child(node, 2);
            }
            else if (name is "munder" or "msub")
            {
                low = Child(node, 1);
            }
            else
            {
                high = Child(node, 1);
            }
            int mask = smash;
            bool integral = InRange("∫", op, "∳") || op == "⨌";
            if (underOver != (display && !integral))
                mask += underOver ? 1 : 2;
            if (Name(low) == "mo" && Text(low) == "⬚")
            {
                mask += 8;
                low = null;
            }
            if (Name(high) == "mo" && Text(high) == "⬚")
            {
                mask += 16;
                high = null;
            }
            var ret = Dump(First(node)) + (mask > 0 ? mask.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
            // digit limits as subscript and superscript digits (∑₀¹), as for any base
            bool digits = low == null || Name(low) == "mn" && Text(low).All(c => c >= '0' && c <= '9');
            foreach (var (script, mark) in new[] { (low, "_"), (high, "^") })
            {
                if (script == null)
                    continue;
                if (digits && Name(script) == "mn" && Text(script).All(c => c >= '0' && c <= '9') && GetSelectionCodes(script).Length == 0)
                    ret += DigitScript(Text(script), mark == "_" ? DigitSubscripts : DigitSuperscripts);
                else
                {
                    ret += mark + LimitText(script);
                    digits = false;
                }
            }
            return ret;
        }

        private string LimitText(XElement script)
        {
            var text = Dump(script).Trim(' ');
            if (Name(script) is "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover" && !(Ch(text, 0) == "(" && Ch(text, Len(text) - 1) == ")") ||
                Role(script) == ":fenced" && Ch(text, 0) == "(")
                text = "(" + text + ")";
            return text;
        }

        private XElement functionName;
        private XElement plainBinomial;

        /// <summary>An operand in parentheses, which reading drops: a fenced one in two pairs.</summary>
        private string Grouped(XElement node)
        {
            var text = Dump(node).Trim(' ');
            if (Ch(text, 0) == "(" && Ch(text, Len(text) - 1) == ")" && RemoveOuterParens(text) != text && Role(node) != ":fenced")
                return text;
            return "(" + text + ")";
        }

        /// <summary>The name of a function with scripts that is written with the function apply sign after it, or null.</summary>
        private static XElement FunctionName(XElement head)
        {
            if (Name(head) is not ("msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover"))
                return null;
            var name = First(head);
            bool nested = false;
            while (Name(name) is "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover")
            {
                name = First(name);
                nested = true;
            }
            if (Name(name) != "mi" || Len(Text(name)) < 2 || name.HasElements)
                return null;
            return nested || !IsFunctionName(Text(name)) ? name : null;
        }

        /// <summary>The operand of an n-ary operator that does not grow it (∫64), an mpadded of no height.</summary>
        private static bool IsSmashed(XElement naryand) =>
            Name(naryand) == "mpadded" && Attr(naryand, "height") == "0" && naryand.Attributes().All(a => a.Name == "height" || a.Name == "arg");

        private string SmashedContent(XElement naryand)
        {
            var content = Nary(naryand, "", ChildCount(naryand)).Trim(' ');
            return ChildCount(naryand) > 1 ? "〖" + content + "〗" : content;
        }

        /// <summary>A script of mmultiscripts: a space (a placeholder of MathType) in parentheses, which reading keeps.</summary>
        private string ScriptText(XElement script)
        {
            var text = Dump(script);
            return text.Length > 0 && text.Trim().Length == 0 ? "(" + text + ")" : text.TrimEnd(' ');
        }

        /// <summary>The base of digit scripts (𝑥₁): grouped when it has scripts of its own or several parts.</summary>
        private string ScriptBase(XElement node)
        {
            var ret = Dump(node);
            if (IsScripted(node) || Name(node) == "mrow" && ChildCount(node) > 1 && !HasAttr(node, "intent"))
                ret = "〖" + ret.TrimEnd(' ') + "〗";
            return ret;
        }

        private string Nary(XElement node, string op, int cNode)
        {
            var parts = new List<string>();
            for (int i = 0; i < cNode; i++)
                parts.Add(Dump(Child(node, i)));
            return string.Join(op, parts);
        }

        private string Dump(XElement value, bool noAddParens = false)
        {
            var ret = DumpCore(value, noAddParens);
            if (value == null || !explicitIntents.Contains(value) && !explicitArgs.Contains(value))
                return ret;
            var core = ret.Trim(' ');
            var lead = ret.Substring(0, ret.Length - ret.TrimStart(' ').Length);
            var trail = ret.Substring(ret.TrimEnd(' ').Length);
            // a row is grouped, unless its fences make it one (parentheses, bars, a binomial coefficient)
            if (Name(value) == "mrow" && ChildCount(value) > 1 && !(Name(First(value)) == "mo" && Name(Last(value)) == "mo" && Text(First(value)).Length > 0))
                core = "〖" + core + "〗";
            if (explicitIntents.Contains(value))
                core = "ⓘ(\"" + Attr(value, "intent").Replace("\"", "\\\"") + "\"" + core + ")";
            if (explicitArgs.Contains(value))
                core = "ⓐ(" + Attr(value, "arg") + " " + core + ")";
            return lead + core + trail;
        }

        private string DumpCore(XElement value, bool noAddParens)
        {
            if (value == null)
                return "";

            int cNode = ChildCount(value) > 0 ? ChildCount(value) : 1;
            var intent = Role(value);
            XElement nodeLEC;
            string op;
            var ret = "";
            string symbol;
            string val;
            var parent = value.Parent;

            switch (Name(value))
            {
                case "mtable":
                    symbol = "■";
                    if (intent == ":system-of-equations")
                    {
                        symbol = "█";
                    }
                    else if (HasAttr(parent, "intent") && MatrixIntents.Any(m => m.Intent == Attr(parent, "intent")))
                    {
                        symbol = MatrixIntents.First(m => m.Intent == Attr(parent, "intent")).Symbol;
                    }
                    else if (intent == ":math-paragraph")
                    {
                        var rows = Children(value);
                        for (int i = 0; i < rows.Count; i++)
                        {
                            var node = rows[i];
                            if (Name(node) == "mlabeledtr" || Attr(First(node), "intent") == ":equation-label")
                            {
                                var text = Text(First(node));
                                if (ChildCount(node) == 3)
                                    ret += Dump(Child(node, 1)) + "&";
                                ret += Dump(Last(node)) + "#" + text;
                            }
                            else
                            {
                                ret += Dump(node);
                            }
                            if (i < rows.Count - 1)
                                ret += "\n";
                        }
                        break;
                    }
                    else if (cNode == 1 && HasEqLabel(value))
                    {
                        var eqno = Text(First(First(First(value))));
                        return Dump(Last(First(value))) + "#" + eqno;
                    }
                    ret = symbol + "(" + Nary(value, "@", cNode) + ")";
                    break;

                case "mtr":
                    ret = Nary(value, "&", cNode);
                    // an empty first cell (of an equation label) is not written
                    if (Ch(ret, 0) == "&" && First(value) != null && Dump(First(value)).Length == 0)
                        ret = Sub(ret, 1);
                    break;

                case "mtd":
                    if (intent == ":no-equation-label")
                        return "";
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
                    if (!string.IsNullOrEmpty(notation))
                    {
                        foreach (var (key, cls) in SymbolClasses)
                        {
                            if (cls == notation)
                            {
                                ret = Unary(value, key);
                                break;
                            }
                        }
                        if (ret.Length > 0)
                            break;
                        int mask = 0;
                        foreach (Match attr in Regex.Matches(notation, "[a-z]+"))
                        {
                            foreach (var (bit, cls) in MaskClasses)
                            {
                                if (cls == attr.Value)
                                    mask += bit;
                            }
                        }
                        if (mask != 0)
                        {
                            ret = Unary(value, "");
                            ret = "▭(" + (mask ^ 15) + "&" + ret + ")";
                            break;
                        }
                    }
                    ret = Unary(value, "▭");
                    break;
                }

                case "mphantom":
                    ret = Unary(value, "⟡");
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

                    if (First(value) != null && Name(First(value)) == "mphantom")
                    {
                        if (mask == 2)
                            op = "⇳";
                        else if (mask == 12)
                            op = "⬄";
                        ret = Sub(Dump(First(value)), 1);
                        ret = op.Length > 0 ? op + ret : "⟡(" + mask + "&" + RemoveOuterParens(ret) + ")";
                        break;
                    }
                    switch (mask)
                    {
                        case 2: op = "⬌"; break;
                        case 4: op = "⬆"; break;
                        case 8: op = "⬇"; break;
                        case 12: op = "⬍"; break;
                    }
                    mask |= 1;
                    if (op.Length > 0)
                    {
                        ret = Unary(value, op);
                    }
                    else
                    {
                        ret = RemoveOuterParens(Nary(value, "", cNode));
                        ret = "⟡(" + mask + "&" + ret + ")";
                    }
                    break;
                }

                case "mstyle":
                    ret = Nary(value, "", cNode);
                    val = Attr(value, "mathcolor");
                    if (!string.IsNullOrEmpty(val))
                        ret = "✎(" + val + "&" + ret + ")";
                    val = Attr(value, "mathbackground");
                    if (!string.IsNullOrEmpty(val))
                        ret = "☁(" + val + "&" + ret + ")";
                    break;

                case "msqrt":
                    ret = Unary(value, "√");
                    break;

                case "mroot":
                    ret = "√(" + Dump(Last(value), true) + "&" + Dump(First(value), true) + ")";
                    break;

                case "mfrac":
                    op = "/";
                    if (Attr(value, "displaystyle") == "false")
                        op = "⊘";
                    else if (Attr(value, "bevelled") == "true")
                        op = "⁄";
                    val = Attr(value, "linethickness");
                    if (val == "0" || val == "0.0pt")
                    {
                        op = "¦";
                        if (parent != plainBinomial && HasAttr(parent, "intent") && Attr(parent, "intent").StartsWith("binomial-coefficient", System.StringComparison.Ordinal) ||
                            Attr(First(parent), "title") == "binomial coefficient")
                            op = "⒞";
                    }
                    ret = Binary(value, op);
                    // after a space operator, which does not end its numerator, the fraction is grouped
                    if (Attr(parent, "intent") == ":function" || Name(Previous(value)) == "mo" && Text(Previous(value)).Length > 0 && Text(Previous(value)).Trim().Length == 0)
                        ret = "〖" + ret.Trim(' ') + "〗";
                    else if (Previous(value) != null && Name(Previous(value)) != "mo")
                        ret = " " + ret;
                    break;

                case "msup":
                    nodeLEC = Last(value);
                    if (IsDigitArg(value) && GetSelectionCodes(nodeLEC).Length == 0)
                    {
                        ret = ScriptBase(First(value)) + DigitScript(Text(nodeLEC), DigitSuperscripts);
                        break;
                    }
                    // a prime and a power of it (𝑥′²)
                    if (Name(nodeLEC) == "mrow" && ChildCount(nodeLEC) == 2 && Name(First(nodeLEC)) == "mo" && IsPrime(Text(First(nodeLEC))) &&
                        Name(Last(nodeLEC)) == "mn" && Text(Last(nodeLEC)).All(char.IsDigit) && GetSelectionCodes(nodeLEC).Length == 0)
                    {
                        ret = ScriptBase(First(value)) + Text(First(nodeLEC)) + DigitScript(Text(Last(nodeLEC)), DigitSuperscripts);
                        break;
                    }
                    op = "^";
                    if (nodeLEC != null && Name(nodeLEC) == "mo" && IsPrime(Text(nodeLEC)))
                        op = "";
                    ret = Binary(value, op);
                    if (nodeLEC != null && Attr(nodeLEC, "intent") == "transpose" && !explicitArgs.Contains(nodeLEC))
                    {
                        // the T (𝑇, or "T" upright) written as the transpose sign
                        var script = Dump(nodeLEC) + " ";
                        if (ret.EndsWith(script, System.StringComparison.Ordinal))
                            ret = ret.Substring(0, ret.Length - script.Length) + "⊺";
                    }
                    break;

                case "mover":
                case "munder":
                    // a script of a horizontal bracket: ⏞(𝑥+𝑦)^(𝑛), ⏟(𝑥+𝑦)_(𝑛)
                    if (Name(First(value)) == Name(value) && ChildCount(First(value)) == 2 && Text(Last(First(value))).Length > 0 &&
                        (Name(value) == "mover" ? OverBrackets : UnderBrackets).Contains(Text(Last(First(value)))))
                    {
                        var script = Dump(Last(value)).Trim(' ');
                        if (Len(script) > 1 && !(Ch(script, 0) == "(" && RemoveOuterParens(script) != script && Role(Last(value)) != ":fenced"))
                            script = "(" + script + ")";
                        ret = Dump(Last(First(value))) + Grouped(First(First(value))) + (Name(value) == "mover" ? "┴" : "┬") + script;
                        break;
                    }
                    if (Name(value) == "munder")
                        goto case "munder-plain";
                    if (OverBrackets.Contains(Text(Last(value))) && Text(Last(value)).Length > 0)
                    {
                        ret = Dump(Last(value)) + Dump(First(value));
                        break;
                    }
                    // a combining character over the base is an accent, also without accent="true"
                    op = HasAttr(value, "accent") || IsCombining(Text(Last(value))) ? "" : "┴";
                    ret = Binary(value, op);
                    break;

                case "munder-plain":
                    if (UnderBrackets.Contains(Text(Last(value))) && Text(Last(value)).Length > 0)
                    {
                        ret = Dump(Last(value)) + Dump(First(value));
                        break;
                    }
                    op = HasAttr(value, "accentunder") || Attr(value, "accent") == "true" || IsCombining(Text(Last(value))) ? "" : "┬";
                    // lim_ is under lim in a function of a display equation only
                    if ((Text(First(value)) == "lim" && !First(value).HasElements || functionName != null && IsFunctionName(Text(functionName))) && display &&
                        Role(parent) == ":function")
                        op = "_";
                    ret = Binary(value, op);
                    break;

                case "msub":
                    nodeLEC = Last(value);
                    if (IsDigitArg(value) && GetSelectionCodes(nodeLEC).Length == 0)
                    {
                        ret = ScriptBase(First(value)) + DigitScript(Text(nodeLEC), DigitSubscripts);
                        break;
                    }
                    ret = Binary(value, "_");
                    break;

                case "munderover":
                case "msubsup":
                    if (Name(value) == "munderover")
                    {
                        var parentIntent = Role(parent);
                        if (string.IsNullOrEmpty(parentIntent) || !parentIntent.StartsWith(":largeop", System.StringComparison.Ordinal))
                        {
                            ret = Ternary(value, "┬", "┴");
                            break;
                        }
                    }
                    nodeLEC = Last(value);
                    if (nodeLEC != null && GetSelectionCodes(Child(value, 1)).Length == 0 && GetSelectionCodes(nodeLEC).Length == 0)
                    {
                        if (IsDigitArg(value))
                        {
                            ret = ScriptBase(First(value)) + DigitScript(Text(Child(value, 1)), DigitSubscripts);
                            if (IsAsciiDigit(Text(nodeLEC)))
                            {
                                ret += DigitScript(Text(nodeLEC), DigitSuperscripts);
                                break;
                            }
                            ret += "^" + Dump(nodeLEC);
                            break;
                        }
                        if (IsPrime(Text(nodeLEC)) && ChildCount(Child(value, 1)) < 2)
                        {
                            ret = Dump(First(value)) + Text(nodeLEC);
                            if (IsAsciiDigit(Text(Child(value, 1))))
                                ret += DigitScript(Text(Child(value, 1)), DigitSubscripts);
                            else
                                ret += "_" + Dump(Child(value, 1));
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
                            ret = "_" + ScriptText(children[prescripts + 1]);
                        if (prescripts + 2 < children.Count && Name(children[prescripts + 2]) != "none")
                            ret += "^" + (Attr(children[prescripts + 2], "intent") == "transpose" ? "⊺" : ScriptText(children[prescripts + 2]));
                        // the prescripts in parentheses after something they would be the scripts of; before a base with
                        // scripts of its own, which they would join, the base and they are grouped instead
                        if (ret.Length > 0 && IsScripted(Child(value, 0)))
                        {
                            ret = "〖" + ret + " 〖" + Dump(Child(value, 0)).TrimEnd(' ') + "〗〗";
                            if (children.Count > 1 && Name(children[1]) != "mprescripts")
                                ret = "〖" + ret + "〗";
                        }
                        else
                        {
                            if (ret.Length > 0)
                                ret = Previous(value) != null ? "(" + ret + ")" : ret + " ";
                            ret += Dump(Child(value, 0));
                        }
                    }
                    else
                    {
                        ret += Dump(Child(value, 0));
                    }
                    int end = prescripts >= 0 ? prescripts : children.Count;
                    if (end > 1 && Name(children[1]) != "none")
                        ret += "_" + ScriptText(children[1]);
                    if (end > 2 && Name(children[2]) != "none")
                        ret += "^" + ScriptText(children[2]);
                    break;
                }

                case "mfenced":
                {
                    var opClose = Attr(value, "close") ?? ")";
                    var opOpen = Attr(value, "open") ?? "(";
                    var opSeparators = Characters((Attr(value, "separators") ?? ",").Replace(" ", ""));
                    var children = Children(value);
                    ret = opOpen;
                    for (int i = 0; i < children.Count; i++)
                    {
                        ret += Dump(children[i]);
                        if (i < children.Count - 1 && opSeparators.Count > 0)
                            ret += i < opSeparators.Count - 1 ? opSeparators[i] : opSeparators[opSeparators.Count - 1];
                    }
                    ret += opClose;
                    break;
                }

                case "mo":
                    val = Text(value);
                    if (val == "\u200B" && Role(parent) == ":piecewise")
                        return "";
                    if (intent == ":text")
                    {
                        ret = "\\" + val;
                        break;
                    }
                    // an operator that reading would not take for one (ℏ, an n-ary operator without operand) is in a remark
                    if (rawOperators.Contains(value) || IsLoneStructure(value))
                    {
                        ret = "ⓡ(" + val + "&U+" + CodePointAt(val, 0).ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + ")";
                        break;
                    }
                    if (val == "/")
                    {
                        // a slash that is not a fraction: ∕ reads as one, \/ as the slash of text
                        ret = "∕";
                        break;
                    }
                    if (val == "|")
                    {
                        ret = val;
                        foreach (var node in new[] { parent, parent?.Parent })
                        {
                            if (node != null && node != value && Role(node) == ":fenced" && Text(First(node)) == "⟨" && Text(Last(node)) == "⟩" &&
                                First(node) != value && Last(node) != value)
                                ret = "│";
                        }
                        break;
                    }
                    if (HasAttr(value, "title"))
                    {
                        switch (Attr(value, "title"))
                        {
                            case "differential":
                            case "derivative":
                                ret = "ⅆ";
                                break;
                            case "binomial coefficient":
                                val = "";
                                break;
                        }
                    }
                    if (ret.Length == 0)
                        ret = val;
                    break;

                case "mi":
                    if (value == functionName)
                    {
                        ret = Text(value) + "\u2061";
                        break;
                    }
                    if (IsDoubleStruck(intent))
                    {
                        ret = intent;
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
                            if (HasAttr(value, "title"))
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
                            }
                            // an upright letter is text; a math alphanumeric (𝑥) already has its style
                            if (c != "∞" && c != "⋯" && !InRange("Α", c, "Ω") && CodePointAt(c, 0) <= 0xFFFF)
                            {
                                ret = "\"" + c + "\"";
                                break;
                            }
                        }
                        else if (mathstyle != null)
                        {
                            ret = GetMathAlphanumeric(c, mathstyle);
                            break;
                        }
                    }
                    ret = Text(value);
                    break;

                case "mn":
                    ret = Text(value);
                    break;

                case "mtext":
                    // a letter alone in quotes is an upright identifier: ￗ() is text
                    if (Len(Text(value)) == 1 && char.IsLetter(Text(value), 0))
                        ret = "ￗ(" + Text(value) + ")";
                    else
                        ret = "\"" + Text(value).Replace("\"", "\\\"") + "\"";
                    break;

                case "ms":
                {
                    var quoted = (Attr(value, "lquote") ?? "\"") + Text(value) + (Attr(value, "rquote") ?? "\"");
                    ret = "\"" + quoted.Replace("\"", "\\\"") + "\"";
                    break;
                }

                case "mspace":
                {
                    var width = Attr(value, "width");
                    if (!string.IsNullOrEmpty(width))
                    {
                        int i = System.Array.IndexOf(SpaceWidths, width);
                        if (i >= 0)
                            ret = UniSpaces[i];
                    }
                    break;
                }
            }

            var selcode = GetSelectionCodes(value);
            if (ret.Length > 0 || IsLeaf(value))
            {
                if (selcode.Length == 0)
                    return ret;
                if (NeedPreSpace.Contains(Name(value)))
                    selcode += " ";
                return selcode + ret;
            }

            var nodes = Children(value);
            // an n-ary operator and its operand, a row of the operand after it (in a row of their own, or not when it was the
            // only content of its parent); a function name and the function apply sign after it
            int naryOperand = -1;
            int naryStart = 0;
            bool functionMarked = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                bool isHead = IsNaryHead(node) && nodes.Count == 2 && i == 0 && Name(nodes[1]) is "mrow" or "mpadded";
                bool isOperand = i == naryOperand;
                // a space ends the operand of an n-ary operator that has more after it, as in ∫₀¹ 𝑓(𝑥) 𝑑𝑥
                if (i > 0 && Name(node) != "mo" && (Name(nodes[i - 1]) == "mrow" && (Role(nodes[i - 1]) ?? "").StartsWith(":largeop", System.StringComparison.Ordinal) ||
                    i - 1 == naryOperand) && Len(ret) > 0 && Ch(ret, Len(ret) - 1) != " " && Ch(ret, Len(ret) - 1) != "〗")
                    ret += " ";
                // the operand of an enclosure or a phantom (▁𝑥, ⟡𝑥) ends at a space: ▁𝑥 ▁𝑦, not ▁(𝑥▁𝑦)
                if (i > 0 && (Name(nodes[i - 1]) is "menclose" or "mphantom" or "mpadded" or "msqrt" ||
                    Name(nodes[i - 1]) == "mo" && Attr(nodes[i - 1], "intent") == ":text" && (IsNaryHead(node) || Name(node) == "mrow" && Role(node) == ":largeop")) && Len(ret) > 0 && Ch(ret, Len(ret) - 1) != " " && Ch(ret, Len(ret) - 1) != ")")
                    ret += " ";
                // a differential (ⅆ) right after a letter reads with a thin space before it, which a space avoids
                if (i > 0 && Attr(node, "intent") == "ⅆ" && Len(ret) > 0 && Ch(ret, Len(ret) - 1) != " " && Ch(ret, Len(ret) - 1) != "\u2009")
                    ret += " ";
                ret += CheckSpace(i, node, ret);
                string dumped;
                bool differential = false;
                if (isHead)
                {
                    naryStart = ret.Length;
                    naryOperand = i + 1;
                    dumped = NaryOperator(node, nodes[i + 1]);
                }
                else if (isOperand)
                {
                    // a binomial coefficient as an operand is in parentheses: 𝑛⒞𝑘 would take what follows it in
                    plainBinomial = (Attr(node, "intent") ?? "").StartsWith("binomial-coefficient", System.StringComparison.Ordinal) ? node : null;
                    dumped = IsSmashed(node) ? SmashedContent(node) : Dump(node);
                    plainBinomial = null;
                    // the operand of an integral and its differential (∫_0^1 𝑓 ⅆ𝑥): reading takes the differential after it in
                    if (Name(node) == "mrow" && ChildCount(node) == 3 && Attr(Child(node, 1), "intent") == "ⅆ" &&
                        InRange("∫", Text(IsNaryHeadBase(nodes[i - 1])), "∳"))
                    {
                        dumped = " " + Dump(First(node)).Trim(' ') + " " + Dump(Child(node, 1)) + Dump(Last(node));
                        differential = true;
                    }
                }
                else if (i + 1 < nodes.Count && Name(nodes[i + 1]) == "mo" && Text(nodes[i + 1]) == "\u2061" && FunctionName(node) is { } name)
                {
                    // a function name with scripts that is not one UnicodeMath knows (argmax), or whose scripts are not
                    // where they go (lim²_(𝑥→0)): the function apply sign follows the name, as in argmax⁡┬(𝑥∈𝑆) 𝑓(𝑥)
                    functionName = name;
                    dumped = Dump(node);
                    functionName = null;
                    functionMarked = true;
                }
                else if (functionMarked && Name(node) == "mo" && Text(node) == "\u2061")
                {
                    dumped = " ";
                    functionMarked = false;
                }
                else
                {
                    dumped = Dump(node);
                }
                // a comma or a point before digits would make them one number (1,5): a space keeps them apart
                if (i > 0 && Name(nodes[i - 1]) == "mo" && Text(nodes[i - 1]) is "," or "." && Len(dumped) > 0 && IsAsciiDigit(Ch(dumped, 0)) &&
                    Len(ret.TrimEnd(' ')) > 1 && Ch(ret.TrimEnd(' '), Len(ret.TrimEnd(' ')) - 2) != "∞")
                    dumped = " " + dumped;
                if (isOperand && Name(node) == "mrow" && NeedBeginEnd(node) && !differential && !IsFenceDelimited(node) && !(Ch(dumped.Trim(' '), 0) == "〖"))
                    dumped = "〖" + dumped.Trim(' ') + "〗";
                // the operand of an n-ary operator with digit scripts (∑₀¹) is apart from them, as in ∑₀¹ 𝑥
                // the operand of an n-ary operator after ▒, as Word writes it: without it, ∫𝑎 is not an integral of 𝑎
                // the operand of an n-ary operator after ▒ when it has no limits (∫▒𝑎: ∫𝑎 is not an integral of 𝑎), else
                // after a space
                if (isOperand && !differential)
                {
                    ret = ret.TrimEnd(' ');
                    dumped = (Name(nodes[i - 1]) == "mo" ? "▒" : " ") + dumped.TrimStart(' ');
                }
                // an n-ary operation followed by a character of text (\/), which its operand would take in, is grouped
                if (i < nodes.Count - 1 && Name(node) == "mrow" && (Role(node) ?? "").StartsWith(":largeop", System.StringComparison.Ordinal) &&
                    Name(nodes[i + 1]) == "mo" && Attr(nodes[i + 1], "intent") == ":text")
                    dumped = "〖" + dumped.Trim(' ') + "〗";
                ret += dumped;
                if (isOperand && i + 1 < nodes.Count && Name(nodes[i + 1]) == "mo" && Attr(nodes[i + 1], "intent") == ":text")
                    ret = ret.Substring(0, naryStart) + "〖" + ret.Substring(naryStart).Trim(' ') + "〗";
            }

            if (selcode.Length > 0)
            {
                if (Name(value) == "math" && Len(selcode) == 4 && Ch(selcode, 2) == ChildCount(value).ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    ret += " Ⓐ()";
                }
                else
                {
                    if (cNode > 1)
                        selcode += " ";
                    ret = selcode + ret;
                }
            }

            var mrowIntent = Name(value) == "mrow" ? Role(value) ?? "" : "";
            if (Name(value) == "mrow" && HasAttr(value, "href"))
                return "\\href(" + Attr(value, "href") + "&" + ret.Trim(' ') + ")";
            if (mrowIntent.Length > 0)
            {
                if (mrowIntent == ":piecewise")
                    return "Ⓒ" + Sub(ret, 2);
                if (mrowIntent == ":fenced" && ChildCount(value) > 0 && Text(Last(value)).Length == 0)
                    return Text(First(value)).Length == 0 ? "〖" + ret + "〗" : (IsOpening(Text(First(value))) ? "" : "├") + ret + "┤";
                if (mrowIntent == ":fenced" && ChildCount(value) > 1 && Name(First(value)) == "mo" && Name(Last(value)) == "mo")
                {
                    // a fence that does not open or close (]𝑎[, or none) is marked with ├ and ┤, and so is a sized one (├1()
                    var close = Text(Last(value));
                    var open = Text(First(value));
                    var closeSize = FenceSize(Last(value));
                    var openSize = FenceSize(First(value));
                    // an interval written with brackets the other way (]𝑎,𝑏[), which reading takes for one
                    bool interval = open is "[" or "]" && close is "[" or "]" && ChildCount(value) == 5 && Text(Child(value, 2)) == "," &&
                        closeSize.Length == 0 && openSize.Length == 0;
                    // a bar closes only after an opening bar: ├𝑎┤| otherwise
                    if (!interval && (!IsClosing(close) || (close == "|" || close == "‖") && open != close || closeSize.Length > 0) && Len(ret) >= Len(close))
                        ret = Sub(ret, 0, Len(ret) - Len(close)) + "┤" + closeSize + (close == "\u200B" ? "" : close);
                    if (!interval && (!IsOpening(open) || openSize.Length > 0))
                        ret = "├" + openSize + (open == "\u200B" ? Sub(ret, Len(open)) : ret);
                }
                if (mrowIntent.StartsWith("cardinality", System.StringComparison.Ordinal))
                {
                    ret = Sub(ret, 1, Len(ret) - 1);
                    return NeedParens(ret) ? "ⓒ(" + ret + ")" : "ⓒ" + ret + " ";
                }
                if (mrowIntent.StartsWith("binomial-coefficient", System.StringComparison.Ordinal) && value != plainBinomial ||
                    mrowIntent.EndsWith("matrix", System.StringComparison.Ordinal) || mrowIntent.EndsWith("determinant", System.StringComparison.Ordinal))
                {
                    var chars = Characters(ret);
                    int last = chars.Count - 1;
                    if (chars.Count > 0 && chars[0] == "|")
                        return Sub(ret, 1, last);
                    if (chars.Count > 0 && chars[0] == "(" && chars[last] == ")")
                        return Sub(ret, 1, last);
                    // the brackets of the other matrices, which UnicodeMathML keeps around the matrix
                    if (chars.Count > 1 && (chars[0] == "[" && chars[last] == "]" || chars[0] == "‖" && chars[last] == "‖" || chars[0] == "{" && chars[last] == "}"))
                        return Sub(ret, 1, last);
                    bool binomial = false;
                    int cParen = 0;
                    int iOpen = -1;
                    for (int i = 0; i < chars.Count; i++)
                    {
                        switch (chars[i])
                        {
                            case "(":
                                cParen++;
                                iOpen = i;
                                break;
                            case ")":
                                cParen--;
                                if (cParen == 0 && binomial && iOpen >= 0)
                                    return Sub(ret, 0, iOpen) + Sub(ret, iOpen + 1, i) + Sub(ret, i + 1);
                                iOpen = -1;
                                break;
                            case "⒞":
                                binomial = true;
                                break;
                        }
                    }
                    return ret;
                }
                if (mrowIntent == ":function" && Previous(value) != null && First(value) != null && Name(First(value)) == "mi" &&
                    CodePointAt(Text(First(value)), 0) < 0x2100 && Name(Previous(value)) == "mi")
                    return " " + ret;
            }
            if (First(value) != null && Name(First(value)) == "mo" && IsOpenDelimiter(Text(First(value))) && Attr(First(value), "intent") != ":text")
            {
                if (Name(Last(value)) != "mo" || Text(Last(value)).Length == 0)
                    ret += "┤";
            }
            // an absolute value of bars (|𝑎|𝑏|𝑐|) or of parentheses keeps its content in parentheses, which only group
            if (Name(value) == "mrow" && cNode > 2 && Name(First(value)) == "mo" && Name(Last(value)) == "mo" && Text(First(value)) is "|" or "‖" &&
                Text(Last(value)) == Text(First(value)) && Ch(ret, 0) == Text(First(value)) && Ch(ret, Len(ret) - 1) == Text(Last(value)))
            {
                var inner = Sub(ret, 1, Len(ret) - 1);
                if (inner.Any(c => c == '|' || c == '‖') || Ch(inner, 0) == "(" && Ch(inner, Len(inner) - 1) == ")")
                    ret = Text(First(value)) + "(" + inner + ")" + Text(Last(value));
            }
            if (Name(value) == "mrow" && cNode > 1 && mrowIntent.Length == 0 && Name(parent) is "msub" or "msup" or "msubsup" or "mmultiscripts" && value == First(parent) &&
                !(Ch(ret, 0) == "〖" && Ch(ret, Len(ret) - 1) == "〗"))
                ret = "〖" + ret + "〗";
            else if (cNode > 1 && Name(value) != "math" && !noAddParens && mrowIntent != ":fenced" && IsMathMLObject(parent, true) &&
                (NeedParens(ret) || Name(parent) is "msub" or "msup" or "msubsup" && value != First(parent) && Characters(ret.TrimEnd(' ')).Any(c => c is "^" or "_")))
            {
                // the base of a script keeps parentheses, so a group without them is in invisible brackets
                ret = Name(parent) is "msub" or "msup" or "msubsup" && value == First(parent) ? "〖" + ret + "〗" : "(" + ret + ")";
            }
            return ret;
        }

        /// <summary>The size of a fence of UnicodeMath (├1( to ├4(), from the minimum size 1.25em to the power of it.</summary>
        private static string FenceSize(XElement fence)
        {
            var size = Attr(fence, "minsize");
            if (size == null || !size.EndsWith("em", System.StringComparison.Ordinal) ||
                !double.TryParse(size.Substring(0, size.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var em) || em <= 1)
                return "";
            var n = (int)System.Math.Round(System.Math.Log(em) / System.Math.Log(1.25));
            return n >= 1 && n <= 4 ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        }

        /// <summary>A token element, whose text the switch above writes: an empty one gives an empty string.</summary>
        private static bool IsLeaf(XElement value)
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
