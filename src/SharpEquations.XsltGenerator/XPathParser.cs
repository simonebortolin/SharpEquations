namespace SharpEquations.XsltGenerator
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    internal abstract class Expr
    {
    }

    internal sealed class BinaryExpr : Expr
    {
        public BinaryExpr(string op, Expr left, Expr right)
        {
            Op = op;
            Left = left;
            Right = right;
        }

        public string Op { get; }

        public Expr Left { get; }

        public Expr Right { get; }
    }

    internal sealed class NegateExpr : Expr
    {
        public NegateExpr(Expr operand) => Operand = operand;

        public Expr Operand { get; }
    }

    internal sealed class LiteralExpr : Expr
    {
        public LiteralExpr(string value) => Value = value;

        public string Value { get; }
    }

    internal sealed class NumberExpr : Expr
    {
        public NumberExpr(double value) => Value = value;

        public double Value { get; }
    }

    internal sealed class VariableExpr : Expr
    {
        public VariableExpr(string name) => Name = name;

        public string Name { get; }
    }

    internal sealed class FunctionExpr : Expr
    {
        public FunctionExpr(string name, List<Expr> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        public string Name { get; }

        public List<Expr> Arguments { get; }
    }

    internal sealed class FilterExpr : Expr
    {
        public FilterExpr(Expr primary, List<Expr> predicates)
        {
            Primary = primary;
            Predicates = predicates;
        }

        public Expr Primary { get; }

        public List<Expr> Predicates { get; }
    }

    /// <summary>A location path (no start: relative to the context node, or absolute), or a filter expression followed by steps.</summary>
    internal sealed class PathExpr : Expr
    {
        public PathExpr(Expr? start, bool absolute, List<Step> steps)
        {
            Start = start;
            Absolute = absolute;
            Steps = steps;
        }

        public Expr? Start { get; }

        public bool Absolute { get; }

        public List<Step> Steps { get; }
    }

    internal enum TestKind
    {
        Name,
        Namespace,
        AnyName,
        Node,
        Text,
        Comment,
        ProcessingInstruction,
    }

    internal sealed class NodeTestSyntax
    {
        public NodeTestSyntax(TestKind kind, string? prefix = null, string? local = null)
        {
            Kind = kind;
            Prefix = prefix;
            Local = local;
        }

        public TestKind Kind { get; }

        public string? Prefix { get; }

        /// <summary>The local name, or the target of a processing-instruction() test.</summary>
        public string? Local { get; }
    }

    internal sealed class Step
    {
        public Step(string axis, NodeTestSyntax test, List<Expr> predicates)
        {
            Axis = axis;
            Test = test;
            Predicates = predicates;
        }

        public string Axis { get; }

        public NodeTestSyntax Test { get; }

        public List<Expr> Predicates { get; }

        public bool IsDescendantOrSelfNode => Axis == "descendant-or-self" && Test.Kind == TestKind.Node && Predicates.Count == 0;
    }

    /// <summary>A parser of XPath 1.0 expressions.</summary>
    internal sealed class XPathParser
    {
        private enum TokenKind
        {
            Name,
            NameStar,
            Star,
            Literal,
            Number,
            Variable,
            Operator,
            LeftParen,
            RightParen,
            LeftBracket,
            RightBracket,
            Dot,
            DotDot,
            At,
            Comma,
            ColonColon,
            Slash,
            DoubleSlash,
            Pipe,
            End,
        }

        private sealed class Token
        {
            public Token(TokenKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public TokenKind Kind { get; }

            public string Text { get; }
        }

        private static readonly HashSet<string> NodeTypes = new HashSet<string> { "node", "text", "comment", "processing-instruction" };

        private static readonly HashSet<string> Axes = new HashSet<string>
        {
            "ancestor", "ancestor-or-self", "attribute", "child", "descendant", "descendant-or-self", "following",
            "following-sibling", "namespace", "parent", "preceding", "preceding-sibling", "self",
        };

        private readonly string source;
        private readonly List<Token> tokens;
        private int index;

        private XPathParser(string source)
        {
            this.source = source;
            tokens = Tokenize(source);
        }

        public static Expr Parse(string source)
        {
            var parser = new XPathParser(source);
            var expr = parser.ParseOr();
            parser.Expect(TokenKind.End);
            return expr;
        }

        private Token Current => tokens[index];

        private Token Peek(int offset) => tokens[System.Math.Min(index + offset, tokens.Count - 1)];

        private XsltException Error(string message) => new XsltException(message + " in the expression '" + source + "'.");

        private Token Expect(TokenKind kind)
        {
            if (Current.Kind != kind)
                throw Error("Expected " + kind + ", found '" + Current.Text + "'");
            return tokens[index++];
        }

        private bool IsOperator(string op) => Current.Kind == TokenKind.Operator && Current.Text == op;

        private Expr ParseOr()
        {
            var left = ParseAnd();
            while (IsOperator("or"))
            {
                index++;
                left = new BinaryExpr("or", left, ParseAnd());
            }
            return left;
        }

        private Expr ParseAnd()
        {
            var left = ParseEquality();
            while (IsOperator("and"))
            {
                index++;
                left = new BinaryExpr("and", left, ParseEquality());
            }
            return left;
        }

        private Expr ParseEquality()
        {
            var left = ParseRelational();
            while (IsOperator("=") || IsOperator("!="))
            {
                var op = tokens[index++].Text;
                left = new BinaryExpr(op, left, ParseRelational());
            }
            return left;
        }

        private Expr ParseRelational()
        {
            var left = ParseAdditive();
            while (IsOperator("<") || IsOperator("<=") || IsOperator(">") || IsOperator(">="))
            {
                var op = tokens[index++].Text;
                left = new BinaryExpr(op, left, ParseAdditive());
            }
            return left;
        }

        private Expr ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOperator("+") || IsOperator("-"))
            {
                var op = tokens[index++].Text;
                left = new BinaryExpr(op, left, ParseMultiplicative());
            }
            return left;
        }

        private Expr ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsOperator("*") || IsOperator("div") || IsOperator("mod"))
            {
                var op = tokens[index++].Text;
                left = new BinaryExpr(op, left, ParseUnary());
            }
            return left;
        }

        private Expr ParseUnary()
        {
            if (IsOperator("-"))
            {
                index++;
                return new NegateExpr(ParseUnary());
            }
            return ParseUnion();
        }

        private Expr ParseUnion()
        {
            var left = ParsePath();
            while (Current.Kind == TokenKind.Pipe)
            {
                index++;
                left = new BinaryExpr("|", left, ParsePath());
            }
            return left;
        }

        private bool StartsFilter()
        {
            switch (Current.Kind)
            {
                case TokenKind.Variable:
                case TokenKind.LeftParen:
                case TokenKind.Literal:
                case TokenKind.Number:
                    return true;
                case TokenKind.Name:
                    return Peek(1).Kind == TokenKind.LeftParen && !NodeTypes.Contains(Current.Text);
                default:
                    return false;
            }
        }

        private bool StartsStep()
        {
            switch (Current.Kind)
            {
                case TokenKind.Name:
                case TokenKind.NameStar:
                case TokenKind.Star:
                case TokenKind.Dot:
                case TokenKind.DotDot:
                case TokenKind.At:
                    return true;
                default:
                    return false;
            }
        }

        private Expr ParsePath()
        {
            if (StartsFilter())
            {
                var primary = ParsePrimary();
                var predicates = ParsePredicates();
                Expr filter = predicates.Count == 0 ? primary : new FilterExpr(primary, predicates);
                if (Current.Kind != TokenKind.Slash && Current.Kind != TokenKind.DoubleSlash)
                    return filter;
                var steps = new List<Step>();
                ParseRelativePath(steps);
                return new PathExpr(filter, false, steps);
            }

            switch (Current.Kind)
            {
                case TokenKind.Slash:
                {
                    index++;
                    var steps = new List<Step>();
                    if (StartsStep())
                    {
                        steps.Add(ParseStep());
                        ParseRelativePath(steps);
                    }
                    return new PathExpr(null, true, steps);
                }
                case TokenKind.DoubleSlash:
                {
                    var steps = new List<Step>();
                    ParseRelativePath(steps);
                    return new PathExpr(null, true, steps);
                }
                default:
                {
                    var steps = new List<Step> { ParseStep() };
                    ParseRelativePath(steps);
                    return new PathExpr(null, false, steps);
                }
            }
        }

        // the steps after the current one: ('/' Step | '//' Step)*
        private void ParseRelativePath(List<Step> steps)
        {
            while (true)
            {
                switch (Current.Kind)
                {
                    case TokenKind.Slash:
                        index++;
                        steps.Add(ParseStep());
                        break;
                    case TokenKind.DoubleSlash:
                        index++;
                        steps.Add(new Step("descendant-or-self", new NodeTestSyntax(TestKind.Node), new List<Expr>()));
                        steps.Add(ParseStep());
                        break;
                    default:
                        return;
                }
            }
        }

        private Step ParseStep()
        {
            switch (Current.Kind)
            {
                case TokenKind.Dot:
                    index++;
                    return new Step("self", new NodeTestSyntax(TestKind.Node), new List<Expr>());
                case TokenKind.DotDot:
                    index++;
                    return new Step("parent", new NodeTestSyntax(TestKind.Node), new List<Expr>());
            }

            var axis = "child";
            if (Current.Kind == TokenKind.At)
            {
                index++;
                axis = "attribute";
            }
            else if (Current.Kind == TokenKind.Name && Peek(1).Kind == TokenKind.ColonColon)
            {
                axis = Current.Text;
                if (!Axes.Contains(axis))
                    throw Error("Unknown axis '" + axis + "'");
                index += 2;
            }
            var test = ParseNodeTest();
            return new Step(axis, test, ParsePredicates());
        }

        private NodeTestSyntax ParseNodeTest()
        {
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.Star:
                    index++;
                    return new NodeTestSyntax(TestKind.AnyName);
                case TokenKind.NameStar:
                    index++;
                    return new NodeTestSyntax(TestKind.Namespace, token.Text.Substring(0, token.Text.Length - 2));
                case TokenKind.Name when Peek(1).Kind == TokenKind.LeftParen && NodeTypes.Contains(token.Text):
                {
                    index += 2;
                    string? target = null;
                    if (token.Text == "processing-instruction" && Current.Kind == TokenKind.Literal)
                        target = tokens[index++].Text;
                    Expect(TokenKind.RightParen);
                    switch (token.Text)
                    {
                        case "node":
                            return new NodeTestSyntax(TestKind.Node);
                        case "text":
                            return new NodeTestSyntax(TestKind.Text);
                        case "comment":
                            return new NodeTestSyntax(TestKind.Comment);
                        default:
                            return new NodeTestSyntax(TestKind.ProcessingInstruction, local: target);
                    }
                }
                case TokenKind.Name:
                {
                    index++;
                    var colon = token.Text.IndexOf(':');
                    return colon < 0
                        ? new NodeTestSyntax(TestKind.Name, null, token.Text)
                        : new NodeTestSyntax(TestKind.Name, token.Text.Substring(0, colon), token.Text.Substring(colon + 1));
                }
                default:
                    throw Error("Expected a node test, found '" + token.Text + "'");
            }
        }

        private List<Expr> ParsePredicates()
        {
            var predicates = new List<Expr>();
            while (Current.Kind == TokenKind.LeftBracket)
            {
                index++;
                predicates.Add(ParseOr());
                Expect(TokenKind.RightBracket);
            }
            return predicates;
        }

        private Expr ParsePrimary()
        {
            var token = tokens[index++];
            switch (token.Kind)
            {
                case TokenKind.Variable:
                    return new VariableExpr(token.Text);
                case TokenKind.Literal:
                    return new LiteralExpr(token.Text);
                case TokenKind.Number:
                    return new NumberExpr(double.Parse(token.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture));
                case TokenKind.LeftParen:
                {
                    var expr = ParseOr();
                    Expect(TokenKind.RightParen);
                    return expr;
                }
                default:
                {
                    Expect(TokenKind.LeftParen);
                    var arguments = new List<Expr>();
                    if (Current.Kind != TokenKind.RightParen)
                    {
                        arguments.Add(ParseOr());
                        while (Current.Kind == TokenKind.Comma)
                        {
                            index++;
                            arguments.Add(ParseOr());
                        }
                    }
                    Expect(TokenKind.RightParen);
                    return new FunctionExpr(token.Text, arguments);
                }
            }
        }

        private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;

        private static string ReadName(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && IsNameChar(s[i]))
                i++;
            return s.Substring(start, i - start);
        }

        private static List<Token> Tokenize(string s)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (true)
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n'))
                    i++;
                if (i >= s.Length)
                {
                    tokens.Add(new Token(TokenKind.End, "end"));
                    return tokens;
                }

                // after a token that ends an operand, * and the operator names are operators (XPath 1.0, 3.7)
                var operand = tokens.Count > 0 && EndsOperand(tokens[tokens.Count - 1]);
                var c = s[i];
                var next = i + 1 < s.Length ? s[i + 1] : '\0';
                switch (c)
                {
                    case '(':
                        tokens.Add(new Token(TokenKind.LeftParen, "("));
                        i++;
                        break;
                    case ')':
                        tokens.Add(new Token(TokenKind.RightParen, ")"));
                        i++;
                        break;
                    case '[':
                        tokens.Add(new Token(TokenKind.LeftBracket, "["));
                        i++;
                        break;
                    case ']':
                        tokens.Add(new Token(TokenKind.RightBracket, "]"));
                        i++;
                        break;
                    case ',':
                        tokens.Add(new Token(TokenKind.Comma, ","));
                        i++;
                        break;
                    case '@':
                        tokens.Add(new Token(TokenKind.At, "@"));
                        i++;
                        break;
                    case '|':
                        tokens.Add(new Token(TokenKind.Pipe, "|"));
                        i++;
                        break;
                    case ':' when next == ':':
                        tokens.Add(new Token(TokenKind.ColonColon, "::"));
                        i += 2;
                        break;
                    case '/' when next == '/':
                        tokens.Add(new Token(TokenKind.DoubleSlash, "//"));
                        i += 2;
                        break;
                    case '/':
                        tokens.Add(new Token(TokenKind.Slash, "/"));
                        i++;
                        break;
                    case '.' when next == '.':
                        tokens.Add(new Token(TokenKind.DotDot, ".."));
                        i += 2;
                        break;
                    case '.' when !char.IsDigit(next):
                        tokens.Add(new Token(TokenKind.Dot, "."));
                        i++;
                        break;
                    case '*':
                        tokens.Add(operand ? new Token(TokenKind.Operator, "*") : new Token(TokenKind.Star, "*"));
                        i++;
                        break;
                    case '+':
                    case '-':
                    case '=':
                        tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                        i++;
                        break;
                    case '!' when next == '=':
                        tokens.Add(new Token(TokenKind.Operator, "!="));
                        i += 2;
                        break;
                    case '<':
                    case '>':
                        if (next == '=')
                        {
                            tokens.Add(new Token(TokenKind.Operator, c + "="));
                            i += 2;
                        }
                        else
                        {
                            tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                            i++;
                        }
                        break;
                    case '"':
                    case '\'':
                    {
                        var end = s.IndexOf(c, i + 1);
                        if (end < 0)
                            throw new XsltException("Unterminated literal in the expression '" + s + "'.");
                        tokens.Add(new Token(TokenKind.Literal, s.Substring(i + 1, end - i - 1)));
                        i = end + 1;
                        break;
                    }
                    case '$':
                    {
                        i++;
                        var name = ReadName(s, ref i);
                        if (i < s.Length && s[i] == ':' && i + 1 < s.Length && IsNameStart(s[i + 1]))
                        {
                            i++;
                            name += ":" + ReadName(s, ref i);
                        }
                        tokens.Add(new Token(TokenKind.Variable, name));
                        break;
                    }
                    default:
                        if (char.IsDigit(c) || c == '.')
                        {
                            var number = new StringBuilder();
                            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                                number.Append(s[i++]);
                            tokens.Add(new Token(TokenKind.Number, number.ToString()));
                        }
                        else if (IsNameStart(c))
                        {
                            var name = ReadName(s, ref i);
                            if (operand && (name == "and" || name == "or" || name == "div" || name == "mod"))
                            {
                                tokens.Add(new Token(TokenKind.Operator, name));
                            }
                            else if (i + 1 < s.Length && s[i] == ':' && s[i + 1] == '*')
                            {
                                tokens.Add(new Token(TokenKind.NameStar, name + ":*"));
                                i += 2;
                            }
                            else if (i + 1 < s.Length && s[i] == ':' && IsNameStart(s[i + 1]))
                            {
                                i++;
                                tokens.Add(new Token(TokenKind.Name, name + ":" + ReadName(s, ref i)));
                            }
                            else
                            {
                                tokens.Add(new Token(TokenKind.Name, name));
                            }
                        }
                        else
                        {
                            throw new XsltException("Unexpected character '" + c + "' in the expression '" + s + "'.");
                        }
                        break;
                }
            }
        }

        private static bool EndsOperand(Token token)
        {
            switch (token.Kind)
            {
                case TokenKind.At:
                case TokenKind.ColonColon:
                case TokenKind.LeftParen:
                case TokenKind.LeftBracket:
                case TokenKind.Comma:
                case TokenKind.Operator:
                case TokenKind.Slash:
                case TokenKind.DoubleSlash:
                case TokenKind.Pipe:
                    return false;
                default:
                    return true;
            }
        }
    }

    internal sealed class XsltException : System.Exception
    {
        public XsltException(string message)
            : base(message)
        {
        }

        /// <summary>The line in the stylesheet, from 1, or 0 if unknown.</summary>
        public int Line { get; set; }

        public int Column { get; set; }
    }
}
