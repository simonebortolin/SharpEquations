namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpEquations;

    /// <summary>
    /// The graph of a formula: an equation the graphs can draw (in UnicodeMath) and the values its parameters start from,
    /// so that the curve shows its shape (those not given start from 1).
    /// </summary>
    public sealed class GraphPreset
    {
        public GraphPreset(string unicodeMath, params (string Parameter, double Value)[] values)
        {
            UnicodeMath = unicodeMath;
            Values = values.ToDictionary(v => v.Parameter, v => v.Value, StringComparer.Ordinal);
        }

        public string UnicodeMath { get; }

        public IReadOnlyDictionary<string, double> Values { get; }

        /// <summary>The graph, with its parameters at their starting values.</summary>
        public GraphPlot Plot(AngleUnit angles = AngleUnit.Radians)
        {
            var plot = GraphPlot.FromEquation(Equation.Parse(UnicodeMath, EquationFormat.UnicodeMath), angles);
            foreach (var (parameter, value) in Values)
                plot[parameter] = value;
            return plot;
        }
    }

    /// <summary>A formula of the library: its subject, its name, the formula in UnicodeMath, and its graph, if it has one.</summary>
    public sealed class Formula
    {
        public Formula(string subject, string name, string unicodeMath, GraphPreset? graph = null)
        {
            Subject = subject;
            Name = name;
            UnicodeMath = unicodeMath;
            Graph = graph;
        }

        public string Subject { get; }

        public string Name { get; }

        public string UnicodeMath { get; }

        public GraphPreset? Graph { get; }
    }

    /// <summary>The library of formulas, by subject.</summary>
    public static class Formulas
    {
        public static IReadOnlyList<Formula> All { get; } = new[]
        {
            new Formula("Algebra", "Quadratic formula", "x=(-b±√(b^2-4ac))/2a"),
            new Formula("Algebra", "Difference of squares", "a^2-b^2=(a+b)(a-b)"),
            new Formula("Algebra", "Square of a sum", "(a+b)^2=a^2+2ab+b^2"),
            new Formula("Algebra", "Binomial theorem", "(a+b)^n=∑_(k=0)^n (n¦k) a^k b^(n-k)"),
            new Formula("Algebra", "Arithmetic series", "∑_(k=1)^n k=(n(n+1))/2"),
            new Formula("Algebra", "Geometric series", "∑_(k=0)^(n-1) ar^k=a (1-r^n)/(1-r)"),
            new Formula("Algebra", "Logarithm of a product", "log_b⁡(xy)=log_b⁡x+log_b⁡y"),
            new Formula("Algebra", "Change of base", "log_b⁡x=(ln⁡x)/(ln⁡b)"),
            new Formula("Algebra", "Inverse of a 2×2 matrix", "(■(a&b@c&d))^(-1)=1/(ad-bc) (■(d&-b@-c&a))"),

            new Formula("Geometry", "Pythagorean theorem", "a^2+b^2=c^2"),
            new Formula("Geometry", "Area of a circle", "A=πr^2", new GraphPreset("y=πx^2")),
            new Formula("Geometry", "Circumference", "C=2πr"),
            new Formula("Geometry", "Area of a triangle", "A=1/2 bh"),
            new Formula("Geometry", "Heron's formula", "A=√(s(s-a)(s-b)(s-c))"),
            new Formula("Geometry", "Volume of a sphere", "V=4/3 πr^3"),
            new Formula("Geometry", "Surface of a sphere", "S=4πr^2"),
            new Formula("Geometry", "Volume of a cone", "V=1/3 πr^2 h"),
            new Formula("Geometry", "Distance between two points", "d=√((x_2-x_1)^2+(y_2-y_1)^2)"),

            new Formula("Trigonometry", "Pythagorean identity", "sin^2⁡θ+cos^2⁡θ=1", new GraphPreset("x^2+y^2=1")),
            new Formula("Trigonometry", "Tangent", "tan⁡θ=sin⁡θ/cos⁡θ", new GraphPreset("y=tan⁡x")),
            new Formula("Trigonometry", "Sine of a sum", "sin⁡(α+β)=sin⁡α cos⁡β+cos⁡α sin⁡β"),
            new Formula("Trigonometry", "Cosine of a sum", "cos⁡(α+β)=cos⁡α cos⁡β-sin⁡α sin⁡β"),
            new Formula("Trigonometry", "Double angle", "sin⁡2θ=2 sin⁡θ cos⁡θ"),
            new Formula("Trigonometry", "Law of sines", "a/sin⁡α=b/sin⁡β=c/sin⁡γ"),
            new Formula("Trigonometry", "Law of cosines", "c^2=a^2+b^2-2ab cos⁡γ"),
            new Formula("Trigonometry", "Euler's formula", "e^(iθ)=cos⁡θ+i sin⁡θ"),

            new Formula("Calculus", "Definition of the derivative", "f′(x)=lim┬(h→0) (f(x+h)-f(x))/h"),
            new Formula("Calculus", "Product rule", "(fg)′=f′g+fg′"),
            new Formula("Calculus", "Chain rule", "ⅆ/ⅆx f(g(x))=f′(g(x)) g′(x)"),
            new Formula("Calculus", "Power rule", "ⅆ/ⅆx x^n=nx^(n-1)"),
            new Formula("Calculus", "Fundamental theorem", "∫_a^b f′(x) ⅆx=f(b)-f(a)"),
            new Formula("Calculus", "Integration by parts", "∫ u ⅆv=uv-∫ v ⅆu"),
            new Formula("Calculus", "Taylor series", "f(x)=∑_(n=0)^∞ (f^((n)) (a))/n! (x-a)^n", new GraphPreset("y=x-x^3/6+x^5/120")),
            new Formula("Calculus", "Gaussian integral", "∫_-∞^∞ e^(-x^2) ⅆx=√π", new GraphPreset("y=e^(-x^2)")),
            new Formula("Calculus", "A limit", "lim┬(x→0) sin⁡x/x=1", new GraphPreset("y=sin⁡x/x")),

            new Formula("Statistics", "Mean", "x̄=1/n ∑_(i=1)^n x_i"),
            new Formula("Statistics", "Standard deviation", "σ=√(1/N ∑_(i=1)^N (x_i-μ)^2)"),
            new Formula("Statistics", "Binomial coefficient", "(n¦k)=n!/(k!(n-k)!)"),
            new Formula("Statistics", "Normal distribution", "f(x)=1/(σ√2π) e^(-1/2 ((x-μ)/σ)^2)", new GraphPreset("y=1/(σ√2π) e^(-1/2 ((x-μ)/σ)^2)", ("μ", 0), ("σ", 1))),
            new Formula("Statistics", "Bayes' theorem", "P(A|B)=(P(B|A) P(A))/P(B)"),

            new Formula("Physics", "Newton's second law", "𝐅=m𝐚"),
            new Formula("Physics", "Mass–energy equivalence", "E=mc^2"),
            new Formula("Physics", "Kinetic energy", "E_k=1/2 mv^2"),
            new Formula("Physics", "Uniform acceleration", "s=v_0 t+1/2 at^2", new GraphPreset("y=v x+1/2 a x^2", ("v", 2), ("a", -1))),
            new Formula("Physics", "Universal gravitation", "F=G (m_1 m_2)/r^2"),
            new Formula("Physics", "Coulomb's law", "F=1/(4πε_0) (q_1 q_2)/r^2"),
            new Formula("Physics", "Ideal gas law", "pV=nRT"),
            new Formula("Physics", "Schrödinger equation", "iℏ ∂/∂t Ψ=ĤΨ"),
            new Formula("Physics", "Simple harmonic motion", "x(t)=A cos⁡(ωt+φ)", new GraphPreset("y=A cos⁡(ωx+φ)", ("A", 2), ("ω", 1), ("φ", 0))),
            new Formula("Physics", "Damped oscillation", "x(t)=A e^(-γt) cos⁡ωt", new GraphPreset("y=A e^(-γx) cos⁡(ωx)", ("A", 4), ("γ", 0.3), ("ω", 3))),
            new Formula("Physics", "Projectile", "y=x tan⁡θ-(g x^2)/(2v^2 cos^2⁡θ)", new GraphPreset("y=x tan⁡θ-(g x^2)/(2v^2 cos^2⁡θ)", ("θ", 0.8), ("g", 9.8), ("v", 8))),

            new Formula("Curves", "Line", "y=mx+b", new GraphPreset("y=mx+b", ("m", 0.5), ("b", 1))),
            new Formula("Curves", "Parabola", "y=a(x-h)^2+k", new GraphPreset("y=a(x-h)^2+k", ("a", 0.5), ("h", 1), ("k", -2))),
            new Formula("Curves", "Cubic", "y=ax^3+bx^2+cx+d", new GraphPreset("y=ax^3+bx^2+cx+d", ("a", 0.2), ("b", 0), ("c", -2), ("d", 0))),
            new Formula("Curves", "Circle", "(x-h)^2+(y-k)^2=r^2", new GraphPreset("(x-h)^2+(y-k)^2=r^2", ("h", 1), ("k", 1), ("r", 3))),
            new Formula("Curves", "Ellipse", "x^2/a^2+y^2/b^2=1", new GraphPreset("x^2/a^2+y^2/b^2=1", ("a", 4), ("b", 2))),
            new Formula("Curves", "Hyperbola", "x^2/a^2-y^2/b^2=1", new GraphPreset("x^2/a^2-y^2/b^2=1", ("a", 2), ("b", 1.5))),
            new Formula("Curves", "Sine wave", "y=A sin⁡(kx+φ)", new GraphPreset("y=A sin⁡(kx+φ)", ("A", 2), ("k", 1), ("φ", 0))),
            new Formula("Curves", "Exponential", "y=a e^(kx)", new GraphPreset("y=a e^(kx)", ("a", 1), ("k", 0.5))),
            new Formula("Curves", "Logarithm", "y=a ln⁡(x-h)", new GraphPreset("y=a ln⁡(x-h)", ("a", 2), ("h", 0))),
            new Formula("Curves", "Logistic", "y=L/(1+e^(-k(x-c)))", new GraphPreset("y=L/(1+e^(-k(x-c)))", ("L", 4), ("k", 1), ("c", 0))),
            new Formula("Curves", "Cardioid", "r=a(1+cos⁡θ)", new GraphPreset("r=a(1+cos⁡θ)", ("a", 2))),
            new Formula("Curves", "Rose", "r=a cos⁡(kθ)", new GraphPreset("r=a cos⁡(kθ)", ("a", 4), ("k", 3))),
            new Formula("Curves", "Limaçon", "r=b+a cos⁡θ", new GraphPreset("r=b+a cos⁡θ", ("a", 3), ("b", 1.5))),
            new Formula("Curves", "Spiral of Archimedes", "r=aθ", new GraphPreset("r=aθ", ("a", 0.5))),
            new Formula("Curves", "Lissajous curve", "x=A sin⁡(at+δ), y=B sin⁡bt", new GraphPreset("x=A sin⁡(at+δ), y=B sin⁡(bt)", ("A", 4), ("B", 3), ("a", 3), ("b", 2), ("δ", 0))),
            new Formula("Curves", "Cycloid", "x=r(t-sin⁡t), y=r(1-cos⁡t)", new GraphPreset("x=r(t-sin⁡t), y=r(1-cos⁡t)", ("r", 1.5))),
            new Formula("Curves", "Astroid", "x^(2/3)+y^(2/3)=a^(2/3)", new GraphPreset("x=a cos^3⁡t, y=a sin^3⁡t", ("a", 4))),
        };

        /// <summary>The subjects, in the order of the library.</summary>
        public static IReadOnlyList<string> Subjects { get; } = All.Select(f => f.Subject).Distinct().ToList();

        /// <summary>The formulas of a subject (all of them for null) whose name or subject contains the text, ignoring case.</summary>
        public static IEnumerable<Formula> Find(string? subject, string? text) => All.Where(f =>
            (subject == null || f.Subject == subject)
            && (string.IsNullOrWhiteSpace(text)
                || f.Name.Contains(text!.Trim(), StringComparison.OrdinalIgnoreCase)
                || f.Subject.Contains(text!.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
}
