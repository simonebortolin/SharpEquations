namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using AngouriMath;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// Whether a result of an engine is the expected one, by what it means rather than by how it is written: values and
    /// expressions are compared numerically (with the letters at a few points), an antiderivative up to a constant, and
    /// a simplified, expanded or factored form also by its shape. Both sides are read from their equations by
    /// <see cref="MathReader"/>, so the check holds for any engine that gives equations.
    /// </summary>
    public static class Reference
    {
        private static readonly double[] Points = { 0.37, 0.61, 0.83, 1.21, 1.73, 2.63 };
        private const double Tolerance = 1e-8;

        /// <summary>Null if the result is the expected one, or what differs.</summary>
        public static string? Check(CasCase test, CasResult result)
        {
            var expected = test.Expected.Select(e => MathReader.Read(Equation.Parse(e, EquationFormat.UnicodeMath))).ToList();
            List<Entity> actual;
            try
            {
                actual = result.Values.Select(MathReader.Read).ToList();
            }
            catch (NotSupportedException e)
            {
                return "The result cannot be read back: " + e.Message + " (" + result.Text + ")";
            }

            switch (test.Action)
            {
                case CasAction.Solve:
                    if (actual.Count != expected.Count)
                        return $"{actual.Count} solutions instead of {expected.Count}: {result.Text}";
                    var left = actual.ToList();
                    foreach (var solution in expected)
                    {
                        var match = left.FindIndex(a => Same(a, solution));
                        if (match < 0)
                            return "No solution " + solution.ToString() + ": " + result.Text;
                        left.RemoveAt(match);
                    }
                    return null;
                case var _ when actual.Count != 1:
                    return "No value: " + result.Text;
                case CasAction.Integrate:
                    return SameUpToConstant(WithoutConstant(actual[0], expected[0]), expected[0]) ? null : "Not an antiderivative of the expected one: " + result.Text;
                case var _ when !Same(actual[0], expected[0]):
                    return "Not the expected value: " + result.Text;
                case CasAction.Simplify when Size(result.Values[0]) > Size(Equation.Parse(test.Expected[0], EquationFormat.UnicodeMath)):
                    return "Not as simple as the expected form: " + result.Text;
                case CasAction.Expand when !IsExpanded(actual[0]):
                    return "Not expanded: " + result.Text;
                case CasAction.Factor when !IsFactored(actual[0]):
                    return "Not a product: " + result.Text;
                default:
                    return null;
            }
        }

        /// <summary>Whether two expressions have the same value, at a few points of their letters.</summary>
        public static bool Same(Entity a, Entity b)
        {
            var compared = 0;
            foreach (var values in Assignments(a, b))
            {
                if (Value(a, values) is not { } x || Value(b, values) is not { } y)
                    continue;
                if (!Close(x, y))
                    return false;
                compared++;
            }
            return compared > 0;
        }

        // F − G is the same at every point where both are real (elsewhere they may be on other branches)
        private static bool SameUpToConstant(Entity f, Entity g)
        {
            Complex? first = null;
            var compared = 0;
            foreach (var values in Assignments(f, g))
            {
                if (Value(f, values) is not { } x || Value(g, values) is not { } y || !IsReal(x) || !IsReal(y))
                    continue;
                first ??= x - y;
                if (!Close(x - y, first.Value))
                    return false;
                compared++;
            }
            return compared > 1;
        }

        // the constant of integration an engine adds (a letter of its own, C) taken as 0
        private static Entity WithoutConstant(Entity antiderivative, Entity expected)
        {
            var letters = expected.Vars.Select(v => v.Name).ToHashSet();
            foreach (var letter in antiderivative.Vars.Where(v => !letters.Contains(v.Name)).ToList())
                antiderivative = antiderivative.Substitute(letter, 0);
            return antiderivative;
        }

        private static bool IsReal(Complex z) => Math.Abs(z.Imaginary) <= Tolerance * Math.Max(1, Math.Abs(z.Real));

        private static bool Close(Complex x, Complex y) =>
            Complex.Abs(x - y) <= Tolerance * Math.Max(1, Math.Max(Complex.Abs(x), Complex.Abs(y)));

        // each letter at a point, a different one for each letter
        private static IEnumerable<Dictionary<string, double>> Assignments(Entity a, Entity b)
        {
            var letters = a.Vars.Concat(b.Vars).Select(v => v.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (letters.Count == 0)
            {
                yield return new Dictionary<string, double>();
                yield break;
            }
            for (var k = 0; k < Points.Length; k++)
                yield return letters.Select((n, i) => (n, Points[(k + 2 * i) % Points.Length])).ToDictionary(p => p.n, p => p.Item2);
        }

        private static Complex? Value(Entity expression, IReadOnlyDictionary<string, double> values)
        {
            try
            {
                foreach (var (name, value) in values)
                    expression = expression.Substitute(MathS.Var(name), value);
                if (expression.EvalNumerical() is not Entity.Number.Complex number)
                    return null;
                var z = new Complex(number.RealPart.EDecimal.ToDouble(), number.ImaginaryPart.EDecimal.ToDouble());
                return double.IsFinite(z.Real) && double.IsFinite(z.Imaginary) ? z : null;
            }
#pragma warning disable CA1031 // a point where the expression has no value is skipped
            catch (Exception)
#pragma warning restore CA1031
            {
                return null;
            }
        }

        private static int Size(Equation equation) => equation.ToMathML().DescendantsAndSelf().Count();

        // no sum multiplied, or raised to a whole power
        private static bool IsExpanded(Entity e) => e switch
        {
            Entity.Mulf m when IsSum(m.Multiplier) || IsSum(m.Multiplicand) => false,
            Entity.Powf p when IsSum(p.Base) && p.Exponent is Entity.Number.Integer => false,
            _ => e.DirectChildren.All(IsExpanded),
        };

        // a product, a power or a quotient of them, or the opposite of one
        private static bool IsFactored(Entity e) => e switch
        {
            Entity.Mulf m when m.Multiplier is Entity.Number => IsFactored(m.Multiplicand) || m.Multiplicand is Entity.Variable,
            Entity.Mulf or Entity.Powf => true,
            Entity.Divf d => IsFactored(d.Dividend),
            _ => false,
        };

        private static bool IsSum(Entity e) => e is Entity.Sumf or Entity.Minusf;
    }
}
