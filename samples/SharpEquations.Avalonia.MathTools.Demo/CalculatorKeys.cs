namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using SharpEquations.Editing;

    /// <summary>A key of the keypad: its face (UnicodeMath, or plain text), its tool tip, and what it does to the editor.</summary>
    public sealed class CalculatorKey
    {
        public CalculatorKey(string face, string tip, Action<MathEditor> press, bool isMath = true)
        {
            Face = face;
            Tip = tip;
            Press = press;
            IsMath = isMath;
        }

        /// <summary>The face of the key: UnicodeMath, drawn as an equation, when <see cref="IsMath"/>, otherwise plain text.</summary>
        public string Face { get; }

        public string Tip { get; }

        public Action<MathEditor> Press { get; }

        public bool IsMath { get; }
    }

    /// <summary>
    /// The keys of the scientific keypad. A key types into the editor as the keyboard would (<c>sin(</c>, <c>^2␣</c>,
    /// <c>×</c>), so that what it types builds up with what is before it, or inserts a structure with its slots, as the
    /// buttons of the equation tools of Word (a fraction, a root, a sum with its limits), around the selection if any.
    /// </summary>
    public static class CalculatorKeys
    {
        private static readonly MathNode Empty = MathNode.Row(Array.Empty<MathNode>());

        /// <summary>The functions, powers, structures and constants, in six columns.</summary>
        public static IReadOnlyList<CalculatorKey> Functions { get; } = new[]
        {
            Function("sin"), Function("cos"), Function("tan"),
            Typed("sin^-1", "Arcsine", "sin^(-1)("), Typed("cos^-1", "Arccosine", "cos^(-1)("), Typed("tan^-1", "Arctangent", "tan^(-1)("),
            Function("sinh"), Function("cosh"), Function("tanh"),
            Function("log"), Function("ln"),
            Template("log_b", "Logarithm to a base", MathNode.Element("msub", new[] { MathNode.Row(new[] { MathNode.Token("mi", "log") }), Empty })),
            Power("x^2", "Square", "2"), Typed("x^n", "Power", "^"), Typed("x_n", "Subscript", "_"),
            Structure("√x", "Square root", "msqrt"), Structure("√(n&x)", "Root", "mroot"), Structure("a/b", "Fraction", "mfrac"),
            Typed("e^x", "Exponential", "e^"), Typed("10^x", "Power of ten", "10^"),
            Fences("|x|", "Absolute value", "|", "|"), Fences("(x)", "Parentheses", "(", ")"),
            Typed("n!", "Factorial", "!"), Typed("±", "Plus or minus", "±"),
            Typed("π", "Pi", "π"), Typed("e", "Euler's number", "e"), Typed("i", "Imaginary unit", "i"),
            Typed("∞", "Infinity", "∞"), Typed("θ", "Theta", "θ"), Typed("°", "Degrees", "°"),
            Template("∑_(k=1)^n", "Sum", MathNode.Element("munderover", new[] { Operator("∑"), Empty, Empty })),
            Template("∏_(k=1)^n", "Product", MathNode.Element("munderover", new[] { Operator("∏"), Empty, Empty })),
            Typed("∫", "Integral", "∫"),
            Template("∫_a^b", "Integral with limits", MathNode.Element("msubsup", new[] { Operator("∫"), Empty, Empty })),
            Typed("ⅆ/ⅆx", "Derivative", "ⅆ/ⅆx "),
            Template("lim┬(x→a)", "Limit", MathNode.Element("munder", new[] { MathNode.Row(new[] { MathNode.Token("mi", "lim") }), Empty })),
        };

        /// <summary>The digits, the operators, the brackets and the variables, in five columns.</summary>
        public static IReadOnlyList<CalculatorKey> Numbers { get; } = new[]
        {
            Digit("7"), Digit("8"), Digit("9"), Typed("÷", "Divide", "÷"), new CalculatorKey("(", "Open bracket", e => e.Insert("("), isMath: false),
            Digit("4"), Digit("5"), Digit("6"), Typed("×", "Multiply", "×"), new CalculatorKey(")", "Close bracket", e => e.Insert(")"), isMath: false),
            Digit("1"), Digit("2"), Digit("3"), Typed("−", "Subtract", "-"), Typed("x", "x", "x"),
            Digit("0"), Typed(".", "Decimal point", "."), Typed(",", "Comma", ","), Typed("+", "Add", "+"), Typed("y", "y", "y"),
            Typed("=", "Equals", "="), Typed("<", "Less than", "<"), Typed(">", "Greater than", ">"), Typed("≤", "Less than or equal to", "≤"), Typed("≥", "Greater than or equal to", "≥"),
        };

        /// <summary>The keys that move the caret, delete, undo and build up.</summary>
        public static IReadOnlyList<CalculatorKey> Editing { get; } = new[]
        {
            new CalculatorKey("←", "Move left", e => e.MoveLeft(), isMath: false),
            new CalculatorKey("→", "Move right", e => e.MoveRight(), isMath: false),
            new CalculatorKey("⌫", "Delete the character or the structure before the caret", e => e.Backspace(), isMath: false),
            new CalculatorKey("↶", "Undo", e => e.Undo(), isMath: false),
            new CalculatorKey("↷", "Redo", e => e.Redo(), isMath: false),
            new CalculatorKey("Build up", "Build up what is typed", e => e.BuildUpAll(), isMath: false),
            new CalculatorKey("Clear", "An empty equation", e => e.Load(null), isMath: false),
        };

        private static MathNode Operator(string text) => MathNode.Row(new[] { MathNode.Token("mo", text) });

        private static CalculatorKey Digit(string digit) => Typed(digit, digit, digit);

        private static CalculatorKey Function(string name) => Typed(name, name, name + "(");

        private static CalculatorKey Typed(string face, string tip, string text) => new CalculatorKey(face, tip, e => e.Insert(text));

        private static CalculatorKey Structure(string face, string tip, string name) => new CalculatorKey(face, tip, e => e.InsertStructure(name));

        private static CalculatorKey Template(string face, string tip, MathNode node) => new CalculatorKey(face, tip, e => e.InsertTemplate(node));

        private static CalculatorKey Fences(string face, string tip, string open, string close) => new CalculatorKey(face, tip, e => e.InsertFences(open, close));

        // a power of the selection, or of what is before the caret: typed as ^2 and a space, which builds it up
        private static CalculatorKey Power(string face, string tip, string exponent) => new CalculatorKey(face, tip, e =>
        {
            if (e.Selection == null)
            {
                e.Insert("^" + exponent + " ");
                return;
            }
            e.InsertStructure("msup");
            e.Insert(exponent);
            e.MoveRight();
        });
    }
}
