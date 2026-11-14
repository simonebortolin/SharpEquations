namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using AngouriMath;

    /// <summary>
    /// The sums to infinity that AngouriMath leaves as they are, each by its known value:
    /// <list type="bullet">
    /// <item>c/kᵖ, whose sum is ζ(p): π²/6 for p = 2, a rational times πᵖ for any even p (by the Bernoulli numbers), and no
    /// end for p = 1;</item>
    /// <item>c(−1)ᵏ/kᵖ, by η(p) = (1 − 2¹⁻ᵖ) ζ(p): ln 2 for p = 1;</item>
    /// <item>c(−1)ᵏ/(2k+1)ᵖ, whose sum is β(p) for an odd p (by the Euler numbers): π/4 for p = 1;</item>
    /// <item>c/((k−a)(k−b)) with b − a a whole number, whose terms cancel but for the first b − a.</item>
    /// </list>
    /// The form of a term is found from its values at some k, so that 1/k², k⁻² and 2/(2k²) are the same; its constant
    /// is then the exact value at the first k. A sum of such terms is the sum of their sums, when each has one. A sum
    /// that starts later than the form does has the first terms taken away.
    /// </summary>
    public static class Series
    {
        private const int MostPower = 20;

        private const double Tolerance = 1e-9;

        /// <summary>The expression with each sum to infinity that has a known value replaced by it.</summary>
        public static Entity Close(Entity expression) =>
            expression.Nodes.Any(n => n is Entity.Summationf) ? expression.Replace(e => e is Entity.Summationf sum ? Value(sum) ?? e : e) : expression;

        /// <summary>The value of a sum to infinity of a known form, or null.</summary>
        public static Entity? Value(Entity.Summationf sum)
        {
            if (sum.To != MathS.oo || sum.Var is not Entity.Variable k || sum.From is not Entity.Number.Integer from
                || sum.Expression.Vars.Any(v => v != k))
                return null;
            var start = from.EInteger.ToInt64Checked();
            return Value(sum.Expression, k, start) ?? Split(sum.Expression, k, start);
        }

        // a sum of terms, each with its own sum (also by AngouriMath, as a geometric one)
        private static Entity? Split(Entity body, Entity.Variable k, long start)
        {
            var parts = new List<Entity>();
            Terms(body, parts, 1);
            if (parts.Count < 2)
                return null;
            Entity total = 0;
            foreach (var part in parts)
            {
                var value = Value(part, k, start) ?? MathS.Sum(part, k, start, MathS.oo).Simplify();
                if (value.Nodes.Any(n => n is Entity.Summationf || n == MathS.oo || n == -MathS.oo))
                    return null;
                total += value;
            }
            return total;
        }

        private static void Terms(Entity e, List<Entity> parts, int sign)
        {
            switch (e)
            {
                case Entity.Sumf s:
                    Terms(s.Augend, parts, sign);
                    Terms(s.Addend, parts, sign);
                    break;
                case Entity.Minusf m:
                    Terms(m.Subtrahend, parts, sign);
                    Terms(m.Minuend, parts, -sign);
                    break;
                default:
                    parts.Add(sign > 0 ? e : -e);
                    break;
            }
        }

        private static Entity? Value(Entity body, Entity.Variable k, long start)
        {
            var at = new[] { 0L, 1, 2, 3, 4, 5, 17, 100, 1000 }.Select(d => start + d).ToArray();
            var terms = new double[at.Length];
            for (var i = 0; i < at.Length; i++)
            {
                if (Number(body.Substitute(k, at[i])) is not { } term)
                    return null;
                terms[i] = term;
            }

            for (var p = 1; p <= MostPower; p++)
            {
                if (start >= 1 && Constant(at, n => terms[n] * Math.Pow(at[n], p)))
                    return p == 1 ? Divergent(Exact(body * k, k, start)) : Zeta(Exact(body * MathS.Pow(k, p), k, start), p, start);
                if (start >= 1 && Constant(at, n => terms[n] * Math.Pow(at[n], p) * Sign(at[n])))
                    return Eta(Exact(body * MathS.Pow(k, p) * MathS.Pow(-1, k), k, start), p, start);
                if (start >= 0 && p % 2 == 1 && Constant(at, n => terms[n] * Math.Pow(2 * at[n] + 1, p) * Sign(at[n])))
                    return Beta(Exact(body * MathS.Pow(2 * k + 1, p) * MathS.Pow(-1, k), k, start), p, start);
            }
            return Telescoping(body, k, start, at, terms);
        }

        // ∑_(k≥start) c/kᵖ, p > 1 (for p = 1 it has no end)
        private static Entity? Zeta(Entity c, int p, long start)
        {
            if (p % 2 == 1)
                return null;
            var head = Fraction.Zero;
            for (var n = 1L; n < start; n++)
                head += new Fraction(1, BigInteger.Pow(n, p));
            Entity zeta = ZetaOfEven(p) * MathS.Pow(MathS.pi, p) - head.Entity;
            return c * zeta;
        }

        // ∑_(k≥start) c(−1)ᵏ/kᵖ: from 1 it is −η(p)
        private static Entity? Eta(Entity c, int p, long start)
        {
            Entity eta;
            if (p == 1)
                eta = MathS.Ln(2);
            else if (p % 2 == 0)
                eta = (Fraction.One - new Fraction(1, BigInteger.Pow(2, p - 1))).Entity * ZetaOfEven(p) * MathS.Pow(MathS.pi, p);
            else
                return null;
            var head = Fraction.Zero;
            for (var n = 1L; n < start; n++)
                head += new Fraction(n % 2 == 0 ? 1 : -1, BigInteger.Pow(n, p));
            return c * (-eta - head.Entity);
        }

        // ∑_(k≥start) c(−1)ᵏ/(2k+1)ᵖ, p odd: from 0 it is β(p)
        private static Entity Beta(Entity c, int p, long start)
        {
            var m = (p - 1) / 2;
            var beta = new Fraction((m % 2 == 0 ? 1 : -1) * Euler(2 * m), BigInteger.Pow(4, m + 1) * Factorial(2 * m)).Entity * MathS.Pow(MathS.pi, p);
            var head = Fraction.Zero;
            for (var n = 0L; n < start; n++)
                head += new Fraction(n % 2 == 0 ? 1 : -1, BigInteger.Pow(2 * n + 1, p));
            return c * (beta - head.Entity);
        }

        // ∑_(k≥start) c/((k−a)(k−b)), b − a = d whole: c/d (1/(start−b) + … + 1/(start−b+d−1))
        private static Entity? Telescoping(Entity body, Entity.Variable k, long start, long[] at, double[] terms)
        {
            if (terms.Any(t => t == 0))
                return null;
            double Q(int n) => 1 / terms[n];
            var a2 = (Q(2) - 2 * Q(1) + Q(0)) / 2;
            var a1 = Q(1) - Q(0) - a2 * (2 * at[0] + 1);
            var a0 = Q(0) - a1 * at[0] - a2 * at[0] * at[0];
            if (Math.Abs(a2) < Tolerance * (Math.Abs(a1) + Math.Abs(a0))
                || !at.Select((n, i) => (n, i)).All(x => Close(a2 * x.n * x.n + a1 * x.n + a0, Q(x.i))))
                return null;
            var discriminant = a1 * a1 - 4 * a2 * a0;
            if (discriminant < 0)
                return null;
            var root = Math.Sqrt(discriminant) / (2 * Math.Abs(a2));
            var middle = -a1 / (2 * a2);
            var difference = 2 * root;
            var d = Math.Round(difference);
            if (d < 1 || d > 64 || Math.Abs(difference - d) > Tolerance * Math.Max(1, d)
                || Rational(middle + root) is not { } b || middle + root >= start)
                return null;
            var a = b - new Fraction((long)d, 1);
            var c = Exact(body * (k - a.Entity) * (k - b.Entity), k, start);
            var sum = Fraction.Zero;
            for (var j = 0; j < d; j++)
                sum += Fraction.One / (new Fraction(start + j, 1) - b);
            return c * sum.Entity / (int)d;
        }

        private static Entity.Number.Real? Divergent(Entity c) => Number(c) is { } value ? (value > 0 ? MathS.oo : -MathS.oo) : null;

        private static bool Constant(long[] at, Func<int, double> value)
        {
            var first = value(0);
            if (first == 0 || double.IsNaN(first) || double.IsInfinity(first))
                return false;
            for (var n = 1; n < at.Length; n++)
                if (!Close(value(n), first))
                    return false;
            return true;
        }

        private static bool Close(double x, double y) => Math.Abs(x - y) <= Tolerance * Math.Max(Math.Abs(x), Math.Abs(y));

        private static double Sign(long n) => n % 2 == 0 ? 1 : -1;

        private static Entity Exact(Entity e, Entity.Variable k, long start) => e.Substitute(k, start).Simplify();

        private static double? Number(Entity e)
        {
            try
            {
                if (e.EvalNumerical() is not Entity.Number.Complex value)
                    return null;
                var re = value.RealPart.EDecimal.ToDouble();
                var im = value.ImaginaryPart.EDecimal.ToDouble();
                return Math.Abs(im) > 1e-12 * Math.Max(1, Math.Abs(re)) || double.IsNaN(re) || double.IsInfinity(re) ? null : re;
            }
            catch (AngouriMath.Core.Exceptions.AngouriMathBaseException)
            {
                return null;
            }
        }

        // a decimal near a fraction with a small denominator
        private static Fraction? Rational(double value)
        {
            for (var den = 1; den <= 12; den++)
            {
                var num = Math.Round(value * den);
                if (Math.Abs(value * den - num) <= Tolerance * Math.Max(1, Math.Abs(num)))
                    return new Fraction(new BigInteger(num), den);
            }
            return null;
        }

        // ζ(p)/πᵖ for an even p: (−1)^(p/2+1) Bₚ 2ᵖ / (2 p!)
        private static Entity ZetaOfEven(int p) =>
            (Bernoulli(p) * new Fraction((p / 2 % 2 == 1 ? 1 : -1) * BigInteger.Pow(2, p), 2 * Factorial(p))).Entity;

        // Bₙ, by the algorithm of Akiyama and Tanigawa
        private static Fraction Bernoulli(int n)
        {
            var a = new Fraction[n + 1];
            for (var m = 0; m <= n; m++)
            {
                a[m] = new Fraction(1, m + 1);
                for (var j = m; j >= 1; j--)
                    a[j - 1] = new Fraction(j, 1) * (a[j - 1] - a[j]);
            }
            return a[0];
        }

        // the Euler numbers E₀ = 1, E₂ = −1, E₄ = 5, …: E₂ₘ = −∑ C(2m, 2i) E₂ᵢ for i < m
        private static BigInteger Euler(int n)
        {
            var e = new BigInteger[n / 2 + 1];
            e[0] = 1;
            for (var m = 1; m <= n / 2; m++)
            {
                BigInteger sum = 0;
                for (var i = 0; i < m; i++)
                    sum += Binomial(2 * m, 2 * i) * e[i];
                e[m] = -sum;
            }
            return e[n / 2];
        }

        private static BigInteger Factorial(int n)
        {
            BigInteger f = 1;
            for (var i = 2; i <= n; i++)
                f *= i;
            return f;
        }

        private static BigInteger Binomial(int n, int r) => Factorial(n) / (Factorial(r) * Factorial(n - r));

        private readonly struct Fraction
        {
            public static readonly Fraction Zero = new Fraction(0, 1);

            public static readonly Fraction One = new Fraction(1, 1);

            public Fraction(BigInteger numerator, BigInteger denominator)
            {
                if (denominator.Sign < 0)
                {
                    numerator = -numerator;
                    denominator = -denominator;
                }
                var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
                Numerator = gcd.IsZero ? numerator : numerator / gcd;
                Denominator = gcd.IsZero ? denominator : denominator / gcd;
            }

            public BigInteger Numerator { get; }

            public BigInteger Denominator { get; }

            public Entity Entity => MathS.FromString(
                Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture), useCache: false);

            public static Fraction operator +(Fraction x, Fraction y) =>
                new Fraction(x.Numerator * y.Denominator + y.Numerator * x.Denominator, x.Denominator * y.Denominator);

            public static Fraction operator -(Fraction x, Fraction y) =>
                new Fraction(x.Numerator * y.Denominator - y.Numerator * x.Denominator, x.Denominator * y.Denominator);

            public static Fraction operator *(Fraction x, Fraction y) => new Fraction(x.Numerator * y.Numerator, x.Denominator * y.Denominator);

            public static Fraction operator /(Fraction x, Fraction y) => new Fraction(x.Numerator * y.Denominator, x.Denominator * y.Numerator);
        }
    }
}
