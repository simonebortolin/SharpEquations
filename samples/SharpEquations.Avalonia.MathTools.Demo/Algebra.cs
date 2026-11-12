namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AngouriMath;
    using SharpEquations;

    /// <summary>A result of <see cref="Algebra"/>: what was done, and the result as an equation (null if it cannot be drawn) and as text.</summary>
    public sealed record AlgebraResult(string Action, Equation? Math, string Text)
    {
        /// <summary>The solutions as decimal numbers (<c>a+bi</c> for complex ones), when they are not plain numbers already; null otherwise.</summary>
        public Equation? Approximate { get; init; }

        /// <summary>The text of <see cref="Approximate"/>.</summary>
        public string? ApproximateText { get; init; }
    }

    /// <summary>
    /// What the calculator computes with AngouriMath: the value of an expression (exact, and as a decimal number),
    /// the solutions of an equation, simplification, expansion, factoring, derivatives and integrals. The results come
    /// back through LaTeX, which AngouriMath writes and SharpEquations reads.
    /// </summary>
    public static class Algebra
    {
        public const string Evaluate = "Evaluate";
        public const string Solve = "Solve";
        public const string Simplify = "Simplify";
        public const string Expand = "Expand";
        public const string Factor = "Factor";
        public const string Differentiate = "Differentiate";
        public const string Integrate = "Integrate";

        /// <summary>The actions, in the order of the buttons.</summary>
        public static readonly IReadOnlyList<string> Actions = new[] { Evaluate, Solve, Simplify, Expand, Factor, Differentiate, Integrate };

        /// <summary>The letters of an expression, x first when there is one.</summary>
        public static IReadOnlyList<string> Variables(Entity expression) =>
            expression.Vars.Select(v => v.Name).Distinct().OrderBy(v => v == "x" ? 0 : 1).ThenBy(v => v, StringComparer.Ordinal).ToList();

        /// <summary>Does an action to an expression read by <see cref="MathReader"/>; <paramref name="variable"/> is the letter to solve for, or to differentiate and integrate by.</summary>
        public static AlgebraResult Run(string action, Entity expression, string? variable)
        {
            var letters = Variables(expression);
            var x = MathS.Var(variable ?? (letters.Count > 0 ? letters[0] : "x"));
            switch (action)
            {
                case Evaluate:
                    return Value(expression);
                case Solve:
                    var equation = expression is Entity.Equalsf ? expression : expression.Equalizes(0);
                    var solutions = equation.Solve(x).Simplify();
                    return Approximations(Result(Solve, Latex(x) + @"\in " + Latex(solutions), x.ToString() + " ∈ " + solutions.ToString()), x, solutions);
                case Simplify:
                    return Result(Simplify, expression.Simplify());
                case Expand:
                    return Result(Expand, expression.Expand());
                case Factor:
                    return Result(Factor, expression.Factorize());
                case Differentiate:
                    return Result(Differentiate, expression.Differentiate(x).Simplify());
                case Integrate:
                    return Result(Integrate, expression.Integrate(x).Simplify());
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Not an action of the calculator.");
            }
        }

        // the exact value, and the decimal one when it differs: 1/3 ≈ 0.333333, √2 ≈ 1.41421
        private static AlgebraResult Value(Entity expression)
        {
            if (expression is Entity.Equalsf || expression.Vars.Any())
                return Result(Evaluate, expression.Simplify());

            var exact = expression.Simplify();
            var approximate = Decimal(exact.EvalNumerical());
            var exactLatex = Latex(exact);
            var exactText = exact.ToString();
            if (approximate == null || approximate == exactText)
                return Result(Evaluate, exactLatex, exactText);
            return Result(Evaluate, exactLatex + @"\approx " + approximate, exactText + " ≈ " + approximate);
        }

        // x ∈ {…} with radicals gets a second line, x ≈ {1.2071, -0.5+0.866i}: the same solutions as decimal numbers
        private static AlgebraResult Approximations(AlgebraResult result, Entity.Variable x, Entity solutions)
        {
            if (solutions is not Entity.Set.FiniteSet set || !set.Elements.Any())
                return result;
            var exact = set.Elements.ToList();
            if (exact.All(e => e is Entity.Number))
                return result;

            var values = new List<string>();
            foreach (var element in exact)
            {
                string? value;
                try
                {
                    value = Decimal(element.EvalNumerical(), 6);
                }
                catch (AngouriMath.Core.Exceptions.AngouriMathBaseException)
                {
                    value = null;
                }
                if (value == null)
                    return result;
                values.Add(value);
            }

            var list = string.Join(", ", values);
            var approximate = Result(Solve, Latex(x) + @"\approx \left\{" + string.Join(@",\: ", values) + @"\right\}", x.ToString() + " ≈ {" + list + "}");
            return result with { Approximate = approximate.Math, ApproximateText = approximate.Text };
        }

        /// <summary>A number of AngouriMath as the calculator shows it: <paramref name="digits"/> significant digits, <c>a+bi</c> for a complex one; null for what is not a number.</summary>
        public static string? Decimal(Entity value, int digits = 10)
        {
            switch (value)
            {
                case Entity.Number.Real real:
                    return Format(real.EDecimal.ToDouble(), digits);
                case Entity.Number.Complex complex:
                    var re = complex.RealPart.EDecimal.ToDouble();
                    var im = complex.ImaginaryPart.EDecimal.ToDouble();
                    if (Math.Abs(im) < 1e-12 * Math.Max(1, Math.Abs(re)))
                        return Format(re, digits);
                    var imaginary = (Math.Abs(im) == 1 ? "" : Format(Math.Abs(im), digits)) + "i";
                    return Math.Abs(re) < 1e-12 * Math.Abs(im) ? (im < 0 ? "-" : "") + imaginary : Format(re, digits) + (im < 0 ? "-" : "+") + imaginary;
                default:
                    return null;
            }
        }

        private static string Format(double value, int digits) =>
            double.IsInfinity(value) ? (value > 0 ? "∞" : "-∞") : value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        private static AlgebraResult Result(string action, Entity value) => Result(action, Latex(value), value.ToString());

        private static AlgebraResult Result(string action, string latex, string text)
        {
            var options = new EquationOptions { Display = true };
            var math = Equation.TryParse(latex, EquationFormat.LaTeX, options, out var parsed)
                && !parsed!.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror")
                ? parsed
                : null;
            return new AlgebraResult(action, math, text);
        }

        // the LaTeX of AngouriMath, with its constants as the LaTeX reader knows them
        private static string Latex(Entity value) => value.Latexize()
            .Replace(@"\mathrm{\pi}", @"\pi", StringComparison.Ordinal)
            .Replace(@"\mathrm{e}", "e", StringComparison.Ordinal)
            .Replace(@"\mathrm{i}", "i", StringComparison.Ordinal);
    }
}
