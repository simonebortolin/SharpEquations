namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// A node of an equation being edited: an element of presentation MathML, or a <em>run</em> of linear text that is
    /// not built up yet. Nodes are immutable: an edit gives a new tree, which shares the nodes it did not change.
    /// </summary>
    /// <remarks>
    /// Every slot of a structure (the numerator of a fraction, the base and the scripts of a script, a cell of a table)
    /// is a row, <c>mrow</c>, even where MathML has a single element or an inferred row, so that the caret always has a
    /// row to stand in. <see cref="FromMathML"/> adds these rows and <see cref="ToMathML()"/> writes them back as
    /// <c>mrow</c>, which the normalization of an <see cref="Equation"/> removes where they group nothing.
    /// </remarks>
    public sealed class MathNode
    {
        /// <summary>The <see cref="Name"/> of a run of linear text.</summary>
        public const string RunName = "#run";

        private static readonly IReadOnlyList<MathNode> NoChildren = Array.Empty<MathNode>();
        private static readonly IReadOnlyList<KeyValuePair<string, string>> NoAttributes = Array.Empty<KeyValuePair<string, string>>();

        private readonly XElement? source;

        private MathNode(string name, IReadOnlyList<KeyValuePair<string, string>>? attributes, string text, IReadOnlyList<MathNode>? children, XElement? source = null)
        {
            Name = name;
            Attributes = attributes ?? NoAttributes;
            Text = text;
            Children = children ?? NoChildren;
            this.source = source;
        }

        /// <summary>The local name of the MathML element (<c>mi</c>, <c>mfrac</c>...), or <see cref="RunName"/>.</summary>
        public string Name { get; }

        /// <summary>The attributes of the element, by the <see cref="XName"/> of each written as a string.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; }

        /// <summary>The text of a token element (<c>mi</c>, <c>mn</c>, <c>mo</c>, <c>mtext</c>, <c>ms</c>) or of a run.</summary>
        public string Text { get; }

        /// <summary>The children; those of a structure that the caret can enter are rows.</summary>
        public IReadOnlyList<MathNode> Children { get; }

        /// <summary>A run of linear text, not built up yet.</summary>
        public bool IsRun => Name == RunName;

        /// <summary>A row, <c>mrow</c>: a sequence of nodes, where the caret stands.</summary>
        public bool IsRow => Name == "mrow";

        /// <summary>A token element: <c>mi</c>, <c>mn</c>, <c>mo</c>, <c>mtext</c> or <c>ms</c>.</summary>
        public bool IsToken => MathSchema.IsToken(Name);

        /// <summary>An element kept as it was read, which the caret does not enter (<c>semantics</c>, <c>mstack</c>...).</summary>
        public bool IsOpaque => source != null;

        /// <summary>The MathML of an <see cref="IsOpaque"/> node, as it was read.</summary>
        public XElement? Source => source == null ? null : new XElement(source);

        /// <summary>A new run of linear text.</summary>
        public static MathNode Run(string text) => new MathNode(RunName, null, text ?? throw new ArgumentNullException(nameof(text)), null);

        /// <summary>A new row of the nodes.</summary>
        public static MathNode Row(IEnumerable<MathNode> children, IReadOnlyList<KeyValuePair<string, string>>? attributes = null) =>
            new MathNode("mrow", attributes, string.Empty, children.ToArray());

        /// <summary>A new token element.</summary>
        public static MathNode Token(string name, string text, IReadOnlyList<KeyValuePair<string, string>>? attributes = null) =>
            new MathNode(name, attributes, text, null);

        /// <summary>A new element with children (a structure, a leaf such as <c>mspace</c>, or <c>math</c>).</summary>
        public static MathNode Element(string name, IEnumerable<MathNode> children, IReadOnlyList<KeyValuePair<string, string>>? attributes = null) =>
            new MathNode(name, attributes, string.Empty, children.ToArray());

        /// <summary>The value of an attribute without namespace, or null.</summary>
        public string? GetAttribute(string name)
        {
            foreach (var attribute in Attributes)
            {
                if (attribute.Key == name)
                    return attribute.Value;
            }
            return null;
        }

        /// <summary>A copy with other children.</summary>
        public MathNode WithChildren(IEnumerable<MathNode> children) => new MathNode(Name, Attributes, Text, children.ToArray(), source);

        /// <summary>A copy with one child replaced.</summary>
        public MathNode WithChild(int index, MathNode child)
        {
            var children = Children.ToArray();
            children[index] = child;
            return new MathNode(Name, Attributes, Text, children, source);
        }

        /// <summary>A copy with another text.</summary>
        public MathNode WithText(string text) => new MathNode(Name, Attributes, text, Children, source);

        /// <summary>A copy with an attribute set, or removed when the value is null.</summary>
        public MathNode WithAttribute(string name, string? value)
        {
            var attributes = Attributes.Where(a => a.Key != name).ToList();
            if (value != null)
                attributes.Add(new KeyValuePair<string, string>(name, value));
            return new MathNode(Name, attributes, Text, Children, source);
        }

        /// <summary>The nodes of a MathML element, with a row in every slot.</summary>
        public static MathNode FromMathML(XElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var name = element.Name.LocalName;
            var attributes = ReadAttributes(element);
            switch (MathSchema.KindOf(name))
            {
                case ElementKind.Token:
                    return new MathNode(name, attributes, element.Value, null);
                case ElementKind.Leaf:
                    return new MathNode(name, attributes, string.Empty, null);
                case ElementKind.Row:
                    return new MathNode(name, attributes, string.Empty, element.Elements().Select(FromMathML).ToArray());
                case ElementKind.InferredRow:
                    // the children in a row of their own, even a single mrow, which groups them (a bracket)
                    return new MathNode(name, attributes, string.Empty, new[] { Row(element.Elements().Select(FromMathML)) });
                case ElementKind.Fixed:
                    return new MathNode(name, attributes, string.Empty, element.Elements().Select(Slot).ToArray());
                case ElementKind.Table:
                    return new MathNode(name, attributes, string.Empty, element.Elements().Select(FromMathML).ToArray());
                default:
                    return new MathNode(name, attributes, element.Value, null, new XElement(element));
            }

            static MathNode Slot(XElement child) => child.Name.LocalName switch
            {
                "mprescripts" => new MathNode("mprescripts", null, string.Empty, null),
                "none" => Row(Array.Empty<MathNode>()),
                _ when IsPlainRow(child) => FromMathML(child),
                _ => Row(new[] { FromMathML(child) }),
            };
        }

        /// <summary>The MathML of the node; a run is written as <c>merror</c> with its text.</summary>
        public XElement ToMathML() => ToMathML(UnbuiltRun).Single();

        internal static IEnumerable<XElement> UnbuiltRun(MathNode run)
        {
            yield return new XElement(MathMLNamespaces.MathML + "merror", new XElement(MathMLNamespaces.MathML + "mtext", run.Text));
        }

        internal IEnumerable<XElement> ToMathML(Func<MathNode, IEnumerable<XElement>> runs)
        {
            var ns = MathMLNamespaces.MathML;
            if (IsRun)
            {
                foreach (var element in runs(this))
                    yield return element;
                yield break;
            }

            if (source != null)
            {
                yield return new XElement(source);
                yield break;
            }

            var result = new XElement(ns + Name, Attributes.Select(a => new XAttribute(XName.Get(a.Key), a.Value)));
            switch (MathSchema.KindOf(Name))
            {
                case ElementKind.Token when Text.Length > 0:
                    result.Add(Text);
                    break;
                case ElementKind.Token:
                    break;
                case ElementKind.Leaf:
                    break;
                case ElementKind.InferredRow when Children.Count == 1 && Children[0].IsRow && Children[0].Attributes.Count == 0:
                    result.Add(Children[0].Children.SelectMany(c => c.ToMathML(runs)));
                    break;
                case ElementKind.Fixed:
                    foreach (var child in Children)
                    {
                        if (Name == "mmultiscripts" && child.IsRow && child.Children.Count == 0 && child.Attributes.Count == 0)
                            result.Add(new XElement(ns + "none"));
                        else if (child.IsRow && child.Attributes.Count == 0 && child.Children.Count == 1 && !child.Children[0].IsRun)
                            result.Add(child.Children[0].ToMathML(runs));
                        else
                            result.Add(child.ToMathML(runs));
                    }
                    break;
                default:
                    result.Add(Children.SelectMany(c => c.ToMathML(runs)));
                    break;
            }
            yield return result;
        }

        /// <inheritdoc/>
        public override string ToString() => IsRun ? "«" + Text + "»" : ToMathML().ToString(SaveOptions.DisableFormatting);

        private static bool IsPlainRow(XElement element) => element.Name.LocalName == "mrow" && !element.Attributes().Any(a => !a.IsNamespaceDeclaration);

        private static List<KeyValuePair<string, string>>? ReadAttributes(XElement element)
        {
            List<KeyValuePair<string, string>>? list = null;
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;
                (list ??= new List<KeyValuePair<string, string>>()).Add(new KeyValuePair<string, string>(attribute.Name.ToString(), attribute.Value));
            }
            return list;
        }
    }
}
