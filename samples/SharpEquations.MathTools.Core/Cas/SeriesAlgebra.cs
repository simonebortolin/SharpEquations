namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Numerics;
    using AngouriMath;

    /// <summary>An exact fraction of whole numbers, in lowest terms with a positive denominator.</summary>
    internal readonly struct Fraction : IEquatable<Fraction>
    {
        public static readonly Fraction Zero = new Fraction(0, 1);

        public static readonly Fraction One = new Fraction(1, 1);

        public static readonly Fraction MinusOne = new Fraction(-1, 1);

        private readonly BigInteger denominator;

        public Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
                throw new DivideByZeroException();
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator / gcd;
            this.denominator = denominator / gcd;
        }

        public BigInteger Numerator { get; }

        public BigInteger Denominator => denominator.IsZero ? BigInteger.One : denominator;

        public bool IsZero => Numerator.IsZero;

        public bool IsInteger => Denominator.IsOne;

        public int Sign => Numerator.Sign;

        /// <summary>The fraction as a number of AngouriMath.</summary>
        public Entity Entity => MathS.FromString(
            IsInteger
                ? "(" + Numerator.ToString(CultureInfo.InvariantCulture) + ")"
                : "(" + Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture) + ")",
            useCache: false);

        public static implicit operator Fraction(long value) => new Fraction(value, 1);

        public static implicit operator Fraction(BigInteger value) => new Fraction(value, 1);

        public static Fraction operator +(Fraction x, Fraction y) =>
            new Fraction(x.Numerator * y.Denominator + y.Numerator * x.Denominator, x.Denominator * y.Denominator);

        public static Fraction operator -(Fraction x, Fraction y) =>
            new Fraction(x.Numerator * y.Denominator - y.Numerator * x.Denominator, x.Denominator * y.Denominator);

        public static Fraction operator -(Fraction x) => new Fraction(-x.Numerator, x.Denominator);

        public static Fraction operator *(Fraction x, Fraction y) => new Fraction(x.Numerator * y.Numerator, x.Denominator * y.Denominator);

        public static Fraction operator /(Fraction x, Fraction y) => new Fraction(x.Numerator * y.Denominator, x.Denominator * y.Numerator);

        public static bool operator ==(Fraction x, Fraction y) => x.Equals(y);

        public static bool operator !=(Fraction x, Fraction y) => !x.Equals(y);

        public static bool operator <(Fraction x, Fraction y) => (x - y).Sign < 0;

        public static bool operator >(Fraction x, Fraction y) => (x - y).Sign > 0;

        public static bool operator <=(Fraction x, Fraction y) => (x - y).Sign <= 0;

        public static bool operator >=(Fraction x, Fraction y) => (x - y).Sign >= 0;

        /// <summary>The fraction to a whole power, which may be negative.</summary>
        public Fraction Pow(int exponent) => exponent >= 0
            ? new Fraction(BigInteger.Pow(Numerator, exponent), BigInteger.Pow(Denominator, exponent))
            : new Fraction(BigInteger.Pow(Denominator, -exponent), BigInteger.Pow(Numerator, -exponent));

        /// <summary>The greatest whole number not above the fraction.</summary>
        public BigInteger Floor()
        {
            var quotient = BigInteger.DivRem(Numerator, Denominator, out var remainder);
            return remainder.Sign < 0 ? quotient - 1 : quotient;
        }

        /// <summary>The fraction whose square this is, or null.</summary>
        public Fraction? SquareRoot()
        {
            if (Sign < 0)
                return null;
            var n = Root(Numerator);
            var d = Root(Denominator);
            return n * n == Numerator && d * d == Denominator ? new Fraction(n, d) : (Fraction?)null;
        }

        public bool Equals(Fraction other) => Numerator == other.Numerator && Denominator == other.Denominator;

        public override bool Equals(object? obj) => obj is Fraction other && Equals(other);

        public override int GetHashCode() => Numerator.GetHashCode() ^ (Denominator.GetHashCode() * 397);

        public override string ToString() => IsInteger ? Numerator.ToString(CultureInfo.InvariantCulture) : Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture);

        // the whole part of the square root, by Newton's method
        private static BigInteger Root(BigInteger n)
        {
            if (n < 2)
                return n;
            var x = (BigInteger)Math.Sqrt((double)n);
            while (x * x > n)
                x--;
            while ((x + 1) * (x + 1) <= n)
                x++;
            return x;
        }
    }

    /// <summary>A polynomial of one letter with fractions as coefficients, from the constant one up.</summary>
    internal sealed class Polynomial
    {
        public static readonly Polynomial Zero = new Polynomial(Array.Empty<Fraction>());

        public static readonly Polynomial One = Constant(Fraction.One);

        public static readonly Polynomial X = new Polynomial(new[] { Fraction.Zero, Fraction.One });

        private readonly Fraction[] coefficients;

        public Polynomial(IReadOnlyList<Fraction> coefficients)
        {
            var length = coefficients.Count;
            while (length > 0 && coefficients[length - 1].IsZero)
                length--;
            this.coefficients = new Fraction[length];
            for (var i = 0; i < length; i++)
                this.coefficients[i] = coefficients[i];
        }

        /// <summary>The degree, −1 for the polynomial zero.</summary>
        public int Degree => coefficients.Length - 1;

        public bool IsZero => coefficients.Length == 0;

        public Fraction Leading => IsZero ? Fraction.Zero : coefficients[coefficients.Length - 1];

        public Fraction this[int power] => power < coefficients.Length ? coefficients[power] : Fraction.Zero;

        public static Polynomial Constant(Fraction value) => new Polynomial(new[] { value });

        /// <summary>x − root.</summary>
        public static Polynomial Linear(Fraction root) => new Polynomial(new[] { -root, Fraction.One });

        public static Polynomial operator +(Polynomial x, Polynomial y)
        {
            var sum = new Fraction[Math.Max(x.coefficients.Length, y.coefficients.Length)];
            for (var i = 0; i < sum.Length; i++)
                sum[i] = x[i] + y[i];
            return new Polynomial(sum);
        }

        public static Polynomial operator -(Polynomial x, Polynomial y) => x + y * Fraction.MinusOne;

        public static Polynomial operator *(Polynomial x, Fraction y)
        {
            var product = new Fraction[x.coefficients.Length];
            for (var i = 0; i < product.Length; i++)
                product[i] = x.coefficients[i] * y;
            return new Polynomial(product);
        }

        public static Polynomial operator *(Polynomial x, Polynomial y)
        {
            if (x.IsZero || y.IsZero)
                return Zero;
            var product = new Fraction[x.coefficients.Length + y.coefficients.Length - 1];
            for (var i = 0; i < product.Length; i++)
                product[i] = Fraction.Zero;
            for (var i = 0; i < x.coefficients.Length; i++)
                for (var j = 0; j < y.coefficients.Length; j++)
                    product[i + j] += x.coefficients[i] * y.coefficients[j];
            return new Polynomial(product);
        }

        /// <summary>The quotient and the remainder of a division by a polynomial that is not zero.</summary>
        public (Polynomial Quotient, Polynomial Remainder) DivRem(Polynomial divisor)
        {
            if (divisor.IsZero)
                throw new DivideByZeroException();
            var remainder = (Fraction[])coefficients.Clone();
            var quotient = new Fraction[Math.Max(0, Degree - divisor.Degree + 1)];
            for (var i = 0; i < quotient.Length; i++)
                quotient[i] = Fraction.Zero;
            for (var power = Degree; power >= divisor.Degree; power--)
            {
                var factor = remainder[power] / divisor.Leading;
                quotient[power - divisor.Degree] = factor;
                for (var i = 0; i <= divisor.Degree; i++)
                    remainder[power - divisor.Degree + i] -= factor * divisor[i];
            }
            return (new Polynomial(quotient), new Polynomial(remainder));
        }

        /// <summary>The greatest common divisor, with 1 as its first coefficient.</summary>
        public static Polynomial Gcd(Polynomial x, Polynomial y)
        {
            while (!y.IsZero)
                (x, y) = (y, x.DivRem(y).Remainder);
            return x.IsZero ? x : x * (Fraction.One / x.Leading);
        }

        public Fraction Evaluate(Fraction at)
        {
            var value = Fraction.Zero;
            for (var i = coefficients.Length - 1; i >= 0; i--)
                value = value * at + coefficients[i];
            return value;
        }

        /// <summary>The polynomial of scale·x + shift.</summary>
        public Polynomial Compose(Fraction scale, Fraction shift)
        {
            var inner = new Polynomial(new[] { shift, scale });
            var result = Zero;
            for (var i = coefficients.Length - 1; i >= 0; i--)
                result = result * inner + Constant(coefficients[i]);
            return result;
        }

        public Polynomial Pow(int exponent)
        {
            var result = One;
            for (var i = 0; i < exponent; i++)
                result *= this;
            return result;
        }
    }

    /// <summary>A quotient of polynomials in lowest terms, the first coefficient of the denominator 1.</summary>
    internal sealed class RationalFunction
    {
        public static readonly RationalFunction Zero = new RationalFunction(Polynomial.Zero, Polynomial.One);

        public RationalFunction(Polynomial numerator, Polynomial denominator)
        {
            if (denominator.IsZero)
                throw new DivideByZeroException();
            var gcd = Polynomial.Gcd(numerator, denominator);
            var scale = Fraction.One / denominator.Leading;
            Numerator = numerator.DivRem(gcd).Quotient * (scale * gcd.Leading);
            Denominator = denominator.DivRem(gcd).Quotient * (scale * gcd.Leading);
            var leading = Fraction.One / Denominator.Leading;
            Numerator *= leading;
            Denominator *= leading;
        }

        public Polynomial Numerator { get; }

        public Polynomial Denominator { get; }

        public bool IsZero => Numerator.IsZero;

        /// <summary>How much faster the denominator grows: the degree of the one less that of the other.</summary>
        public int Gap => Denominator.Degree - Numerator.Degree;

        public static RationalFunction operator +(RationalFunction x, RationalFunction y) =>
            new RationalFunction(x.Numerator * y.Denominator + y.Numerator * x.Denominator, x.Denominator * y.Denominator);

        public static RationalFunction operator -(RationalFunction x) => new RationalFunction(x.Numerator * Fraction.MinusOne, x.Denominator);

        public static RationalFunction operator -(RationalFunction x, RationalFunction y) => x + -y;

        public static RationalFunction operator *(RationalFunction x, RationalFunction y) =>
            new RationalFunction(x.Numerator * y.Numerator, x.Denominator * y.Denominator);

        /// <summary>The quotient by a function that is not zero.</summary>
        public static RationalFunction operator /(RationalFunction x, RationalFunction y) =>
            new RationalFunction(x.Numerator * y.Denominator, x.Denominator * y.Numerator);

        /// <summary>The function of scale·x + shift.</summary>
        public RationalFunction Compose(Fraction scale, Fraction shift) =>
            new RationalFunction(Numerator.Compose(scale, shift), Denominator.Compose(scale, shift));
    }
}
