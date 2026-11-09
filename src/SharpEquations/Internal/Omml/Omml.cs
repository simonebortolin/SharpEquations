namespace SharpEquations.Internal.Omml
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using SharpEquations.Internal.Text;

    /// <summary>The names and helpers shared by <see cref="OmmlParser"/> and <see cref="OmmlSerializer"/>.</summary>
    internal static partial class Omml
    {
        internal static readonly XNamespace M = MathMLNamespaces.Omml;

        internal static readonly XNamespace W = MathMLNamespaces.WordprocessingML;

        internal static readonly XNamespace Mml = MathMLNamespaces.MathML;

        // The class of the table that holds the lines of a display equation aligned with m:aln.
        internal const string AlignedLinesClass = "aligned-lines";

        [GeneratedRegex("^ +| +$| {2,}", RegexOptions.CultureInvariant)]
        internal static partial Regex CollapsibleSpaces();

        internal static bool IsOn(XElement property)
        {
            var value = property.Attribute(M + "val");
            return value == null || IsTrue(value.Value);
        }

        internal static bool IsRow(XElement element)
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
        /// Groups the children of the math element in one mrow: some consumers, LibreOffice among them,
        /// lay out each child of math on a line of its own.
        /// </summary>
        internal static XElement SingleRow(XElement math)
        {
            if (math.Elements().Count() > 1)
            {
                var row = new XElement(Mml + "mrow", math.Nodes());
                math.ReplaceNodes(row);
            }
            return math;
        }

        internal static bool IsTrue(string value)
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

        /// <summary>
        /// Rewrites the element with the m: and w: prefixes declared once on the root, as the OMML parsers expect.
        /// </summary>
        internal static XElement Canonical(XElement element)
        {
            var copy = new XElement(element);
            foreach (var e in copy.DescendantsAndSelf())
                e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            copy.Add(new XAttribute(XNamespace.Xmlns + "m", M.NamespaceName), new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName));
            return copy;
        }
    }
}
