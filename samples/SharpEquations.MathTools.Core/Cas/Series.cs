namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using AngouriMath;

    /// <summary>
    /// The sums to infinity that AngouriMath leaves as they are, when the term is a quotient of polynomials of the index
    /// with fractions as coefficients, P(k)/Q(k), or one with the sign of (−1)ᵏ:
    /// <list type="bullet">
    /// <item>the quotient is split in partial fractions c/(k−r)ʲ over the fractions r that are roots of Q;</item>
    /// <item>those with j = 1 sum to values of the digamma function at fractions, which the theorem of Gauss writes with
    /// π and logarithms: ln 2 for (−1)ᵏ⁺¹/k, π/4 for (−1)ᵏ/(2k+1), 1 for 1/(k(k+1));</item>
    /// <item>those with j &gt; 1 sum to values of ζ(j, x): a fraction times πʲ for an even j at 1 and 1/2 (π²/6 for 1/k²,
    /// by the Bernoulli numbers), and for any j at x and 1 − x together, by the derivatives of the cotangent (π³/32 for
    /// (−1)ᵏ/(2k+1)³);</item>
    /// <item>a factor k² + bk + c of Q with no such roots, with b whole and a number over it, sums by the residues of
    /// π cot πz: (π coth π − 1)/2 for 1/(k²+1);</item>
    /// <item>a sum with the sign of (−1)ᵏ is the sum of its even terms less that of its odd ones;</item>
    /// <item>a term that does not go to zero fast enough has a sum with no end, ∞ or −∞.</item>
    /// </list>
    /// A factor of the term without the index is taken out, and a sum of terms is the sum of their sums, when each has
    /// one (also by AngouriMath, as a geometric one). What is not of these forms, or has no such value (ζ(3) for 1/k³),
    /// is left as it is.
    /// </summary>
    public static class Series
    {
        private const int MostTerms = 5000;

        private const int MostDenominator = 360;

        private const int MostPower = 64;

        private static readonly BigInteger MostCoefficient = BigInteger.Pow(10, 12);

        private static readonly Fraction Half = new Fraction(1, 2);

        /// <summary>The expression with each sum to infinity that has a known value replaced by it.</summary>
        public static Entity Close(Entity expression) =>
            expression.Nodes.Any(n => n is Entity.Summationf) ? expression.Replace(e => e is Entity.Summationf sum ? Value(sum) ?? e : e) : expression;

        /// <summary>The value of a sum to infinity of a known form, or null.</summary>
        public static Entity? Value(Entity.Summationf sum)
        {
            if (sum.To != MathS.oo || sum.Var is not Entity.Variable k || sum.From is not Entity.Number.Integer from
                || !from.EInteger.CanFitInInt64() || sum.Expression.Vars.Any(v => v != k))
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
                    Terms(m.Minuend, parts, sign);
                    Terms(m.Subtrahend, parts, -sign);
                    break;
                default:
                    parts.Add(sign > 0 ? e : -e);
                    break;
            }
        }

        private static Entity? Value(Entity body, Entity.Variable k, long start)
        {
            if (Read(body, k) is { } term)
                return Sum(term.Plain, term.Alternating, start);
            if (Peel(body, k) is not { } peeled || Value(peeled.Term, k, start) is not { } value)
                return null;
            if (value != MathS.oo && value != -MathS.oo)
                return peeled.Constant * value;
            return Sign(peeled.Constant) switch
            {
                > 0 => value,
                < 0 => value == MathS.oo ? -MathS.oo : MathS.oo,
                _ => null,
            };
        }

        // a factor without the index, and the rest
        private static (Entity Constant, Entity Term)? Peel(Entity body, Entity.Variable k) => body switch
        {
            Entity.Mulf m when Free(m.Multiplier, k) => (m.Multiplier, m.Multiplicand),
            Entity.Mulf m when Free(m.Multiplicand, k) => (m.Multiplicand, m.Multiplier),
            Entity.Divf d when Free(d.Divisor, k) => (1 / d.Divisor, d.Dividend),
            Entity.Divf d when Free(d.Dividend, k) && d.Dividend is not Entity.Number => (d.Dividend, 1 / d.Divisor),
            Entity.Divf d when Peel(d.Divisor, k) is { } divisor => (1 / divisor.Constant, d.Dividend / divisor.Term),
            _ => null,
        };

        private static bool Free(Entity e, Entity.Variable k) => !e.Vars.Contains(k);

        private static int Sign(Entity constant)
        {
            try
            {
                return constant.EvalNumerical() is Entity.Number.Real real ? real.EDecimal.Sign : 0;
            }
            catch (AngouriMath.Core.Exceptions.AngouriMathBaseException)
            {
                return 0;
            }
        }

        // the term as plain(k) + (−1)ᵏ alternating(k), when it is made of fractions, the index, the four operations, whole
        // powers and powers of −1
        private static (RationalFunction Plain, RationalFunction Alternating)? Read(Entity e, Entity.Variable k)
        {
            switch (e)
            {
                case Entity.Number.Rational number:
                    return (Constant(ToFraction(number)), RationalFunction.Zero);
                case Entity.Variable v when v == k:
                    return (new RationalFunction(Polynomial.X, Polynomial.One), RationalFunction.Zero);
                case Entity.Sumf s when Read(s.Augend, k) is { } x && Read(s.Addend, k) is { } y:
                    return (x.Plain + y.Plain, x.Alternating + y.Alternating);
                case Entity.Minusf s when Read(s.Minuend, k) is { } x && Read(s.Subtrahend, k) is { } y:
                    return (x.Plain - y.Plain, x.Alternating - y.Alternating);
                case Entity.Mulf m when Read(m.Multiplier, k) is { } x && Read(m.Multiplicand, k) is { } y:
                    return Multiply(x, y);
                case Entity.Divf d when Read(d.Dividend, k) is { } x && Read(d.Divisor, k) is { } y:
                    return Divide(x, y);
                case Entity.Powf p when Free(p.Exponent, k):
                    if (Whole(p.Exponent) is not { } power || Math.Abs(power) > MostPower || Read(p.Base, k) is not { } b)
                        return null;
                    var result = (Plain: Constant(Fraction.One), Alternating: RationalFunction.Zero);
                    for (var i = 0; i < Math.Abs(power); i++)
                        result = Multiply(result, b);
                    return power < 0 ? Divide((Constant(Fraction.One), RationalFunction.Zero), result) : result;
                case Entity.Powf p when Free(p.Base, k) && Whole(p.Base) == -1 && Read(p.Exponent, k) is { } exponent:
                    var line = exponent.Plain.Numerator;
                    if (!exponent.Alternating.IsZero || exponent.Plain.Denominator.Degree != 0 || line.Degree > 1
                        || !line[0].IsInteger || !line[1].IsInteger)
                        return null;
                    var sign = Constant(line[0].Numerator.IsEven ? 1 : -1);
                    return line[1].Numerator.IsEven ? (sign, RationalFunction.Zero) : (RationalFunction.Zero, sign);
                default:
                    return null;
            }
        }

        // (−1)ᵏ times itself is 1
        private static (RationalFunction Plain, RationalFunction Alternating) Multiply(
            (RationalFunction Plain, RationalFunction Alternating) x, (RationalFunction Plain, RationalFunction Alternating) y) =>
            (x.Plain * y.Plain + x.Alternating * y.Alternating, x.Plain * y.Alternating + x.Alternating * y.Plain);

        private static (RationalFunction Plain, RationalFunction Alternating)? Divide(
            (RationalFunction Plain, RationalFunction Alternating) x, (RationalFunction Plain, RationalFunction Alternating) y)
        {
            if (y.Alternating.IsZero)
                return y.Plain.IsZero ? null : (x.Plain / y.Plain, x.Alternating / y.Plain);
            if (y.Plain.IsZero)
                return (x.Alternating / y.Alternating, x.Plain / y.Alternating);
            var norm = y.Plain * y.Plain - y.Alternating * y.Alternating;
            if (norm.IsZero)
                return null;
            return ((x.Plain * y.Plain - x.Alternating * y.Alternating) / norm, (x.Alternating * y.Plain - x.Plain * y.Alternating) / norm);
        }

        private static RationalFunction Constant(Fraction value) => new RationalFunction(Polynomial.Constant(value), Polynomial.One);

        private static Fraction ToFraction(Entity.Number.Rational number) => new Fraction(
            BigInteger.Parse(number.ERational.Numerator.ToString(), CultureInfo.InvariantCulture),
            BigInteger.Parse(number.ERational.Denominator.ToString(), CultureInfo.InvariantCulture));

        private static long? Whole(Entity e) =>
            e.Evaled is Entity.Number.Integer whole && whole.EInteger.CanFitInInt64() ? whole.EInteger.ToInt64Checked() : (long?)null;

        // ∑_(k≥start) plain(k) + (−1)ᵏ alternating(k)
        private static Entity? Sum(RationalFunction plain, RationalFunction alternating, long start)
        {
            var total = new Combination();
            if (!alternating.IsZero)
            {
                // the terms have to go to zero; then the even ones less the odd ones, as a sum over m with k = start + 2m
                if (alternating.Gap < 1 || Decompose(alternating, start) is not { } parts)
                    return null;
                var sign = start % 2 == 0 ? 1 : -1;
                var halved = new Parts();
                foreach (var (c, root, power) in parts.Poles)
                {
                    var scale = c * Half.Pow(power) * sign;
                    halved.Poles.Add((scale, (root - start) * Half, power));
                    halved.Poles.Add((-scale, (root - start - 1) * Half, power));
                }
                foreach (var (beta, b, c) in parts.Quadratics)
                {
                    for (var odd = 0; odd <= 1; odd++)
                    {
                        Fraction first = start + odd;
                        var scale = beta * new Fraction(odd == 0 ? sign : -sign, 4);
                        halved.Quadratics.Add((scale, first + b * Half, (first * first + b * first + c) * new Fraction(1, 4)));
                    }
                }
                if (!Add(total, halved, 0))
                    return null;
            }
            if (!plain.IsZero)
            {
                if (plain.Gap < 2)
                    return Defined(plain, start) ? (plain.Numerator.Leading.Sign > 0 ? MathS.oo : -MathS.oo) : null;
                if (Decompose(plain, start) is not { } parts || !Add(total, parts, start))
                    return null;
            }
            return total.Value();
        }

        // whether the quotient is a number at each index of the sum
        private static bool Defined(RationalFunction r, long start) =>
            Roots(r.Denominator) is { } factors && !factors.Roots.Any(f => f.Root.IsInteger && f.Root >= start);

        // the partial fractions of a quotient, when its denominator has only fractions as roots but for one factor of
        // degree 2, and no root is an index of the sum
        private static Parts? Decompose(RationalFunction r, long start)
        {
            if (Roots(r.Denominator) is not { } factors || (factors.Left.Degree != 0 && factors.Left.Degree != 2))
                return null;
            var parts = new Parts();
            var rest = r;
            foreach (var (root, multiplicity) in factors.Roots)
            {
                if (root.IsInteger && root >= start)
                    return null;

                // the first coefficients of the quotient as a series in k − root
                var numerator = r.Numerator.Compose(Fraction.One, root);
                var others = r.Denominator.DivRem(Polynomial.Linear(root).Pow(multiplicity)).Quotient.Compose(Fraction.One, root);
                var series = new Fraction[multiplicity];
                for (var n = 0; n < multiplicity; n++)
                {
                    var coefficient = numerator[n];
                    for (var i = 1; i <= n; i++)
                        coefficient -= others[i] * series[n - i];
                    series[n] = coefficient / others[0];
                }
                for (var power = 1; power <= multiplicity; power++)
                {
                    var c = series[multiplicity - power];
                    if (c.IsZero)
                        continue;
                    parts.Poles.Add((c, root, power));
                    rest -= new RationalFunction(Polynomial.Constant(c), Polynomial.Linear(root).Pow(power));
                }
            }
            if (factors.Left.Degree == 0)
                return rest.IsZero ? parts : null;
            if (rest.Denominator.Degree != 2 || rest.Numerator.Degree != 0)
                return null;
            parts.Quadratics.Add((rest.Numerator[0], rest.Denominator[1], rest.Denominator[0]));
            return parts;
        }

        // the roots of a polynomial that are fractions, and what is left of it without them
        private static (List<(Fraction Root, int Multiplicity)> Roots, Polynomial Left)? Roots(Polynomial q)
        {
            var roots = new List<(Fraction Root, int Multiplicity)>();
            var rest = q;
            var zero = 0;
            for (; rest.Degree > 0 && rest[0].IsZero; zero++)
                rest = rest.DivRem(Polynomial.X).Quotient;
            if (zero > 0)
                roots.Add((Fraction.Zero, zero));
            if (rest.Degree < 1)
                return (roots, rest);

            // such a root is a divisor of the last coefficient over one of the first, once they are whole
            var common = BigInteger.One;
            for (var i = 0; i <= rest.Degree; i++)
                common = common / BigInteger.GreatestCommonDivisor(common, rest[i].Denominator) * rest[i].Denominator;
            if (Divisors(BigInteger.Abs((rest[0] * common).Numerator)) is not { } last
                || Divisors(BigInteger.Abs((rest.Leading * common).Numerator)) is not { } first)
                return null;
            foreach (var p in last)
            {
                foreach (var d in first)
                {
                    if (!BigInteger.GreatestCommonDivisor(p, d).IsOne)
                        continue;
                    foreach (var candidate in new[] { new Fraction(p, d), new Fraction(-p, d) })
                    {
                        var multiplicity = 0;
                        for (; rest.Degree > 0 && rest.Evaluate(candidate).IsZero; multiplicity++)
                            rest = rest.DivRem(Polynomial.Linear(candidate)).Quotient;
                        if (multiplicity > 0)
                            roots.Add((candidate, multiplicity));
                    }
                }
            }
            return (roots, rest);
        }

        private static List<BigInteger>? Divisors(BigInteger n)
        {
            if (n.IsZero || n > MostCoefficient)
                return null;
            var divisors = new List<BigInteger> { BigInteger.One };
            var left = (ulong)n;
            for (ulong prime = 2; prime * prime <= left; prime++)
            {
                var count = divisors.Count;
                BigInteger power = 1;
                while (left % prime == 0)
                {
                    left /= prime;
                    power *= prime;
                    for (var i = 0; i < count; i++)
                        divisors.Add(divisors[i] * power);
                }
            }
            if (left > 1)
            {
                var count = divisors.Count;
                for (var i = 0; i < count; i++)
                    divisors.Add(divisors[i] * left);
            }
            return divisors;
        }

        // adds ∑_(k≥start) of the partial fractions, whose terms in 1/k cancel; false when it has no value in closed form
        private static bool Add(Combination total, Parts parts, long start)
        {
            // ∑_(k≥start) c/(k−r)ʲ is −c ψ(x) for j = 1 and c ζ(j, x) for the others, at x = start − r; x is brought
            // between 0 and 1 by taking the first terms away, or putting them back
            var hurwitz = new Dictionary<(int Power, Fraction At), Fraction>();
            foreach (var (c, root, power) in parts.Poles)
            {
                var x = start - root;
                var whole = -(-x).Floor() - 1;
                if (BigInteger.Abs(whole) > MostTerms)
                    return false;
                var at = x - whole;
                var head = Fraction.Zero;
                for (var i = Math.Min((long)whole, 0); i < Math.Max((long)whole, 0); i++)
                {
                    if ((at + i).IsZero)
                        return false;
                    head += (at + i).Pow(-power);
                }
                total.Add(whole.Sign >= 0 ? -c * head : c * head);
                if (power > 1)
                    hurwitz[(power, at)] = (hurwitz.TryGetValue((power, at), out var sum) ? sum : Fraction.Zero) + c;
                else if (!Digamma(total, at, -c))
                    return false;
            }

            foreach (var entry in hurwitz)
            {
                var (power, at) = entry.Key;
                var a = entry.Value;
                if (a.IsZero)
                    continue;
                Entity pi = MathS.Pow(MathS.pi, power);
                if (at == Fraction.One || at == Half)
                {
                    // ζ(j) and ζ(j, 1/2) = (2ʲ − 1) ζ(j)
                    if (power % 2 == 1)
                        return false;
                    total.Add(a * (at == Half ? BigInteger.Pow(2, power) - 1 : BigInteger.One) * ZetaOfEven(power), pi);
                    continue;
                }

                // ζ(j, x) + (−1)ʲ ζ(j, 1−x) = (−1)ʲ⁻¹ π dʲ⁻¹/dxʲ⁻¹ cot πx / (j−1)!
                var mirror = Fraction.One - at;
                var b = hurwitz.TryGetValue((power, mirror), out var other) ? other : Fraction.Zero;
                if (b != (power % 2 == 0 ? a : -a))
                    return false;
                if (at > mirror)
                    continue;
                if (at.Denominator > MostDenominator)
                    return false;
                var derivative = CotangentDerivative(power - 1);
                var scale = (power % 2 == 0 ? -a : a) / Factorial(power - 1);
                var (cot, irrational, square) = Cotangent(at);
                if (square == 0)
                {
                    Entity polynomial = 0;
                    for (var i = 0; i < derivative.Length; i++)
                        if (!derivative[i].IsZero)
                            polynomial += ((Fraction)derivative[i]).Entity * MathS.Pow(irrational!, i);
                    total.Add(scale, pi, polynomial);
                    continue;
                }
                var even = Fraction.Zero;
                var odd = Fraction.Zero;
                for (var i = 0; i < derivative.Length; i++)
                {
                    var term = derivative[i] * cot.Pow(i) * BigInteger.Pow(square, i / 2);
                    if (i % 2 == 0)
                        even += term;
                    else
                        odd += term;
                }
                total.Add(scale * even, pi);
                total.Add(scale * odd, pi, irrational);
            }

            // ∑_(k≥start) β/(k² + bk + c), by ψ(1−z) − ψ(z) = π cot πz at the roots: half the sum over all the whole
            // numbers, less (or plus) the terms between the two halves
            foreach (var (beta, b, c) in parts.Quadratics)
            {
                if (!b.IsInteger)
                    return false;
                var span = 2 * start + b.Numerator;
                if (BigInteger.Abs(span) > MostTerms)
                    return false;
                var quadratic = new Polynomial(new[] { c, b, Fraction.One });
                var between = Fraction.Zero;
                for (var i = span.Sign > 0 ? 1 : (long)span; i <= (span.Sign > 0 ? (long)span - 1 : 0); i++)
                    between += Fraction.One / quadratic.Evaluate(start - i);
                total.Add((span.Sign > 0 ? -beta : beta) * Half * between);

                var discriminant = b * b - 4 * c;
                var quarter = (discriminant.Sign < 0 ? -discriminant : discriminant) * new Fraction(1, 4);
                var rational = quarter.SquareRoot();
                Entity half = rational?.Entity ?? MathS.Sqrt(quarter.Entity);
                var angle = MathS.pi * half;
                var function = (discriminant.Sign < 0, span.IsEven) switch
                {
                    (true, true) => MathS.Hyperbolic.Cotanh(angle),
                    (true, false) => MathS.Hyperbolic.Tanh(angle),
                    (false, true) => -MathS.Cotan(angle),
                    (false, false) => MathS.Tan(angle),
                };
                if (rational is { } root)
                    total.Add(beta * Half / root, MathS.pi, function);
                else
                    total.Add(beta * Half, MathS.pi, function / half);
            }
            return true;
        }

        // adds weight·ψ(p/q), by the theorem of Gauss:
        // ψ(p/q) = −γ − ln 2q − π/2 cot(πp/q) + 2 ∑_(0<n<q/2) cos(2πnp/q) ln sin(πn/q)
        private static bool Digamma(Combination total, Fraction at, Fraction weight)
        {
            total.Gamma -= weight;
            if (at == Fraction.One)
                return true;
            if (at.Denominator > MostDenominator)
                return false;
            var p = (long)at.Numerator;
            var q = (long)at.Denominator;
            var left = 2 * q;
            for (long prime = 2; left > 1; prime++)
                for (; left % prime == 0; left /= prime)
                    total.Add(-weight, MathS.Ln(prime));
            var (cot, irrational, _) = Cotangent(at);
            total.Add(-weight * Half * cot, MathS.pi, irrational);
            for (long n = 1; 2 * n < q; n++)
            {
                var (cos, cosine) = Cosine(new Fraction(n * p, q));
                foreach (var (coefficient, logarithm) in LogSine(new Fraction(n, q)))
                    total.Add(2 * weight * cos * coefficient, cosine, logarithm);
            }
            return true;
        }

        // cot πx as a fraction times 1, √3 (whose square is given) or the cotangent itself (square 0)
        private static (Fraction Value, Entity? Irrational, int Square) Cotangent(Fraction x)
        {
            switch ((int)x.Numerator, (int)x.Denominator)
            {
                case (1, 2): return (Fraction.Zero, null, 1);
                case (1, 4): return (Fraction.One, null, 1);
                case (3, 4): return (Fraction.MinusOne, null, 1);
                case (1, 3): return (new Fraction(1, 3), MathS.Sqrt(3), 3);
                case (2, 3): return (new Fraction(-1, 3), MathS.Sqrt(3), 3);
                case (1, 6): return (Fraction.One, MathS.Sqrt(3), 3);
                case (5, 6): return (Fraction.MinusOne, MathS.Sqrt(3), 3);
                default: return (Fraction.One, MathS.Cotan(MathS.pi * x.Entity), 0);
            }
        }

        // cos 2πt as a fraction, or times the cosine itself
        private static (Fraction Value, Entity? Irrational) Cosine(Fraction t)
        {
            t -= t.Floor();
            switch ((int)t.Numerator, (int)t.Denominator)
            {
                case (0, 1): return (Fraction.One, null);
                case (1, 2): return (Fraction.MinusOne, null);
                case (1, 4) or (3, 4): return (Fraction.Zero, null);
                case (1, 3) or (2, 3): return (new Fraction(-1, 2), null);
                case (1, 6) or (5, 6): return (Half, null);
                default: return (Fraction.One, MathS.Cos(2 * MathS.pi * t.Entity));
            }
        }

        // ln sin πt for 0 < t < 1, as fractions times logarithms
        private static (Fraction Coefficient, Entity Logarithm)[] LogSine(Fraction t)
        {
            switch ((int)t.Numerator, (int)t.Denominator)
            {
                case (1, 2): return Array.Empty<(Fraction, Entity)>();
                case (1, 6) or (5, 6): return new[] { (Fraction.MinusOne, MathS.Ln(2)) };
                case (1, 4) or (3, 4): return new[] { (new Fraction(-1, 2), MathS.Ln(2)) };
                case (1, 3) or (2, 3): return new[] { (Half, MathS.Ln(3)), (Fraction.MinusOne, MathS.Ln(2)) };
                default: return new[] { (Fraction.One, MathS.Ln(MathS.Sin(MathS.pi * t.Entity))) };
            }
        }

        // dᵐ/dxᵐ cot πx = πᵐ Pₘ(cot πx): P₀ = c, Pₘ₊₁ = −(1 + c²) Pₘ′
        private static BigInteger[] CotangentDerivative(int order)
        {
            var p = new[] { BigInteger.Zero, BigInteger.One };
            for (var m = 0; m < order; m++)
            {
                var next = new BigInteger[p.Length + 1];
                for (var i = 1; i < p.Length; i++)
                {
                    next[i - 1] -= i * p[i];
                    next[i + 1] -= i * p[i];
                }
                p = next;
            }
            return p;
        }

        // ζ(p)/πᵖ for an even p: (−1)^(p/2+1) Bₚ 2ᵖ / (2 p!)
        private static Fraction ZetaOfEven(int p) =>
            Bernoulli(p) * new Fraction((p / 2 % 2 == 1 ? 1 : -1) * BigInteger.Pow(2, p), 2 * Factorial(p));

        // Bₙ, by the algorithm of Akiyama and Tanigawa
        private static Fraction Bernoulli(int n)
        {
            var a = new Fraction[n + 1];
            for (var m = 0; m <= n; m++)
            {
                a[m] = new Fraction(1, m + 1);
                for (var j = m; j >= 1; j--)
                    a[j - 1] = j * (a[j - 1] - a[j]);
            }
            return a[0];
        }

        private static BigInteger Factorial(int n)
        {
            BigInteger f = 1;
            for (var i = 2; i <= n; i++)
                f *= i;
            return f;
        }

        // the partial fractions c/(k − root)ʲ and β/(k² + bk + c) of a term
        private sealed class Parts
        {
            public List<(Fraction C, Fraction Root, int Power)> Poles { get; } = new List<(Fraction, Fraction, int)>();

            public List<(Fraction Beta, Fraction B, Fraction C)> Quadratics { get; } = new List<(Fraction, Fraction, Fraction)>();
        }

        // a fraction plus fractions times numbers that are not (π², ln 2, π√3…), those of one number added together
        private sealed class Combination
        {
            private readonly Dictionary<string, (Fraction Coefficient, Entity Number)> numbers = new Dictionary<string, (Fraction, Entity)>();

            private readonly List<string> order = new List<string>();

            private Fraction fraction = Fraction.Zero;

            /// <summary>How many times the constant of Euler, which no value in closed form has.</summary>
            public Fraction Gamma { get; set; } = Fraction.Zero;

            public void Add(Fraction coefficient, params Entity?[] factors)
            {
                Entity? number = null;
                foreach (var factor in factors)
                    if (factor is not null)
                        number = number is null ? factor : number * factor;
                if (number is null)
                {
                    fraction += coefficient;
                    return;
                }
                var key = number.ToString();
                if (numbers.TryGetValue(key, out var known))
                {
                    numbers[key] = (known.Coefficient + coefficient, known.Number);
                    return;
                }
                numbers[key] = (coefficient, number);
                order.Add(key);
            }

            public Entity? Value()
            {
                if (!Gamma.IsZero)
                    return null;
                Entity? sum = null;
                foreach (var key in order)
                {
                    var (coefficient, number) = numbers[key];
                    if (coefficient.IsZero)
                        continue;
                    var term = coefficient == Fraction.One ? number : coefficient.Entity * number;
                    sum = sum is null ? term : sum + term;
                }
                if (sum is null || !fraction.IsZero)
                    sum = sum is null ? fraction.Entity : sum + fraction.Entity;
                return sum;
            }
        }
    }
}
