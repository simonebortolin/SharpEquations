namespace SharpEquations.MathTools.Tests
{
    using System.Threading.Tasks;
    using SharpEquations.MathTools.Graphing;

    /// <summary>The region of the plane a graph shows, and the marks of its axes.</summary>
    public class GraphingTests
    {
        [Test]
        [Arguments(-10, 10, 8, 2)]
        [Arguments(0, 1, 8, 0.1)]
        [Arguments(0, 1, 4, 0.2)]
        [Arguments(-3, 3, 8, 1)]
        [Arguments(0, 1000, 5, 200)]
        [Arguments(0, 0.003, 6, 0.0005)]
        public async Task StepIsRound(double min, double max, int count, double step) =>
            await Assert.That(Ticks.Step(min, max, count)).IsEqualTo(step).Within(1e-12);

        [Test]
        public async Task ValuesHaveZero()
        {
            var values = Ticks.Values(-1, 1, 10);
            await Assert.That(values.Count).IsEqualTo(11);
            await Assert.That(values[5]).IsEqualTo(0);
            await Assert.That(values[0]).IsEqualTo(-1).Within(1e-12);
        }

        [Test]
        public async Task ZoomKeepsThePoint()
        {
            var zoomed = Viewport.Default.Zoom(0.5, 4, -2);
            await Assert.That(zoomed).IsEqualTo(new Viewport(-3, 7, -6, 4));
            var (x, y) = zoomed.FromScreen(350, 200, 500, 500);
            await Assert.That(x).IsEqualTo(4);
            await Assert.That(y).IsEqualTo(0);
        }

        [Test]
        public async Task ScreenRoundTrips()
        {
            var viewport = new Viewport(-2, 6, -1, 3).Pan(0.25, -0.5);
            await Assert.That(viewport).IsEqualTo(new Viewport(0, 8, -3, 1));
            var (px, py) = viewport.ToScreen(2, 0, 800, 400);
            await Assert.That(px).IsEqualTo(200);
            await Assert.That(py).IsEqualTo(100);
        }
    }
}
