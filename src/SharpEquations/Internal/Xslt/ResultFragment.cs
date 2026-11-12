#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System.Xml.Linq;

    /// <summary>A result tree fragment: the value of a variable or parameter given by its content.</summary>
    internal sealed class ResultFragment
    {
        private readonly string? text;
        private XElement? root;

        public ResultFragment(XElement root) => this.root = root;

        /// <summary>A fragment that is only text.</summary>
        public ResultFragment(string text) => this.text = text;

        /// <summary>The text of a fragment that is only text, or null.</summary>
        public string? Text => text;

        /// <summary>The nodes of the fragment, as the children of an element that is not part of it.</summary>
        public XElement Root => root ??= new XElement(XsltOutput.Container, text!.Length > 0 ? new XText(text) : null);

        public string Value => text ?? root!.Value;
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
}
