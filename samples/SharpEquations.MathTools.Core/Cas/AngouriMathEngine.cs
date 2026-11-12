namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AngouriMath;
    using SharpEquations;

    /// <summary>
    /// The computer algebra of the math tools, with AngouriMath: the equation is read by <see cref="MathReader"/>, and
    /// the results come back through LaTeX, which AngouriMath writes and SharpEquations reads.
    /// </summary>
    public sealed class AngouriMathEngine : ICasEngine
    {
        public string Name => "AngouriMath";

        public IReadOnlyList<string> Variables(Equation equation) => Variables(MathReader.Read(equation));

        public CasResult Run(CasAction action, Equation equation, string? variable = null) => Run(action, MathReader.Read(equation), variable);

        /// <summary>The letters of an expression, x first when there is one.</summary>
        public static IReadOnlyList<string> Variables(Entity expression) =>
            expression.Vars.Select(v => v.Name).Distinct().OrderBy(v => v == "x" ? 0 : 1).ThenBy(v => v, StringComparer.Ordinal).ToList();

        /// <summary>Does an action to an expression of AngouriMath.</summary>
        public static CasResult Run(CasAction action, Entity expression, string? variable)
        {
            var letters = Variables(expression);
            var x = MathS.Var(variable ?? (letters.Count > 0 ? letters[0] : "x"));
            switch (action)
            {
                case CasAction.Evaluate:
                    return Value(expression);
                case CasAction.Solve:
                    var equation = expression is Entity.Equalsf ? expression : expression.Equalizes(0);
                    var solutions = equation.Solve(x).Simplify();
                    return Approximations(Result(action, Latex(x) + @"\in " + Latex(solutions), x.ToString() + " ∈ " + solutions.ToString()) with
                    {
                        Values = solutions is Entity.Set.FiniteSet set ? Equations(set.Elements) : Array.Empty<Equation>(),
                    }, x, solutions);
                case CasAction.Simplify:
                    return Result(action, expression.Simplify());
                case CasAction.Expand:
                    return Result(action, expression.Expand());
                case CasAction.Factor:
                    return Result(action, expression.Factorize());
                case CasAction.Differentiate:
                    return Result(action, expression.Differentiate(x).Simplify());
                case CasAction.Integrate:
                    return Result(action, expression.Integrate(x).Simplify());
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Not an action of the engine.");
            }
        }

        // the exact value, and the decimal one when it differs: 1/3 ≈ 0.333333, √2 ≈ 1.41421
        private static CasResult Value(Entity expression)
        {
            if (expression is Entity.Equalsf || expression.Vars.Count > 0)
                return Result(CasAction.Evaluate, expression.Simplify());

            var exact = expression.Simplify();
            var approximate = Decimal(exact.EvalNumerical());
            var exactLatex = Latex(exact);
            var exactText = exact.ToString();
            var values = Equations(new[] { exact });
            if (approximate == null || approximate == exactText)
                return Result(CasAction.Evaluate, exactLatex, exactText) with { Values = values };
            return Result(CasAction.Evaluate, exactLatex + @"\approx " + approximate, exactText + " ≈ " + approximate) with { Values = values };
        }

        // x ∈ {…} with radicals gets a second line, x ≈ {1.2071, -0.5+0.866i}: the same solutions as decimal numbers
        private static CasResult Approximations(CasResult result, Entity.Variable x, Entity solutions)
        {
            if (solutions is not Entity.Set.FiniteSet set || set.Count == 0)
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
            var approximate = Result(CasAction.Solve, Latex(x) + @"\approx \left\{" + string.Join(@",\: ", values) + @"\right\}", x.ToString() + " ≈ {" + list + "}");
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

        private static CasResult Result(CasAction action, Entity value) =>
            Result(action, Latex(value), value.ToString()) with { Values = Equations(new[] { value }) };

        // the values that read back from their LaTeX (all, or none)
        private static IReadOnlyList<Equation> Equations(IEnumerable<Entity> values)
        {
            var equations = new List<Equation>();
            foreach (var value in values)
            {
                if (Parse(Latex(value)) is not { } equation)
                    return Array.Empty<Equation>();
                equations.Add(equation);
            }
            return equations;
        }

        private static Equation? Parse(string latex) =>
            Equation.TryParse(latex, EquationFormat.LaTeX, new EquationOptions { Display = true }, out var parsed)
            && !parsed!.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror")
                ? parsed
                : null;

        private static CasResult Result(CasAction action, string latex, string text)
        {
            return new CasResult(action, Parse(latex), text);
        }

        // the LaTeX of AngouriMath, with its constants as the LaTeX reader knows them
        private static string Latex(Entity value) => value.Latexize()
            .Replace(@"\mathrm{\pi}", @"\pi", StringComparison.Ordinal)
            .Replace(@"\mathrm{e}", "e", StringComparison.Ordinal)
            .Replace(@"\mathrm{i}", "i", StringComparison.Ordinal);
    }
}
