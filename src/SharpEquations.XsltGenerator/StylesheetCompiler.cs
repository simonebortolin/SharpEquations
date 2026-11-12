namespace SharpEquations.XsltGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// What a compilation of the stylesheet takes as known of its templates, parameters and global variables, and
    /// learns of them: the stylesheet is compiled again, with what the compilation before learnt, until nothing new
    /// is learnt. Every fact starts as the one that gives the best code and only moves to the one that is always
    /// right, so the last compilation holds.
    /// </summary>
    internal sealed class Facts
    {
        /// <summary>The types of the values given to the parameters of the named templates.</summary>
        public Dictionary<string, XType> ParameterTypes { get; } = new Dictionary<string, XType>();

        /// <summary>The parameters of the named templates that a call leaves to their default.</summary>
        public HashSet<string> Omitted { get; } = new HashSet<string>();

        /// <summary>The named templates that read the context node, its position, its size.</summary>
        public HashSet<string> NodeUsed { get; } = new HashSet<string>();

        public HashSet<string> PositionUsed { get; } = new HashSet<string>();

        public HashSet<string> SizeUsed { get; } = new HashSet<string>();

        /// <summary>The modes with a template that reads the position of the node, the size.</summary>
        public HashSet<string> ModePosition { get; } = new HashSet<string>();

        public HashSet<string> ModeSize { get; } = new HashSet<string>();

        /// <summary>The named templates that write more than text.</summary>
        public HashSet<string> NotText { get; } = new HashSet<string>();

        /// <summary>The named templates that read the input, or a global variable that depends on it.</summary>
        public HashSet<string> Impure { get; } = new HashSet<string>();

        /// <summary>The global variables that depend on the input, or are not strings.</summary>
        public HashSet<string> Dynamic { get; } = new HashSet<string>();

        private IEnumerable<HashSet<string>> Sets => new[] { Omitted, NodeUsed, PositionUsed, SizeUsed, ModePosition, ModeSize, NotText, Impure, Dynamic };

        public static string Parameter(string template, string parameter) => template + "\n" + parameter;

        public static XType Join(XType left, XType right)
        {
            if (left == XType.None)
                return right;
            if (right == XType.None || left == right)
                return left;
            // a node is a node-set too
            return (left == XType.Node && right == XType.NodeSet) || (left == XType.NodeSet && right == XType.Node) ? XType.NodeSet : XType.Any;
        }

        public void Give(string parameter, XType type) =>
            ParameterTypes[parameter] = Join(ParameterTypes.TryGetValue(parameter, out var known) ? known : XType.None, type);

        /// <summary>Adds what another compilation learnt; whether it was anything new.</summary>
        public bool Add(Facts other)
        {
            var before = Count;
            foreach (var parameter in other.ParameterTypes)
                Give(parameter.Key, parameter.Value);
            foreach (var pair in Sets.Zip(other.Sets, (mine, theirs) => (mine, theirs)))
                pair.mine.UnionWith(pair.theirs);
            return Count != before;
        }

        // every fact moves one way, so the count tells whether one moved
        private int Count => Sets.Sum(s => s.Count) + ParameterTypes.Values.Sum(t => t == XType.None ? 0 : t == XType.Any ? 3 : t == XType.NodeSet ? 2 : 1);
    }

    /// <summary>
    /// An XSLT 1.0 stylesheet compiled to a C# class: a method for each template, with typed parameters for the named
    /// ones; the dispatch of <c>xsl:apply-templates</c> for each mode (by the name of the element first); a lazy
    /// property for each global variable, computed once for all when it is a string that does not depend on the
    /// input; a method for each expression that walks the tree. What the stylesheets of SharpEquations do not use
    /// (keys, sorting, numbering, imports, extension functions) is reported as an error.
    /// </summary>
    internal sealed class StylesheetCompiler
    {
        private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

        private readonly XElement stylesheet;
        private readonly string className;
        private readonly string fileName;
        private readonly Facts facts;
        private readonly XPathCompiler xpath;
        private readonly Names locals = new Names();

        private readonly CodeWriter nameFields = new CodeWriter(2);
        private readonly CodeWriter staticFields = new CodeWriter(2);
        private readonly CodeWriter instanceFields = new CodeWriter(2);
        private readonly CodeWriter members = new CodeWriter(2);

        private readonly Dictionary<string, string> names = new Dictionary<string, string>();
        private readonly Dictionary<string, string> helpers = new Dictionary<string, string>();
        private readonly Dictionary<string, string> statics = new Dictionary<string, string>();
        private readonly Dictionary<string, NamedTemplate> namedTemplates = new Dictionary<string, NamedTemplate>();
        private readonly Dictionary<string, GlobalVariable> globals = new Dictionary<string, GlobalVariable>();
        private readonly Dictionary<string, int> modes = new Dictionary<string, int> { [""] = 0 };
        private readonly List<Rule> rules = new List<Rule>();

        private sealed class NamedTemplate
        {
            public NamedTemplate(string name, string method, List<string> parameters, bool matches)
            {
                Name = name;
                Method = method;
                Parameters = parameters;
                Matches = matches;
            }

            public string Name { get; }

            public string Method { get; }

            public List<string> Parameters { get; }

            /// <summary>Whether the template has a pattern too, and takes its parameters from xsl:apply-templates.</summary>
            public bool Matches { get; }
        }

        private sealed class GlobalVariable
        {
            public GlobalVariable(XElement element) => Element = element;

            public XElement Element { get; }

            public Variable? Variable { get; set; }

            public bool Compiling { get; set; }
        }

        private enum Bucket
        {
            Root,
            Element,
            NamedElement,
            Attribute,
            Text,
            Comment,
            ProcessingInstruction,
        }

        private sealed class PatternStep
        {
            public PatternStep(Step step, bool afterDescendant)
            {
                Step = step;
                AfterDescendant = afterDescendant;
            }

            public Step Step { get; }

            /// <summary>Whether the step follows <c>//</c> rather than <c>/</c>.</summary>
            public bool AfterDescendant { get; }
        }

        private sealed class Rule
        {
            public Rule(int index, XElement template, string method, int mode, double priority, bool isRoot, bool absolute, List<PatternStep> steps)
            {
                Index = index;
                Template = template;
                Method = method;
                Mode = mode;
                Priority = priority;
                IsRoot = isRoot;
                Absolute = absolute;
                Steps = steps;
            }

            public int Index { get; }

            public XElement Template { get; }

            public string Method { get; }

            public int Mode { get; }

            public double Priority { get; }

            public bool IsRoot { get; }

            public bool Absolute { get; }

            public List<PatternStep> Steps { get; }

            public Step? Last => Steps.Count == 0 ? null : Steps[Steps.Count - 1].Step;

            /// <summary>Whether the rule matches every node it is tried on, those its last step tells by their kind or name.</summary>
            public bool Always { get; set; }
        }

        public StylesheetCompiler(XElement stylesheet, string className, string fileName, Facts facts)
        {
            this.stylesheet = stylesheet;
            this.className = className;
            this.fileName = fileName;
            this.facts = facts;
            xpath = new XPathCompiler(this);
        }

        /// <summary>What this compilation learnt.</summary>
        public Facts Learnt { get; } = new Facts();

        public string NewName(string prefix) => locals.New(prefix);

        public static XsltException Error(XObject node, string message)
        {
            var info = (IXmlLineInfo)node;
            return new XsltException(message) { Line = info.HasLineInfo() ? info.LineNumber : 0, Column = info.HasLineInfo() ? info.LinePosition : 0 };
        }

        // static fields: names, node tests, tables

        public string Name(XName name)
        {
            var key = name.ToString();
            if (!names.TryGetValue(key, out var field))
            {
                field = "Name" + names.Count;
                names.Add(key, field);
                nameFields.Line("private static readonly XName " + field + " = XName.Get(" + XPathCompiler.Literal(name.LocalName) + ", " + XPathCompiler.Literal(name.NamespaceName) + ");");
            }
            return field;
        }

        /// <summary>A static field of the class with this value, the same for the same value.</summary>
        public string StaticField(string type, string init)
        {
            var key = type + " " + init;
            if (!statics.TryGetValue(key, out var field))
            {
                field = "Static" + statics.Count;
                statics.Add(key, field);
                staticFields.Line("private static readonly " + type + " " + field + " = " + init + ";");
            }
            return field;
        }

        /// <summary>A static field for a value kept from one use to the next.</summary>
        public string CacheField(string type)
        {
            var field = NewName("Cache");
            staticFields.Line("private static " + type + " " + field + ";");
            return field;
        }

        /// <summary>A method of the class with this body, the same for the same body; its name.</summary>
        public string HelperMethod(string returnType, string parameters, string body)
        {
            var key = returnType + " (" + parameters + ")\n" + body;
            if (!helpers.TryGetValue(key, out var method))
            {
                method = "Path" + helpers.Count;
                helpers.Add(key, method);
                members.Line().Open("private " + returnType + " " + method + "(" + parameters + ")").Raw(body).Close();
            }
            return method;
        }

        public static string Namespace(string? prefix, XElement scope)
        {
            if (prefix == null)
                return "";
            var ns = scope.GetNamespaceOfPrefix(prefix);
            if (ns == null)
                throw Error(scope, "The prefix '" + prefix + "' is not declared.");
            return ns.NamespaceName;
        }

        // the stylesheet

        public string Compile()
        {
            if (stylesheet.Name != Xsl + "stylesheet" && stylesheet.Name != Xsl + "transform")
                throw Error(stylesheet, "Not an XSLT stylesheet.");

            var templates = new List<XElement>();
            foreach (var element in stylesheet.Elements())
            {
                if (element.Name.Namespace != Xsl)
                    continue;
                switch (element.Name.LocalName)
                {
                    case "template":
                        templates.Add(element);
                        break;
                    case "variable":
                    case "param":
                        globals[Attr(element, "name", true)!] = new GlobalVariable(element);
                        break;
                    case "output":
                        break;
                    default:
                        throw Error(element, "xsl:" + element.Name.LocalName + " is not supported.");
                }
            }

            foreach (var mode in stylesheet.Descendants().Where(e => e.Name == Xsl + "apply-templates" || e.Name == Xsl + "template"))
                ModeIndex((string?)mode.Attribute("mode"));

            for (var i = 0; i < templates.Count; i++)
            {
                var name = Attr(templates[i], "name", false);
                if (name != null)
                    namedTemplates[name] = new NamedTemplate(name, "Named" + i, TemplateParameters(templates[i]).Select(p => Attr(p, "name", true)!).ToList(), Attr(templates[i], "match", false) != null);
            }

            for (var i = 0; i < templates.Count; i++)
                CompileTemplate(templates[i], i);

            for (var mode = 0; mode < modes.Count; mode++)
                CompileDispatch(mode);

            return Assemble();
        }

        private int ModeIndex(string? mode)
        {
            var key = mode ?? "";
            if (!modes.TryGetValue(key, out var index))
            {
                index = modes.Count;
                modes.Add(key, index);
            }
            return index;
        }

        private static string? Attr(XElement element, string name, bool required)
        {
            var value = (string?)element.Attribute(name);
            if (value == null && required)
                throw Error(element, "xsl:" + element.Name.LocalName + " needs the attribute " + name + ".");
            return value;
        }

        private static IEnumerable<XElement> TemplateParameters(XElement template) =>
            template.Elements().TakeWhile(e => e.Name == Xsl + "param");

        private string Assemble()
        {
            var file = new CodeWriter();
            file.Line("// <auto-generated/>")
                .Line("// " + fileName + ", compiled by SharpEquations.XsltGenerator")
                .Line("#nullable enable")
                .Line("#pragma warning disable")
                .Line()
                .Open("namespace SharpEquations.Internal.Xslt")
                .Line("using System.Collections.Generic;")
                .Line("using System.Xml.Linq;")
                .Line("using static SharpEquations.Internal.Xslt.XPathFunctions;")
                .Line()
                .Line("// the stylesheet " + fileName + ", compiled to C#")
                .Open("internal sealed partial class " + className);
            file.Append(nameFields).Append(staticFields);
            file.Line()
                .Line("private readonly XsltOutput output = new XsltOutput();")
                .Line("private readonly XObject root;")
                .Append(instanceFields)
                .Line()
                .Line("private " + className + "(XObject root) => this.root = root;")
                .Line()
                .Line("/// <summary>Runs the stylesheet on the document of the element (the element itself, unless it has a parent).</summary>")
                .Open("public static partial XElement? Transform(XElement input)")
                .Line("var document = new XDocument(input);")
                .Line("var stylesheet = new " + className + "(document);")
                .Line("stylesheet.Dispatch0(document, 1, 1, null);")
                .Line("return stylesheet.output.Result();")
                .Close();
            file.Append(members);
            file.Close().Close();
            return file.ToString();
        }

        // templates

        private void CompileTemplate(XElement template, int index)
        {
            var name = Attr(template, "name", false);
            var match = Attr(template, "match", false);
            if (name == null && match == null)
                throw Error(template, "A template needs a name or a match pattern.");

            var parameters = TemplateParameters(template).ToList();
            if (name != null)
            {
                var named = namedTemplates[name];
                var types = named.Parameters.Select(p => ParameterType(named, p)).ToList();
                if (named.Matches)
                {
                    foreach (var parameter in named.Parameters)
                        Learnt.Give(Facts.Parameter(name, parameter), XType.Any);
                }
                var signature = string.Concat(named.Parameters.Select((p, i) => ", " + XPathCompiler.TypeName(types[i]) + "? a" + i));
                var focus = new Focus();
                var purity = new Purity();
                var w = new CodeWriter(2);
                w.Line().Open("private void " + named.Method + "(XObject n0, int p0, int s0" + signature + ")");
                CompileBody(template, parameters, i => "a" + i, i => types[i], named, focus, purity, w);
                w.Close();
                members.Append(w);
                if (focus.NodeUsed || focus.CurrentUsed)
                    Learnt.NodeUsed.Add(name);
                if (focus.PositionUsed)
                    Learnt.PositionUsed.Add(name);
                if (focus.SizeUsed)
                    Learnt.SizeUsed.Add(name);
                if (purity.Impure)
                    Learnt.Impure.Add(name);
                if (!IsTextOnly(template.Nodes()))
                    Learnt.NotText.Add(name);
            }

            if (match == null)
                return;

            var mode = ModeIndex((string?)template.Attribute("mode"));
            var method = "Template" + index;
            var body = new CodeWriter(2);
            body.Line().Open("private void " + method + "(XObject n0, int p0, int s0, TemplateArguments? args)");
            var templateFocus = new Focus();
            if (name != null)
            {
                var named = namedTemplates[name];
                var context = TemplateContext(template, templateFocus, new Purity(), new Scope(null));
                body.Line(Call(named, context, named.Parameters.Select(p => "args?.Get(" + XPathCompiler.Literal(p) + ")")));
            }
            else
            {
                var names = parameters.Select(p => Attr(p, "name", true)!).ToList();
                CompileBody(template, parameters, i => "args?.Get(" + XPathCompiler.Literal(names[i]) + ")", i => XType.Any, null, templateFocus, new Purity(), body);
            }
            body.Close();
            members.Append(body);
            if (templateFocus.PositionUsed)
                Learnt.ModePosition.Add(ModeKey(mode));
            if (templateFocus.SizeUsed)
                Learnt.ModeSize.Add(ModeKey(mode));

            var priority = (string?)template.Attribute("priority");
            foreach (var alternative in Alternatives(XPathParser.Parse(match), template))
            {
                var steps = new List<PatternStep>();
                var afterDescendant = false;
                foreach (var step in alternative.Steps)
                {
                    if (step.IsDescendantOrSelfNode)
                    {
                        afterDescendant = true;
                        continue;
                    }
                    if (step.Axis != "child" && step.Axis != "attribute")
                        throw Error(template, "The axis " + step.Axis + " cannot be used in a pattern.");
                    steps.Add(new PatternStep(step, afterDescendant));
                    afterDescendant = false;
                }
                var isRoot = alternative.Absolute && steps.Count == 0;
                // a leading // anchors nothing: the pattern matches at any depth
                var absolute = alternative.Absolute && steps.Count > 0 && !steps[0].AfterDescendant;
                var rule = new Rule(rules.Count, template, method, mode,
                    priority != null ? double.Parse(priority, System.Globalization.CultureInfo.InvariantCulture) : DefaultPriority(alternative, steps),
                    isRoot, absolute, steps);
                rules.Add(rule);
                CompileMatch(rule);
            }
        }

        private static string ModeKey(int mode) => mode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private Context TemplateContext(XElement element, Focus focus, Purity purity, Scope scope) =>
            new Context("n0", NodeKind.Object, "p0", "s0", focus, "n0", NodeKind.Object, focus, scope, element, locals, purity);

        /// <summary>The type of a parameter of a named template: that of every value it is given, if they have the same.</summary>
        private XType ParameterType(NamedTemplate template, string parameter)
        {
            if (template.Matches)
                return XType.Any;
            return facts.ParameterTypes.TryGetValue(Facts.Parameter(template.Name, parameter), out var type) ? type : XType.None;
        }

        /// <summary>
        /// The call of a named template, which is given the context node, its position and its size only if it reads them.
        /// </summary>
        private string Call(NamedTemplate template, Context context, IEnumerable<string> arguments)
        {
            if (facts.Impure.Contains(template.Name))
                context.Purity.Impure = true;
            var node = facts.NodeUsed.Contains(template.Name) ? context.Node : "null";
            var position = facts.PositionUsed.Contains(template.Name) ? context.Position : "0";
            var size = facts.SizeUsed.Contains(template.Name) ? context.Size : "0";
            return template.Method + "(" + node + ", " + position + ", " + size + string.Concat(arguments.Select(a => ", " + a)) + ");";
        }

        /// <summary>The value as a parameter of the type takes it.</summary>
        private static string Convert(Compiled value, XType type)
        {
            switch (type)
            {
                case XType.Any:
                case XType.None:
                    return XPathCompiler.Box(value);
                case var _ when type == value.Type:
                    return value.Code;
                case XType.NodeSet when value.Type == XType.Node:
                    return "ListOf(" + value.Code + ")";
                case XType.Node:
                    return "null";
                // no value of another type reaches a parameter whose type is known
                case XType.Number:
                    return "0D";
                case XType.Boolean:
                    return "false";
                case XType.NodeSet:
                    return "Empty";
                case XType.Fragment:
                    return "null!";
                default:
                    return "\"\"";
            }
        }

        private static IEnumerable<PathExpr> Alternatives(Expr pattern, XElement template)
        {
            switch (pattern)
            {
                case BinaryExpr union when union.Op == "|":
                    return Alternatives(union.Left, template).Concat(Alternatives(union.Right, template));
                case PathExpr path when path.Start == null:
                    return new[] { path };
                default:
                    throw Error(template, "Not a pattern: " + (string?)template.Attribute("match"));
            }
        }

        private static double DefaultPriority(PathExpr alternative, List<PatternStep> steps)
        {
            if (alternative.Absolute || steps.Count != 1 || steps[0].Step.Predicates.Count > 0)
                return 0.5;
            var test = steps[0].Step.Test;
            switch (test.Kind)
            {
                case TestKind.Name:
                    return 0;
                case TestKind.Namespace:
                    return -0.25;
                case TestKind.ProcessingInstruction when test.Local != null:
                    return 0;
                default:
                    return -0.5;
            }
        }

        private void CompileBody(XElement template, List<XElement> parameters, System.Func<int, string> argument, System.Func<int, XType> type, NamedTemplate? named,
            Focus focus, Purity purity, CodeWriter w)
        {
            var scope = new Scope(null);
            var context = TemplateContext(template, focus, purity, scope);
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var name = Attr(parameter, "name", true)!;
                var id = NewName("v");
                var paramContext = context.WithElement(parameter);
                var select = Attr(parameter, "select", false);
                var content = select == null && HasContent(parameter);
                var valueType = type(i) == XType.Number || type(i) == XType.Boolean;

                // the default is one of the values of the parameter if a call leaves the parameter to it
                void Default(XType defaultType)
                {
                    if (named != null && facts.Omitted.Contains(Facts.Parameter(named.Name, name)))
                        Learnt.Give(Facts.Parameter(named.Name, name), defaultType);
                }

                if (select != null)
                {
                    var value = xpath.Compile(select, paramContext).Bound();
                    Default(value.Type);
                    // a node that is null is an empty node-set, not a parameter left to its default
                    w.Line(type(i) == XType.Node
                        ? "var " + id + " = ReferenceEquals(" + argument(i) + ", Omitted) ? " + Convert(value, type(i)) + " : " + argument(i) + ";"
                        : "var " + id + " = " + argument(i) + " ?? " + Convert(value, type(i)) + ";");
                }
                else if (type(i) == XType.Node)
                {
                    Default(content ? XType.Fragment : XType.String);
                    w.Line("var " + id + " = ReferenceEquals(" + argument(i) + ", Omitted) ? null : " + argument(i) + ";");
                }
                else if (content && !valueType)
                {
                    w.Line("var " + id + " = " + argument(i) + ";");
                    w.Open("if (" + id + " == null)");
                    var value = Capture(parameter, paramContext, w);
                    Default(value.Type);
                    w.Line(id + " = " + Convert(value, type(i)) + ";");
                    w.Close();
                }
                else
                {
                    Default(content ? XType.Fragment : XType.String);
                    w.Line("var " + id + " = " + argument(i) + " ?? " + Convert(new Compiled("\"\"", XType.String), type(i)) + ";");
                }
                scope.Add(name, new Variable(id, type(i), type(i) == XType.Node ? NodeOrder.Single : NodeOrder.Ordered));
            }
            CompileSequence(template.Nodes().Where(n => !(n is XElement e && parameters.Contains(e))), context, w);
        }

        /// <summary>
        /// Whether the instructions write only text, so that what they write can be kept as a string: no element, no
        /// text written as markup, no template applied, and only named templates that write text called.
        /// </summary>
        private bool IsTextOnly(IEnumerable<XNode> nodes)
        {
            foreach (var element in nodes.OfType<XElement>())
            {
                if (element.Name.Namespace != Xsl)
                    return false;
                switch (element.Name.LocalName)
                {
                    case "text":
                    case "value-of":
                        if (DisablesEscaping(element))
                            return false;
                        break;
                    case "variable":
                    case "param":
                    case "message":
                    case "fallback":
                        break;
                    case "if":
                    case "choose":
                    case "when":
                    case "otherwise":
                    case "for-each":
                        if (!IsTextOnly(element.Nodes()))
                            return false;
                        break;
                    case "call-template":
                        if (facts.NotText.Contains((string?)element.Attribute("name") ?? ""))
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// What the instructions write as a C# expression of a string, if they are only text, values and choices
        /// between them: null if they need statements.
        /// </summary>
        private string? TextExpression(IEnumerable<XNode> nodes, Context context)
        {
            var parts = new List<string>();
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case XText text when text.Value.Trim().Length == 0:
                        break;
                    case XText text:
                        parts.Add(XPathCompiler.Literal(text.Value));
                        break;
                    case XElement element when element.Name == Xsl + "text" && !DisablesEscaping(element):
                        if (element.Value.Length > 0)
                            parts.Add(XPathCompiler.Literal(element.Value));
                        break;
                    case XElement element when element.Name == Xsl + "value-of" && !DisablesEscaping(element) && element.Attribute("select") != null:
                        parts.Add(XPathCompiler.Str(xpath.Compile(Attr(element, "select", true)!, context.WithElement(element))));
                        break;
                    case XElement element when element.Name == Xsl + "if" && element.Attribute("test") != null:
                    {
                        var then = TextExpression(element.Nodes(), context.WithElement(element));
                        if (then == null)
                            return null;
                        parts.Add("(" + XPathCompiler.Bool(xpath.Compile(Attr(element, "test", true)!, context.WithElement(element))) + " ? " + then + " : \"\")");
                        break;
                    }
                    case XElement element when element.Name == Xsl + "choose":
                    {
                        var choice = new StringBuilder();
                        var otherwise = "\"\"";
                        foreach (var branch in element.Elements())
                        {
                            var value = TextExpression(branch.Nodes(), context.WithElement(branch));
                            if (value == null)
                                return null;
                            if (branch.Name == Xsl + "when" && branch.Attribute("test") != null)
                                choice.Append(XPathCompiler.Bool(xpath.Compile(Attr(branch, "test", true)!, context.WithElement(branch)))).Append(" ? ").Append(value).Append(" : ");
                            else if (branch.Name == Xsl + "otherwise" && branch == element.Elements().Last())
                                otherwise = value;
                            else
                                return null;
                        }
                        parts.Add("(" + choice + otherwise + ")");
                        break;
                    }
                    case XElement _:
                        return null;
                }
            }
            switch (parts.Count)
            {
                case 0:
                    return "\"\"";
                case 1:
                    return parts[0];
                default:
                    return "string.Concat(" + string.Join(", ", parts) + ")";
            }
        }

        /// <summary>
        /// The content of a variable or parameter as its value: a string when it only writes text, a result tree
        /// fragment otherwise.
        /// </summary>
        private Compiled Capture(XElement element, Context context, CodeWriter w)
        {
            if (!IsTextOnly(element.Nodes()))
            {
                var fragment = NewName("f");
                w.Line("output.BeginCapture();");
                CompileSequence(element.Nodes(), context, w);
                w.Line("var " + fragment + " = output.EndFragment();");
                return new Compiled(fragment, XType.Fragment);
            }

            var expression = TextExpression(element.Nodes(), context);
            if (expression != null)
                return new Compiled(expression, XType.Text);
            var text = NewName("t");
            w.Line("output.BeginText();");
            CompileSequence(element.Nodes(), context, w);
            w.Line("var " + text + " = output.EndText();");
            return new Compiled(text, XType.Text);
        }

        private static bool HasContent(XElement element) =>
            element.Nodes().Any(n => n is XElement || (n is XText t && t.Value.Trim().Length > 0));

        // patterns

        private void CompileMatch(Rule rule)
        {
            var w = new CodeWriter(2);
            if (rule.IsRoot)
            {
                w.Line().Line("private static bool Match" + rule.Index + "(XObject node) => node is XDocument;");
                members.Append(w);
                return;
            }

            for (var i = 0; i < rule.Steps.Count; i++)
            {
                var step = rule.Steps[i].Step;
                var focus = new Focus();
                var context = new Context("node", NodeKind.Object, null, null, focus, "node", NodeKind.Object, focus, new Scope(null), rule.Template, new Names(), new Purity());
                var test = MatchStep(step, context);

                w.Line().Open("private bool Match" + rule.Index + "_" + i + "(XObject node)");
                if (test != null)
                    w.Line("if (!(" + test + "))").Line("    return false;");
                if (i == 0)
                {
                    w.Line(rule.Absolute ? "return Parent(node) is XDocument;" : "return true;");
                }
                else if (rule.Steps[i].AfterDescendant)
                {
                    w.Line("for (var ancestor = Parent(node); ancestor != null; ancestor = Parent(ancestor))");
                    w.Open().Line("if (Match" + rule.Index + "_" + (i - 1) + "(ancestor))").Line("    return true;").Close();
                    w.Line("return false;");
                }
                else
                {
                    w.Line("var parent = Parent(node);");
                    w.Line("return parent != null && Match" + rule.Index + "_" + (i - 1) + "(parent);");
                }
                w.Close();
            }
            w.Line().Line("private bool Match" + rule.Index + "(XObject node) => Match" + rule.Index + "_" + (rule.Steps.Count - 1) + "(node);");
            members.Append(w);

            var last = rule.Last!;
            if (rule.Steps.Count == 1 && !rule.Absolute && last.Predicates.Count == 0)
            {
                switch (last.Test.Kind)
                {
                    case TestKind.AnyName:
                    case TestKind.Node:
                        rule.Always = true;
                        break;
                    case TestKind.Namespace:
                        break;
                    case TestKind.ProcessingInstruction:
                        rule.Always = last.Test.Local == null;
                        break;
                    default:
                        rule.Always = last.Axis != "attribute";
                        break;
                }
            }
        }

        /// <summary>
        /// Whether the node, <c>node</c> in the generated code, is one the step of a pattern selects from its parent: the
        /// C# condition, or null if every node is.
        /// </summary>
        private string? MatchStep(Step step, Context context)
        {
            var attributeAxis = step.Axis == "attribute";
            if (step.Predicates.Any(p => xpath.IsPositional(p, context)))
            {
                // the predicates count the node among those the step selects from its parent
                var body = new CodeWriter(3);
                body.Line("var parent = Parent(node);").Line("if (parent == null)").Line("    return false;");
                xpath.EmitStep(new Cursor("parent", NodeKind.Object), step, context, body,
                    cursor => body.Line("if (ReferenceEquals(" + cursor.Var + ", node))").Line("    return true;"));
                body.Line("return false;");
                return HelperMethod("bool", "XObject node", body.ToString()) + "(node)";
            }

            XPathCompiler.Tested tested;
            string? condition;
            if (attributeAxis)
            {
                var attribute = context.Names.New("a_");
                tested = xpath.Test(step.Test, true, new Cursor(attribute, NodeKind.Attribute), false, context);
                condition = "node is XAttribute " + attribute + (tested.Condition == null ? "" : " && " + tested.Condition);
            }
            else
            {
                tested = xpath.Test(step.Test, false, new Cursor("node", NodeKind.Object), false, context);
                condition = tested.Condition;
            }
            if (tested.Never)
                return "false";
            foreach (var predicate in step.Predicates)
            {
                var value = XPathCompiler.Bool(xpath.Compile(predicate, context.WithFocus(tested.Cursor.Var, tested.Cursor.Kind, null, null, new Focus())));
                condition = condition == null ? value : condition + " && " + value;
            }
            return condition;
        }

        private static Bucket[] Buckets(Rule rule)
        {
            if (rule.IsRoot)
                return new[] { Bucket.Root };
            var last = rule.Last!;
            if (last.Axis == "attribute")
                return new[] { Bucket.Attribute };
            switch (last.Test.Kind)
            {
                case TestKind.Name:
                    return new[] { Bucket.NamedElement };
                case TestKind.Namespace:
                case TestKind.AnyName:
                    return new[] { Bucket.Element };
                case TestKind.Node:
                    return new[] { Bucket.Element, Bucket.Text, Bucket.Comment, Bucket.ProcessingInstruction };
                case TestKind.Text:
                    return new[] { Bucket.Text };
                case TestKind.Comment:
                    return new[] { Bucket.Comment };
                default:
                    return new[] { Bucket.ProcessingInstruction };
            }
        }

        private void CompileDispatch(int mode)
        {
            // the rule that wins is the one of highest priority and, among those, the last in the stylesheet
            var ordered = rules.Where(r => r.Mode == mode).OrderByDescending(r => r.Priority).ThenByDescending(r => r.Index).ToList();
            List<Rule> In(Bucket bucket) => ordered.Where(r => Buckets(r).Contains(bucket)).ToList();
            var position = facts.ModePosition.Contains(ModeKey(mode));
            var size = facts.ModeSize.Contains(ModeKey(mode));

            var w = new CodeWriter(2);
            w.Line().Open("private void Apply" + mode + "(List<XObject> nodes, TemplateArguments? args)");
            w.Line("for (var i = 0; i < nodes.Count; i++)").Line("    Dispatch" + mode + "(nodes[i], i + 1, nodes.Count, args);");
            w.Close();

            // the children of a node, without a list of them unless a template reads how many they are
            w.Line().Open("private void ApplyChildren" + mode + "(XObject node, TemplateArguments? args)");
            if (size)
            {
                w.Line("Apply" + mode + "(ChildNodes(node), args);");
            }
            else
            {
                if (position)
                    w.Line("var position = 0;");
                w.Open("for (var child = FirstChild(node); child != null; child = child.NextNode)");
                w.Line("if (IsNode(child))").Line("    Dispatch" + mode + "(child, " + (position ? "++position" : "0") + ", 0, args);");
                w.Close();
            }
            w.Close();

            w.Line().Open("private void Dispatch" + mode + "(XObject node, int position, int size, TemplateArguments? args)");
            w.Open("switch (node)");

            w.Line("case XElement element:").Open();
            var named = ordered.Where(r => Buckets(r).Contains(Bucket.NamedElement)).ToList();
            var elementNames = named.Select(r => XName.Get(r.Last!.Test.Local!, Namespace(r.Last.Test.Prefix, r.Template))).Distinct().ToList();
            var generic = In(Bucket.Element);
            if (elementNames.Count > 0)
            {
                var table = "Elements" + mode;
                staticFields.Line("private static readonly Dictionary<XName, int> " + table + " = new Dictionary<XName, int>")
                    .Line("{");
                for (var i = 0; i < elementNames.Count; i++)
                    staticFields.Line("    [" + Name(elementNames[i]) + "] = " + i + ",");
                staticFields.Line("};");
                w.Open("switch (" + table + ".TryGetValue(element.Name, out var index) ? index : -1)");
                for (var i = 0; i < elementNames.Count; i++)
                {
                    var name = elementNames[i];
                    w.Line("case " + i + ":").Open();
                    var candidates = ordered.Where(r => generic.Contains(r)
                        || (named.Contains(r) && XName.Get(r.Last!.Test.Local!, Namespace(r.Last.Test.Prefix, r.Template)) == name));
                    EmitCandidates(candidates, w, true);
                    w.Line("break;").Close();
                }
                w.Line("default:").Open();
                EmitCandidates(generic, w, true);
                w.Line("break;").Close();
                w.Close();
            }
            else
            {
                EmitCandidates(generic, w, true);
            }
            w.Line("ApplyChildren" + mode + "(node, null);").Line("return;").Close();

            w.Line("case XDocument _:").Open();
            EmitCandidates(In(Bucket.Root), w);
            w.Line("ApplyChildren" + mode + "(node, null);").Line("return;").Close();

            w.Line("case XAttribute _:").Open();
            EmitCandidates(In(Bucket.Attribute), w);
            w.Line("output.Text(StringValue(node));").Line("return;").Close();

            w.Line("case XText _:").Open();
            EmitCandidates(In(Bucket.Text), w);
            w.Line("output.Text(StringValue(node));").Line("return;").Close();

            w.Line("case XComment _:").Open();
            EmitCandidates(In(Bucket.Comment), w);
            w.Line("return;").Close();

            w.Line("case XProcessingInstruction _:").Open();
            EmitCandidates(In(Bucket.ProcessingInstruction), w);
            w.Line("return;").Close();

            w.Close().Close();
            members.Append(w);
        }

        /// <summary>
        /// The child and the text a rule asks of an element, if its pattern starts its first predicate with
        /// <c>child = 'text'</c>: the rule can only match an element that has such a child.
        /// </summary>
        private static (XName Child, string Text)? KeyOf(Rule rule)
        {
            var last = rule.Last;
            if (last == null || last.Axis != "child" || last.Predicates.Count == 0)
                return null;
            var condition = last.Predicates[0];
            while (condition is BinaryExpr and && and.Op == "and")
                condition = and.Left;
            if (!(condition is BinaryExpr equal) || equal.Op != "=")
                return null;
            var path = equal.Left as PathExpr ?? equal.Right as PathExpr;
            var literal = equal.Left as LiteralExpr ?? equal.Right as LiteralExpr;
            if (path == null || literal == null || path.Start != null || path.Absolute || path.Steps.Count != 1)
                return null;
            var step = path.Steps[0];
            if (step.Axis != "child" || step.Test.Kind != TestKind.Name || step.Predicates.Count != 0)
                return null;
            return (XName.Get(step.Test.Local!, Namespace(step.Test.Prefix, rule.Template)), literal.Value);
        }

        /// <summary>
        /// The rules tried in order. Among those of an element, the many that ask for a child with a text (the
        /// characters of MTEF, each with its code) are found by the text of that child, when the element has one.
        /// </summary>
        private void EmitCandidates(IEnumerable<Rule> candidates, CodeWriter w, bool ofElement = false)
        {
            var rules = candidates.ToList();
            var always = rules.FindIndex(r => r.Always);
            if (always >= 0)
                rules = rules.Take(always + 1).ToList();
            var keyed = ofElement
                ? rules.Where(r => KeyOf(r) != null).GroupBy(r => KeyOf(r)!.Value.Child).OrderByDescending(g => g.Count()).FirstOrDefault()
                : null;
            var child = keyed != null && keyed.Count() >= 8 ? keyed.Key : null;
            bool Keyed(Rule rule) => child != null && KeyOf(rule)?.Child == child;

            void Try(Rule rule)
            {
                if (rule.Always)
                {
                    w.Line(rule.Method + "(node, position, size, args);").Line("return;");
                    return;
                }
                w.Open("if (Match" + rule.Index + "(node))");
                w.Line(rule.Method + "(node, position, size, args);").Line("return;");
                w.Close();
            }

            var key = NewName("key");
            var count = NewName("keys");
            if (child != null)
                w.Line("var " + key + " = KeyValue(element, " + Name(child) + ", out var " + count + ");");
            for (var i = 0; i < rules.Count;)
            {
                if (!Keyed(rules[i]))
                {
                    Try(rules[i++]);
                    continue;
                }
                var run = rules.Skip(i).TakeWhile(Keyed).ToList();
                i += run.Count;
                w.Open("if (" + count + " == 1)");
                w.Open("switch (" + key + ")");
                foreach (var group in run.GroupBy(r => KeyOf(r)!.Value.Text))
                {
                    w.Line("case " + XPathCompiler.Literal(group.Key) + ":").Open();
                    foreach (var rule in group)
                        Try(rule);
                    w.Line("break;").Close();
                }
                w.Close().Close();
                // with more than one such child, any of them may have the text
                w.Open("else if (" + count + " > 1)");
                foreach (var rule in run)
                    Try(rule);
                w.Close();
            }
        }

        // global variables and parameters

        public Variable? Global(string name, Purity purity)
        {
            if (!globals.TryGetValue(name, out var global))
                return null;
            if (facts.Dynamic.Contains(name))
                purity.Impure = true;
            if (global.Variable != null)
                return global.Variable;
            if (global.Compiling)
                throw Error(global.Element, "The global variable $" + name + " depends on itself.");
            global.Compiling = true;

            var id = "G" + NewName("_");
            var element = global.Element;
            var focus = new Focus();
            var own = new Purity();
            var context = TemplateContext(element, focus, own, new Scope(null));
            var body = new CodeWriter(3);
            var select = Attr(element, "select", false);
            Compiled value;
            var order = NodeOrder.Ordered;
            if (select != null)
            {
                var expr = XPathParser.Parse(select);
                value = xpath.Compile(expr, context);
                if (value.Type == XType.NodeSet)
                    order = xpath.OrderOf(expr, context);
            }
            else if (HasContent(element))
            {
                value = Capture(element, context, body);
            }
            else
            {
                value = new Compiled("\"\"", XType.String);
            }
            var type = value.Type;
            var typeName = XPathCompiler.TypeName(type);
            body.Line("return " + value.Code + ";");

            var w = new CodeWriter(2);
            w.Line().Open("private " + typeName + " Compute" + id + "()");
            w.Line("XObject n0 = root;").Line("const int p0 = 1, s0 = 1;");
            w.Raw(body.ToString());
            w.Close();

            // a string that does not depend on the input is computed once for all
            var text = type == XType.String || type == XType.Text;
            if (own.Impure || focus.NodeUsed || focus.CurrentUsed || !text)
                Learnt.Dynamic.Add(name);
            if (text && !facts.Dynamic.Contains(name))
            {
                staticFields.Line("private static string? " + id + "Value;");
                w.Line().Line("private string " + id + " => " + id + "Value ??= Compute" + id + "();");
            }
            else
            {
                instanceFields.Line("private bool " + id + "Done;").Line("private " + typeName + " " + id + "Value = default!;");
                w.Line().Open("private " + typeName + " " + id)
                    .Open("get")
                    .Open("if (!" + id + "Done)")
                    .Line(id + "Value = Compute" + id + "();")
                    .Line(id + "Done = true;")
                    .Close()
                    .Line("return " + id + "Value;")
                    .Close()
                    .Close();
            }
            members.Append(w);
            global.Variable = new Variable(id, type, order);
            return global.Variable;
        }

        // instructions

        private void CompileSequence(IEnumerable<XNode> nodes, Context context, CodeWriter w)
        {
            context = context.WithScope(new Scope(context.Scope));
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case XText text when text.Value.Trim().Length == 0:
                        break;
                    case XText text:
                        w.Line("output.Text(" + XPathCompiler.Literal(text.Value) + ");");
                        break;
                    case XElement element:
                        try
                        {
                            if (element.Name.Namespace == Xsl)
                                CompileInstruction(element, context.WithElement(element), w);
                            else
                                CompileLiteralElement(element, context.WithElement(element), w);
                        }
                        catch (XsltException e) when (e.Line == 0)
                        {
                            var info = (IXmlLineInfo)element;
                            e.Line = info.LineNumber;
                            e.Column = info.LinePosition;
                            throw;
                        }
                        break;
                }
            }
        }

        private void CompileLiteralElement(XElement element, Context context, CodeWriter w)
        {
            w.Line("output.StartElement(" + Name(element.Name) + ");");
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;
                if (attribute.Name.Namespace == Xsl)
                {
                    if (attribute.Name.LocalName == "use-attribute-sets")
                        throw Error(attribute, "xsl:use-attribute-sets is not supported.");
                    continue;
                }
                w.Line("output.Attribute(" + Name(attribute.Name) + ", " + XPathCompiler.Str(AttributeValueTemplate(attribute.Value, context)) + ");");
            }
            CompileSequence(element.Nodes(), context, w);
            w.Line("output.EndElement();");
        }

        private void CompileInstruction(XElement element, Context context, CodeWriter w)
        {
            switch (element.Name.LocalName)
            {
                case "apply-templates":
                {
                    var select = Attr(element, "select", false);
                    var mode = ModeIndex((string?)element.Attribute("mode"));
                    context.Purity.Impure = true;
                    var args = "null";
                    var parameters = WithParameters(element, context, w);
                    if (parameters.Count > 0)
                    {
                        args = NewName("args");
                        w.Line("var " + args + " = new TemplateArguments(new[] { " + string.Join(", ", parameters.Keys.Select(XPathCompiler.Literal))
                            + " }, new object[] { " + string.Join(", ", parameters.Values.Select(XPathCompiler.Box)) + " });");
                    }
                    if (select == null)
                    {
                        w.Line("ApplyChildren" + mode + "(" + context.Node + ", " + args + ");");
                        break;
                    }

                    // the templates are given the position of each node, and a list of the nodes for their number,
                    // only if one of the mode reads them
                    var expr = XPathParser.Parse(select);
                    if (facts.ModeSize.Contains(ModeKey(mode)) || !XPathCompiler.IsStreamed(xpath.OrderOf(expr, context)))
                    {
                        w.Line("Apply" + mode + "(" + xpath.NodesCode(expr, context) + ", " + args + ");");
                        break;
                    }
                    var position = "0";
                    if (facts.ModePosition.Contains(ModeKey(mode)))
                    {
                        var p = NewName("p");
                        w.Line("var " + p + " = 0;");
                        position = "++" + p;
                    }
                    xpath.EmitNodes(expr, context, w, cursor => w.Line("Dispatch" + mode + "(" + cursor.Var + ", " + position + ", 0, " + args + ");"));
                    break;
                }
                case "call-template":
                {
                    var name = Attr(element, "name", true)!;
                    if (!namedTemplates.TryGetValue(name, out var template))
                        throw Error(element, "There is no template named " + name + ".");
                    var parameters = WithParameters(element, context, w);
                    var args = new List<string>();
                    foreach (var parameter in template.Parameters)
                    {
                        var key = Facts.Parameter(name, parameter);
                        if (parameters.TryGetValue(parameter, out var value))
                        {
                            Learnt.Give(key, value.Type);
                            args.Add(Convert(value, ParameterType(template, parameter)));
                        }
                        else
                        {
                            Learnt.Omitted.Add(key);
                            args.Add(ParameterType(template, parameter) == XType.Node ? "Omitted" : "null");
                        }
                    }
                    w.Line(Call(template, context, args));
                    break;
                }
                case "value-of":
                {
                    var value = XPathCompiler.Str(xpath.Compile(Attr(element, "select", true)!, context));
                    w.Line((DisablesEscaping(element) ? "output.RawText(" : "output.Text(") + value + ");");
                    break;
                }
                case "text":
                    if (element.Value.Length > 0)
                        w.Line((DisablesEscaping(element) ? "output.RawText(" : "output.Text(") + XPathCompiler.Literal(element.Value) + ");");
                    break;
                case "if":
                    w.Open("if (" + XPathCompiler.Bool(xpath.Compile(Attr(element, "test", true)!, context)) + ")");
                    CompileSequence(element.Nodes(), context, w);
                    w.Close();
                    break;
                case "choose":
                {
                    var first = true;
                    foreach (var branch in element.Elements())
                    {
                        switch (branch.Name.LocalName)
                        {
                            case "when" when branch.Name.Namespace == Xsl:
                                var test = XPathCompiler.Bool(xpath.Compile(Attr(branch, "test", true)!, context.WithElement(branch)));
                                w.Open((first ? "if (" : "else if (") + test + ")");
                                break;
                            case "otherwise" when branch.Name.Namespace == Xsl && !first:
                                w.Open("else");
                                break;
                            case "otherwise" when branch.Name.Namespace == Xsl:
                                w.Open();
                                break;
                            default:
                                throw Error(branch, "xsl:choose may contain only xsl:when and xsl:otherwise.");
                        }
                        CompileSequence(branch.Nodes(), context.WithElement(branch), w);
                        w.Close();
                        first = false;
                    }
                    break;
                }
                case "for-each":
                {
                    if (element.Elements(Xsl + "sort").Any())
                        throw Error(element, "xsl:sort is not supported.");
                    var expr = XPathParser.Parse(Attr(element, "select", true)!);
                    var kind = xpath.KindOf(expr, context);
                    var n = NewName("n");
                    var p = NewName("p");
                    var s = NewName("s");

                    // the content first: whether it reads the position and the number of the nodes tells the loop it needs
                    var focus = new Focus();
                    var body = new CodeWriter();
                    CompileSequence(element.Nodes(), new Context(n, kind, p, s, focus, n, kind, focus, context.Scope, element, context.Names, context.Purity), body);
                    if (!focus.SizeUsed && XPathCompiler.IsStreamed(xpath.OrderOf(expr, context)))
                    {
                        if (focus.PositionUsed)
                            w.Line("var " + p + " = 0;");
                        xpath.EmitNodes(expr, context, w, cursor =>
                        {
                            w.Line("var " + n + " = " + cursor.Var + ";");
                            if (focus.PositionUsed)
                                w.Line(p + "++;");
                            w.Indented(body);
                        });
                        break;
                    }
                    var set = NewName("set");
                    var i = NewName("i");
                    w.Line("var " + set + " = " + xpath.NodesCode(expr, context) + ";");
                    w.Open("for (var " + i + " = 0; " + i + " < " + set + ".Count; " + i + "++)");
                    w.Line("var " + n + " = " + (kind == NodeKind.Object ? "" : "(" + XPathCompiler.TypeName(kind) + ")") + set + "[" + i + "];");
                    if (focus.PositionUsed)
                        w.Line("var " + p + " = " + i + " + 1;");
                    if (focus.SizeUsed)
                        w.Line("var " + s + " = " + set + ".Count;");
                    w.Indented(body);
                    w.Close();
                    break;
                }
                case "variable":
                {
                    var name = Attr(element, "name", true)!;
                    var id = NewName("v");
                    var select = Attr(element, "select", false);
                    Compiled value;
                    var order = NodeOrder.Ordered;
                    if (select != null)
                    {
                        var expr = XPathParser.Parse(select);
                        value = xpath.Compile(expr, context).Bound();
                        if (value.Type == XType.NodeSet)
                            order = xpath.OrderOf(expr, context);
                    }
                    else if (HasContent(element))
                    {
                        value = Capture(element, context, w);
                    }
                    else
                    {
                        value = new Compiled("\"\"", XType.String);
                    }
                    w.Line("var " + id + " = " + value.Code + ";");
                    context.Scope.Add(name, new Variable(id, value.Type, order));
                    break;
                }
                case "attribute":
                {
                    var name = ComputedName(element, context, false);
                    w.Line("output.Attribute(" + name + ", " + Content(element, context, w) + ");");
                    break;
                }
                case "element":
                {
                    if (element.Attribute("use-attribute-sets") != null)
                        throw Error(element, "use-attribute-sets is not supported.");
                    w.Line("output.StartElement(" + ComputedName(element, context, true) + ");");
                    CompileSequence(element.Nodes(), context, w);
                    w.Line("output.EndElement();");
                    break;
                }
                case "copy-of":
                {
                    var expr = XPathParser.Parse(Attr(element, "select", true)!);
                    var value = xpath.Compile(expr, context);
                    switch (value.Type)
                    {
                        case XType.NodeSet when XPathCompiler.IsStreamed(xpath.OrderOf(expr, context)):
                            xpath.EmitNodes(expr, context, w, cursor => w.Line("output.Copy(" + cursor.Var + ");"));
                            break;
                        case XType.Text:
                            w.Line("output.CopyOfText(" + value.Code + ");");
                            break;
                        case XType.Number:
                        case XType.Boolean:
                            w.Line("output.CopyOf(" + XPathCompiler.Str(value) + ");");
                            break;
                        default:
                            w.Line("output.CopyOf(" + value.Code + ");");
                            break;
                    }
                    break;
                }
                case "copy":
                {
                    if (element.Attribute("use-attribute-sets") != null)
                        throw Error(element, "use-attribute-sets is not supported.");
                    var opened = NewName("opened");
                    w.Line("var " + opened + " = output.StartCopy(" + context.Node + ");");
                    w.Open("if (" + opened + " || " + context.Node + " is XDocument)");
                    CompileSequence(element.Nodes(), context, w);
                    w.Line("if (" + opened + ")").Line("    output.EndElement();");
                    w.Close();
                    break;
                }
                case "comment":
                    w.Line("output.Comment(" + Content(element, context, w) + ");");
                    break;
                case "processing-instruction":
                {
                    var target = XPathCompiler.Str(AttributeValueTemplate(Attr(element, "name", true)!, context));
                    w.Line("output.ProcessingInstruction(" + target + ", " + Content(element, context, w) + ");");
                    break;
                }
                case "message":
                    if ((string?)element.Attribute("terminate") == "yes")
                        w.Line("throw new System.InvalidOperationException(" + Content(element, context, w) + ");");
                    break;
                case "fallback":
                    break;
                default:
                    throw Error(element, "xsl:" + element.Name.LocalName + " is not supported here.");
            }
        }

        private static bool DisablesEscaping(XElement element) => (string?)element.Attribute("disable-output-escaping") == "yes";

        /// <summary>The parameters of xsl:apply-templates or xsl:call-template, by name.</summary>
        private Dictionary<string, Compiled> WithParameters(XElement element, Context context, CodeWriter w)
        {
            var parameters = new Dictionary<string, Compiled>();
            foreach (var child in element.Elements())
            {
                if (child.Name == Xsl + "sort")
                    throw Error(child, "xsl:sort is not supported.");
                if (child.Name != Xsl + "with-param")
                    throw Error(child, "Unexpected " + child.Name.LocalName + ".");
                var name = Attr(child, "name", true)!;
                var select = Attr(child, "select", false);
                var paramContext = context.WithElement(child);
                if (select != null)
                {
                    // compiled here, before the statements of the parameters after it
                    parameters[name] = xpath.Compile(select, paramContext).Bound();
                }
                else if (HasContent(child))
                {
                    parameters[name] = Capture(child, paramContext, w);
                }
                else
                {
                    parameters[name] = new Compiled("\"\"", XType.String);
                }
            }
            return parameters;
        }

        /// <summary>The content of an instruction as a string (the value of an attribute, a comment...).</summary>
        private string Content(XElement element, Context context, CodeWriter w)
        {
            var nodes = element.Nodes().Where(n => !(n is XText t && t.Value.Trim().Length == 0)).ToList();
            switch (nodes.Count)
            {
                case 0:
                    return "\"\"";
                case 1 when nodes[0] is XText text:
                    return XPathCompiler.Literal(text.Value);
                case 1 when nodes[0] is XElement e && e.Name == Xsl + "text":
                    return XPathCompiler.Literal(e.Value);
                case 1 when nodes[0] is XElement e && e.Name == Xsl + "value-of":
                    return XPathCompiler.Str(xpath.Compile(Attr(e, "select", true)!, context.WithElement(e)));
            }
            if (IsTextOnly(element.Nodes()))
                return Capture(element, context, w).Code;
            var id = NewName("text");
            w.Line("output.BeginCapture();");
            CompileSequence(element.Nodes(), context, w);
            w.Line("var " + id + " = output.EndString();");
            return id;
        }

        /// <summary>The name of xsl:element or xsl:attribute; the default namespace applies to elements only.</summary>
        private string ComputedName(XElement element, Context context, bool isElement)
        {
            var name = Attr(element, "name", true)!;
            var ns = (string?)element.Attribute("namespace");
            if (ns != null && ns.IndexOf('{') >= 0)
                throw Error(element, "A computed namespace is not supported.");
            if (name.IndexOf('{') < 0)
            {
                var colon = name.IndexOf(':');
                var local = colon < 0 ? name : name.Substring(colon + 1);
                if (ns == null)
                    ns = colon < 0 ? (isElement ? element.GetDefaultNamespace().NamespaceName : "") : Namespace(name.Substring(0, colon), element);
                return Name(XName.Get(local, ns));
            }

            var value = XPathCompiler.Str(AttributeValueTemplate(name, context));
            if (ns != null)
                return "XName.Get(" + value + ", " + XPathCompiler.Literal(ns) + ")";
            var table = NewName("Prefixes");
            var declarations = element.AncestorsAndSelf().SelectMany(e => e.Attributes()).Where(a => a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.Xmlns)
                .GroupBy(a => a.Name.LocalName).Select(g => g.First());
            staticFields.Line("private static readonly string[] " + table + " = new[] { "
                + string.Join(", ", declarations.SelectMany(a => new[] { XPathCompiler.Literal(a.Name.LocalName), XPathCompiler.Literal(a.Value) })) + " };");
            var defaultNamespace = isElement ? element.GetDefaultNamespace().NamespaceName : "";
            return "ResolveName(" + value + ", " + XPathCompiler.Literal(defaultNamespace) + ", " + table + ")";
        }

        private Compiled AttributeValueTemplate(string template, Context context)
        {
            var parts = new List<string>();
            var literal = new StringBuilder();
            var i = 0;
            while (i < template.Length)
            {
                var c = template[i];
                switch (c)
                {
                    case '{' when i + 1 < template.Length && template[i + 1] == '{':
                    case '}' when i + 1 < template.Length && template[i + 1] == '}':
                        literal.Append(c);
                        i += 2;
                        break;
                    case '{':
                    {
                        var end = i + 1;
                        var quote = '\0';
                        while (end < template.Length && (quote != '\0' || template[end] != '}'))
                        {
                            if (quote == '\0' && (template[end] == '\'' || template[end] == '"'))
                                quote = template[end];
                            else if (template[end] == quote)
                                quote = '\0';
                            end++;
                        }
                        if (end >= template.Length)
                            throw new XsltException("Unterminated expression in the attribute value template '" + template + "'.");
                        if (literal.Length > 0)
                        {
                            parts.Add(XPathCompiler.Literal(literal.ToString()));
                            literal.Clear();
                        }
                        parts.Add(XPathCompiler.Str(xpath.Compile(template.Substring(i + 1, end - i - 1), context)));
                        i = end + 1;
                        break;
                    }
                    default:
                        literal.Append(c);
                        i++;
                        break;
                }
            }
            if (literal.Length > 0 || parts.Count == 0)
                parts.Add(XPathCompiler.Literal(literal.ToString()));
            return new Compiled(parts.Count == 1 ? parts[0] : "string.Concat(" + string.Join(", ", parts) + ")", XType.String);
        }
    }
}
