namespace SharpEquations.XsltGenerator
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    /// <summary>
    /// The static type of an XPath value in the generated code. <c>Text</c> is a result tree fragment that holds only
    /// text, kept as a string; <c>None</c> is the type of a parameter no call has given a value yet, while the types
    /// of the parameters are inferred.
    /// </summary>
    internal enum XType
    {
        None,
        String,
        Number,
        Boolean,
        NodeSet,
        Text,
        Fragment,
        Any,
    }

    /// <summary>What is known of the order of the nodes a path yields as the generated loops reach them.</summary>
    internal enum NodeOrder
    {
        /// <summary>At most one node.</summary>
        Single,

        /// <summary>In document order, without duplicates, none an ancestor of another.</summary>
        Disjoint,

        /// <summary>In document order, without duplicates.</summary>
        Ordered,

        /// <summary>In reverse document order, without duplicates.</summary>
        Reversed,

        Unordered,
    }

    /// <summary>The C# type of the variable that holds a node.</summary>
    internal enum NodeKind
    {
        Object,
        Node,
        Element,
        Attribute,
        Text,
    }

    /// <summary>A node the generated code holds in a variable.</summary>
    internal readonly struct Cursor
    {
        public Cursor(string variable, NodeKind kind)
        {
            Var = variable;
            Kind = kind;
        }

        public string Var { get; }

        public NodeKind Kind { get; }
    }

    /// <summary>
    /// An expression compiled to C#. A node-set is compiled when it is used, to the code its use needs: a test for a
    /// node, the string value of the first one, a count, or the list of the nodes (<see cref="Code"/>).
    /// </summary>
    internal sealed class Compiled
    {
        private readonly XPathCompiler? owner;
        private string? code;

        public Compiled(string code, XType type)
        {
            this.code = code;
            Type = type;
        }

        public Compiled(XPathCompiler owner, Expr expr, Context context)
        {
            this.owner = owner;
            Expr = expr;
            Context = context;
            Type = XType.NodeSet;
        }

        public string Code => code ??= owner!.NodesCode(Expr!, Context!);

        public XType Type { get; }

        public Expr? Expr { get; }

        public Context? Context { get; }

        public XPathCompiler Owner => owner!;

        public bool IsNodeSet => Expr != null;
    }

    internal sealed class Variable
    {
        public Variable(string id, XType type, NodeOrder order = NodeOrder.Ordered)
        {
            Id = id;
            Type = type;
            Order = order;
        }

        public string Id { get; }

        public XType Type { get; }

        /// <summary>The order of the nodes of a node-set.</summary>
        public NodeOrder Order { get; }
    }

    internal class Scope
    {
        private readonly Dictionary<string, Variable> variables = new Dictionary<string, Variable>();
        private readonly Scope? parent;

        public Scope(Scope? parent) => this.parent = parent;

        public void Add(string name, Variable variable) => variables[name] = variable;

        public virtual Variable? Find(string name) =>
            variables.TryGetValue(name, out var variable) ? variable : parent?.Find(name);
    }

    /// <summary>The scope of a method generated for an expression: the variables it reads are its parameters.</summary>
    internal sealed class CapturingScope : Scope
    {
        private readonly Scope outer;

        public CapturingScope(Scope outer)
            : base(null) => this.outer = outer;

        public List<Variable> Captured { get; } = new List<Variable>();

        public override Variable? Find(string name)
        {
            var variable = outer.Find(name);
            if (variable != null && !Captured.Contains(variable))
                Captured.Add(variable);
            return variable;
        }
    }

    /// <summary>What the code compiled in a context read of it.</summary>
    internal sealed class Focus
    {
        public bool NodeUsed { get; set; }

        public bool PositionUsed { get; set; }

        public bool SizeUsed { get; set; }

        public bool CurrentUsed { get; set; }
    }

    /// <summary>Whether what is compiled depends on more than its parameters: the input, or a variable that does.</summary>
    internal sealed class Purity
    {
        public bool Impure { get; set; }
    }

    internal sealed class Names
    {
        private int counter;

        public string New(string prefix) => prefix + (++counter).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Where an expression is evaluated: the C# names of the context node, position and size, and of the current
    /// node of XSLT; the variables in scope; the element whose namespace declarations resolve its prefixes.
    /// </summary>
    internal sealed class Context
    {
        private readonly string node;
        private readonly string? position;
        private readonly string? size;
        private readonly string current;
        private readonly Focus focus;
        private readonly Focus currentFocus;

        public Context(string node, NodeKind nodeKind, string? position, string? size, Focus focus, string current, NodeKind currentKind, Focus currentFocus,
            Scope scope, XElement element, Names names, Purity purity)
        {
            this.node = node;
            NodeKind = nodeKind;
            this.position = position;
            this.size = size;
            this.focus = focus;
            this.current = current;
            CurrentKind = currentKind;
            this.currentFocus = currentFocus;
            Scope = scope;
            Element = element;
            Names = names;
            Purity = purity;
        }

        public string Node
        {
            get
            {
                focus.NodeUsed = true;
                return node;
            }
        }

        public NodeKind NodeKind { get; }

        public string Position
        {
            get
            {
                focus.PositionUsed = true;
                return position ?? throw new XsltException("The position of the context is not known here.");
            }
        }

        public string Size
        {
            get
            {
                focus.SizeUsed = true;
                return size ?? throw new XsltException("The size of the context is not known here.");
            }
        }

        public string Current
        {
            get
            {
                currentFocus.CurrentUsed = true;
                return current;
            }
        }

        public NodeKind CurrentKind { get; }

        public Scope Scope { get; }

        public XElement Element { get; }

        public Names Names { get; }

        public Purity Purity { get; }

        public Context WithFocus(string node, NodeKind kind, string? position, string? size, Focus focus) =>
            new Context(node, kind, position, size, focus, current, CurrentKind, currentFocus, Scope, Element, Names, Purity);

        public Context WithScope(Scope scope) =>
            new Context(node, NodeKind, position, size, focus, current, CurrentKind, currentFocus, scope, Element, Names, Purity);

        public Context WithElement(XElement element) =>
            new Context(node, NodeKind, position, size, focus, current, CurrentKind, currentFocus, Scope, element, Names, Purity);
    }

    /// <summary>
    /// XPath 1.0 expressions compiled to C#: the values as expressions over <c>XPathFunctions</c>, the location paths
    /// as loops over the nodes, in the method that uses them or in a method of their own.
    /// </summary>
    internal sealed class XPathCompiler
    {
        private readonly StylesheetCompiler stylesheet;

        public XPathCompiler(StylesheetCompiler stylesheet) => this.stylesheet = stylesheet;

        public Compiled Compile(string source, Context context) => Compile(XPathParser.Parse(source), context);

        public Compiled Compile(Expr expr, Context context)
        {
            switch (expr)
            {
                case LiteralExpr literal:
                    return new Compiled(Literal(literal.Value), XType.String);
                case NumberExpr number:
                    return new Compiled(Number(number.Value), XType.Number);
                case VariableExpr variable:
                {
                    var found = Find(variable, context);
                    return found.Type == XType.NodeSet ? new Compiled(this, expr, context) : new Compiled(found.Id, found.Type);
                }
                case NegateExpr negate:
                    return new Compiled("(-" + Num(Compile(negate.Operand, context)) + ")", XType.Number);
                case BinaryExpr union when union.Op == "|":
                    return new Compiled(this, expr, context);
                case BinaryExpr binary:
                    return CompileBinary(binary, context);
                case FunctionExpr function when function.Name == "current":
                    if (function.Arguments.Count != 0)
                        throw new XsltException("Wrong number of arguments to current().");
                    return new Compiled(this, expr, context);
                case FunctionExpr function:
                    return CompileFunction(function, context);
                case FilterExpr _:
                case PathExpr _:
                    return new Compiled(this, expr, context);
                default:
                    throw new XsltException("Unknown expression.");
            }
        }

        private Variable Find(VariableExpr variable, Context context) =>
            context.Scope.Find(variable.Name) ?? stylesheet.Global(variable.Name, context.Purity)
                ?? throw new XsltException("The variable $" + variable.Name + " is not declared.");

        /// <summary>The type of the value of an expression, without compiling it.</summary>
        public XType TypeOf(Expr expr, Context context)
        {
            switch (expr)
            {
                case LiteralExpr _:
                    return XType.String;
                case NumberExpr _:
                case NegateExpr _:
                    return XType.Number;
                case VariableExpr variable:
                    return Find(variable, context).Type;
                case BinaryExpr binary:
                    switch (binary.Op)
                    {
                        case "|":
                            return XType.NodeSet;
                        case "+":
                        case "-":
                        case "*":
                        case "div":
                        case "mod":
                            return XType.Number;
                        default:
                            return XType.Boolean;
                    }
                case FunctionExpr function:
                    switch (function.Name)
                    {
                        case "last":
                        case "position":
                        case "count":
                        case "sum":
                        case "string-length":
                        case "number":
                        case "floor":
                        case "ceiling":
                        case "round":
                            return XType.Number;
                        case "starts-with":
                        case "contains":
                        case "boolean":
                        case "not":
                        case "true":
                        case "false":
                            return XType.Boolean;
                        case "current":
                            return XType.NodeSet;
                        default:
                            return XType.String;
                    }
                default:
                    return XType.NodeSet;
            }
        }

        private Compiled CompileBinary(BinaryExpr binary, Context context)
        {
            var left = Compile(binary.Left, context);
            var right = Compile(binary.Right, context);
            switch (binary.Op)
            {
                case "or":
                    return new Compiled("(" + Bool(left) + " || " + Bool(right) + ")", XType.Boolean);
                case "and":
                    return new Compiled("(" + Bool(left) + " && " + Bool(right) + ")", XType.Boolean);
                case "+":
                case "-":
                case "*":
                    return new Compiled("(" + Num(left) + " " + binary.Op + " " + Num(right) + ")", XType.Number);
                case "div":
                    return new Compiled("(" + Num(left) + " / " + Num(right) + ")", XType.Number);
                case "mod":
                    return new Compiled("(" + Num(left) + " % " + Num(right) + ")", XType.Number);
                default:
                    return CompileComparison(binary.Op, left, right);
            }
        }

        private static bool IsUnknown(XType type) => type == XType.Any || type == XType.None;

        private Compiled CompileComparison(string op, Compiled left, Compiled right)
        {
            if (left.IsNodeSet && !right.IsNodeSet && !IsUnknown(right.Type))
                return new Compiled(CompareNodes(left, op, right, false), XType.Boolean);
            if (right.IsNodeSet && !left.IsNodeSet && !IsUnknown(left.Type))
                return new Compiled(CompareNodes(right, op, left, true), XType.Boolean);
            if (left.IsNodeSet || right.IsNodeSet || IsUnknown(left.Type) || IsUnknown(right.Type))
                return new Compiled("Compare(" + Box(left) + ", " + Box(right) + ", Comparison." + ComparisonName(op) + ")", XType.Boolean);
            var csharp = op == "=" ? "==" : op;
            string code;
            if (op != "=" && op != "!=")
                code = "(" + Num(left) + " " + csharp + " " + Num(right) + ")";
            else if (left.Type == XType.Boolean || right.Type == XType.Boolean)
                code = "(" + Bool(left) + " " + csharp + " " + Bool(right) + ")";
            else if (left.Type == XType.Number || right.Type == XType.Number)
                code = "(" + Num(left) + " " + csharp + " " + Num(right) + ")";
            else
                code = "(" + Str(left) + " " + csharp + " " + Str(right) + ")";
            return new Compiled(code, XType.Boolean);
        }

        /// <summary>
        /// A node-set compared with a value that is not one: whether a node compares so, by its string value (with a
        /// boolean, the node-set as a boolean).
        /// </summary>
        private string CompareNodes(Compiled nodes, string op, Compiled other, bool swapped)
        {
            var csharp = op == "=" ? "==" : op;
            var equality = op == "=" || op == "!=";
            if (other.Type == XType.Boolean)
            {
                var exists = Bool(nodes);
                if (equality)
                    return "(" + exists + " " + csharp + " " + other.Code + ")";
                var number = "(" + exists + " ? 1D : 0D)";
                var otherNumber = "(" + other.Code + " ? 1D : 0D)";
                return "(" + (swapped ? otherNumber + " " + csharp + " " + number : number + " " + csharp + " " + otherNumber) + ")";
            }

            var numeric = other.Type == XType.Number || !equality;
            var argument = numeric ? Num(other) : Str(other);
            return Helper("bool", nodes.Context!, (context, w) =>
            {
                EmitNodes(nodes.Expr!, context, w, cursor =>
                {
                    var value = numeric ? "Num(" + StringValue(cursor) + ")" : StringValue(cursor);
                    w.Line("if (" + (swapped ? "o " + csharp + " " + value : value + " " + csharp + " o") + ")").Line("    return true;");
                });
                w.Line("return false;");
            }, numeric ? "double" : "string", argument);
        }

        private static string ComparisonName(string op)
        {
            switch (op)
            {
                case "=":
                    return "Equal";
                case "!=":
                    return "NotEqual";
                case "<":
                    return "Less";
                case "<=":
                    return "LessOrEqual";
                case ">":
                    return "Greater";
                default:
                    return "GreaterOrEqual";
            }
        }

        private Compiled CompileFunction(FunctionExpr function, Context context)
        {
            var args = function.Arguments.Select(a => Compile(a, context)).ToList();
            void Arity(int min, int max)
            {
                if (args.Count < min || args.Count > max)
                    throw new XsltException("Wrong number of arguments to " + function.Name + "().");
            }

            string StringArgument(int i) => args.Count > i ? Str(args[i]) : StringValue(new Cursor(context.Node, context.NodeKind));

            switch (function.Name)
            {
                case "last":
                    Arity(0, 0);
                    return new Compiled("((double)" + context.Size + ")", XType.Number);
                case "position":
                    Arity(0, 0);
                    return new Compiled("((double)" + context.Position + ")", XType.Number);
                case "count":
                    Arity(1, 1);
                    return new Compiled(args[0].IsNodeSet ? "((double)" + CountCode(args[0].Expr!, args[0].Context!) + ")" : "Count(" + Nodes(args[0]) + ")", XType.Number);
                case "sum":
                    Arity(1, 1);
                    return new Compiled("Sum(" + Nodes(args[0]) + ")", XType.Number);
                case "local-name":
                case "namespace-uri":
                case "name":
                {
                    Arity(0, 1);
                    var method = function.Name == "local-name" ? "LocalName" : function.Name == "name" ? "Name" : "NamespaceUri";
                    string node;
                    if (args.Count == 0)
                        node = context.Node;
                    else if (args[0].IsNodeSet)
                        node = FirstNodeCode(args[0].Expr!, args[0].Context!);
                    else
                        node = Nodes(args[0]);
                    return new Compiled(method + "(" + node + ")", XType.String);
                }
                case "string":
                    Arity(0, 1);
                    return new Compiled(StringArgument(0), XType.String);
                case "concat":
                    if (args.Count < 2)
                        throw new XsltException("concat() takes at least two arguments.");
                    return new Compiled("string.Concat(" + string.Join(", ", args.Select(Str)) + ")", XType.String);
                case "starts-with":
                    Arity(2, 2);
                    return new Compiled("StartsWith(" + Str(args[0]) + ", " + Str(args[1]) + ")", XType.Boolean);
                case "contains":
                    Arity(2, 2);
                    return new Compiled("Contains(" + Str(args[0]) + ", " + Str(args[1]) + ")", XType.Boolean);
                case "substring-before":
                    Arity(2, 2);
                    return new Compiled("SubstringBefore(" + Str(args[0]) + ", " + Str(args[1]) + ")", XType.String);
                case "substring-after":
                    Arity(2, 2);
                    return new Compiled("SubstringAfter(" + Str(args[0]) + ", " + Str(args[1]) + ")", XType.String);
                case "substring":
                    Arity(2, 3);
                    return new Compiled("Substring(" + string.Join(", ", new[] { Str(args[0]) }.Concat(args.Skip(1).Select(Num))) + ")", XType.String);
                case "string-length":
                    Arity(0, 1);
                    return new Compiled("StringLength(" + StringArgument(0) + ")", XType.Number);
                case "normalize-space":
                    Arity(0, 1);
                    return new Compiled("NormalizeSpace(" + StringArgument(0) + ")", XType.String);
                case "translate":
                    Arity(3, 3);
                    if (function.Arguments[1] is LiteralExpr from && function.Arguments[2] is LiteralExpr to)
                        return new Compiled(stylesheet.StaticField("TranslateMap", "new TranslateMap(" + Literal(from.Value) + ", " + Literal(to.Value) + ")") + ".Apply(" + Str(args[0]) + ")", XType.String);
                    return new Compiled("Translate(" + Str(args[0]) + ", " + Str(args[1]) + ", " + Str(args[2]) + ", ref " + stylesheet.CacheField("TranslateMap?") + ")", XType.String);
                case "boolean":
                    Arity(1, 1);
                    return new Compiled(Bool(args[0]), XType.Boolean);
                case "not":
                    Arity(1, 1);
                    return new Compiled("(!" + Bool(args[0]) + ")", XType.Boolean);
                case "true":
                    Arity(0, 0);
                    return new Compiled("true", XType.Boolean);
                case "false":
                    Arity(0, 0);
                    return new Compiled("false", XType.Boolean);
                case "number":
                    Arity(0, 1);
                    return new Compiled(args.Count == 0 ? "Num(" + StringArgument(0) + ")" : Num(args[0]), XType.Number);
                case "floor":
                    Arity(1, 1);
                    return new Compiled("System.Math.Floor(" + Num(args[0]) + ")", XType.Number);
                case "ceiling":
                    Arity(1, 1);
                    return new Compiled("System.Math.Ceiling(" + Num(args[0]) + ")", XType.Number);
                case "round":
                    Arity(1, 1);
                    return new Compiled("Round(" + Num(args[0]) + ")", XType.Number);
                default:
                    throw new XsltException("The function " + function.Name + "() is not supported.");
            }
        }

        // node-sets: what the use of one needs

        /// <summary>The list of the nodes, in document order.</summary>
        public string NodesCode(Expr expr, Context context)
        {
            switch (expr)
            {
                case VariableExpr variable:
                {
                    var found = Find(variable, context);
                    return found.Type == XType.NodeSet ? found.Id : Nodes(new Compiled(found.Id, found.Type));
                }
                case BinaryExpr union when union.Op == "|":
                    return "Union(" + NodesCode(union.Left, context) + ", " + NodesCode(union.Right, context) + ")";
                case FilterExpr _:
                case PathExpr _:
                case FunctionExpr _ when IsCurrent(expr):
                {
                    var order = OrderOf(expr, context);
                    return Helper("List<XObject>", context, (inner, w) =>
                    {
                        if (order == NodeOrder.Single)
                        {
                            EmitNodes(expr, inner, w, cursor => w.Line("return Single(" + cursor.Var + ");"));
                            w.Line("return Empty;");
                            return;
                        }
                        w.Line("var list = new List<XObject>();");
                        EmitNodes(expr, inner, w, cursor => w.Line("list.Add(" + cursor.Var + ");"));
                        switch (order)
                        {
                            case NodeOrder.Reversed:
                                w.Line("list.Reverse();");
                                break;
                            case NodeOrder.Unordered:
                                w.Line("SortUnique(list);");
                                break;
                        }
                        w.Line("return list;");
                    });
                }
                default:
                    return Nodes(Compile(expr, context));
            }
        }

        private static bool IsCurrent(Expr expr) => expr is FunctionExpr function && function.Name == "current";

        public static bool IsStreamed(NodeOrder order) => order == NodeOrder.Single || order == NodeOrder.Disjoint || order == NodeOrder.Ordered;

        /// <summary>Whether the node-set has a node.</summary>
        private string ExistsCode(Expr expr, Context context)
        {
            switch (expr)
            {
                case VariableExpr _:
                    return "(" + NodesCode(expr, context) + ".Count > 0)";
                case BinaryExpr union when union.Op == "|":
                    return "(" + ExistsCode(union.Left, context) + " || " + ExistsCode(union.Right, context) + ")";
                default:
                    return Helper("bool", context, (inner, w) =>
                    {
                        EmitNodes(expr, inner, w, cursor => w.Line("return true;"));
                        w.Line("return false;");
                    });
            }
        }

        /// <summary>The first node in document order, or null.</summary>
        private string FirstNodeCode(Expr expr, Context context)
        {
            var order = OrderOf(expr, context);
            if (expr is VariableExpr || expr is BinaryExpr || order == NodeOrder.Unordered)
                return "First(" + NodesCode(expr, context) + ")";
            return Helper("XObject?", context, (inner, w) =>
            {
                if (order == NodeOrder.Reversed)
                {
                    w.Line("XObject? first = null;");
                    EmitNodes(expr, inner, w, cursor => w.Line("first = " + cursor.Var + ";"));
                    w.Line("return first;");
                    return;
                }
                EmitNodes(expr, inner, w, cursor => w.Line("return " + cursor.Var + ";"));
                w.Line("return null;");
            });
        }

        /// <summary>The string value of the first node in document order, or the empty string.</summary>
        private string FirstStringCode(Expr expr, Context context)
        {
            var order = OrderOf(expr, context);
            if (expr is VariableExpr || expr is BinaryExpr || order == NodeOrder.Unordered)
                return "Str(" + NodesCode(expr, context) + ")";
            return Helper("string", context, (inner, w) =>
            {
                if (order == NodeOrder.Reversed)
                {
                    w.Line("XObject? first = null;");
                    EmitNodes(expr, inner, w, cursor => w.Line("first = " + cursor.Var + ";"));
                    w.Line("return first == null ? \"\" : StringValue(first);");
                    return;
                }
                EmitNodes(expr, inner, w, cursor => w.Line("return " + StringValue(cursor) + ";"));
                w.Line("return \"\";");
            });
        }

        private string CountCode(Expr expr, Context context)
        {
            if (expr is VariableExpr || expr is BinaryExpr || OrderOf(expr, context) == NodeOrder.Unordered)
                return NodesCode(expr, context) + ".Count";
            return Helper("int", context, (inner, w) =>
            {
                w.Line("var count = 0;");
                EmitNodes(expr, inner, w, cursor => w.Line("count++;"));
                w.Line("return count;");
            });
        }

        /// <summary>
        /// A method for an expression, whose parameters are what the expression reads of its context (the node, the
        /// position, the size, the current node) and the variables it reads; the code that calls it.
        /// </summary>
        private string Helper(string returnType, Context context, System.Action<Context, CodeWriter> emit, string? extraType = null, string? extraArgument = null)
        {
            var scope = new CapturingScope(context.Scope);
            var focus = new Focus();
            var inner = new Context("n", context.NodeKind, "p", "s", focus, "c", context.CurrentKind, focus, scope, context.Element, new Names(), context.Purity);
            var body = new CodeWriter(3);
            emit(inner, body);

            var parameters = new List<string>();
            var arguments = new List<string>();
            if (focus.NodeUsed)
            {
                parameters.Add(TypeName(context.NodeKind) + " n");
                arguments.Add(context.Node);
            }
            if (focus.PositionUsed)
            {
                parameters.Add("int p");
                arguments.Add(context.Position);
            }
            if (focus.SizeUsed)
            {
                parameters.Add("int s");
                arguments.Add(context.Size);
            }
            if (focus.CurrentUsed)
            {
                parameters.Add(TypeName(context.CurrentKind) + " c");
                arguments.Add(context.Current);
            }
            foreach (var variable in scope.Captured)
            {
                parameters.Add(TypeName(variable.Type) + " " + variable.Id);
                arguments.Add(variable.Id);
            }
            if (extraType != null)
            {
                parameters.Add(extraType + " o");
                arguments.Add(extraArgument!);
            }
            return stylesheet.HelperMethod(returnType, string.Join(", ", parameters), body.ToString()) + "(" + string.Join(", ", arguments) + ")";
        }

        // node-sets: the loops

        /// <summary>The order in which <see cref="EmitNodes"/> reaches the nodes of the expression.</summary>
        public NodeOrder OrderOf(Expr expr, Context context)
        {
            switch (expr)
            {
                case VariableExpr variable:
                {
                    var found = Find(variable, context);
                    return found.Type == XType.NodeSet ? found.Order : NodeOrder.Ordered;
                }
                case FunctionExpr _ when IsCurrent(expr):
                    return NodeOrder.Single;
                case FilterExpr filter:
                    return OrderOf(filter.Primary, context) == NodeOrder.Single || filter.Predicates.Any(p => SelectsOne(p, context)) ? NodeOrder.Single : NodeOrder.Ordered;
                case PathExpr path:
                {
                    var order = path.Start == null ? NodeOrder.Single : OrderOf(path.Start, context);
                    foreach (var step in Steps(path, context))
                        order = After(order, step, context);
                    return order;
                }
                default:
                    return NodeOrder.Ordered;
            }
        }

        private NodeOrder After(NodeOrder order, Step step, Context context)
        {
            if (step.Axis == "self")
                return order;
            var one = step.Predicates.Any(p => SelectsOne(p, context));
            switch (order)
            {
                case NodeOrder.Single:
                    switch (step.Axis)
                    {
                        case "parent":
                            return NodeOrder.Single;
                        case "attribute" when step.Test.Kind == TestKind.Name:
                            return NodeOrder.Single;
                        case "child":
                        case "attribute":
                        case "following-sibling":
                            return one ? NodeOrder.Single : NodeOrder.Disjoint;
                        case "descendant":
                        case "descendant-or-self":
                            return one ? NodeOrder.Single : NodeOrder.Ordered;
                        case "preceding-sibling" when !IsReverse(step, context):
                            return NodeOrder.Disjoint;
                        default:
                            return one ? NodeOrder.Single : NodeOrder.Reversed;
                    }
                case NodeOrder.Disjoint:
                    switch (step.Axis)
                    {
                        case "child":
                        case "attribute":
                            return NodeOrder.Disjoint;
                        case "descendant":
                        case "descendant-or-self":
                            return NodeOrder.Ordered;
                        default:
                            return NodeOrder.Unordered;
                    }
                case NodeOrder.Ordered when step.Axis == "attribute":
                    return NodeOrder.Ordered;
                default:
                    return NodeOrder.Unordered;
            }
        }

        // a predicate that keeps at most one node of those of a context node: a position
        private bool SelectsOne(Expr predicate, Context context) => IsLast(predicate) || TypeOf(predicate, context) == XType.Number;

        private static bool IsLast(Expr predicate) => predicate is FunctionExpr function && function.Name == "last" && function.Arguments.Count == 0;

        // the nodes before a node are reached from the nearest, as their positions count, only when a predicate counts them
        private bool IsReverse(Step step, Context context)
        {
            switch (step.Axis)
            {
                case "ancestor":
                case "ancestor-or-self":
                    return true;
                case "preceding-sibling":
                    return step.Predicates.Any(p => IsPositional(p, context));
                default:
                    return false;
            }
        }

        /// <summary>The C# type of the variables that hold the nodes of the expression.</summary>
        public NodeKind KindOf(Expr expr, Context context)
        {
            switch (expr)
            {
                case FunctionExpr _ when IsCurrent(expr):
                    return context.CurrentKind;
                case PathExpr path:
                {
                    NodeKind kind;
                    if (path.Start != null)
                        kind = KindOf(path.Start, context);
                    else
                        kind = path.Absolute ? NodeKind.Object : context.NodeKind;
                    foreach (var step in Steps(path, context))
                        kind = KindAfter(step, kind, context);
                    return kind;
                }
                default:
                    return NodeKind.Object;
            }
        }

        private NodeKind KindAfter(Step step, NodeKind kind, Context context) =>
            Test(step.Test, step.Axis == "attribute", new Cursor("node", AxisKind(step.Axis, kind)), false, context).Cursor.Kind;

        private static NodeKind AxisKind(string axis, NodeKind kind)
        {
            switch (axis)
            {
                case "child":
                case "following-sibling":
                case "preceding-sibling":
                    return NodeKind.Node;
                case "attribute":
                    return NodeKind.Attribute;
                case "self":
                    return kind;
                default:
                    return NodeKind.Object;
            }
        }

        /// <summary>
        /// The steps of a path as they are compiled: without the steps that do nothing, and with //name as
        /// descendant::name when its predicates do not count positions among the children.
        /// </summary>
        private List<Step> Steps(PathExpr path, Context context)
        {
            var steps = new List<Step>();
            for (var i = 0; i < path.Steps.Count; i++)
            {
                var step = path.Steps[i];
                if (step.Axis == "self" && step.Test.Kind == TestKind.Node && step.Predicates.Count == 0)
                    continue;
                if (step.IsDescendantOrSelfNode && i + 1 < path.Steps.Count && path.Steps[i + 1].Axis == "child"
                    && path.Steps[i + 1].Predicates.All(p => !IsPositional(p, context)))
                {
                    var next = path.Steps[i + 1];
                    steps.Add(new Step("descendant", next.Test, next.Predicates));
                    i++;
                    continue;
                }
                steps.Add(step);
            }
            return steps;
        }

        /// <summary>
        /// Writes the loops over the nodes of the expression, and what <paramref name="inner"/> writes where a node is
        /// reached. The nodes are reached in the order <see cref="OrderOf"/> tells, held as <see cref="KindOf"/> tells.
        /// </summary>
        public void EmitNodes(Expr expr, Context context, CodeWriter w, System.Action<Cursor> inner)
        {
            var names = context.Names;
            switch (expr)
            {
                case PathExpr path:
                {
                    var steps = Steps(path, context);
                    void Chain(int i, Cursor cursor)
                    {
                        if (i == steps.Count)
                            inner(cursor);
                        else
                            EmitStep(cursor, steps[i], context, w, next => Chain(i + 1, next));
                    }

                    if (path.Start != null)
                    {
                        EmitNodes(path.Start, context, w, cursor => Chain(0, cursor));
                    }
                    else if (path.Absolute)
                    {
                        var root = names.New("x_");
                        w.Line("var " + root + " = RootNode(" + context.Node + ");");
                        Chain(0, new Cursor(root, NodeKind.Object));
                    }
                    else
                    {
                        Chain(0, new Cursor(context.Node, context.NodeKind));
                    }
                    break;
                }
                case FilterExpr filter:
                    EmitFiltered(NodeKind.Object, true, source => EmitList(NodesCode(filter.Primary, context), context, w, source), filter.Predicates, context, w, inner);
                    break;
                case FunctionExpr _ when IsCurrent(expr):
                    inner(new Cursor(context.Current, context.CurrentKind));
                    break;
                default:
                    EmitList(NodesCode(expr, context), context, w, inner);
                    break;
            }
        }

        private static void EmitList(string list, Context context, CodeWriter w, System.Action<Cursor> inner)
        {
            var l = context.Names.New("l_");
            var i = context.Names.New("i_");
            var x = context.Names.New("x_");
            w.Line("var " + l + " = " + list + ";");
            w.Open("for (var " + i + " = 0; " + i + " < " + l + ".Count; " + i + "++)");
            w.Line("var " + x + " = " + l + "[" + i + "];");
            inner(new Cursor(x, NodeKind.Object));
            w.Close();
        }

        public void EmitStep(Cursor cursor, Step step, Context context, CodeWriter w, System.Action<Cursor> inner)
        {
            var reverse = IsReverse(step, context);
            EmitFiltered(KindAfter(step, cursor.Kind, context), IsLoop(step), source => EmitAxis(cursor, step, reverse, context, w, source), step.Predicates, context, w, inner);
        }

        private static bool IsDeclaration(NodeTestSyntax test) => test.Prefix == "xmlns" || (test.Prefix == null && test.Local == "xmlns");

        // whether the nodes of the step are reached in a loop, which a break ends
        private static bool IsLoop(Step step)
        {
            switch (step.Axis)
            {
                case "self":
                case "parent":
                    return false;
                case "attribute":
                    return step.Test.Kind != TestKind.Name || IsDeclaration(step.Test);
                default:
                    return true;
            }
        }

        private sealed class Predicate
        {
            public Predicate(string position, string size)
            {
                Position = position;
                Size = size;
            }

            public string Position { get; }

            public string Size { get; }

            public string Code { get; set; } = "";

            public bool Positional { get; set; }

            public bool IsLast { get; set; }

            public bool NeedsSize { get; set; }

            public bool CanBreak { get; set; }
        }

        /// <summary>
        /// The nodes of a source that pass the predicates. A predicate that counts positions counts them among the
        /// nodes that passed the ones before it; one that reads the size has them counted first.
        /// </summary>
        public void EmitFiltered(NodeKind kind, bool isLoop, System.Action<System.Action<Cursor>> source, List<Expr> predicates, Context context, CodeWriter w, System.Action<Cursor> inner)
        {
            if (predicates.Count == 0)
            {
                source(inner);
                return;
            }

            var names = context.Names;
            var node = names.New("m_");
            var compiled = new List<Predicate>();
            foreach (var expr in predicates)
            {
                var predicate = new Predicate(names.New("pos_"), names.New("size_")) { Positional = IsPositional(expr, context), IsLast = IsLast(expr) };
                if (!predicate.IsLast)
                {
                    var focus = new Focus();
                    var value = Compile(expr, context.WithFocus(node, kind, predicate.Position, predicate.Size, focus));
                    switch (value.Type)
                    {
                        case XType.Number:
                            predicate.Code = predicate.Position + " == " + value.Code;
                            predicate.CanBreak = !focus.NodeUsed && !focus.PositionUsed;
                            break;
                        case XType.Any:
                        case XType.None:
                            predicate.Code = "Predicate(" + value.Code + ", " + predicate.Position + ")";
                            break;
                        default:
                            predicate.Code = Bool(value);
                            break;
                    }
                    predicate.NeedsSize = focus.SizeUsed;
                }
                compiled.Add(predicate);
            }

            // whether the nodes that pass the first predicates are reached in the loop of the source
            bool InLoop(int count) => isLoop && !compiled.Take(count).Any(p => p.Positional && p.IsLast);

            void Passing(int count, System.Action body)
            {
                if (count == 0)
                {
                    source(cursor =>
                    {
                        w.Line("var " + node + " = " + cursor.Var + ";");
                        body();
                    });
                    return;
                }

                var predicate = compiled[count - 1];
                if (!predicate.Positional)
                {
                    Passing(count - 1, () =>
                    {
                        w.Open("if (" + predicate.Code + ")");
                        body();
                        w.Close();
                    });
                    return;
                }

                if (predicate.IsLast)
                {
                    var last = names.New("last_");
                    w.Line(TypeName(kind) + "? " + last + " = null;");
                    Passing(count - 1, () => w.Line(last + " = " + node + ";"));
                    w.Open("if (" + last + " != null)");
                    w.Line("var " + node + " = " + last + ";");
                    body();
                    w.Close();
                    return;
                }

                // the two passes declare the same variables, each in a block of its own
                if (predicate.NeedsSize)
                {
                    w.Line("var " + predicate.Size + " = 0;");
                    w.Open();
                    Passing(count - 1, () => w.Line(predicate.Size + "++;"));
                    w.Close();
                    w.Open();
                }
                w.Line("var " + predicate.Position + " = 0;");
                Passing(count - 1, () =>
                {
                    w.Line(predicate.Position + "++;");
                    w.Open("if (" + predicate.Code + ")");
                    body();
                    if (predicate.CanBreak && InLoop(count - 1))
                        w.Line("break;");
                    w.Close();
                });
                if (predicate.NeedsSize)
                    w.Close();
            }

            Passing(compiled.Count, () => inner(new Cursor(node, kind)));
        }

        /// <summary>The nodes of an axis from a node that pass the node test of the step.</summary>
        private void EmitAxis(Cursor cursor, Step step, bool reverse, Context context, CodeWriter w, System.Action<Cursor> inner)
        {
            var names = context.Names;
            var c = cursor.Var;
            var leaf = cursor.Kind == NodeKind.Attribute || cursor.Kind == NodeKind.Text;

            void Pass(Cursor candidate, bool attributeAxis, bool amongChildren, string? condition = null)
            {
                var tested = Test(step.Test, attributeAxis, candidate, amongChildren, context);
                if (tested.Never)
                    return;
                var all = condition == null ? tested.Condition : tested.Condition == null ? condition : condition + " && " + tested.Condition;
                if (all == null)
                {
                    inner(tested.Cursor);
                    return;
                }
                w.Open("if (" + all + ")");
                inner(tested.Cursor);
                w.Close();
            }

            void Loop(string type, string first, string next, bool amongChildren, NodeKind kind, string? until = null)
            {
                var x = names.New("x_");
                w.Open("for (" + type + " " + x + " = " + first + "; " + x + " != null" + (until == null ? "" : " && !ReferenceEquals(" + x + ", " + until + ")") + "; " + x + " = " + string.Format(CultureInfo.InvariantCulture, next, x) + ")");
                Pass(new Cursor(x, kind), false, amongChildren);
                w.Close();
            }

            var typed = cursor.Kind == NodeKind.Element || cursor.Kind == NodeKind.Node || cursor.Kind == NodeKind.Text;
            switch (step.Axis)
            {
                case "child":
                    if (!leaf)
                        Loop("var", cursor.Kind == NodeKind.Element ? c + ".FirstNode" : "FirstChild(" + c + ")", "{0}.NextNode", true, NodeKind.Node);
                    break;
                case "descendant":
                    if (!leaf)
                        Loop("XObject?", "FirstChild(" + c + ")", "NextInTree({0}, " + c + ")", true, NodeKind.Object);
                    break;
                case "descendant-or-self":
                    Loop("XObject?", c, "NextInTree({0}, " + c + ")", true, NodeKind.Object);
                    break;
                case "following-sibling":
                    if (cursor.Kind != NodeKind.Attribute)
                        Loop("var", typed ? c + ".NextNode" : "NextSibling(" + c + ")", "{0}.NextNode", true, NodeKind.Node);
                    break;
                case "preceding-sibling" when cursor.Kind == NodeKind.Attribute:
                    break;
                case "preceding-sibling" when reverse:
                    Loop("var", typed ? c + ".PreviousNode" : "PreviousSibling(" + c + ")", "{0}.PreviousNode", true, NodeKind.Node);
                    break;
                case "preceding-sibling":
                    Loop("var", "FirstSibling(" + c + ")", "{0}.NextNode", true, NodeKind.Node, c);
                    break;
                case "ancestor":
                    Loop("var", "Parent(" + c + ")", "Parent({0})", false, NodeKind.Object);
                    break;
                case "ancestor-or-self":
                    Loop("XObject?", c, "Parent({0})", false, NodeKind.Object);
                    break;
                case "parent":
                {
                    var parent = names.New("x_");
                    w.Line("var " + parent + " = Parent(" + c + ");");
                    Pass(new Cursor(parent, NodeKind.Object), false, false, parent + " != null");
                    break;
                }
                case "self":
                    Pass(cursor, false, false);
                    break;
                case "attribute":
                {
                    if (leaf)
                        break;
                    var element = c;
                    if (cursor.Kind != NodeKind.Element)
                    {
                        element = names.New("e_");
                        w.Open("if (" + c + " is XElement " + element + ")");
                    }
                    var attribute = names.New("a_");
                    if (IsLoop(step))
                    {
                        w.Open("for (var " + attribute + " = " + element + ".FirstAttribute; " + attribute + " != null; " + attribute + " = " + attribute + ".NextAttribute)");
                        Pass(new Cursor(attribute, NodeKind.Attribute), true, false, "!" + attribute + ".IsNamespaceDeclaration");
                        w.Close();
                    }
                    else
                    {
                        w.Line("var " + attribute + " = " + element + ".Attribute(" + stylesheet.Name(XName.Get(step.Test.Local!, StylesheetCompiler.Namespace(step.Test.Prefix, context.Element))) + ");");
                        w.Open("if (" + attribute + " != null)");
                        inner(new Cursor(attribute, NodeKind.Attribute));
                        w.Close();
                    }
                    if (cursor.Kind != NodeKind.Element)
                        w.Close();
                    break;
                }
                default:
                    throw new XsltException("The axis " + step.Axis + " is not supported.");
            }
        }

        internal readonly struct Tested
        {
            public Tested(string? condition, Cursor cursor, bool never = false)
            {
                Condition = condition;
                Cursor = cursor;
                Never = never;
            }

            /// <summary>The C# condition of the test, or null when every node passes.</summary>
            public string? Condition { get; }

            /// <summary>The node, as the type the test tells it has.</summary>
            public Cursor Cursor { get; }

            /// <summary>Whether no node passes.</summary>
            public bool Never { get; }
        }

        /// <summary>
        /// A node test on a node: on the attribute axis, whose nodes are attributes, or on another axis, where a name
        /// is the name of an element. Among the children of a node, those of a document that are no nodes of XPath
        /// (the document type, the white space) do not pass.
        /// </summary>
        public Tested Test(NodeTestSyntax test, bool attributeAxis, Cursor cursor, bool amongChildren, Context context)
        {
            var c = cursor.Var;
            if (attributeAxis)
            {
                switch (test.Kind)
                {
                    case TestKind.Name:
                        return new Tested(c + ".Name == " + stylesheet.Name(XName.Get(test.Local!, StylesheetCompiler.Namespace(test.Prefix, context.Element))), cursor);
                    case TestKind.Namespace:
                        return new Tested(c + ".Name.Namespace == " + NamespaceField(test, context), cursor);
                    case TestKind.AnyName:
                    case TestKind.Node:
                        return new Tested(null, cursor);
                    default:
                        return new Tested(null, cursor, true);
                }
            }

            var leaf = cursor.Kind == NodeKind.Attribute || cursor.Kind == NodeKind.Text;
            switch (test.Kind)
            {
                case TestKind.Name:
                case TestKind.Namespace:
                case TestKind.AnyName:
                {
                    if (leaf)
                        return new Tested(null, new Cursor(c, NodeKind.Element), true);
                    var element = c;
                    string? condition = null;
                    if (cursor.Kind != NodeKind.Element)
                    {
                        element = context.Names.New("e_");
                        condition = c + " is XElement " + element;
                    }
                    string? name = null;
                    switch (test.Kind)
                    {
                        case TestKind.Name:
                            name = element + ".Name == " + stylesheet.Name(XName.Get(test.Local!, StylesheetCompiler.Namespace(test.Prefix, context.Element)));
                            break;
                        case TestKind.Namespace:
                            name = element + ".Name.Namespace == " + NamespaceField(test, context);
                            break;
                    }
                    if (name != null)
                        condition = condition == null ? name : condition + " && " + name;
                    return new Tested(condition, new Cursor(element, NodeKind.Element));
                }
                case TestKind.Node:
                    return new Tested(amongChildren ? "IsNode(" + c + ")" : null, cursor);
                case TestKind.Text:
                {
                    switch (cursor.Kind)
                    {
                        case NodeKind.Text:
                            return new Tested(null, cursor);
                        case NodeKind.Element:
                        case NodeKind.Attribute:
                            return new Tested(null, new Cursor(c, NodeKind.Text), true);
                    }
                    var text = context.Names.New("t_");
                    return new Tested(c + " is XText " + text + (amongChildren ? " && IsNode(" + text + ")" : ""), new Cursor(text, NodeKind.Text));
                }
                case TestKind.Comment:
                    return new Tested(c + " is XComment", cursor, cursor.Kind == NodeKind.Element || leaf);
                default:
                {
                    if (test.Local == null)
                        return new Tested(c + " is XProcessingInstruction", cursor, cursor.Kind == NodeKind.Element || leaf);
                    var instruction = context.Names.New("i_");
                    return new Tested(c + " is XProcessingInstruction " + instruction + " && " + instruction + ".Target == " + Literal(test.Local), cursor, cursor.Kind == NodeKind.Element || leaf);
                }
            }
        }

        private string NamespaceField(NodeTestSyntax test, Context context) =>
            stylesheet.StaticField("XNamespace", "XNamespace.Get(" + Literal(StylesheetCompiler.Namespace(test.Prefix, context.Element)) + ")");

        /// <summary>
        /// Whether the value of a predicate depends on the position of the node among the others: a number (or a
        /// value that may be one), or an expression that reads position() or last().
        /// </summary>
        public bool IsPositional(Expr predicate, Context context)
        {
            if (UsesFocus(predicate))
                return true;
            var type = TypeOf(predicate, context);
            return type == XType.Number || IsUnknown(type);
        }

        // whether the expression reads the position or size of its context (not that of a nested predicate)
        private static bool UsesFocus(Expr expr)
        {
            switch (expr)
            {
                case FunctionExpr function when function.Name == "position" || function.Name == "last":
                    return true;
                case FunctionExpr function:
                    return function.Arguments.Any(UsesFocus);
                case BinaryExpr binary:
                    return UsesFocus(binary.Left) || UsesFocus(binary.Right);
                case NegateExpr negate:
                    return UsesFocus(negate.Operand);
                case FilterExpr filter:
                    return UsesFocus(filter.Primary);
                case PathExpr path:
                    return path.Start != null && UsesFocus(path.Start);
                default:
                    return false;
            }
        }

        // conversions

        public static string StringValue(Cursor cursor) =>
            cursor.Kind == NodeKind.Object || cursor.Kind == NodeKind.Node ? "StringValue(" + cursor.Var + ")" : cursor.Var + ".Value";

        public static string Str(Compiled value)
        {
            if (value.IsNodeSet)
                return value.Owner.FirstStringCode(value.Expr!, value.Context!);
            return value.Type == XType.String || value.Type == XType.Text ? value.Code : "Str(" + value.Code + ")";
        }

        public static string Num(Compiled value)
        {
            if (value.IsNodeSet)
                return "Num(" + Str(value) + ")";
            return value.Type == XType.Number ? value.Code : "Num(" + value.Code + ")";
        }

        public static string Bool(Compiled value)
        {
            if (value.IsNodeSet)
                return value.Owner.ExistsCode(value.Expr!, value.Context!);
            switch (value.Type)
            {
                case XType.Boolean:
                    return value.Code;
                // a result tree fragment is a node-set of its root node
                case XType.Text:
                case XType.Fragment:
                    return "true";
                default:
                    return "Bool(" + value.Code + ")";
            }
        }

        /// <summary>The value as an object: one of unknown type, as the value of a parameter of unknown type.</summary>
        public static string Box(Compiled value)
        {
            switch (value.Type)
            {
                case XType.Text:
                    return "new ResultFragment(" + value.Code + ")";
                case XType.Any:
                case XType.None:
                    return value.Code;
                default:
                    return "(object)" + value.Code;
            }
        }

        public static string Nodes(Compiled value)
        {
            switch (value.Type)
            {
                case XType.NodeSet:
                    return value.Code;
                case XType.Any:
                case XType.None:
                    return "Nodes(" + value.Code + ")";
                case XType.Text:
                case XType.Fragment:
                    throw new XsltException("A result tree fragment cannot be used as a node-set in XSLT 1.0.");
                default:
                    throw new XsltException("A " + value.Type + " cannot be used as a node-set.");
            }
        }

        public static string TypeName(XType type)
        {
            switch (type)
            {
                case XType.String:
                case XType.Text:
                    return "string";
                case XType.Number:
                    return "double";
                case XType.Boolean:
                    return "bool";
                case XType.NodeSet:
                    return "List<XObject>";
                case XType.Fragment:
                    return "ResultFragment";
                default:
                    return "object";
            }
        }

        public static string TypeName(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Node:
                    return "XNode";
                case NodeKind.Element:
                    return "XElement";
                case NodeKind.Attribute:
                    return "XAttribute";
                case NodeKind.Text:
                    return "XText";
                default:
                    return "XObject";
            }
        }

        public static string Literal(string value)
        {
            var result = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '"':
                        result.Append("\\\"");
                        break;
                    case var _ when c < 0x20 || c >= 0x7F:
                        result.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        break;
                    default:
                        result.Append(c);
                        break;
                }
            }
            return result.Append('"').ToString();
        }

        public static string Number(double value)
        {
            if (double.IsNaN(value))
                return "double.NaN";
            if (double.IsPositiveInfinity(value))
                return "double.PositiveInfinity";
            return value.ToString("R", CultureInfo.InvariantCulture) + "D";
        }
    }
}
