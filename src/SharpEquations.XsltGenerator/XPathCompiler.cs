namespace SharpEquations.XsltGenerator
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    /// <summary>The static type of an XPath value in the generated code.</summary>
    internal enum XType
    {
        String,
        Number,
        Boolean,
        NodeSet,
        Fragment,
        Any,
    }

    internal sealed class Compiled
    {
        public Compiled(string code, XType type)
        {
            Code = code;
            Type = type;
        }

        public string Code { get; }

        public XType Type { get; }
    }

    internal sealed class Variable
    {
        public Variable(string id, XType type)
        {
            Id = id;
            Type = type;
        }

        public string Id { get; }

        public XType Type { get; }
    }

    internal sealed class Scope
    {
        private readonly Dictionary<string, Variable> variables = new Dictionary<string, Variable>();
        private readonly Scope? parent;

        public Scope(Scope? parent) => this.parent = parent;

        public void Add(string name, Variable variable) => variables[name] = variable;

        public Variable? Find(string name) =>
            variables.TryGetValue(name, out var variable) ? variable : parent?.Find(name);
    }

    /// <summary>
    /// Where an expression is evaluated: the C# names of the context node, position and size, and of the current
    /// node of XSLT; the variables in scope; the element whose namespace declarations resolve its prefixes.
    /// </summary>
    internal sealed class Context
    {
        public Context(string node, string position, string size, string current, Scope scope, XElement element)
        {
            Node = node;
            Position = position;
            Size = size;
            Current = current;
            Scope = scope;
            Element = element;
        }

        public string Node { get; }

        public string Position { get; }

        public string Size { get; }

        public string Current { get; }

        public Scope Scope { get; }

        public XElement Element { get; }

        public Context WithFocus(string node, string position, string size) => new Context(node, position, size, Current, Scope, Element);

        public Context WithScope(Scope scope) => new Context(Node, Position, Size, Current, scope, Element);

        public Context WithElement(XElement element) => new Context(Node, Position, Size, Current, Scope, element);
    }

    /// <summary>XPath 1.0 expressions compiled to C# expressions over <c>XPathFunctions</c>.</summary>
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
                    var found = context.Scope.Find(variable.Name) ?? stylesheet.Global(variable.Name)
                        ?? throw new XsltException("The variable $" + variable.Name + " is not declared.");
                    return new Compiled(found.Id, found.Type);
                }
                case NegateExpr negate:
                    return new Compiled("(-" + Num(Compile(negate.Operand, context)) + ")", XType.Number);
                case BinaryExpr binary:
                    return CompileBinary(binary, context);
                case FunctionExpr function:
                    return CompileFunction(function, context);
                case FilterExpr filter:
                {
                    var code = Nodes(Compile(filter.Primary, context));
                    foreach (var predicate in filter.Predicates)
                        code = "Filter(" + code + ", " + Predicate(predicate, context) + ")";
                    return new Compiled(code, XType.NodeSet);
                }
                case PathExpr path:
                    return CompilePath(path, context);
                default:
                    throw new XsltException("Unknown expression.");
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
                case "|":
                    return new Compiled("Union(" + Nodes(left) + ", " + Nodes(right) + ")", XType.NodeSet);
                default:
                    return CompileComparison(binary.Op, left, right);
            }
        }

        private static bool IsNodes(XType type) => type == XType.NodeSet || type == XType.Fragment || type == XType.Any;

        private static Compiled CompileComparison(string op, Compiled left, Compiled right)
        {
            if (IsNodes(left.Type) || IsNodes(right.Type))
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

            string StringArgument(int i) => args.Count > i ? Str(args[i]) : "StringValue(" + context.Node + ")";

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
                    return new Compiled("Count(" + Nodes(args[0]) + ")", XType.Number);
                case "sum":
                    Arity(1, 1);
                    return new Compiled("Sum(" + Nodes(args[0]) + ")", XType.Number);
                case "local-name":
                case "namespace-uri":
                case "name":
                {
                    Arity(0, 1);
                    var method = function.Name == "local-name" ? "LocalName" : function.Name == "name" ? "Name" : "NamespaceUri";
                    return new Compiled(method + "(" + (args.Count == 0 ? context.Node : Nodes(args[0])) + ")", XType.String);
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
                    return new Compiled("Translate(" + Str(args[0]) + ", " + Str(args[1]) + ", " + Str(args[2]) + ", ref " + stylesheet.InstanceField("TranslateMap?") + ")", XType.String);
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
                    return new Compiled(args.Count == 0 ? "Num(StringValue(" + context.Node + "))" : Num(args[0]), XType.Number);
                case "floor":
                    Arity(1, 1);
                    return new Compiled("System.Math.Floor(" + Num(args[0]) + ")", XType.Number);
                case "ceiling":
                    Arity(1, 1);
                    return new Compiled("System.Math.Ceiling(" + Num(args[0]) + ")", XType.Number);
                case "round":
                    Arity(1, 1);
                    return new Compiled("Round(" + Num(args[0]) + ")", XType.Number);
                case "current":
                    Arity(0, 0);
                    return new Compiled("Single(" + context.Current + ")", XType.NodeSet);
                default:
                    throw new XsltException("The function " + function.Name + "() is not supported.");
            }
        }

        private Compiled CompilePath(PathExpr path, Context context)
        {
            string code;
            if (path.Start != null)
                code = Nodes(Compile(path.Start, context));
            else if (path.Absolute)
                code = "Root(" + context.Node + ")";
            else
                code = "Single(" + context.Node + ")";

            var steps = path.Steps;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step.Axis == "self" && step.Test.Kind == TestKind.Node && step.Predicates.Count == 0)
                    continue;

                // //name: descendant::name, when the predicates do not count positions among the children
                if (step.IsDescendantOrSelfNode && i + 1 < steps.Count && steps[i + 1].Axis == "child"
                    && steps[i + 1].Predicates.All(p => !IsPositional(p, context)))
                {
                    var next = steps[i + 1];
                    code = StepCode(code, "Descendant", next.Test, next.Predicates, false, context);
                    i++;
                    continue;
                }
                code = StepCode(code, AxisName(step.Axis), step.Test, step.Predicates, step.Axis == "attribute", context);
            }
            return new Compiled(code, XType.NodeSet);
        }

        private string StepCode(string input, string axis, NodeTestSyntax test, List<Expr> predicates, bool attributeAxis, Context context)
        {
            var testField = stylesheet.NodeTest(test, context.Element);
            if (predicates.Count == 0)
                return "Step(" + input + ", Axis." + axis + ", " + testField + ")";
            return "Step(" + input + ", Axis." + axis + ", " + testField + ", " + string.Join(", ", predicates.Select(p => Predicate(p, context))) + ")";
        }

        public static string AxisName(string axis)
        {
            switch (axis)
            {
                case "child":
                    return "Child";
                case "descendant":
                    return "Descendant";
                case "descendant-or-self":
                    return "DescendantOrSelf";
                case "parent":
                    return "Parent";
                case "ancestor":
                    return "Ancestor";
                case "ancestor-or-self":
                    return "AncestorOrSelf";
                case "following-sibling":
                    return "FollowingSibling";
                case "preceding-sibling":
                    return "PrecedingSibling";
                case "self":
                    return "Self";
                case "attribute":
                    return "Attribute";
                default:
                    throw new XsltException("The axis " + axis + " is not supported.");
            }
        }

        /// <summary>A lambda of <c>XPathPredicate</c>: the predicate evaluated with the node, its position and the size.</summary>
        public string Predicate(Expr predicate, Context context)
        {
            var n = stylesheet.NewName("n");
            var p = stylesheet.NewName("p");
            var s = stylesheet.NewName("s");
            var value = Compile(predicate, context.WithFocus(n, p, s));
            string body;
            switch (value.Type)
            {
                case XType.Number:
                    body = p + " == " + value.Code;
                    break;
                case XType.Any:
                    body = "Predicate(" + value.Code + ", " + p + ")";
                    break;
                default:
                    body = Bool(value);
                    break;
            }
            return "(" + n + ", " + p + ", " + s + ") => " + body;
        }

        /// <summary>
        /// Whether the value of a predicate depends on the position of the node among the others: a number (or a
        /// value that may be one), or an expression that reads position() or last().
        /// </summary>
        public bool IsPositional(Expr predicate, Context context)
        {
            if (UsesFocus(predicate))
                return true;
            var type = Compile(predicate, context.WithFocus("n", "p", "s")).Type;
            return type == XType.Number || type == XType.Any;
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

        public static string Str(Compiled value) => value.Type == XType.String ? value.Code : "Str(" + value.Code + ")";

        public static string Num(Compiled value) => value.Type == XType.Number ? value.Code : "Num(" + value.Code + ")";

        public static string Bool(Compiled value) => value.Type == XType.Boolean ? value.Code : "Bool(" + value.Code + ")";

        public static string Box(Compiled value) => "(object)" + value.Code;

        public static string Nodes(Compiled value)
        {
            switch (value.Type)
            {
                case XType.NodeSet:
                    return value.Code;
                case XType.Any:
                    return "Nodes(" + value.Code + ")";
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
