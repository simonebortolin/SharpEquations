namespace SharpEquations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;

    /// <summary>
    /// Converts equations between Office Math Markup Language (OMML) and presentation MathML.
    /// </summary>
    /// <remarks>
    /// The conversion is done by the omml2mml.xsl and mml2omml.xsl stylesheets of the TEI Stylesheets
    /// (BSD-2-Clause, see NOTICE), run with XslCompiledTransform. The conversion is lossy in both
    /// directions: run formatting inside OMML (fonts, colours) is not carried to MathML, and content
    /// MathML is not supported.
    /// </remarks>
    public static class OmmlConverter
    {
        private static readonly XNamespace M = MathMLNamespaces.Omml;
        private static readonly XNamespace W = MathMLNamespaces.WordprocessingML;
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        // The OMML properties whose empty value means "none" instead of the default.
        private static readonly HashSet<string> EmptyDelimiters = new HashSet<string>(StringComparer.Ordinal) { "begChr", "endChr", "sepChr" };

        // The OMML properties of type ST_OnOff. The stylesheets predate ISO 29500 and only understand "on" and "off".
        private static readonly HashSet<string> OnOffProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "aln", "alnScr", "degHide", "diff", "dispDef", "grow", "hideBot", "hideLeft", "hideRight", "hideTop",
            "lit", "maxDist", "noBreak", "nor", "objDist", "opEmu", "plcHide", "show", "smallFrac", "strikeBLTR",
            "strikeH", "strikeTLBR", "strikeV", "subHide", "supHide", "transp", "wrapRight", "zeroAsc", "zeroDesc", "zeroWid",
        };

        /// <summary>
        /// Converts an <c>m:oMath</c> or <c>m:oMathPara</c> element to a MathML <c>math</c> element.
        /// A display equation (an <c>m:oMathPara</c>, or an <c>m:oMath</c> inside one) gets <c>display="block"</c>.
        /// </summary>
        public static XElement ToMathML(XElement omml)
        {
            if (omml == null)
                throw new ArgumentNullException(nameof(omml));

            List<XElement> equations;
            bool display;
            if (omml.Name == M + "oMathPara")
            {
                equations = omml.Elements(M + "oMath").ToList();
                display = true;
            }
            else if (omml.Name == M + "oMath")
            {
                equations = new List<XElement> { omml };
                display = omml.Parent != null && omml.Parent.Name == M + "oMathPara";
            }
            else
            {
                throw new ArgumentException("Expected an m:oMath or m:oMathPara element, not " + omml.Name + ".", nameof(omml));
            }

            var aligned = equations.Count > 1 && equations.Any(e => e.Descendants(M + "aln").Any(IsOn));
            if (aligned)
                equations = new List<XElement> { AlignedLines(equations) };

            var converted = equations.Select(e => SingleRow(EquationStylesheets.Transform("omml2mml.xsl", PrepareOmml(e)))).ToList();
            XElement math;
            if (converted.Count == 1)
            {
                math = converted[0];
                if (aligned)
                    math.Element(Mml + "mtable")?.SetAttributeValue("class", AlignedLinesClass);
            }
            else
            {
                // the alignment point makes it an equation array again: mml2omml takes other tables for matrices;
                // the class tells FromMathML to write the lines back as equations of their own
                math = new XElement(Mml + "math",
                    new XElement(Mml + "mtable", new XAttribute("class", AlignedLinesClass),
                        converted.Select(c => new XElement(Mml + "mtr", new XElement(Mml + "mtd", new XElement(Mml + "mrow", new XElement(Mml + "maligngroup"), c.Nodes()))))));
            }
            // Spaces are text: an identifier made of spaces is dropped or misread by consumers (LibreOffice
            // reads it as an empty style), and a plain space token is collapsed.
            foreach (var token in math.Descendants().Where(e => !e.HasElements && e.Value.Length > 0 && e.Value.Trim().Length == 0).ToList())
            {
                token.Value = new string('\u00A0', token.Value.Length);
                if (token.Name == Mml + "mi" || token.Name == Mml + "mn" || token.Name == Mml + "mo")
                {
                    token.Name = Mml + "mtext";
                    token.Attributes().Where(a => a.Name.LocalName != "mathvariant").Remove();
                }
            }
            // MathML also trims tokens and collapses their spaces: " ove " would touch the formula around it.
            foreach (var text in math.Descendants(Mml + "mtext").Where(e => !e.HasElements))
                text.Value = CollapsibleSpaces.Replace(text.Value, m => new string('\u00A0', m.Length));
            JoinNumbers(math);
            LabelEquationNumbers(math);
            if (display)
                math.SetAttributeValue("display", "block");
            return math;
        }

        // The class of the table that holds the lines of a display equation aligned with m:aln.
        private const string AlignedLinesClass = "aligned-lines";

        private static readonly Regex CollapsibleSpaces = new Regex("^ +| +$| {2,}", RegexOptions.CultureInvariant);

        /// <summary>
        /// The lines of a display equation aligned with m:aln, as an equation array: the alignment mark of
        /// an array is an "&amp;" before the aligned text.
        /// </summary>
        private static XElement AlignedLines(List<XElement> lines)
        {
            var rows = lines.Select(line =>
            {
                var row = new XElement(M + "e", line.Nodes());
                var aligned = row.Elements(M + "r").FirstOrDefault(r => r.Descendants(M + "aln").Any(IsOn))?.Element(M + "t");
                if (aligned != null)
                    aligned.Value = "&" + aligned.Value;
                row.Descendants(M + "aln").Remove();
                return row;
            });
            return new XElement(M + "oMath", new XElement(M + "eqArr", rows));
        }

        private static bool IsOn(XElement property)
        {
            var value = property.Attribute(M + "val");
            return value == null || IsTrue(value.Value);
        }

        /// <summary>
        /// Word splits runs anywhere, so "0.09" or "20000" can arrive as several number tokens. They are one number.
        /// </summary>
        private static void JoinNumbers(XElement math)
        {
            foreach (var row in math.DescendantsAndSelf().Where(IsRow).ToList())
            {
                var items = row.Elements().ToList();
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i].Name != Mml + "mn" || items[i].HasAttributes)
                        continue;
                    var number = items[i];
                    while (i + 1 < items.Count)
                    {
                        var next = items[i + 1];
                        var point = next.Name == Mml + "mo" && next.Value == ".";
                        var digits = point && i + 2 < items.Count ? items[i + 2] : next;
                        if (digits.Name != Mml + "mn" || digits.HasAttributes)
                            break;
                        number.Value += (point ? "." : "") + digits.Value;
                        digits.Remove();
                        if (point)
                            next.Remove();
                        items.RemoveRange(i + 1, point ? 2 : 1);
                    }
                }
            }
        }

        private static bool IsRow(XElement element)
        {
            if (element.Name.Namespace != Mml)
                return false;
            switch (element.Name.LocalName)
            {
                case "math":
                case "mrow":
                case "mtd":
                case "mstyle":
                case "msqrt":
                case "mpadded":
                case "mphantom":
                case "menclose":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Word numbers an equation by writing "#" and the number after it, as "E=mc²#(1)" (in an equation
        /// array, one per row): such rows become MathML labeled rows, the label in the first cell.
        /// </summary>
        private static void LabelEquationNumbers(XElement math)
        {
            foreach (var table in math.Descendants(Mml + "mtable").Where(t => t.Parent == math || t.Descendants(Mml + "maligngroup").Any()).ToList())
            {
                foreach (var row in table.Elements(Mml + "mtr").ToList())
                {
                    var cells = row.Elements(Mml + "mtd").ToList();
                    var numbered = cells.Count == 1 ? SplitEquationNumber(cells[0]) : null;
                    if (numbered != null)
                    {
                        row.ReplaceWith(new XElement(Mml + "mlabeledtr", row.Attributes(),
                            new XElement(Mml + "mtd", numbered.Value.Label),
                            new XElement(Mml + "mtd", cells[0].Attributes(), numbered.Value.Equation)));
                    }
                }
            }
            if (math.Elements(Mml + "mtable").Any())
                return;
            var single = SplitEquationNumber(math);
            if (single != null)
            {
                math.ReplaceNodes(new XElement(Mml + "mtable",
                    new XElement(Mml + "mlabeledtr",
                        new XElement(Mml + "mtd", single.Value.Label),
                        new XElement(Mml + "mtd", single.Value.Equation))));
            }
        }

        /// <summary>
        /// The equation and its number when the row of the element has a "#" operator followed by a number.
        /// </summary>
        private static (XElement Equation, XElement Label)? SplitEquationNumber(XElement element)
        {
            var row = element;
            while (row.Elements().Count() == 1 && row.Elements().Single().Name == Mml + "mrow")
                row = row.Elements().Single();
            var items = row.Elements().ToList();
            var hash = items.FindLastIndex(e => e.Name == Mml + "mo" && e.Value.Trim() == "#");
            if (hash < 0)
                return null;
            bool IsSpace(XElement e) => e.Name == Mml + "mtext" && e.Value.Trim().Trim('\u00A0').Length == 0;
            var equation = items.Take(hash).ToList();
            var label = items.Skip(hash + 1).ToList();
            while (equation.Count > 0 && IsSpace(equation[equation.Count - 1]))
                equation.RemoveAt(equation.Count - 1);
            while (label.Count > 0 && IsSpace(label[0]))
                label.RemoveAt(0);
            if (label.Count == 0)
                return null;
            return (new XElement(Mml + "mrow", equation.Select(e => new XElement(e))), new XElement(Mml + "mrow", label.Select(e => new XElement(e))));
        }

        /// <summary>
        /// Converts a MathML <c>math</c> element to OMML: an <c>m:oMathPara</c> for a display equation
        /// (<c>display="block"</c>), otherwise an <c>m:oMath</c>. MathML without a namespace is accepted.
        /// </summary>
        public static XElement FromMathML(XElement mathML)
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
        /// Groups the children of the math element in one mrow: some consumers, LibreOffice among them,
        /// lay out each child of math on a line of its own.
        /// </summary>
        private static XElement SingleRow(XElement math)
        {
            if (math.Elements().Count() > 1)
            {
                var row = new XElement(Mml + "mrow", math.Nodes());
                math.ReplaceNodes(row);
            }
            return math;
        }

        private static XElement PrepareOmml(XElement oMath)
        {
            var copy = new XElement(oMath);
            foreach (var property in copy.Descendants().Where(e => e.Name.Namespace == M && OnOffProperties.Contains(e.Name.LocalName)))
            {
                var value = property.Attribute(M + "val");
                property.SetAttributeValue(M + "val", value == null || IsTrue(value.Value) ? "on" : "off");
            }
            return copy;
        }

        private static bool IsTrue(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "0":
                case "off":
                case "false":
                    return false;
                default:
                    return true;
            }
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
                        cells.Skip(1).SelectMany(c => c.Nodes()), new XElement(Mml + "mo", "#"), cells[0].Nodes())));
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

        private static bool IsNaryWithoutLimits(XElement op)
        {
            var text = op.Value.Trim();
            if (text.Length != 1 || !NaryOperators.Contains(text) || BinaryOperators.Contains(text) || !IsRow(op.Parent))
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
                    return text.Length == 1 && NaryOperators.Contains(text);
                default:
                    return false;
            }
        }

        private const string NaryOperators = "∫∬∭∮∯∰∲∳∱∩∪∏∐∑⋀⋁⋂⋃⨀⨁⨂⨃⨄⨅⨆";

        // Also n-ary with limits, but between two operands when they have none.
        private const string BinaryOperators = "∩∪";

        /// <summary>
        /// Rewrites the element with the m: and w: prefixes declared once on the root, as the OMML parsers expect.
        /// </summary>
        private static XElement Canonical(XElement element)
        {
            var copy = new XElement(element);
            foreach (var e in copy.DescendantsAndSelf())
                e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            copy.Add(new XAttribute(XNamespace.Xmlns + "m", M.NamespaceName), new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName));
            return copy;
        }
    }
}
