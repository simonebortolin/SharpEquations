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
        private static readonly XName Container = XName.Get("fragment");

        private readonly Stack<XContainer> open = new Stack<XContainer>();
        private readonly Stack<XElement> roots = new Stack<XElement>();
        private readonly XElement result = new XElement(Container);

        // the text written since the last node, as it is and as markup (escaped, with the unescaped text as it is)
        private readonly StringBuilder text = new StringBuilder();
        private readonly StringBuilder markup = new StringBuilder();
        private bool hasText;
        private bool hasMarkup;

        public XsltOutput()
        {
            open.Push(result);
            roots.Push(result);
        }

        private XContainer Top => open.Peek();

        public void StartElement(XName name)
        {
            Flush();
            var element = new XElement(name);
            Top.Add(element);
            open.Push(element);
        }

        public void EndElement()
        {
            Flush();
            open.Pop();
        }

        public void Attribute(XName name, string value)
        {
            Flush();
            if (!(Top is XElement element) || ReferenceEquals(element, roots.Peek()))
                throw new InvalidOperationException("An attribute can be added only to an element.");
            if (element.Nodes().Any(n => !(n is XText t) || t.Value.Length > 0))
                throw new InvalidOperationException("An attribute cannot be added to an element after its children.");
            element.SetAttributeValue(name, value);
        }

        public void Text(string value)
        {
            hasText = true;
            text.Append(value);
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

        /// <summary>Text written with disable-output-escaping.</summary>
        public void RawText(string value)
        {
            hasText = true;
            hasMarkup = true;
            text.Append(value);
            markup.Append(value);
        }

        private void Flush()
        {
            if (!hasText)
                return;
            var top = Top;
            if (hasMarkup)
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(new StringReader("<r>" + markup + "</r>"), settings))
                    top.Add(XElement.Load(reader, LoadOptions.PreserveWhitespace).Nodes().ToList());
            }
            else if (text.Length > 0 || (top is XElement element && element.IsEmpty && !ReferenceEquals(element, roots.Peek())))
            {
                top.Add(text.ToString());
            }
            text.Clear();
            markup.Clear();
            hasText = false;
            hasMarkup = false;
        }

        public void Comment(string value)
        {
            Flush();
            Top.Add(new XComment(value));
        }

        public void ProcessingInstruction(string target, string data)
        {
            Flush();
            Top.Add(new XProcessingInstruction(target, data));
        }

        public void CopyOf(List<XObject> nodes)
        {
            foreach (var node in nodes)
                Copy(node);
        }

        public void CopyOf(ResultFragment fragment)
        {
            foreach (var node in fragment.Root.Nodes())
                Copy(node);
        }

        public void CopyOf(string value) => Text(value);

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

        private void Copy(XObject node)
        {
            switch (node)
            {
                case XDocument document:
                    foreach (var child in XPathFunctions.Children(document))
                        Copy(child);
                    break;
                case XElement element:
                    Flush();
                    Top.Add(new XElement(element));
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

        public void BeginCapture()
        {
            Flush();
            var root = new XElement(Container);
            open.Push(root);
            roots.Push(root);
        }

        public ResultFragment EndFragment()
        {
            Flush();
            open.Pop();
            return new ResultFragment(roots.Pop());
        }

        /// <summary>The text captured, for an attribute or a comment, where disable-output-escaping does not apply.</summary>
        public string EndString()
        {
            hasMarkup = false;
            Flush();
            open.Pop();
            return roots.Pop().Value;
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
