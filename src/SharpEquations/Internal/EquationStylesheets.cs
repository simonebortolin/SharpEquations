namespace SharpEquations.Internal
{
    using System;
    using System.Collections.Generic;
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

        /// <summary>Runs the stylesheet, or returns null when its output is empty.</summary>
        /// <exception cref="ArgumentException">The input is nested more than <see cref="MaxDepth"/> levels deep.</exception>
        public static XElement Transform(string name, XElement input)
        {
            if (Depth(input) > MaxDepth)
                throw new ArgumentException("The equation is nested too deeply.", nameof(input));
            switch (name)
            {
                case "omml2mml.xsl":
                    return Omml2mmlStylesheet.Transform(input);
                case "mml2omml.xsl":
                    return Mml2ommlStylesheet.Transform(input);
                case "ctop.xsl":
                    return CtopStylesheet.Transform(input);
                case "mtef2mml.xsl":
                    return Mtef2mmlStylesheet.Transform(input);
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "No such stylesheet.");
            }
        }

        private static int Depth(XElement root)
        {
            var depth = 0;
            var stack = new Stack<(XElement Element, int Depth)>();
            stack.Push((root, 1));
            while (stack.Count > 0)
            {
                var (element, level) = stack.Pop();
                if (level > depth)
                    depth = level;
                if (depth > MaxDepth)
                    break;
                foreach (var child in element.Elements())
                    stack.Push((child, level + 1));
            }
            return depth;
        }
    }
}
