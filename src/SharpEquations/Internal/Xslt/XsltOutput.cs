#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// The result tree a generated stylesheet builds, as LINQ to XML, with the result tree fragments and the
    /// attribute values its instructions build on the way. It gives what XslCompiledTransform gives when its output
    /// is written as text and parsed again: the text written with disable-output-escaping is markup, which takes
    /// effect with the text next to it (a character reference may come in pieces), and empty text leaves an empty
    /// element with an end tag.
    /// </summary>
    internal sealed class XsltOutput
    {
        public static readonly XName Container = XName.Get("fragment");

        private readonly XElement result = new XElement(Container);

        // the element being written, the root of what is written (the result, or the fragment being captured), and
        // those of the captures around it
        private XContainer top;
        private XElement root;
        private Stack<XContainer>? outer;

        // the text written since the last node: as it is (one piece, or more in the builder) and, once some is written
        // with disable-output-escaping, as markup (escaped, with the unescaped text as it is)
        private string? piece;
        private StringBuilder? text;
        private StringBuilder? markup;
        private bool hasText;
        private bool hasMarkup;

        // the text captured as strings, one capture after the other, and where the one in progress starts (-1: none)
        private StringBuilder? captured;
        private Stack<int>? captures;
        private int capture = -1;

        public XsltOutput()
        {
            top = result;
            root = result;
        }

        public void StartElement(XName name)
        {
            Flush();
            var element = new XElement(name);
            top.Add(element);
            top = element;
        }

        public void EndElement()
        {
            Flush();
            top = top.Parent!;
        }

        public void Attribute(XName name, string value)
        {
            Flush();
            if (!(top is XElement element) || ReferenceEquals(element, root))
                throw new InvalidOperationException("An attribute can be added only to an element.");
            for (var node = element.FirstNode; node != null; node = node.NextNode)
            {
                if (!(node is XText t) || t.Value.Length > 0)
                    throw new InvalidOperationException("An attribute cannot be added to an element after its children.");
            }
            element.SetAttributeValue(name, value);
        }

        public void Text(string value)
        {
            if (capture >= 0)
            {
                captured!.Append(value);
                return;
            }
            if (hasMarkup)
                Escape(markup!, value);
            if (!hasText)
            {
                hasText = true;
                piece = value;
                return;
            }
            if (piece != null)
            {
                text ??= new StringBuilder();
                text.Append(piece);
                piece = null;
            }
            text!.Append(value);
        }

        private static void Escape(StringBuilder markup, string value)
        {
            foreach (var c in value)
            {
                switch (c)
                {
                    case '&':
                        markup.Append("&amp;");
                        break;
                    case '<':
                        markup.Append("&lt;");
                        break;
                    case '>':
                        markup.Append("&gt;");
                        break;
                    default:
                        markup.Append(c);
                        break;
                }
            }
        }

        private string Pending => piece ?? text!.ToString();

        /// <summary>Text written with disable-output-escaping.</summary>
        public void RawText(string value)
        {
            if (capture >= 0)
                throw new InvalidOperationException("Text cannot be written as markup here.");
            if (!hasMarkup)
            {
                markup ??= new StringBuilder();
                markup.Clear();
                if (hasText)
                    Escape(markup, Pending);
                hasMarkup = true;
            }
            markup!.Append(value);
            if (!hasText)
            {
                hasText = true;
                piece = value;
                return;
            }
            if (piece != null)
            {
                text ??= new StringBuilder();
                text.Append(piece);
                piece = null;
            }
            text!.Append(value);
        }

        private void Flush()
        {
            if (!hasText)
                return;
            if (hasMarkup)
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(new StringReader("<r>" + markup + "</r>"), settings))
                    top.Add(XElement.Load(reader, LoadOptions.PreserveWhitespace).Nodes().ToList());
                markup!.Clear();
            }
            else
            {
                var pending = Pending;
                if (pending.Length > 0 || (top is XElement element && element.IsEmpty && !ReferenceEquals(element, root)))
                    top.Add(pending);
            }
            piece = null;
            text?.Clear();
            hasText = false;
            hasMarkup = false;
        }

        public void Comment(string value)
        {
            Flush();
            top.Add(new XComment(value));
        }

        public void ProcessingInstruction(string target, string data)
        {
            Flush();
            top.Add(new XProcessingInstruction(target, data));
        }

        public void CopyOf(List<XObject> nodes)
        {
            foreach (var node in nodes)
                Copy(node);
        }

        public void CopyOf(ResultFragment fragment)
        {
            if (fragment.Text != null)
            {
                CopyOfText(fragment.Text);
                return;
            }
            foreach (var node in fragment.Root.Nodes())
                Copy(node);
        }

        public void CopyOf(string value) => Text(value);

        /// <summary>The copy of a result tree fragment that is only text: its text node, if it has one.</summary>
        public void CopyOfText(string value)
        {
            if (value.Length > 0)
                Text(value);
        }

        public void CopyOf(object value)
        {
            switch (value)
            {
                case List<XObject> nodes:
                    CopyOf(nodes);
                    break;
                case ResultFragment fragment:
                    CopyOf(fragment);
                    break;
                default:
                    Text(XPathFunctions.Str(value));
                    break;
            }
        }

        public void Copy(XObject node)
        {
            switch (node)
            {
                case XDocument document:
                    foreach (var child in XPathFunctions.Children(document))
                        Copy(child);
                    break;
                case XElement element:
                    Flush();
                    top.Add(new XElement(element));
                    break;
                case XAttribute attribute:
                    Attribute(attribute.Name, attribute.Value);
                    break;
                case XText t:
                    Text(t.Value);
                    break;
                case XComment comment:
                    Comment(comment.Value);
                    break;
                case XProcessingInstruction instruction:
                    ProcessingInstruction(instruction.Target, instruction.Data);
                    break;
            }
        }

        /// <summary>The shallow copy of xsl:copy: the node without its children, open for content if it is an element.</summary>
        public bool StartCopy(XObject node)
        {
            switch (node)
            {
                case XElement element:
                    StartElement(element.Name);
                    return true;
                case XDocument _:
                    return false;
                default:
                    Copy(node);
                    return false;
            }
        }

        /// <summary>Starts keeping the text written as a string: only text is written until <see cref="EndText"/>.</summary>
        public void BeginText()
        {
            captured ??= new StringBuilder();
            captures ??= new Stack<int>();
            captures.Push(capture);
            capture = captured.Length;
        }

        public string EndText()
        {
            var value = captured!.ToString(capture, captured.Length - capture);
            captured.Length = capture;
            capture = captures!.Pop();
            return value;
        }

        public void BeginCapture()
        {
            Flush();
            captures ??= new Stack<int>();
            captures.Push(capture);
            capture = -1;
            outer ??= new Stack<XContainer>();
            outer.Push(top);
            outer.Push(root);
            root = new XElement(Container);
            top = root;
        }

        private XElement EndCapture()
        {
            Flush();
            var fragment = root;
            root = (XElement)outer!.Pop();
            top = outer.Pop();
            capture = captures!.Pop();
            return fragment;
        }

        public ResultFragment EndFragment() => new ResultFragment(EndCapture());

        /// <summary>The text captured, for an attribute or a comment, where disable-output-escaping does not apply.</summary>
        public string EndString()
        {
            hasMarkup = false;
            return EndCapture().Value;
        }

        /// <summary>The document element of the result, or null if the result is only white space.</summary>
        /// <exception cref="XmlException">The result is not a document.</exception>
        public XElement? Result()
        {
            Flush();
            XElement? element = null;
            foreach (var node in result.Nodes())
            {
                switch (node)
                {
                    case XElement e when element == null:
                        element = e;
                        break;
                    case XElement _:
                        throw new XmlException("The result of the stylesheet has more than one document element.");
                    case XText t when t.Value.Trim().Length > 0:
                        throw new XmlException("The result of the stylesheet has text outside the document element.");
                }
            }
            element?.Remove();
            return element;
        }
    }
}
