namespace SharpEquations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

            var converted = equations.Select(e => SingleRow(EquationStylesheets.Transform("omml2mml.xsl", PrepareOmml(e)))).ToList();
            XElement math;
            if (converted.Count == 1)
            {
                math = converted[0];
            }
            else
            {
                math = new XElement(Mml + "math",
                    new XElement(Mml + "mtable",
                        converted.Select(c => new XElement(Mml + "mtr", new XElement(Mml + "mtd", c.Nodes())))));
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
            LabelEquationNumbers(math);
            if (display)
                math.SetAttributeValue("display", "block");
            return math;
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

            var omml = EquationStylesheets.Transform("mml2omml.xsl", SingleRow(math));
            foreach (var value in omml.Descendants().Attributes(M + "val").Where(a => a.Value.Length == 0).ToList())
                value.Remove();
            omml = Canonical(omml);

            var display = (string)math.Attribute("display") == "block" || (string)math.Attribute("mode") == "display";
            return display ? Canonical(new XElement(M + "oMathPara", omml)) : omml;
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
                labeled.ReplaceWith(new XElement(Mml + "mtr", labeled.Attributes(),
                    new XElement(Mml + "mtd", cells.Skip(1).SelectMany(c => c.Nodes()), new XElement(Mml + "mo", "#"), cells[0].Nodes())));
            }
            foreach (var limits in copy.Descendants().Where(IsNaryOperator).ToList())
            {
                var argument = limits.ElementsAfterSelf().FirstOrDefault();
                if (argument != null && argument.Name != Mml + "mrow" && argument.Name != Mml + "mstyle")
                    argument.ReplaceWith(new XElement(Mml + "mrow", argument));
            }
            return copy;
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
