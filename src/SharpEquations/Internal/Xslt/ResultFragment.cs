#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System.Xml.Linq;

    /// <summary>A result tree fragment: the value of a variable or parameter given by its content.</summary>
    internal sealed class ResultFragment
    {
        public ResultFragment(XElement root) => Root = root;

        /// <summary>The nodes of the fragment, as the children of an element that is not part of it.</summary>
        public XElement Root { get; }

        public string Value => Root.Value;
    }

    /// <summary>The parameters passed by <c>xsl:apply-templates</c>.</summary>
    internal sealed class TemplateArguments
    {
        private readonly string[] names;
        private readonly object[] values;

        public TemplateArguments(string[] names, object[] values)
        {
            this.names = names;
            this.values = values;
        }

        public object? Get(string name)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                    return values[i];
            }
            return null;
        }
    }

    /// <summary>An XPath predicate, or any expression evaluated with a context node, position and size.</summary>
    internal delegate bool XPathPredicate(XObject node, int position, int size);
}
