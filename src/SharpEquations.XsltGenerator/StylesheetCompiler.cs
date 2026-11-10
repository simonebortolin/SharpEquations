namespace SharpEquations.XsltGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// An XSLT 1.0 stylesheet compiled to a C# class: a method for each template, the dispatch of
    /// <c>xsl:apply-templates</c> for each mode (by the name of the element first), a lazy property for each
    /// global variable. What the stylesheets of SharpEquations do not use (keys, sorting, numbering, imports,
    /// extension functions) is reported as an error.
    /// </summary>
    internal sealed class StylesheetCompiler
    {
        private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

        private readonly XElement stylesheet;
        private readonly string className;
        private readonly string fileName;
        private readonly XPathCompiler xpath;

        private readonly CodeWriter nameFields = new CodeWriter(2);
        private readonly CodeWriter testFields = new CodeWriter(2);
        private readonly CodeWriter staticFields = new CodeWriter(2);
        private readonly CodeWriter instanceFields = new CodeWriter(2);
        private readonly CodeWriter members = new CodeWriter(2);

        private readonly Dictionary<string, string> names = new Dictionary<string, string>();
        private readonly Dictionary<string, string> tests = new Dictionary<string, string>();
        private readonly Dictionary<string, string> statics = new Dictionary<string, string>();
        private readonly Dictionary<string, NamedTemplate> namedTemplates = new Dictionary<string, NamedTemplate>();
        private readonly Dictionary<string, GlobalVariable> globals = new Dictionary<string, GlobalVariable>();
        private readonly Dictionary<string, int> modes = new Dictionary<string, int> { [""] = 0 };
        private readonly List<Rule> rules = new List<Rule>();
        private int counter;

        private sealed class NamedTemplate
        {
            public NamedTemplate(string method, List<string> parameters)
            {
                Method = method;
                Parameters = parameters;
            }

            public string Method { get; }

            public List<string> Parameters { get; }
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
        }

        public StylesheetCompiler(XElement stylesheet, string className, string fileName)
        {
            this.stylesheet = stylesheet;
            this.className = className;
            this.fileName = fileName;
            xpath = new XPathCompiler(this);
        }

        public string NewName(string prefix) => prefix + (++counter);

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

        public string NodeTest(NodeTestSyntax test, XElement scope)
        {
            string key;
            string init;
            switch (test.Kind)
            {
                case TestKind.Name:
                {
                    var name = XName.Get(test.Local!, Namespace(test.Prefix, scope));
                    key = "name " + name;
                    init = "NodeTest.Named(" + Name(name) + ")";
                    break;
                }
                case TestKind.Namespace:
                {
                    var ns = Namespace(test.Prefix, scope);
                    key = "ns " + ns;
                    init = "NodeTest.InNamespace(" + XPathCompiler.Literal(ns) + ")";
                    break;
                }
                case TestKind.AnyName:
                    return "NodeTest.AnyName";
                case TestKind.Node:
                    return "NodeTest.Node";
                case TestKind.Text:
                    return "NodeTest.Text";
                case TestKind.Comment:
                    return "NodeTest.Comment";
                default:
                    key = "pi " + test.Local;
                    init = "NodeTest.ProcessingInstruction(" + (test.Local == null ? "null" : XPathCompiler.Literal(test.Local)) + ")";
                    break;
            }
            if (!tests.TryGetValue(key, out var field))
            {
                field = "Test" + tests.Count;
                tests.Add(key, field);
                testFields.Line("private static readonly NodeTest " + field + " = " + init + ";");
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

        /// <summary>A new field of the stylesheet that runs.</summary>
        public string InstanceField(string type)
        {
            var field = NewName("field");
            instanceFields.Line("private " + type + " " + field + ";");
            return field;
        }

        private static string Namespace(string? prefix, XElement scope)
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
                    namedTemplates[name] = new NamedTemplate("Named" + i, TemplateParameters(templates[i]).Select(p => Attr(p, "name", true)!).ToList());
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
            file.Append(nameFields).Append(testFields).Append(staticFields);
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
                .Line("stylesheet.Apply0(Single(document), null);")
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
                var signature = string.Concat(named.Parameters.Select((p, i) => ", object? a" + i));
                var w = new CodeWriter(2);
                w.Line().Open("private void " + named.Method + "(XObject n0, int p0, int s0" + signature + ")");
                CompileBody(template, parameters, i => "a" + i, w);
                w.Close();
                members.Append(w);
            }

            if (match == null)
                return;

            var method = "Template" + index;
            var body = new CodeWriter(2);
            body.Line().Open("private void " + method + "(XObject n0, int p0, int s0, TemplateArguments? args)");
            if (name != null)
            {
                var named = namedTemplates[name];
                body.Line(named.Method + "(n0, p0, s0" + string.Concat(named.Parameters.Select(p => ", args?.Get(" + XPathCompiler.Literal(p) + ")")) + ");");
            }
            else
            {
                var names = parameters.Select(p => Attr(p, "name", true)!).ToList();
                CompileBody(template, parameters, i => "args?.Get(" + XPathCompiler.Literal(names[i]) + ")", body);
            }
            body.Close();
            members.Append(body);

            var mode = ModeIndex((string?)template.Attribute("mode"));
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

        private void CompileBody(XElement template, List<XElement> parameters, System.Func<int, string> argument, CodeWriter w)
        {
            var scope = new Scope(null);
            var context = new Context("n0", "p0", "s0", "n0", scope, template);
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var id = NewName("v");
                var paramContext = context.WithElement(parameter);
                var select = Attr(parameter, "select", false);
                if (select != null)
                {
                    w.Line("var " + id + " = " + argument(i) + " ?? " + XPathCompiler.Box(xpath.Compile(select, paramContext)) + ";");
                }
                else if (HasContent(parameter))
                {
                    w.Line("var " + id + " = " + argument(i) + ";");
                    w.Open("if (" + id + " == null)");
                    w.Line("output.BeginCapture();");
                    CompileSequence(parameter.Nodes(), paramContext, w);
                    w.Line(id + " = output.EndFragment();");
                    w.Close();
                }
                else
                {
                    w.Line("var " + id + " = " + argument(i) + " ?? \"\";");
                }
                scope.Add(Attr(parameter, "name", true)!, new Variable(id, XType.Any));
            }
            CompileSequence(template.Nodes().Where(n => !(n is XElement e && parameters.Contains(e))), context, w);
        }

        private static bool HasContent(XElement element) =>
            element.Nodes().Any(n => n is XElement || (n is XText t && t.Value.Trim().Length > 0));

        // patterns

        private void CompileMatch(Rule rule)
        {
            var w = new CodeWriter(2);
            var context = new Context("node", "1", "1", "node", new Scope(null), rule.Template);
            if (rule.IsRoot)
            {
                w.Line().Line("private static bool Match" + rule.Index + "(XObject node) => node is XDocument;");
                members.Append(w);
                return;
            }

            for (var i = 0; i < rule.Steps.Count; i++)
            {
                var step = rule.Steps[i].Step;
                var attributeAxis = step.Axis == "attribute";
                var test = NodeTest(step.Test, rule.Template) + ".Matches(node, " + (attributeAxis ? "true" : "false") + ")";
                if (step.Predicates.Count > 0)
                {
                    if (step.Predicates.Any(p => xpath.IsPositional(p, context)))
                        test = "MatchesStep(node, Axis." + (attributeAxis ? "Attribute" : "Child") + ", " + NodeTest(step.Test, rule.Template) + ", "
                            + string.Join(", ", step.Predicates.Select(p => xpath.Predicate(p, context))) + ")";
                    else
                        test += string.Concat(step.Predicates.Select(p => " && " + XPathCompiler.Bool(xpath.Compile(p, context))));
                }

                w.Line().Open("private bool Match" + rule.Index + "_" + i + "(XObject node)");
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

            var w = new CodeWriter(2);
            w.Line().Open("private void Apply" + mode + "(List<XObject> nodes, TemplateArguments? args)");
            w.Line("for (var i = 0; i < nodes.Count; i++)").Line("    Dispatch" + mode + "(nodes[i], i + 1, nodes.Count, args);");
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
                    EmitCandidates(candidates, w);
                    w.Line("break;").Close();
                }
                w.Line("default:").Open();
                EmitCandidates(generic, w);
                w.Line("break;").Close();
                w.Close();
            }
            else
            {
                EmitCandidates(generic, w);
            }
            w.Line("Apply" + mode + "(ChildNodes(node), null);").Line("return;").Close();

            w.Line("case XDocument _:").Open();
            EmitCandidates(In(Bucket.Root), w);
            w.Line("Apply" + mode + "(ChildNodes(node), null);").Line("return;").Close();

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

        private static void EmitCandidates(IEnumerable<Rule> candidates, CodeWriter w)
        {
            foreach (var rule in candidates)
            {
                w.Open("if (Match" + rule.Index + "(node))");
                w.Line(rule.Method + "(node, position, size, args);").Line("return;");
                w.Close();
            }
        }

        // global variables and parameters

        public Variable? Global(string name)
        {
            if (!globals.TryGetValue(name, out var global))
                return null;
            if (global.Variable != null)
                return global.Variable;
            if (global.Compiling)
                throw Error(global.Element, "The global variable $" + name + " depends on itself.");
            global.Compiling = true;

            var id = "G" + NewName("_");
            var element = global.Element;
            var context = new Context("n0", "p0", "s0", "n0", new Scope(null), element);
            var w = new CodeWriter(2);
            var select = Attr(element, "select", false);
            XType type;
            w.Line();
            if (select != null)
            {
                var value = xpath.Compile(select, context);
                type = value.Type;
                w.Open("private " + XPathCompiler.TypeName(type) + " Compute" + id + "()");
                w.Line("XObject n0 = root;").Line("const int p0 = 1, s0 = 1;");
                w.Line("return " + value.Code + ";");
            }
            else if (HasContent(element))
            {
                type = XType.Fragment;
                w.Open("private ResultFragment Compute" + id + "()");
                w.Line("XObject n0 = root;").Line("const int p0 = 1, s0 = 1;");
                w.Line("output.BeginCapture();");
                CompileSequence(element.Nodes(), context, w);
                w.Line("return output.EndFragment();");
            }
            else
            {
                type = XType.String;
                w.Open("private string Compute" + id + "()");
                w.Line("return \"\";");
            }
            w.Close();

            var typeName = XPathCompiler.TypeName(type);
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
            members.Append(w);
            global.Variable = new Variable(id, type);
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
                    var nodes = select == null ? "ChildNodes(" + context.Node + ")" : XPathCompiler.Nodes(xpath.Compile(select, context));
                    var args = "null";
                    var parameters = WithParameters(element, context, w);
                    if (parameters.Count > 0)
                    {
                        args = NewName("args");
                        w.Line("var " + args + " = new TemplateArguments(new[] { " + string.Join(", ", parameters.Keys.Select(XPathCompiler.Literal))
                            + " }, new object[] { " + string.Join(", ", parameters.Values) + " });");
                    }
                    w.Line("Apply" + ModeIndex((string?)element.Attribute("mode")) + "(" + nodes + ", " + args + ");");
                    break;
                }
                case "call-template":
                {
                    var name = Attr(element, "name", true)!;
                    if (!namedTemplates.TryGetValue(name, out var template))
                        throw Error(element, "There is no template named " + name + ".");
                    var parameters = WithParameters(element, context, w);
                    var args = string.Concat(template.Parameters.Select(p => ", " + (parameters.TryGetValue(p, out var value) ? value : "null")));
                    w.Line(template.Method + "(" + context.Node + ", " + context.Position + ", " + context.Size + args + ");");
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
                    var set = NewName("set");
                    var i = NewName("i");
                    var n = NewName("n");
                    var p = NewName("p");
                    var s = NewName("s");
                    w.Line("var " + set + " = " + XPathCompiler.Nodes(xpath.Compile(Attr(element, "select", true)!, context)) + ";");
                    w.Open("for (var " + i + " = 0; " + i + " < " + set + ".Count; " + i + "++)");
                    w.Line("var " + n + " = " + set + "[" + i + "];").Line("var " + p + " = " + i + " + 1;").Line("var " + s + " = " + set + ".Count;");
                    CompileSequence(element.Nodes(), new Context(n, p, s, n, context.Scope, element), w);
                    w.Close();
                    break;
                }
                case "variable":
                {
                    var name = Attr(element, "name", true)!;
                    var id = NewName("v");
                    var select = Attr(element, "select", false);
                    XType type;
                    if (select != null)
                    {
                        var value = xpath.Compile(select, context);
                        type = value.Type;
                        w.Line("var " + id + " = " + value.Code + ";");
                    }
                    else if (HasContent(element))
                    {
                        type = XType.Fragment;
                        w.Line("output.BeginCapture();");
                        CompileSequence(element.Nodes(), context, w);
                        w.Line("var " + id + " = output.EndFragment();");
                    }
                    else
                    {
                        type = XType.String;
                        w.Line("var " + id + " = \"\";");
                    }
                    context.Scope.Add(name, new Variable(id, type));
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
                    var value = xpath.Compile(Attr(element, "select", true)!, context);
                    var code = value.Type == XType.Number || value.Type == XType.Boolean ? XPathCompiler.Str(value) : value.Code;
                    w.Line("output.CopyOf(" + code + ");");
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

        /// <summary>The parameters of xsl:apply-templates or xsl:call-template, by name, as C# expressions of type object.</summary>
        private Dictionary<string, string> WithParameters(XElement element, Context context, CodeWriter w)
        {
            var parameters = new Dictionary<string, string>();
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
                    parameters[name] = XPathCompiler.Box(xpath.Compile(select, paramContext));
                }
                else if (HasContent(child))
                {
                    var id = NewName("w");
                    w.Line("output.BeginCapture();");
                    CompileSequence(child.Nodes(), paramContext, w);
                    w.Line("var " + id + " = output.EndFragment();");
                    parameters[name] = id;
                }
                else
                {
                    parameters[name] = "(object)\"\"";
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
