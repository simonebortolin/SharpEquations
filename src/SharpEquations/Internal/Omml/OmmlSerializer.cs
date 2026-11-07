namespace SharpEquations.Internal.Omml
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using SharpEquations.Internal.LaTeX;
    using SharpEquations.Internal.Text;
    using SharpEquations.Internal.UnicodeMath;
    using static SharpEquations.Internal.Omml.Omml;

    /// <summary>
    /// Writes MathML as OMML with the mml2omml.xsl stylesheet of the TEI Stylesheets (BSD-2-Clause, see NOTICE), with
    /// the fixes its input and output need. Content MathML is not supported.
    /// </summary>
    internal static class OmmlSerializer
    {
        // The OMML properties whose empty value means "none" instead of the default.
        private static readonly HashSet<string> EmptyDelimiters = new HashSet<string>(StringComparer.Ordinal) { "begChr", "endChr", "sepChr" };

        /// <summary>The spacing accents of MathML (as omml2mml.xsl writes them) and the combining ones of OMML.</summary>
        private static readonly Dictionary<string, string> CombiningAccents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["`"] = "\u0300", ["´"] = "\u0301", ["^"] = "\u0302", ["~"] = "\u0303", ["¯"] = "\u0304", ["˘"] = "\u0306", ["˙"] = "\u0307",
            ["¨"] = "\u0308", ["˚"] = "\u030A", ["ˇ"] = "\u030C", ["←"] = "\u20D6", ["→"] = "\u20D7", ["↔"] = "\u20E1", ["↼"] = "\u20D0",
            ["⇀"] = "\u20D1",
        };

        /// <summary>
        /// Converts a MathML <c>math</c> element to OMML: an <c>m:oMathPara</c> for a display equation
        /// (<c>display="block"</c>), otherwise an <c>m:oMath</c>. MathML without a namespace is accepted.
        /// </summary>
        public static XElement Serialize(XElement mathML)
        {
            if (mathML == null)
                throw new ArgumentNullException(nameof(mathML));
            var math = PrepareMathML(mathML);
            if (math.Name != Mml + "math")
                throw new ArgumentException("Expected a MathML math element, not " + mathML.Name + ".", nameof(mathML));

            var alignedLines = math.Elements().Count() == 1 && (string)math.Elements().First().Attribute("class") == AlignedLinesClass;
            var omml = EquationStylesheets.Transform("mml2omml.xsl", SingleRow(math));
            // an empty delimiter means none (a brace without its closing one), as Word writes it; without the
            // value the default parenthesis or bar would come back
            foreach (var value in omml.Descendants().Attributes(M + "val").Where(a => a.Value.Length == 0 && !EmptyDelimiters.Contains(a.Parent.Name.LocalName)).ToList())
                value.Remove();
            foreach (var nary in omml.DescendantsAndSelf(M + "nary"))
            {
                HideEmptyLimit(nary, "sub");
                HideEmptyLimit(nary, "sup");
            }
            // the stylesheet writes the WordprocessingML properties first, the schema wants them after m:rPr
            foreach (var properties in omml.Descendants(M + "rPr").Where(p => p.Parent.Name == M + "r" && p.ElementsBeforeSelf().Any()).ToList())
            {
                var run = properties.Parent;
                properties.Remove();
                run.AddFirst(properties);
            }
            // mml2omml writes the default separator of an mfenced, ",", also when the delimiter has one argument
            foreach (var separator in omml.Descendants(M + "dPr").Where(p => p.Parent.Elements(M + "e").Count() == 1).Elements(M + "sepChr").ToList())
                separator.Remove();
            // the schema wants the prescripts of m:sPre before its base, the stylesheet writes the base first
            foreach (var prescripts in omml.DescendantsAndSelf(M + "sPre"))
            {
                var ordered = prescripts.Elements(M + "sPrePr").Concat(prescripts.Elements(M + "sub")).Concat(prescripts.Elements(M + "sup"))
                    .Concat(prescripts.Elements(M + "e")).ToList();
                prescripts.ReplaceNodes(ordered.Select(e => new XElement(e)));
            }
            // OMML accents are combining characters, the inverse of what omml2mml.xsl does
            foreach (var accent in omml.Descendants(M + "accPr").Elements(M + "chr").Attributes(M + "val"))
            {
                if (CombiningAccents.TryGetValue(accent.Value, out var combining))
                    accent.Value = combining;
            }
            omml = Canonical(omml);

            var display = (string)math.Attribute("display") == "block" || (string)math.Attribute("mode") == "display";
            if (alignedLines && omml.Elements().Count() == 1 && omml.Elements().First().Name == M + "eqArr")
                return Canonical(new XElement(M + "oMathPara", LinesOf(omml.Elements().First())));
            return display ? Canonical(new XElement(M + "oMathPara", omml)) : omml;
        }

        /// <summary>
        /// The rows of an equation array made from aligned lines (see AlignedLines) as equations of their own:
        /// the "&amp;" that starts the text of a run becomes the m:aln of that run.
        /// </summary>
        private static IEnumerable<XElement> LinesOf(XElement equationArray)
        {
            foreach (var row in equationArray.Elements(M + "e"))
            {
                var line = new XElement(M + "oMath", row.Nodes());
                var aligned = line.Elements(M + "r").FirstOrDefault(r => StartsWithAlignment((string)r.Element(M + "t")));
                if (aligned != null)
                {
                    var text = aligned.Element(M + "t");
                    text.Value = text.Value.Substring(1);
                    // the stylesheet writes the mark as a run of its own: the run after it is the aligned one
                    if (text.Value.Length == 0 && aligned.ElementsAfterSelf().FirstOrDefault()?.Name == M + "r")
                    {
                        var next = (XElement)aligned.ElementsAfterSelf().First();
                        aligned.Remove();
                        aligned = next;
                    }
                    var properties = aligned.Element(M + "rPr");
                    if (properties == null)
                    {
                        properties = new XElement(M + "rPr");
                        aligned.AddFirst(properties);
                    }
                    properties.Add(new XElement(M + "aln"));
                }
                yield return line;
            }
        }

        private static bool StartsWithAlignment(string text)
        {
            return !string.IsNullOrEmpty(text) && text[0] == '&';
        }

        /// <summary>
        /// An empty limit of an m:nary has to be hidden, or Word shows a placeholder box for it.
        /// </summary>
        private static void HideEmptyLimit(XElement nary, string limit)
        {
            if (nary.Element(M + limit)?.HasElements == false)
                nary.Element(M + "naryPr")?.Element(M + limit + "Hide")?.SetAttributeValue(M + "val", "on");
        }

        /// <summary>
        /// Mathematical alphanumerics (𝑥, 𝐲, ℒ) as Word writes them: the plain character, with the style as a
        /// mathvariant that the stylesheet writes as m:sty or m:scr (an italic letter is the default, without one).
        /// </summary>
        private static void Unstyle(XElement token)
        {
            var styles = new List<string>();
            var plain = new StringBuilder();
            foreach (var codePoint in CodePoints.Of(token.Value))
            {
                if (!LaTeXSymbols.TryGetPlain(codePoint.ToString(), out var style, out var ch))
                    return;
                styles.Add(style);
                plain.Append(ch);
            }
            if (styles.Count == 0 || styles.Distinct().Count() > 1)
                return;
            var variant = UnicodeMathSerializer.MathVariants.First(v => v.Value == styles[0]).Key;
            token.Value = plain.ToString();
            if (variant != "italic" || styles.Count > 1 || token.Name == Mml + "mn")
                token.SetAttributeValue("mathvariant", variant);
        }

        private static XElement PrepareMathML(XElement mathML)
        {
            var copy = new XElement(mathML);
            if (copy.Name.Namespace == XNamespace.None)
            {
                foreach (var element in copy.DescendantsAndSelf().Where(e => e.Name.Namespace == XNamespace.None).ToList())
                    element.Name = Mml + element.Name.LocalName;
            }
            foreach (var semantics in copy.Descendants(Mml + "semantics").Reverse().ToList())
            {
                var presentation = semantics.Elements().FirstOrDefault(e => e.Name != Mml + "annotation" && e.Name != Mml + "annotation-xml");
                if (presentation != null)
                    semantics.ReplaceWith(presentation);
                else
                    semantics.Remove();
            }
            foreach (var token in copy.Descendants().Where(e => (e.Name == Mml + "mi" || e.Name == Mml + "mn") && !e.HasElements && e.Attribute("mathvariant") == null))
                Unstyle(token);
            // an underbar (UnicodeMath ▁, \underline) is an m:bar, as Word writes it, which the stylesheet makes of munder ̲
            foreach (var underbar in copy.Descendants(Mml + "menclose").Where(e => (string)e.Attribute("notation") == "bottom").ToList())
                underbar.ReplaceWith(new XElement(Mml + "munder", new XElement(Mml + "mrow", underbar.Nodes()), new XElement(Mml + "mo", "\u0332")));
            // a comma or a point between numbers in a run of its own: in one run with them, it would join them (3,4)
            foreach (var mo in copy.Descendants(Mml + "mo").Where(e => e.Value is "," or "." && e.ElementsBeforeSelf().LastOrDefault()?.Name == Mml + "mn" &&
                e.ElementsAfterSelf().FirstOrDefault()?.Name == Mml + "mn").ToList())
                mo.ReplaceWith(new XElement(Mml + "mrow", new XElement(mo)));
            // the inner rows first: an outer one copies its content into the mfenced
            foreach (var row in copy.Descendants(Mml + "mrow").Reverse().ToList())
                FenceRow(row);
            foreach (var script in copy.Descendants().Where(IsHorizontalBracket).ToList())
            {
                script.SetAttributeValue(script.Name == Mml + "munder" ? "accentunder" : "accent", "false");
                var basE = script.Elements().First();
                if (basE.Name != Mml + "mrow")
                    basE.ReplaceWith(new XElement(Mml + "mrow", basE));
            }
            // Numbered equations: Word writes the number after a "#" in the row
            foreach (var labeled in copy.Descendants(Mml + "mlabeledtr").ToList())
            {
                var cells = labeled.Elements(Mml + "mtd").ToList();
                if (cells.Count < 2)
                {
                    labeled.Name = Mml + "mtr";
                    continue;
                }
                var aligned = labeled.Parent.Descendants(Mml + "maligngroup").Any();
                labeled.ReplaceWith(new XElement(Mml + "mtr", labeled.Attributes(),
                    new XElement(Mml + "mtd", aligned ? null : new XElement(Mml + "mrow", new XElement(Mml + "maligngroup")),
                        cells.Skip(1).SelectMany(c => c.Nodes()), new XElement(Mml + "mo", "#"), Label(cells[0]))));
            }
            foreach (var limits in copy.Descendants().Where(IsNaryOperator).ToList())
            {
                var argument = limits.ElementsAfterSelf().FirstOrDefault();
                if (argument != null && argument.Name != Mml + "mrow" && argument.Name != Mml + "mstyle")
                    argument.ReplaceWith(new XElement(Mml + "mrow", argument));
            }
            // An n-ary operator without limits is a plain mo followed by its operand. Empty limits make it an m:nary again.
            foreach (var op in copy.Descendants(Mml + "mo").Where(IsNaryWithoutLimits).ToList())
                op.ReplaceWith(new XElement(Mml + "msubsup", new XElement(op), new XElement(Mml + "mrow"), new XElement(Mml + "mrow")));
            return copy;
        }

        private const string OpeningFences = "([{⟨〈⌈⌊❲⟦⟪⟬⟮⦃⦅⦇⦉⦋⦍⦏⦑⦓⦕⦗⧘⧚⧼";
        private const string ClosingFences = ")]}⟩〉⌉⌋❳⟧⟫⟭⟯⦄⦆⦈⦊⦌⦎⦐⦒⦔⦖⦘⧙⧛⧽";

        private const string SymmetricFences = "|‖";

        private const string HorizontalBrackets = "⏞⏟⎴⎵⏜⏝⏠⏡";

        private static string FenceText(XElement mo) => mo.Value.Replace("\u200B", "").Trim();

        /// <summary>Whether the text is a single code point of the set.</summary>
        private static bool IsOneOf(string text, string set) => CodePoints.IsSingle(text, out var codePoint) && CodePoints.Of(set).Contains(codePoint);

        /// <summary>
        /// An mrow that opens and closes with fences, such as (a+b) or |x| written as mo elements, as an mfenced: mml2omml
        /// makes an m:d (a delimiter that grows with its content) only of an mfenced. Separators (│, or | between ⟨ and ⟩)
        /// split the content into the arguments of the m:d.
        /// </summary>
        private static void FenceRow(XElement row)
        {
            var children = row.Elements().ToList();
            if (children.Count < 2 || children[0].Name != Mml + "mo" || children[children.Count - 1].Name != Mml + "mo")
                return;
            var open = FenceText(children[0]);
            var close = FenceText(children[children.Count - 1]);
            if (open.Length == 0 && close.Length == 0)
                return;
            bool symmetric = IsOneOf(open, SymmetricFences);
            if (open.Length > 0 && !IsOneOf(open, OpeningFences) && !symmetric)
                return;
            if (close.Length > 0 && !IsOneOf(close, ClosingFences) && !IsOneOf(close, SymmetricFences))
                return;
            if (symmetric && close != open)
                return;

            var inner = children.Skip(1).Take(children.Count - 2).ToList();
            if (inner.Count == 1 && inner[0].Name == Mml + "mrow" && !inner[0].HasAttributes)
                inner = inner[0].Elements().ToList();
            int depth = 0;
            foreach (var mo in inner.Where(e => e.Name == Mml + "mo"))
            {
                var text = FenceText(mo);
                if (symmetric && text == open)
                    return;

                if (IsOneOf(text, OpeningFences))
                    depth++;
                else if (IsOneOf(text, ClosingFences) && --depth < 0)
                    return;
            }
            if (depth != 0)
                return;

            var separators = "│∣" + (open == "⟨" ? "|" : "");
            var separator = inner.Where(e => e.Name == Mml + "mo" && (string)e.Attribute("separator") == "true").Select(FenceText).FirstOrDefault()
                ?? inner.Where(e => e.Name == Mml + "mo").Select(FenceText).FirstOrDefault(t => IsOneOf(t, separators));
            var arguments = new List<XElement>();
            var current = new List<XElement>();
            foreach (var child in inner)
            {
                if (separator != null && child.Name == Mml + "mo" && FenceText(child) == separator)
                {
                    arguments.Add(new XElement(Mml + "mrow", current));
                    current = new List<XElement>();
                }
                else
                {
                    current.Add(child);
                }
            }
            arguments.Add(new XElement(Mml + "mrow", current));
            row.ReplaceWith(new XElement(Mml + "mfenced", new XAttribute("open", open), new XAttribute("close", close),
                separator != null ? new XAttribute("separators", separator) : null, arguments));
        }

        /// <summary>A brace or bracket over or under its base (⏞, ⏟...), which Word writes as an m:groupChr.</summary>
        private static bool IsHorizontalBracket(XElement element)
        {
            if (element.Name != Mml + "munder" && element.Name != Mml + "mover")
                return false;
            var children = element.Elements().ToList();
            if (children.Count != 2 || children[1].Name != Mml + "mo" || element.Attribute("accent") != null || element.Attribute("accentunder") != null)
                return false;
            var text = children[1].Value.Trim();
            return IsOneOf(text, HorizontalBrackets);
        }

        /// <summary>
        /// The label of a numbered equation; a number in parentheses written as text ("(1)") becomes a delimiter, as
        /// Word writes it: LibreOffice shows parentheses in a run of text as "\(".
        /// </summary>
        private static IEnumerable<XNode> Label(XElement cell)
        {
            var text = CodePoints.Of(cell.Value.Trim()).ToList();
            if (cell.Descendants().Count(e => !e.HasElements) == 1 && text.Count > 2 &&
                text[0].Value == '(' && text[text.Count - 1].Value == ')')
            {
                var inner = text.Skip(1).Take(text.Count - 2).ToList();
                var token = inner.All(c => c.Value >= '0' && c.Value <= '9') ? "mn" : "mtext";
                return new[] { new XElement(Mml + "mfenced", new XAttribute("open", "("), new XAttribute("close", ")"),
                    new XElement(Mml + token, string.Concat(inner.Select(c => c.ToString())))) };
            }
            return cell.Nodes();
        }

        private static bool IsNaryWithoutLimits(XElement op)
        {
            var text = op.Value.Trim();
            if (!IsOneOf(text, NaryOperators) || IsOneOf(text, BinaryOperators) || !IsRow(op.Parent))
                return false;
            var operand = op.ElementsAfterSelf().FirstOrDefault();
            return operand != null && (operand.Name == Mml + "mrow" || operand.Name == Mml + "mstyle");
        }

        /// <summary>
        /// Whether the element is an n-ary operator with limits (a sum, an integral, ...). mml2omml only
        /// takes the operand of an m:nary from an mrow that follows the operator, as Word writes it.
        /// </summary>
        private static bool IsNaryOperator(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "munder":
                case "mover":
                case "munderover":
                case "msub":
                case "msup":
                case "msubsup":
                    var op = element.Elements().FirstOrDefault();
                    if (element.Name.Namespace != Mml || op == null || op.Name != Mml + "mo")
                        return false;
                    var text = op.Value.Trim();
                    return IsOneOf(text, NaryOperators);
                default:
                    return false;
            }
        }

        private const string NaryOperators = "∫∬∭∮∯∰∲∳∱∩∪∏∐∑⋀⋁⋂⋃⨀⨁⨂⨃⨄⨅⨆";

        // Also n-ary with limits, but between two operands when they have none.
        private const string BinaryOperators = "∩∪";
    }
}
