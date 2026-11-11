#if NET10_0_OR_GREATER
namespace SharpEquations.Avalonia.Tests.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Avalonia.MathTools;

    /// <summary>The cases of the solver of triangles, and the laws it writes as UnicodeMath.</summary>
    public class TrianglesTests
    {
        private const double Tolerance = 1e-4;

        [Test]
        public async Task ThreeSides()
        {
            var solution = Triangles.Solve(3, 4, 5, null, null, null);
            await Assert.That(solution.Case).IsEqualTo("SSS");
            var t = solution.Triangles.Single();
            await Assert.That(t.Alpha).IsEqualTo(36.8699).Within(Tolerance);
            await Assert.That(t.Beta).IsEqualTo(53.1301).Within(Tolerance);
            await Assert.That(t.Gamma).IsEqualTo(90).Within(Tolerance);
            await Assert.That(t.Area).IsEqualTo(6).Within(Tolerance);
            await Assert.That(t.Perimeter).IsEqualTo(12).Within(Tolerance);
            await Assert.That(t.Circumradius).IsEqualTo(2.5).Within(Tolerance);
            await Assert.That(t.Inradius).IsEqualTo(1).Within(Tolerance);
        }

        [Test]
        public async Task TwoSidesAndTheAngleBetween()
        {
            var solution = Triangles.Solve(7, null, 9, null, 60, null);
            await Assert.That(solution.Case).IsEqualTo("SAS");
            var t = solution.Triangles.Single();
            await Assert.That(t.B).IsEqualTo(Math.Sqrt(67)).Within(Tolerance);
            await Assert.That(t.Beta).IsEqualTo(60).Within(Tolerance);
            await Assert.That(t.Alpha + t.Beta + t.Gamma).IsEqualTo(180).Within(Tolerance);
        }

        [Test]
        public async Task TwoAnglesAndASide()
        {
            var asa = Triangles.Solve(null, null, 10, 45, 75, null);
            await Assert.That(asa.Case).IsEqualTo("ASA");
            var t = asa.Triangles.Single();
            await Assert.That(t.Gamma).IsEqualTo(60).Within(Tolerance);
            await Assert.That(t.A).IsEqualTo(8.16497).Within(Tolerance);
            await Assert.That(t.B).IsEqualTo(11.15355).Within(Tolerance);

            var aas = Triangles.Solve(5, null, null, 30, 60, null);
            await Assert.That(aas.Case).IsEqualTo("AAS");
            t = aas.Triangles.Single();
            await Assert.That(t.B).IsEqualTo(5 * Math.Sqrt(3)).Within(Tolerance);
            await Assert.That(t.C).IsEqualTo(10).Within(Tolerance);
        }

        /// <summary>Two sides and an angle opposite one of them: two triangles, one, or none.</summary>
        [Test]
        [Arguments(6, 8, 40, 2)]
        [Arguments(8, 6, 40, 1)]
        [Arguments(4, 8, 30, 1)]
        [Arguments(3, 8, 30, 0)]
        public async Task TheAmbiguousCase(double a, double b, double alpha, int count)
        {
            var solution = Triangles.Solve(a, b, null, alpha, null, null);
            await Assert.That(solution.Case).IsEqualTo("SSA");
            await Assert.That(solution.Triangles.Count).IsEqualTo(count);
            await Assert.That(solution.Error is null).IsEqualTo(count > 0);
            foreach (var t in solution.Triangles)
            {
                await Assert.That(t.A).IsEqualTo(a);
                await Assert.That(t.Alpha).IsEqualTo(alpha);
                await Assert.That(t.Alpha + t.Beta + t.Gamma).IsEqualTo(180).Within(Tolerance);
                await Assert.That(t.A / Math.Sin(Triangles.Radians(t.Alpha))).IsEqualTo(t.C / Math.Sin(Triangles.Radians(t.Gamma))).Within(Tolerance);
            }
            if (count == 2)
                await Assert.That(solution.Triangles[0].Beta + solution.Triangles[1].Beta).IsEqualTo(180).Within(Tolerance);
        }

        [Test]
        [Arguments(1d, 2d, 4d, null, null, null)]
        [Arguments(1d, 2d, 3d, null, null, null)]
        [Arguments(null, null, null, 60d, 60d, 60d)]
        [Arguments(1d, null, null, 100d, 80d, null)]
        [Arguments(1d, 2d, null, null, null, null)]
        [Arguments(1d, 2d, 2d, 60d, null, null)]
        [Arguments(1d, 2d, null, 180d, null, null)]
        [Arguments(-1d, 2d, 2d, null, null, null)]
        public async Task NoTriangle(double? a, double? b, double? c, double? alpha, double? beta, double? gamma)
        {
            var solution = Triangles.Solve(a, b, c, alpha, beta, gamma);
            await Assert.That(solution.Triangles).IsEmpty();
            await Assert.That(solution.Error).IsNotNull();
        }

        /// <summary>Every three values of a triangle, at least one a side, give it back (among two, in the ambiguous case).</summary>
        [Test]
        [Arguments(5, 7, 9)]
        [Arguments(3, 4, 5)]
        [Arguments(10, 10, 1)]
        public async Task AnyThreeValuesGiveTheTriangle(double a, double b, double c)
        {
            var triangle = Triangles.Solve(a, b, c, null, null, null).Triangles.Single();
            var all = new[] { triangle.A, triangle.B, triangle.C, triangle.Alpha, triangle.Beta, triangle.Gamma };
            var cases = new HashSet<string>();
            for (var mask = 0; mask < 64; mask++)
            {
                if (System.Numerics.BitOperations.PopCount((uint)mask) != 3 || (mask & 7) == 0)
                    continue;
                var v = Enumerable.Range(0, 6).Select(i => (mask >> i & 1) != 0 ? all[i] : (double?)null).ToArray();
                var solution = Triangles.Solve(v[0], v[1], v[2], v[3], v[4], v[5]);
                cases.Add(solution.Case!);
                var found = solution.Triangles.Any(t => Enumerable.Range(0, 3).All(i =>
                    Math.Abs(t.Side(i) - triangle.Side(i)) < Tolerance && Math.Abs(t.Angle(i) - triangle.Angle(i)) < Tolerance));
                await Assert.That(found).IsTrue().Because("values " + mask);
            }
            await Assert.That(cases).IsEquivalentTo(new[] { "SSS", "SAS", "SSA", "ASA", "AAS" });
        }

        /// <summary>The laws and the values written read as equations, without errors.</summary>
        [Test]
        public async Task StepsAndValuesParse()
        {
            var texts = new List<string>();
            foreach (var solution in new[]
            {
                Triangles.Solve(3, 4, 5, null, null, null),
                Triangles.Solve(7, null, 9, null, 60, null),
                Triangles.Solve(6, 8, null, 40, null, null),
                Triangles.Solve(null, null, 10, 45, 75, null),
                Triangles.Solve(5, null, null, 30, 60, null),
            })
            {
                texts.AddRange(solution.Steps);
                foreach (var t in solution.Triangles)
                {
                    var (sides, angles) = TrianglePage.Describe(t);
                    texts.Add(sides);
                    texts.Add(angles);
                }
            }
            foreach (var text in texts)
            {
                var equation = Equation.Parse(text, EquationFormat.UnicodeMath);
                await Assert.That(equation.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror")).IsFalse().Because(text);
                await Assert.That(Equation.Parse(equation.ToString(EquationFormat.UnicodeMath), EquationFormat.UnicodeMath)).IsEqualTo(equation).Because(text);
            }
            await Assert.That(TrianglePage.Describe(Triangles.Solve(3, 4, 5, null, null, null).Triangles[0])).IsEqualTo(("a=3,  b=4,  c=5", "α=36.8699°,  β=53.1301°,  γ=90°"));
        }
    }
}
#endif
