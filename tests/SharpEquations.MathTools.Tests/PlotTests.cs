namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AngouriMath;
    using SharpEquations;
    using SharpEquations.MathTools;
    using SharpEquations.MathTools.Graphing;

    /// <summary>The graphs of equations: what they draw, the functions compiled for them, and their points.</summary>
    public class PlotTests
    {
        private static GraphPlot Plot(string unicodeMath, AngleUnit angles = AngleUnit.Radians) =>
            GraphPlot.FromEquation(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath), angles);

        [Test]
        [Arguments("x^2-3x+1", 1.7)]
        [Arguments("sin x+cos(2x)", 0.9)]
        [Arguments("e^x/(1+x^2)", -1.3)]
        [Arguments("ln x+log_2 x", 3.1)]
        [Arguments("√(x+4)-∛x", 2.2)]
        [Arguments("|x-2|+⌊x⌋", -0.4)]
        [Arguments("tan^(-1) x+sec x", 0.3)]
        [Arguments("sinh x", 0.8)]
        [Arguments("5!+x", 1)]
        public async Task CompiledAsAngouriMathEvaluates(string unicodeMath, double x)
        {
            var expression = MathReader.Read(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath));
            var compiled = NumericCompiler.Compile(expression, new[] { "x" });
            var expected = ((Entity.Number.Complex)expression.Substitute("x", x).EvalNumerical()).RealPart.EDecimal.ToDouble();
            await Assert.That(compiled(new[] { x })).IsEqualTo(expected).Within(1e-9 * Math.Max(1, Math.Abs(expected)));
        }

        [Test]
        public async Task CubeRootOfANegativeNumberIsReal()
        {
            var compiled = NumericCompiler.Compile(MathReader.Read(Equation.Parse("∛x", EquationFormat.UnicodeMath)), new[] { "x" });
            await Assert.That(compiled(new[] { -8.0 })).IsEqualTo(-2).Within(1e-12);
            var root = NumericCompiler.Compile(MathReader.Read(Equation.Parse("√x", EquationFormat.UnicodeMath)), new[] { "x" });
            await Assert.That(double.IsNaN(root(new[] { -4.0 }))).IsTrue();
        }

        [Test]
        [Arguments("y=x^2", GraphKind.Function)]
        [Arguments("x^3-x", GraphKind.Function)]
        [Arguments("2x+1=y", GraphKind.Function)]
        [Arguments("r=1+cos θ", GraphKind.Polar)]
        [Arguments("x=cos t, y=sin(2t)", GraphKind.Parametric)]
        [Arguments("x^2+y^2=25", GraphKind.Implicit)]
        [Arguments("x=3", GraphKind.Implicit)]
        public async Task KindOfGraph(string unicodeMath, GraphKind kind) =>
            await Assert.That(Plot(unicodeMath).Kind).IsEqualTo(kind);

        [Test]
        public async Task OtherLettersAreParameters()
        {
            var plot = Plot("y=a x^2+b");
            await Assert.That(plot.Parameters).IsEquivalentTo(new[] { "a", "b" });
            await Assert.That(plot.Y(2)).IsEqualTo(5);
            plot["a"] = 3;
            plot["b"] = -1;
            await Assert.That(plot.Y(2)).IsEqualTo(11);
        }

        [Test]
        public async Task DegreesAndDefinitions()
        {
            var degrees = Plot("y=sin x", AngleUnit.Degrees);
            await Assert.That(degrees.Y(30)).IsEqualTo(0.5).Within(1e-12);
            var defined = GraphPlot.FromEquation(Equation.Parse("y=k x", EquationFormat.UnicodeMath), AngleUnit.Radians,
                new System.Collections.Generic.Dictionary<string, Equation> { ["k"] = Equation.Parse("4", EquationFormat.UnicodeMath) });
            await Assert.That(defined.Parameters).IsEmpty();
            await Assert.That(defined.Y(2)).IsEqualTo(8);
        }

        [Test]
        [Arguments("2=3")]
        [Arguments("\"text\"")]
        public async Task NoGraph(string unicodeMath)
        {
            var exception = Assert.Throws<NotSupportedException>(() => Plot(unicodeMath));
            await Assert.That(exception.Message).IsNotEmpty();
        }

        [Test]
        public async Task SmoothCurveIsOneLine()
        {
            var plot = Plot("y=x^2/5");
            var lines = Sampler.Function(plot.Y, Viewport.Default, 800, 600);
            await Assert.That(lines.Count).IsEqualTo(1);
            foreach (var (x, y) in lines[0].Where(p => p.Y < 50))
                await Assert.That(y).IsEqualTo(x * x / 5).Within(1e-9);
        }

        [Test]
        [Arguments("y=10x")]
        [Arguments("y=x^2/2")]
        [Arguments("y=e^x")]
        public async Task SteepCurveIsNotBroken(string unicodeMath)
        {
            // steep, but straight between two samples: a line, not a jump
            var lines = Sampler.Function(Plot(unicodeMath).Y, Viewport.Default, 800, 600);
            await Assert.That(lines.Count).IsEqualTo(1);
            await Assert.That(lines[0][^1].X - lines[0][0].X).IsEqualTo(Viewport.Default.Width).Within(1e-9);
        }

        [Test]
        [Arguments("y=tan x", 7)]
        [Arguments("y=1/x", 2)]
        [Arguments("y=1/(x^2-4)", 3)]
        [Arguments("y=⌊x⌋", 20)]
        public async Task CurveBreaksAtAsymptotesAndSteps(string unicodeMath, int pieces)
        {
            var lines = Sampler.Function(Plot(unicodeMath).Y, Viewport.Default, 800, 600);
            await Assert.That(lines.Count).IsEqualTo(pieces);
        }

        [Test]
        public async Task CurveWithoutValuesIsLeftOut()
        {
            var lines = Sampler.Function(Plot("y=√x").Y, Viewport.Default, 800, 600);
            await Assert.That(lines.Count).IsEqualTo(1);
            await Assert.That(lines[0].Min(p => p.X)).IsGreaterThanOrEqualTo(0);
            await Assert.That(lines[0].Min(p => p.X)).IsLessThan(0.01);
        }

        [Test]
        public async Task PolarAndParametricCurvesAreClosed()
        {
            var cardioid = Plot("r=1+cos θ");
            var lines = Sampler.Parametric(cardioid.Point, cardioid.Range.Min, cardioid.Range.Max, Viewport.Default.Zoom(0.25), 800, 800);
            await Assert.That(lines.Count).IsEqualTo(1);
            var first = lines[0][0];
            var last = lines[0][^1];
            await Assert.That(Math.Abs(first.X - last.X) + Math.Abs(first.Y - last.Y)).IsLessThan(1e-9);
            var circle = Plot("x=3 cos t, y=3 sin t");
            foreach (var (x, y) in Sampler.Parametric(circle.Point, 0, 2 * Math.PI, Viewport.Default, 800, 800).SelectMany(l => l))
                await Assert.That(Math.Sqrt(x * x + y * y)).IsEqualTo(3).Within(1e-9);
        }

        [Test]
        public async Task ImplicitCircle()
        {
            var circle = Plot("x^2+y^2=25");
            var segments = Contours.ZeroSet(circle.F, Viewport.Default, 120, 120);
            await Assert.That(segments.Count).IsGreaterThan(100);
            foreach (var ((ax, ay), (bx, by)) in segments)
            {
                await Assert.That(Math.Sqrt(ax * ax + ay * ay)).IsEqualTo(5).Within(0.05);
                await Assert.That(Math.Sqrt(bx * bx + by * by)).IsEqualTo(5).Within(0.05);
            }
        }

        [Test]
        public async Task PoleIsNoCurve()
        {
            var segments = Contours.ZeroSet(Plot("1/x=y-y").F, Viewport.Default, 80, 80);
            await Assert.That(segments).IsEmpty();
        }
    }
}
