namespace SharpEquations.Internal.Omml
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using SharpEquations.Internal.Text;
    using SharpEquations.Internal.UnicodeMath;
    using static SharpEquations.Internal.Omml.Omml;

    /// <summary>
    /// Reads OMML into MathML with the omml2mml.xsl stylesheet of the TEI Stylesheets (BSD-2-Clause, see NOTICE),
    /// with the fixes its output needs before and after. Run formatting (fonts, colours) is not carried to MathML.
    /// </summary>
    internal static class OmmlParser
    {
        /// <summary>The elements of OMML (ISO/IEC 29500-1, 22.1).</summary>
        private static readonly HashSet<string> Elements = new HashSet<string>(StringComparer.Ordinal)
        {
            "acc", "accPr", "aln", "alnScr", "argPr", "argSz", "bar", "barPr", "baseJc", "begChr", "borderBox", "borderBoxPr",
            "box", "boxPr", "brk", "brkBin", "brkBinSub", "cGp", "cGpRule", "chr", "count", "cSp", "ctrlPr", "d", "defJc",
            "deg", "degHide", "den", "diff", "dispDef", "dPr", "e", "endChr", "eqArr", "eqArrPr", "f", "fName", "fPr", "func",
            "funcPr", "groupChr", "groupChrPr", "grow", "hideBot", "hideLeft", "hideRight", "hideTop", "interSp", "intLim",
            "intraSp", "jc", "lim", "limLoc", "limLow", "limLowPr", "limUpp", "limUppPr", "lit", "lMargin", "m", "mathFont",
            "mathPr", "maxDist", "mc", "mcJc", "mcPr", "mcs", "mPr", "mr", "nary", "naryLim", "naryPr", "noBreak", "nor",
            "num", "objDist", "oMath", "oMathPara", "oMathParaPr", "opEmu", "phant", "phantPr", "plcHide", "pos", "postSp",
            "preSp", "r", "rad", "radPr", "rMargin", "rPr", "rSp", "rSpRule", "scr", "sepChr", "show", "shp", "smallFrac",
            "sPre", "sPrePr", "sSub", "sSubPr", "sSubSup", "sSubSupPr", "sSup", "sSupPr", "strikeBLTR", "strikeH",
            "strikeTLBR", "strikeV", "sty", "sub", "subHide", "sup", "supHide", "t", "transp", "type", "vertJc",
            "wrapIndent", "wrapRight", "zeroAsc", "zeroDesc", "zeroWid",
        };

        /// <summary>
        /// Rejects the elements that are not OMML: WordprocessingML is allowed for the run properties, and markup
        /// compatibility elements only as Word writes them.
        /// </summary>
        public static void Validate(XElement omml)
        {
            foreach (var element in omml.DescendantsAndSelf())
            {
                if (element.Name.Namespace == W || element.Ancestors().Any(a => a.Name.Namespace == W))
                    continue;
                if (element.Name.Namespace != M || !Elements.Contains(element.Name.LocalName))
                    throw new FormatException("Not an element of OMML: " + element.Name + ".");
            }
        }

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
        public static XElement Parse(XElement omml)
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

            // omml2mml splits the text of runs character by character, which breaks surrogate pairs (math
            // alphanumerics such as 𝑥, emoji): they go through the stylesheet as private use characters
            var placeholders = new Dictionary<Rune, Rune>();
            var used = new HashSet<Rune>(equations.SelectMany(e => e.Descendants(M + "t")).SelectMany(t => CodePoints.Of(t.Value)));
            var converted = equations.Select(e => SingleRow(EquationStylesheets.Transform("omml2mml.xsl", HideAstral(PrepareOmml(e), placeholders, used)))).ToList();
            if (placeholders.Count > 0)
            {
                var astral = placeholders.ToDictionary(p => p.Value, p => p.Key);
                foreach (var text in converted.SelectMany(c => c.DescendantNodes()).OfType<XText>())
                    text.Value = RestoreAstral(text.Value, astral);
                foreach (var attribute in converted.SelectMany(c => c.DescendantsAndSelf()).SelectMany(e => e.Attributes()))
                    attribute.Value = RestoreAstral(attribute.Value, astral);
            }
            foreach (var equation in converted)
                UnmarkNames(equation);
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
            // The other spaces (thin, em...) are kept: MathML collapses only those of XML.
            foreach (var token in math.Descendants().Where(e => !e.HasElements && e.Value.Length > 0 && e.Value.Trim().Length == 0).ToList())
            {
                var xmlSpaces = token.Value.Any(IsXmlSpace);
                token.Value = string.Concat(token.Value.Select(c => IsXmlSpace(c) ? '\u00A0' : c));
                if (token.Name == Mml + "mi" || token.Name == Mml + "mn" || token.Name == Mml + "mo" && xmlSpaces)
                {
                    token.Name = Mml + "mtext";
                    token.Attributes().Where(a => a.Name.LocalName != "mathvariant").Remove();
                }
            }
            // MathML also trims tokens and collapses their spaces: " ove " would touch the formula around it.
            foreach (var text in math.Descendants(Mml + "mtext").Where(e => !e.HasElements))
                text.Value = CollapsibleSpaces().Replace(text.Value, m => new string('\u00A0', m.Length));
            JoinNumbers(math);
            LabelEquationNumbers(math);
            if (display)
                math.SetAttributeValue("display", "block");
            return math;
        }

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

        private static bool IsXmlSpace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';

        private static XElement HideAstral(XElement oMath, Dictionary<Rune, Rune> placeholders, HashSet<Rune> used)
        {
            foreach (var text in oMath.Descendants(M + "t").Where(t => CodePoints.Of(t.Value).Any(c => !c.IsBmp)))
            {
                var sb = new StringBuilder();
                foreach (var codePoint in CodePoints.Of(text.Value))
                {
                    var hidden = codePoint;
                    if (!codePoint.IsBmp && !placeholders.TryGetValue(codePoint, out hidden))
                    {
                        int value = 0xE000 + placeholders.Count;
                        while (used.Contains(new Rune(value)))
                            value++;
                        hidden = new Rune(value);
                        used.Add(hidden);
                        placeholders[codePoint] = hidden;
                    }
                    sb.Append(hidden.ToString());
                }
                text.Value = sb.ToString();
            }
            return oMath;
        }

        private static string RestoreAstral(string value, Dictionary<Rune, Rune> astral)
        {
            var sb = new StringBuilder();
            foreach (var codePoint in CodePoints.Of(value))
                sb.Append((astral.TryGetValue(codePoint, out var original) ? original : codePoint).ToString());
            return sb.ToString();
        }

        private static XElement PrepareOmml(XElement oMath)
        {
            var copy = new XElement(oMath);
            foreach (var property in copy.Descendants().Where(e => e.Name.Namespace == M && OnOffProperties.Contains(e.Name.LocalName)))
            {
                var value = property.Attribute(M + "val");
                property.SetAttributeValue(M + "val", value == null || IsTrue(value.Value) ? "on" : "off");
            }
            // an upright word (max, argmin, sharet1) is one identifier, which omml2mml would split letter by letter: it goes
            // through the stylesheet as normal text, marked, and comes back as an mi (see UnmarkNames)
            foreach (var run in copy.Descendants(M + "r").Where(IsUprightName).ToList())
            {
                var properties = run.Element(M + "rPr");
                properties!.Add(new XElement(M + "nor"));
                var text = run.Element(M + "t")!;
                text.Value = NameMark + text.Value;
            }
            // omml2mml writes no mathvariant on numbers, which would lose the style of a bold or double-struck digit:
            // the digits of a styled run go through the stylesheet as their mathematical alphanumerics (𝟏, 𝟙), each an
            // identifier, as UnicodeMath and LaTeX read them
            foreach (var run in copy.Descendants(M + "r"))
            {
                var properties = run.Elements(M + "rPr").LastOrDefault();
                if (properties == null || (string)properties.Element(M + "nor")?.Attribute(M + "val") == "on")
                    continue;
                var style = DigitStyle((string)properties.Element(M + "scr")?.Attribute(M + "val"), (string)properties.Element(M + "sty")?.Attribute(M + "val"));
                if (style == null)
                    continue;
                foreach (var text in run.Elements(M + "t"))
                    text.Value = string.Concat(CodePoints.Of(text.Value).Select(c => c.Value is >= '0' and <= '9' ? UnicodeMathText.GetMathAlphanumeric(c.ToString(), style) : c.ToString()));
            }
            return copy;
        }

        /// <summary>The style of the mathematical digits for the m:scr and m:sty of a run, null for plain digits.</summary>
        private static string DigitStyle(string script, string style)
        {
            return (script, style) switch
            {
                ("monospace", _) => "mtt",
                ("sans-serif", "b") => "mbfsans",
                ("sans-serif", "i" or "bi") => null,
                ("sans-serif", _) => "msans",
                ("double-struck", _) => "Bbb",
                ("roman" or null or "", "b") => "mbf",
                _ => null,
            };
        }

        private const char NameMark = '\uF8FF';

        private static bool IsUprightName(XElement run)
        {
            var properties = run.Element(M + "rPr");
            var texts = run.Elements(M + "t").ToList();
            return properties != null && properties.Element(M + "nor") == null && (string)properties.Element(M + "sty")?.Attribute(M + "val") == "p"
                && !run.Ancestors(M + "fName").Any() && texts.Count == 1 && texts[0].Value.Length > 1
                && AsciiPatterns.Name().IsMatch(texts[0].Value);
        }

        private static void UnmarkNames(XElement math)
        {
            foreach (var text in math.Descendants(Mml + "mtext").Where(t => t.Value.Length > 1 && t.Value[0] == NameMark).ToList())
                text.ReplaceWith(new XElement(Mml + "mi", text.Value.Substring(1)));
        }
    }
}
