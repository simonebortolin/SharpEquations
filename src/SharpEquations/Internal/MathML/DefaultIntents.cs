#nullable enable

namespace SharpEquations.Internal.MathML
{
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// The intents UnicodeMath gives by default, which an equation does not hold (<see cref="MathMLParser"/>), found
    /// again from the structure for the writers that choose their syntax by them: \binom and ⒞ for a binomial
    /// coefficient, pmatrix and ⒨ for a matrix in parentheses, █ and aligned for equations aligned, and the rows of
    /// n-ary operations and function applications, which an equation does not hold either when they fill their parent.
    /// </summary>
    internal static class DefaultIntents
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        /// <summary>The annotation of an intent found again, which is not part of the equation.</summary>
        internal sealed class Restored
        {
            public static readonly Restored Instance = new Restored();
        }

        /// <summary>
        /// A copy of the math element with the default intents found again; MathML that has default intents of its own
        /// (MathML not read as an equation) as it is.
        /// </summary>
        public static XElement Restore(XElement math)
        {
            if (math.DescendantsAndSelf().Attributes("intent").Any(a => MathMLParser.IsDefaultIntent(a.Value) || a.Value is ":math-paragraph" or ":equation-label"))
                return math;
            var copy = new XElement(math);
            foreach (var element in copy.DescendantsAndSelf().Reverse().ToList())
            {
                Regroup(element);
                if (element.Attribute("intent") == null && Intent(element) is { } intent)
                {
                    var attribute = new XAttribute("intent", intent);
                    attribute.AddAnnotation(Restored.Instance);
                    element.Add(attribute);
                }
            }
            return copy;
        }

        /// <summary>Whether an intent is one found again by <see cref="Restore"/>.</summary>
        public static bool IsRestored(XAttribute? intent) => intent?.Annotation<Restored>() != null;

        /// <summary>An n-ary operation or a function application that fills an element: in a row of its own again.</summary>
        private static void Regroup(XElement element)
        {
            var children = element.Elements().ToList();
            if (children.Count < 2 || element.Name.LocalName is not ("math" or "msqrt" or "mstyle" or "merror" or "mpadded" or "mphantom" or "menclose" or "mtd"))
                return;
            if (children.Count == 2 && IsNaryHead(children[0]) && children[1].Name.LocalName is "mrow" or "mpadded" ||
                children.Count == 3 && children[1].Name == Mml + "mo" && children[1].Value == "\u2061")
            {
                var row = new XElement(Mml + "mrow", children);
                element.RemoveNodes();
                element.Add(row);
            }
        }

        private static bool IsNaryHead(XElement head)
        {
            while (head.Name.LocalName is "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover")
            {
                if (head.Elements().FirstOrDefault() is not { } scripted)
                    return false;
                head = scripted;
            }
            return head.Name == Mml + "mo" && head.Value.Length == 1 && "∑∏∐∫∬∭∮∯∰∱∲∳⨌⨀⨁⨂⨃⨄⨅⨆⋀⋁⋂⋃".Contains(head.Value);
        }

        private static string? Intent(XElement element)
        {
            var children = element.Elements().ToList();
            if (element.Name == Mml + "mtable")
                return (string?)element.Attribute("columnspacing") == "0pt" ? ":system-of-equations" : null;
            if (element.Name != Mml + "mrow" || children.Count < 2)
                return null;
            if (children[1].Name == Mml + "mo" && children[1].Value == "\u2061")
                return ":function";
            if (children.Count == 2 && IsNaryHead(children[0]))
                return ":largeop";
            if (children[0].Name != Mml + "mo" || children[children.Count - 1].Name != Mml + "mo" || children[0].HasElements ||
                children[children.Count - 1].HasElements)
                return null;
            var open = children[0].Value;
            var close = children[children.Count - 1].Value;
            if (children.Count == 3 && children[1].Name == Mml + "mtable")
            {
                if (open == "{" && close == "\u200B" && (string?)children[1].Attribute("columnspacing") == "0pt")
                    return ":piecewise";
                switch (open + close)
                {
                    case "()": return ":parenthesized-matrix";
                    case "[]": return ":bracketed-matrix";
                    case "{}": return ":curly-braced-matrix";
                    case "||": return ":determinant";
                    case "‖‖": return ":normed-matrix";
                }
            }
            if (children.Count == 3 && open == "(" && close == ")" && children[1].Name == Mml + "mfrac" && (string?)children[1].Attribute("linethickness") == "0")
                return "binomial-coefficient";
            if (children.Count >= 3 && open == "|" && close == "|")
                return "absolute-value";
            // an interval with a bracket the other way (]𝑎,𝑏], [𝑎,𝑏[), whose brackets do not pair
            if (children.Count == 5 && children[2].Name == Mml + "mo" && children[2].Value == "," && open is "[" or "]" && close is "[" or "]" && open + close != "[]")
                return open == "]" && close == "[" ? "open-interval" : (open == "[" ? "closed" : "open") + "-" + (close == "]" ? "closed" : "open") + "-interval";
            return IsFence(open) && IsFence(close) ? ":fenced" : null;
        }

        private static bool IsFence(string text) =>
            text.Length == 0 || text == "\u200B" || text.Length == 1 && "([{⟨⌈⌊⟦⟪⦃〖❲〔⁅|‖)]}⟩⌉⌋⟧⟫⦄〗❳〕⁆".Contains(text);
    }
}
