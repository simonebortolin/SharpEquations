namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A triangle: its sides <c>a</c>, <c>b</c>, <c>c</c> and the angles opposite them, in degrees.</summary>
    public sealed record Triangle(double A, double B, double C, double Alpha, double Beta, double Gamma)
    {
        public double Perimeter => A + B + C;

        public double Area => 0.5 * B * C * Math.Sin(Triangles.Radians(Alpha));

        /// <summary>The radius of the circle through the vertices.</summary>
        public double Circumradius => A / (2 * Math.Sin(Triangles.Radians(Alpha)));

        /// <summary>The radius of the circle inside, tangent to the sides.</summary>
        public double Inradius => 2 * Area / Perimeter;

        public double Side(int i) => i switch { 0 => A, 1 => B, _ => C };

        public double Angle(int i) => i switch { 0 => Alpha, 1 => Beta, _ => Gamma };
    }

    /// <summary>
    /// What three values of a triangle give: the case (<c>SSS</c>, <c>SAS</c>, <c>SSA</c>, <c>ASA</c>, <c>AAS</c>), the
    /// triangles (none, one, or two in the ambiguous case <c>SSA</c>), the laws used to find them, as UnicodeMath, and
    /// the reason when there is no triangle.
    /// </summary>
    public sealed record TriangleSolution(string? Case, IReadOnlyList<Triangle> Triangles, IReadOnlyList<string> Steps, string? Error);

    /// <summary>The solver of triangles: three of the sides and angles, at least one of them a side, give the others.</summary>
    public static class Triangles
    {
        private const double Epsilon = 1e-9;

        /// <summary>The names of the sides, as UnicodeMath.</summary>
        public static readonly IReadOnlyList<string> SideNames = new[] { "a", "b", "c" };

        /// <summary>The names of the angles, each opposite the side of its index, as UnicodeMath.</summary>
        public static readonly IReadOnlyList<string> AngleNames = new[] { "α", "β", "γ" };

        public static double Radians(double degrees) => degrees * Math.PI / 180;

        public static double Degrees(double radians) => radians * 180 / Math.PI;

        /// <summary>Solves the triangle of the values given (sides, and angles in degrees; null for the unknown ones).</summary>
        public static TriangleSolution Solve(double? a, double? b, double? c, double? alpha, double? beta, double? gamma)
        {
            var sides = new[] { a, b, c };
            var angles = new[] { alpha, beta, gamma };
            var known = sides.Concat(angles).Count(v => v.HasValue);
            if (sides.Any(s => s is <= 0) || angles.Any(t => t is <= 0))
                return Fail("Sides and angles are greater than zero.");
            if (angles.Any(t => t >= 180))
                return Fail("An angle of a triangle is less than 180°.");
            if (known != 3)
                return Fail(known < 3 ? "Give three values: " + (3 - known) + " more." : "Give three values, not " + known + ": clear the others.");

            switch (sides.Count(s => s.HasValue))
            {
                case 0:
                    return Fail("Three angles give the shape of a triangle but not its size: give a side.");
                case 3:
                    return SideSideSide(sides.Select(s => s!.Value).ToArray());
                case 2:
                    var unknown = Array.FindIndex(sides, s => !s.HasValue);
                    return angles[unknown].HasValue
                        ? SideAngleSide(sides, angles[unknown]!.Value, unknown)
                        : SideSideAngle(sides, angles);
                default:
                    return AngleSide(sides, angles);
            }
        }

        private static TriangleSolution SideSideSide(double[] s)
        {
            for (var i = 0; i < 3; i++)
            {
                if (s[i] >= s[(i + 1) % 3] + s[(i + 2) % 3] - Epsilon * s[i])
                    return Fail("No triangle: " + SideNames[i] + " is not shorter than the other two sides together.", "SSS");
            }
            var steps = new List<string>();
            for (var i = 0; i < 2; i++)
                steps.Add(LawOfCosinesForAngle(i));
            steps.Add(AngleSum(2));
            return One("SSS", FromSides(s), steps);
        }

        private static TriangleSolution SideAngleSide(double?[] sides, double angle, int k)
        {
            int i = (k + 1) % 3, j = (k + 2) % 3;
            var s = new double[3];
            s[i] = sides[i]!.Value;
            s[j] = sides[j]!.Value;
            s[k] = Math.Sqrt(s[i] * s[i] + s[j] * s[j] - 2 * s[i] * s[j] * Math.Cos(Radians(angle)));
            var (x, y) = Ordered(i, j);
            var steps = new List<string>
            {
                SideNames[k] + "=√(" + SideNames[x] + "^2+" + SideNames[y] + "^2-2" + SideNames[x] + SideNames[y] + " cos " + AngleNames[k] + ")",
                LawOfCosinesForAngle(i),
                AngleSum(j),
            };
            return One("SAS", FromSides(s), steps);
        }

        private static TriangleSolution SideSideAngle(double?[] sides, double?[] angles)
        {
            var m = Array.FindIndex(angles, t => t.HasValue);
            var n = sides[(m + 1) % 3].HasValue ? (m + 1) % 3 : (m + 2) % 3;
            var k = 3 - m - n;
            double sm = sides[m]!.Value, sn = sides[n]!.Value, tm = angles[m]!.Value;
            var sine = sn * Math.Sin(Radians(tm)) / sm;
            var steps = new List<string>
            {
                "sin " + AngleNames[n] + "=(" + SideNames[n] + " sin " + AngleNames[m] + ")/" + SideNames[m],
                AngleSum(k),
                LawOfSines(k, m),
            };
            if (sine > 1 + Epsilon)
                return Fail("No triangle: " + SideNames[n] + " sin " + AngleNames[m] + " is greater than " + SideNames[m] + ", so " + SideNames[m] + " cannot reach the other side.", "SSA", steps);

            // a sine of 1 within rounding is the right angle, where the two triangles are one (asin is steep there)
            var right = sine >= 1 - Epsilon;
            var first = right ? 90 : Degrees(Math.Asin(sine));
            var triangles = new List<Triangle>();
            foreach (var tn in right ? new[] { first } : new[] { first, 180 - first })
            {
                var tk = 180 - tm - tn;
                if (tk <= Epsilon)
                    continue;
                var s = new double[3];
                var t = new double[3];
                s[m] = sm;
                s[n] = sn;
                t[m] = tm;
                t[n] = tn;
                t[k] = tk;
                s[k] = sm * Math.Sin(Radians(tk)) / Math.Sin(Radians(tm));
                triangles.Add(new Triangle(s[0], s[1], s[2], t[0], t[1], t[2]));
            }
            if (triangles.Count == 0)
                return Fail("No triangle: the angles " + AngleNames[m] + " and " + AngleNames[n] + " make 180° or more.", "SSA", steps);
            if (triangles.Count == 2)
                steps.Insert(1, AngleNames[n] + "'=180°-" + AngleNames[n]);
            return new TriangleSolution("SSA", triangles, steps, null);
        }

        private static TriangleSolution AngleSide(double?[] sides, double?[] angles)
        {
            var i = Array.FindIndex(sides, s => s.HasValue);
            var k = Array.FindIndex(angles, t => !t.HasValue);
            var t = angles.Select(v => v ?? 0).ToArray();
            t[k] = 180 - t.Sum();
            if (t[k] <= Epsilon)
                return Fail("No triangle: the two angles make 180° or more.", k == i ? "ASA" : "AAS");
            var s = new double[3];
            s[i] = sides[i]!.Value;
            var steps = new List<string> { AngleSum(k) };
            foreach (var j in new[] { (i + 1) % 3, (i + 2) % 3 })
            {
                s[j] = s[i] * Math.Sin(Radians(t[j])) / Math.Sin(Radians(t[i]));
                steps.Add(LawOfSines(j, i));
            }
            return One(k == i ? "ASA" : "AAS", new Triangle(s[0], s[1], s[2], t[0], t[1], t[2]), steps);
        }

        private static Triangle FromSides(double[] s)
        {
            var t = new double[3];
            for (var i = 0; i < 2; i++)
            {
                double x = s[(i + 1) % 3], y = s[(i + 2) % 3];
                t[i] = Degrees(Math.Acos(Math.Max(-1, Math.Min(1, (x * x + y * y - s[i] * s[i]) / (2 * x * y)))));
            }
            t[2] = 180 - t[0] - t[1];
            return new Triangle(s[0], s[1], s[2], t[0], t[1], t[2]);
        }

        // cos α=(b^2+c^2-a^2)/2bc
        private static string LawOfCosinesForAngle(int i)
        {
            var (x, y) = Ordered((i + 1) % 3, (i + 2) % 3);
            return "cos " + AngleNames[i] + "=(" + SideNames[x] + "^2+" + SideNames[y] + "^2-" + SideNames[i] + "^2)/(2" + SideNames[x] + SideNames[y] + ")";
        }

        // b=(a sin β)/(sin α)
        private static string LawOfSines(int j, int i) =>
            SideNames[j] + "=(" + SideNames[i] + " sin " + AngleNames[j] + ")/(sin " + AngleNames[i] + ")";

        // γ=180°-α-β
        private static string AngleSum(int k)
        {
            var (x, y) = Ordered((k + 1) % 3, (k + 2) % 3);
            return AngleNames[k] + "=180°-" + AngleNames[x] + "-" + AngleNames[y];
        }

        private static (int, int) Ordered(int i, int j) => i < j ? (i, j) : (j, i);

        private static TriangleSolution One(string kind, Triangle triangle, IReadOnlyList<string> steps) =>
            new TriangleSolution(kind, new[] { triangle }, steps, null);

        private static TriangleSolution Fail(string error, string? kind = null, IReadOnlyList<string>? steps = null) =>
            new TriangleSolution(kind, Array.Empty<Triangle>(), steps ?? Array.Empty<string>(), error);
    }
}
