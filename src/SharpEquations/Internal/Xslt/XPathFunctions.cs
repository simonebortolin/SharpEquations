#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using SharpEquations.Internal.Text;

    internal enum Axis
    {
        Child,
        Descendant,
        DescendantOrSelf,
        Parent,
        Ancestor,
        AncestorOrSelf,
        FollowingSibling,
        PrecedingSibling,
        Self,
        Attribute,
    }

    internal enum Comparison
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
    }

    /// <summary>
    /// The XPath 1.0 data model over LINQ to XML, and its functions, for the code generated from the stylesheets.
    /// The values are strings, doubles, booleans, node-sets (lists in document order, without duplicates) and
    /// result tree fragments; a value of unknown type is one of them as an object. Strings are counted, cut and
    /// translated by code points, as XPath counts characters.
    /// </summary>
    internal static class XPathFunctions
    {
        public static readonly List<XObject> Empty = new List<XObject>();

        private static readonly IComparer<XObject> DocumentOrder = Comparer<XObject>.Create(Compare);

        // conversions

        public static string Str(string value) => value;

        public static string Str(bool value) => value ? "true" : "false";

        public static string Str(double value)
        {
            if (double.IsNaN(value))
                return "NaN";
            if (double.IsPositiveInfinity(value))
                return "Infinity";
            if (double.IsNegativeInfinity(value))
                return "-Infinity";
            if (value == 0)
                return "0";
            var text = value.ToString("R", CultureInfo.InvariantCulture);
            var exponent = text.IndexOf('E');
            return exponent < 0 ? text : ExpandExponent(text, exponent);
        }

        private static string ExpandExponent(string text, int exponent)
        {
            var negative = text[0] == '-';
            var mantissa = text.Substring(negative ? 1 : 0, exponent - (negative ? 1 : 0));
            var power = int.Parse(text.Substring(exponent + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var point = mantissa.IndexOf('.');
            var digits = point < 0 ? mantissa : mantissa.Remove(point, 1);
            var integerDigits = (point < 0 ? mantissa.Length : point) + power;
            string result;
            if (integerDigits <= 0)
                result = "0." + new string('0', -integerDigits) + digits;
            else if (integerDigits >= digits.Length)
                result = digits + new string('0', integerDigits - digits.Length);
            else
                result = digits.Substring(0, integerDigits) + "." + digits.Substring(integerDigits);
            return negative ? "-" + result : result;
        }

        public static string Str(List<XObject> nodes) => nodes.Count == 0 ? "" : StringValue(nodes[0]);

        public static string Str(ResultFragment fragment) => fragment.Value;

        public static string Str(object value)
        {
            switch (value)
            {
                case string s:
                    return s;
                case double d:
                    return Str(d);
                case bool b:
                    return Str(b);
                case List<XObject> nodes:
                    return Str(nodes);
                case ResultFragment fragment:
                    return fragment.Value;
                default:
                    throw new InvalidOperationException("Not an XPath value: " + value);
            }
        }

        public static double Num(string value)
        {
            var s = value.Trim(' ', '\t', '\r', '\n');
            var i = 0;
            if (i < s.Length && s[i] == '-')
                i++;
            var digits = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                i++;
                digits++;
            }
            if (i < s.Length && s[i] == '.')
            {
                i++;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                {
                    i++;
                    digits++;
                }
            }
            if (digits == 0 || i != s.Length)
                return double.NaN;
            return double.Parse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        public static double Num(double value) => value;

        public static double Num(bool value) => value ? 1 : 0;

        public static double Num(List<XObject> nodes) => Num(Str(nodes));

        public static double Num(ResultFragment fragment) => Num(fragment.Value);

        public static double Num(object value)
        {
            switch (value)
            {
                case double d:
                    return d;
                case bool b:
                    return b ? 1 : 0;
                default:
                    return Num(Str(value));
            }
        }

        public static bool Bool(string value) => value.Length > 0;

        public static bool Bool(double value) => value != 0 && !double.IsNaN(value);

        public static bool Bool(bool value) => value;

        public static bool Bool(List<XObject> nodes) => nodes.Count > 0;

        // a result tree fragment is a node-set of its root node
        public static bool Bool(ResultFragment fragment) => true;

        public static bool Bool(object value)
        {
            switch (value)
            {
                case string s:
                    return s.Length > 0;
                case double d:
                    return Bool(d);
                case bool b:
                    return b;
                case List<XObject> nodes:
                    return nodes.Count > 0;
                case ResultFragment _:
                    return true;
                default:
                    throw new InvalidOperationException("Not an XPath value: " + value);
            }
        }

        public static List<XObject> Nodes(object value) =>
            value as List<XObject> ?? throw new InvalidOperationException("A node-set was expected, not " + (value is ResultFragment ? "a result tree fragment." : "a " + value.GetType().Name + "."));

        /// <summary>The value of a predicate: a number selects the node at that position.</summary>
        public static bool Predicate(object value, int position) =>
            value is double d ? d == position : Bool(value);

        public static string StringValue(XObject node)
        {
            switch (node)
            {
                case XElement element:
                    return element.Value;
                case XDocument document:
                    return document.Root?.Value ?? "";
                case XAttribute attribute:
                    return attribute.Value;
                case XText text:
                    return text.Value;
                case XComment comment:
                    return comment.Value;
                case XProcessingInstruction instruction:
                    return instruction.Data;
                default:
                    return "";
            }
        }

        // comparisons, as XPath 1.0 defines them for each pair of types

        public static bool Compare(object left, object right, Comparison comparison)
        {
            if (IsNodes(left))
                return IsNodes(right) ? CompareNodes(left, right, comparison) : CompareNodes(left, right, comparison, false);
            if (IsNodes(right))
                return CompareNodes(right, left, comparison, true);
            if (comparison == Comparison.Equal || comparison == Comparison.NotEqual)
            {
                bool equal;
                if (left is bool || right is bool)
                    equal = Bool(left) == Bool(right);
                else if (left is double || right is double)
                    equal = Num(left) == Num(right);
                else
                    equal = Str(left) == Str(right);
                return comparison == Comparison.Equal ? equal : !equal;
            }
            return Compare(Num(left), Num(right), comparison);
        }

        public static bool Compare(double left, double right, Comparison comparison)
        {
            switch (comparison)
            {
                case Comparison.Equal:
                    return left == right;
                case Comparison.NotEqual:
                    return left != right;
                case Comparison.Less:
                    return left < right;
                case Comparison.LessOrEqual:
                    return left <= right;
                case Comparison.Greater:
                    return left > right;
                default:
                    return left >= right;
            }
        }

        private static bool IsNodes(object value) => value is List<XObject> || value is ResultFragment;

        private static IEnumerable<string> StringValues(object value) =>
            value is ResultFragment fragment ? new[] { fragment.Value } : ((List<XObject>)value).Select(StringValue);

        private static bool CompareNodes(object left, object right, Comparison comparison)
        {
            var rightValues = StringValues(right).ToList();
            foreach (var l in StringValues(left))
            {
                foreach (var r in rightValues)
                {
                    if (CompareStrings(l, r, comparison))
                        return true;
                }
            }
            return false;
        }

        private static bool CompareNodes(object nodes, object other, Comparison comparison, bool swapped)
        {
            if (swapped)
                comparison = Swap(comparison);
            if (other is bool b)
            {
                var value = Bool(nodes);
                return comparison == Comparison.Equal || comparison == Comparison.NotEqual
                    ? (value == b) == (comparison == Comparison.Equal)
                    : Compare(value ? 1 : 0, b ? 1 : 0, comparison);
            }
            if (other is double d)
                return StringValues(nodes).Any(s => Compare(Num(s), d, comparison));
            var text = Str(other);
            return StringValues(nodes).Any(s => CompareStrings(s, text, comparison));
        }

        private static bool CompareStrings(string left, string right, Comparison comparison)
        {
            switch (comparison)
            {
                case Comparison.Equal:
                    return left == right;
                case Comparison.NotEqual:
                    return left != right;
                default:
                    return Compare(Num(left), Num(right), comparison);
            }
        }

        private static Comparison Swap(Comparison comparison)
        {
            switch (comparison)
            {
                case Comparison.Less:
                    return Comparison.Greater;
                case Comparison.LessOrEqual:
                    return Comparison.GreaterOrEqual;
                case Comparison.Greater:
                    return Comparison.Less;
                case Comparison.GreaterOrEqual:
                    return Comparison.LessOrEqual;
                default:
                    return comparison;
            }
        }

        // node-sets

        public static List<XObject> Single(XObject node) => new List<XObject>(1) { node };

        public static List<XObject> Root(XObject node)
        {
            var root = node;
            for (var parent = Parent(root); parent != null; parent = Parent(parent))
                root = parent;
            return Single(root);
        }

        public static XObject? Parent(XObject node)
        {
            switch (node)
            {
                case XAttribute attribute:
                    return attribute.Parent;
                case XDocument _:
                    return null;
                case XNode child:
                    return (XObject?)child.Parent ?? child.Document;
                default:
                    return null;
            }
        }

        public static List<XObject> Union(List<XObject> left, List<XObject> right)
        {
            if (left.Count == 0)
                return right;
            if (right.Count == 0)
                return left;
            var result = new List<XObject>(left.Count + right.Count);
            result.AddRange(left);
            result.AddRange(right);
            SortUnique(result);
            return result;
        }

        public static List<XObject> Step(List<XObject> input, Axis axis, NodeTest test)
        {
            var result = new List<XObject>();
            var attributeAxis = axis == Axis.Attribute;
            foreach (var context in input)
            {
                foreach (var node in Along(context, axis))
                {
                    if (test.Matches(node, attributeAxis))
                        result.Add(node);
                }
            }
            Order(result, input.Count, axis);
            return result;
        }

        public static List<XObject> Step(List<XObject> input, Axis axis, NodeTest test, params XPathPredicate[] predicates)
        {
            var result = new List<XObject>();
            var attributeAxis = axis == Axis.Attribute;
            var candidates = new List<XObject>();
            foreach (var context in input)
            {
                candidates.Clear();
                foreach (var node in Along(context, axis))
                {
                    if (test.Matches(node, attributeAxis))
                        candidates.Add(node);
                }
                result.AddRange(Apply(candidates, predicates));
            }
            Order(result, input.Count, axis);
            return result;
        }

        public static List<XObject> Filter(List<XObject> nodes, XPathPredicate predicate)
        {
            var result = new List<XObject>();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (predicate(nodes[i], i + 1, nodes.Count))
                    result.Add(nodes[i]);
            }
            return result;
        }

        private static List<XObject> Apply(List<XObject> candidates, XPathPredicate[] predicates)
        {
            var nodes = candidates;
            foreach (var predicate in predicates)
                nodes = Filter(nodes, predicate);
            return ReferenceEquals(nodes, candidates) ? new List<XObject>(candidates) : nodes;
        }

        private static bool IsReverse(Axis axis) =>
            axis == Axis.Ancestor || axis == Axis.AncestorOrSelf || axis == Axis.PrecedingSibling;

        private static void Order(List<XObject> nodes, int contexts, Axis axis)
        {
            if (contexts > 1)
                SortUnique(nodes);
            else if (IsReverse(axis))
                nodes.Reverse();
        }

        private static IEnumerable<XObject> Along(XObject node, Axis axis)
        {
            switch (axis)
            {
                case Axis.Child:
                    return Children(node);
                case Axis.Descendant:
                    return Descendants(node);
                case Axis.DescendantOrSelf:
                    return new[] { node }.Concat(Descendants(node));
                case Axis.Parent:
                    var parent = Parent(node);
                    return parent == null ? Enumerable.Empty<XObject>() : new[] { parent };
                case Axis.Ancestor:
                    return Ancestors(node);
                case Axis.AncestorOrSelf:
                    return new[] { node }.Concat(Ancestors(node));
                case Axis.FollowingSibling:
                    return node is XNode following && !(node is XDocument) ? following.NodesAfterSelf().Where(IsInModel) : Enumerable.Empty<XObject>();
                case Axis.PrecedingSibling:
                    return node is XNode preceding && !(node is XDocument) ? preceding.NodesBeforeSelf().Reverse().Where(IsInModel) : Enumerable.Empty<XObject>();
                case Axis.Self:
                    return new[] { node };
                default:
                    return node is XElement element ? element.Attributes().Where(a => !a.IsNamespaceDeclaration) : Enumerable.Empty<XObject>();
            }
        }

        public static List<XObject> ChildNodes(XObject node) => new List<XObject>(Children(node));

        /// <summary>
        /// Whether a node matches a step of a pattern whose predicates count positions: whether the step, from the
        /// parent of the node, selects it.
        /// </summary>
        public static bool MatchesStep(XObject node, Axis axis, NodeTest test, params XPathPredicate[] predicates)
        {
            var parent = Parent(node);
            return parent != null && test.Matches(node, axis == Axis.Attribute) && Step(Single(parent), axis, test, predicates).Contains(node);
        }

        public static IEnumerable<XObject> Children(XObject node) =>
            node is XContainer container ? container.Nodes().Where(IsInModel) : Enumerable.Empty<XObject>();

        private static IEnumerable<XObject> Descendants(XObject node) =>
            node is XContainer container ? container.DescendantNodes().Where(IsInModel) : Enumerable.Empty<XObject>();

        private static IEnumerable<XObject> Ancestors(XObject node)
        {
            for (var parent = Parent(node); parent != null; parent = Parent(parent))
                yield return parent;
        }

        // the document type and the white space around the document element are not nodes of XPath
        private static bool IsInModel(XNode node) =>
            !(node is XDocumentType) && !(node is XText && node.Parent == null);

        private static void SortUnique(List<XObject> nodes)
        {
            nodes.Sort(DocumentOrder);
            var write = 0;
            for (var read = 0; read < nodes.Count; read++)
            {
                if (write == 0 || !ReferenceEquals(nodes[read], nodes[write - 1]))
                    nodes[write++] = nodes[read];
            }
            nodes.RemoveRange(write, nodes.Count - write);
        }

        private static int Compare(XObject? left, XObject? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            // an attribute comes after its element and before the children of the element, so that two nodes are
            // in the order of their elements, unless they have the same
            var leftOwner = left is XAttribute la ? la.Parent! : (XNode)left!;
            var rightOwner = right is XAttribute ra ? ra.Parent! : (XNode)right!;
            if (!ReferenceEquals(leftOwner, rightOwner))
                return XNode.CompareDocumentOrder(leftOwner, rightOwner);
            switch (left)
            {
                case XAttribute a when right is XAttribute b:
                    return AttributeIndex(a).CompareTo(AttributeIndex(b));
                case XAttribute _:
                    return 1;
                default:
                    return -1;
            }
        }

        private static int AttributeIndex(XAttribute attribute)
        {
            var index = 0;
            for (var a = attribute.PreviousAttribute; a != null; a = a.PreviousAttribute)
                index++;
            return index;
        }

        // the functions of the XPath core library

        public static double Count(List<XObject> nodes) => nodes.Count;

        public static double Sum(List<XObject> nodes) => nodes.Sum(n => Num(StringValue(n)));

        public static string LocalName(List<XObject> nodes) => nodes.Count == 0 ? "" : LocalName(nodes[0]);

        public static string LocalName(XObject node)
        {
            switch (node)
            {
                case XElement element:
                    return element.Name.LocalName;
                case XAttribute attribute:
                    return attribute.Name.LocalName;
                case XProcessingInstruction instruction:
                    return instruction.Target;
                default:
                    return "";
            }
        }

        public static string NamespaceUri(List<XObject> nodes) => nodes.Count == 0 ? "" : NamespaceUri(nodes[0]);

        public static string NamespaceUri(XObject node)
        {
            switch (node)
            {
                case XElement element:
                    return element.Name.NamespaceName;
                case XAttribute attribute:
                    return attribute.Name.NamespaceName;
                default:
                    return "";
            }
        }

        public static string Name(List<XObject> nodes) => nodes.Count == 0 ? "" : Name(nodes[0]);

        public static string Name(XObject node)
        {
            XName name;
            XElement? scope;
            switch (node)
            {
                case XElement element:
                    name = element.Name;
                    scope = element;
                    break;
                case XAttribute attribute:
                    name = attribute.Name;
                    scope = attribute.Parent;
                    break;
                default:
                    return LocalName(node);
            }
            var prefix = name.Namespace == XNamespace.None ? null : scope?.GetPrefixOfNamespace(name.Namespace);
            return string.IsNullOrEmpty(prefix) ? name.LocalName : prefix + ":" + name.LocalName;
        }

        public static bool StartsWith(string s, string prefix) => s.StartsWith(prefix, StringComparison.Ordinal);

        public static bool Contains(string s, string part) => s.Contains(part);

        public static string SubstringBefore(string s, string part)
        {
            var index = s.IndexOf(part, StringComparison.Ordinal);
            return index < 0 ? "" : s.Substring(0, index);
        }

        public static string SubstringAfter(string s, string part)
        {
            var index = s.IndexOf(part, StringComparison.Ordinal);
            return index < 0 ? "" : s.Substring(index + part.Length);
        }

        public static string Substring(string s, double start) => Substring(s, start, double.PositiveInfinity);

        public static string Substring(string s, double start, double length)
        {
            var first = Round(start);
            var end = first + Round(length);
            var result = new StringBuilder();
            var position = 0;
            foreach (var rune in CodePoints.Of(s))
            {
                position++;
                if (position >= first && position < end)
                    result.Append(rune.ToString());
            }
            return result.ToString();
        }

        public static double StringLength(string s) => CodePoints.Of(s).Count();

        public static string NormalizeSpace(string s)
        {
            var result = new StringBuilder(s.Length);
            var space = false;
            foreach (var c in s)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    space = result.Length > 0;
                    continue;
                }
                if (space)
                    result.Append(' ');
                space = false;
                result.Append(c);
            }
            return result.ToString();
        }

        public static string Translate(string s, string from, string to)
        {
            var source = CodePoints.Of(from).ToList();
            var target = CodePoints.Of(to).ToList();
            var result = new StringBuilder(s.Length);
            foreach (var rune in CodePoints.Of(s))
            {
                var index = source.IndexOf(rune);
                if (index < 0)
                    result.Append(rune.ToString());
                else if (index < target.Count)
                    result.Append(target[index].ToString());
            }
            return result.ToString();
        }

        public static double Round(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value == 0)
                return value;
            if (value < 0 && value >= -0.5)
                return -0.0;
            return Math.Floor(value + 0.5);
        }

        // XSLT

        /// <summary>The expanded name of a QName computed at run time, with the namespaces in scope where it was written.</summary>
        public static XName ResolveName(string qname, string defaultNamespace, string[] prefixesAndNamespaces)
        {
            var colon = qname.IndexOf(':');
            if (colon < 0)
                return XName.Get(qname, defaultNamespace);
            var prefix = qname.Substring(0, colon);
            for (var i = 0; i < prefixesAndNamespaces.Length; i += 2)
            {
                if (prefixesAndNamespaces[i] == prefix)
                    return XName.Get(qname.Substring(colon + 1), prefixesAndNamespaces[i + 1]);
            }
            throw new InvalidOperationException("The prefix '" + prefix + "' is not declared.");
        }
    }
}
