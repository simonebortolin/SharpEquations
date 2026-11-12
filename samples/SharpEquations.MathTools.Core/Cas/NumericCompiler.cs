namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AngouriMath;

    /// <summary>
    /// An expression of AngouriMath turned into a function of real numbers, for the graphs: a tree of delegates built
    /// once (no code generated at run time, so it runs on WebAssembly too), with NaN where the value is not real.
    /// </summary>
    public static class NumericCompiler
    {
        /// <summary>The expression as a function of the values of <paramref name="letters"/>, in their order (other letters are NaN).</summary>
        public static Func<double[], double> Compile(Entity expression, IReadOnlyList<string> letters)
        {
            var index = letters.Select((name, i) => (name, i)).ToDictionary(p => p.name, p => p.i, StringComparer.Ordinal);
            return Build(expression, index);
        }

        private static Func<double[], double> Build(Entity e, IReadOnlyDictionary<string, int> letters)
        {
            switch (e)
            {
                case Entity.Number.Complex number:
                    var re = number.RealPart.EDecimal.ToDouble();
                    var constant = Math.Abs(number.ImaginaryPart.EDecimal.ToDouble()) < 1e-300 ? re : double.NaN;
                    return _ => constant;
                case var _ when e == MathS.pi:
                    return _ => Math.PI;
                case var _ when e == MathS.e:
                    return _ => Math.E;
                case Entity.Variable variable:
                    if (!letters.TryGetValue(variable.Name, out var i))
                        return _ => double.NaN;
                    return v => v[i];
                case Entity.Sumf:
                    return Binary(e, letters, (a, b) => a + b);
                case Entity.Minusf:
                    return Binary(e, letters, (a, b) => a - b);
                case Entity.Mulf:
                    return Binary(e, letters, (a, b) => a * b);
                case Entity.Divf:
                    return Binary(e, letters, (a, b) => a / b);
                case Entity.Powf power:
                    return Power(power, letters);
                case Entity.Logf log:
                    var logBase = Build(log.Base, letters);
                    var argument = Build(log.Antilogarithm, letters);
                    return v => Math.Log(argument(v)) / Math.Log(logBase(v));
                case Entity.Sinf:
                    return Unary(e, letters, Math.Sin);
                case Entity.Cosf:
                    return Unary(e, letters, Math.Cos);
                case Entity.Tanf:
                    return Unary(e, letters, Math.Tan);
                case Entity.Cotanf:
                    return Unary(e, letters, x => 1 / Math.Tan(x));
                case Entity.Secantf:
                    return Unary(e, letters, x => 1 / Math.Cos(x));
                case Entity.Cosecantf:
                    return Unary(e, letters, x => 1 / Math.Sin(x));
                case Entity.Arcsinf:
                    return Unary(e, letters, Math.Asin);
                case Entity.Arccosf:
                    return Unary(e, letters, Math.Acos);
                case Entity.Arctanf:
                    return Unary(e, letters, Math.Atan);
                case Entity.Arccotanf:
                    return Unary(e, letters, x => Math.PI / 2 - Math.Atan(x));
                case Entity.Absf:
                    return Unary(e, letters, Math.Abs);
                case Entity.Signumf:
                    return Unary(e, letters, x => double.IsNaN(x) ? double.NaN : Math.Sign(x));
                case Entity.Floorf:
                    return Unary(e, letters, Math.Floor);
                case Entity.Ceilf:
                    return Unary(e, letters, Math.Ceiling);
                case Entity.Factorialf:
                    return Unary(e, letters, Factorial);
                default:
                    return Slow(e, letters);
            }
        }

        private static Func<double[], double> Unary(Entity e, IReadOnlyDictionary<string, int> letters, Func<double, double> f)
        {
            var a = Build(e.DirectChildren[0], letters);
            return v => f(a(v));
        }

        private static Func<double[], double> Binary(Entity e, IReadOnlyDictionary<string, int> letters, Func<double, double, double> f)
        {
            var a = Build(e.DirectChildren[0], letters);
            var b = Build(e.DirectChildren[1], letters);
            return v => f(a(v), b(v));
        }

        // a power of a negative number is real when the exponent is a fraction with an odd denominator: ∛(−8) is −2
        private static Func<double[], double> Power(Entity.Powf power, IReadOnlyDictionary<string, int> letters)
        {
            var b = Build(power.Base, letters);
            switch (power.Exponent.Vars.Count > 0 ? power.Exponent : power.Exponent.Simplify())
            {
                case Entity.Number.Integer n when n.EInteger.CanFitInInt32():
                    var k = n.EInteger.ToInt32Checked();
                    return k == 2 ? v => b(v) * b(v) : v => Math.Pow(b(v), k);
                case Entity.Number.Rational q when !q.ERational.Denominator.IsEven:
                    var exponent = q.ERational.ToDouble();
                    var sign = q.ERational.Numerator.IsEven ? 1 : -1;
                    return v => b(v) is var x && x < 0 ? sign * Math.Pow(-x, exponent) : Math.Pow(x, exponent);
                default:
                    var p = Build(power.Exponent, letters);
                    return v => Math.Pow(b(v), p(v));
            }
        }

        private static double Factorial(double x)
        {
            if (x < 0 || x != Math.Floor(x) || x > 170)
                return double.NaN;
            var result = 1.0;
            for (var i = 2; i <= x; i++)
                result *= i;
            return result;
        }

        // what has no delegate of its own: AngouriMath evaluates it, slowly
        private static Func<double[], double> Slow(Entity e, IReadOnlyDictionary<string, int> letters)
        {
            var used = e.Vars.Where(v => letters.ContainsKey(v.Name)).ToList();
            return v =>
            {
                var value = e;
                foreach (var letter in used)
                    value = value.Substitute(letter, v[letters[letter.Name]]);
                try
                {
                    return value.EvalNumerical() is Entity.Number.Complex c && Math.Abs(c.ImaginaryPart.EDecimal.ToDouble()) < 1e-12
                        ? c.RealPart.EDecimal.ToDouble()
                        : double.NaN;
                }
#pragma warning disable CA1031 // no value at this point
                catch (Exception)
#pragma warning restore CA1031
                {
                    return double.NaN;
                }
            };
        }
    }
}
