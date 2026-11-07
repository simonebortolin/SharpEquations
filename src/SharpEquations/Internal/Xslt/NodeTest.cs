#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System.Xml.Linq;

    /// <summary>The node test of an XPath step: a name, a namespace wildcard, <c>*</c> or a node type.</summary>
    internal sealed class NodeTest
    {
        private enum Kind
        {
            Name,
            Namespace,
            AnyName,
            Node,
            Text,
            Comment,
            ProcessingInstruction,
        }

        private readonly Kind kind;
        private readonly XName? name;
        private readonly string? text;

        private NodeTest(Kind kind, XName? name = null, string? text = null)
        {
            this.kind = kind;
            this.name = name;
            this.text = text;
        }

        public static NodeTest Named(XName name) => new NodeTest(Kind.Name, name);

        public static NodeTest InNamespace(string namespaceName) => new NodeTest(Kind.Namespace, text: namespaceName);

        public static readonly NodeTest AnyName = new NodeTest(Kind.AnyName);

        public static readonly NodeTest Node = new NodeTest(Kind.Node);

        public static readonly NodeTest Text = new NodeTest(Kind.Text);

        public static readonly NodeTest Comment = new NodeTest(Kind.Comment);

        public static NodeTest ProcessingInstruction(string? target) => new NodeTest(Kind.ProcessingInstruction, text: target);

        /// <summary>
        /// Whether the node passes the test, on an axis whose principal node type is the attribute (the attribute
        /// axis) or the element (every other axis).
        /// </summary>
        public bool Matches(XObject node, bool attributeAxis)
        {
            switch (kind)
            {
                case Kind.Name:
                    return attributeAxis
                        ? node is XAttribute attribute && attribute.Name == name
                        : node is XElement element && element.Name == name;
                case Kind.Namespace:
                    return attributeAxis
                        ? node is XAttribute attribute2 && attribute2.Name.NamespaceName == text
                        : node is XElement element2 && element2.Name.NamespaceName == text;
                case Kind.AnyName:
                    return attributeAxis ? node is XAttribute : node is XElement;
                case Kind.Node:
                    return true;
                case Kind.Text:
                    return node is XText;
                case Kind.Comment:
                    return node is XComment;
                default:
                    return node is XProcessingInstruction instruction && (text == null || instruction.Target == text);
            }
        }
    }
}
