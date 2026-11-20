namespace SharpEquations.Internal
{
    using System;
    using System.Xml.Linq;
    using SharpEquations.Internal.Xslt;

    /// <summary>
    /// The XSLT 1.0 stylesheets of Resources, compiled to C# at build time by SharpEquations.XsltGenerator
    /// (Internal/Xslt), so that they run without generating code at run time.
    /// </summary>
    internal static class EquationStylesheets
    {
        /// <summary>
        /// The deepest input accepted: the stylesheets recurse over the tree, and a stack overflow cannot be caught.
        /// </summary>
        public const int MaxDepth = 512;

        /// <summary>The message of the exceptions thrown for an equation nested more deeply than can be handled.</summary>
        public const string TooDeep = "The equation is nested too deeply.";

        /// <summary>Whether the element is nested more than <see cref="MaxDepth"/> levels deep.</summary>
        public static bool IsTooDeep(XElement element) => Depth(element) > MaxDepth;

        /// <summary>Runs the stylesheet, or returns null when its output is empty.</summary>
        /// <exception cref="InsufficientExecutionStackException">The input is nested more than <see cref="MaxDepth"/> levels deep.</exception>
        public static XElement Transform(string name, XElement input)
        {
            if (IsTooDeep(input))
                throw new InsufficientExecutionStackException(TooDeep);
            switch (name)
            {
                case "omml2mml.xsl":
                    return Omml2mmlStylesheet.Transform(input);
                case "mml2omml.xsl":
                    return Mml2ommlStylesheet.Transform(input);
                case "mtef2mml.xsl":
                    return Mtef2mmlStylesheet.Transform(input);
                case "ctop.xsl":
                    return CtopStylesheet.Transform(input);
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "No such stylesheet.");
            }
        }

        // without allocating: the input of every equation read goes through it (FirstNode would make an XText of
        // the text of a token)
        private static int Depth(XElement root)
        {
            int depth = 1, deepest = 1;
            var element = root;
            while (true)
            {
                var child = element.HasElements ? NextElement(element.FirstNode) : null;
                if (child != null)
                {
                    element = child;
                    if (++depth > deepest && (deepest = depth) > MaxDepth)
                        return deepest;
                    continue;
                }
                while (true)
                {
                    if (element == root)
                        return deepest;
                    var sibling = NextElement(element.NextNode);
                    if (sibling != null)
                    {
                        element = sibling;
                        break;
                    }
                    element = element.Parent;
                    depth--;
                }
            }
        }

        private static XElement NextElement(XNode node)
        {
            while (node != null && node is not XElement)
                node = node.NextNode;
            return (XElement)node;
        }
    }
}
