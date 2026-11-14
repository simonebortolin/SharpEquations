namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.MathTools;
    using SharpEquations.MathTools.Graphing;

    /// <summary>The graphs in space: what equations draw, the scene sampled for them, the camera and the painter.</summary>
    public class SpaceTests
    {
        private static readonly Vector4 Blue = new(0.2f, 0.4f, 0.9f, 1);

        private static SurfacePlot Plot(string unicodeMath) =>
            SurfacePlot.FromEquation(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath));

        [Test]
        [Arguments("z=sin x cos y", SurfaceKind.Surface)]
        [Arguments("x^2-y^2", SurfaceKind.Surface)]
        [Arguments("x y=z", SurfaceKind.Surface)]
        [Arguments("x=cos t, y=sin t, z=t/5", SurfaceKind.Curve)]
        public async Task KindOfTheEquation(string unicodeMath, SurfaceKind kind) =>
            await Assert.That(Plot(unicodeMath).Kind).IsEqualTo(kind);

        [Test]
        [Arguments("x^2+y^2+z^2=1")]
        [Arguments("y=x^2")]
        [Arguments("z=z+1")]
        public async Task NoGraphInSpace(string unicodeMath) =>
            await Assert.That(() => Plot(unicodeMath)).Throws<NotSupportedException>();

        [Test]
        public async Task SurfaceHasValues()
        {
            var plot = Plot("z=x^2-y^2");
            await Assert.That(plot.Z(3, 2)).IsEqualTo(5).Within(1e-12);
            await Assert.That(plot.Parameters.Count).IsEqualTo(0);
        }

        [Test]
        public async Task ParametersTakeTheirValues()
        {
            var plot = Plot("z=a x+b y");
            await Assert.That(plot.Parameters).IsEquivalentTo(new[] { "a", "b" });
            await Assert.That(plot.Z(2, 3)).IsEqualTo(5).Within(1e-12);
            plot["a"] = -1;
            plot["b"] = 4;
            await Assert.That(plot.Z(2, 3)).IsEqualTo(10).Within(1e-12);
            await Assert.That(() => plot["c"] = 1).Throws<ArgumentException>();
        }

        [Test]
        public async Task CurveHasPoints()
        {
            var plot = Plot("x=cos t, y=sin t, z=k t");
            plot["k"] = 2;
            var (x, y, z) = plot.Point(Math.PI / 2);
            await Assert.That(x).IsEqualTo(0).Within(1e-12);
            await Assert.That(y).IsEqualTo(1).Within(1e-12);
            await Assert.That(z).IsEqualTo(Math.PI).Within(1e-12);
        }

        [Test]
        public async Task BoxMapsToTheCube()
        {
            var box = new Box3(-2, 6, 0, 4, -1, 1);
            await Assert.That(box.ToCube(-2, 4, 0)).IsEqualTo(new Vector3(-1, 1, 0));
            var (x, y, z) = box.FromCube(new Vector3(0, -1, 1));
            await Assert.That((x, y, z)).IsEqualTo((2.0, 0.0, 1.0));
        }

        [Test]
        public async Task MeshHasTwoTrianglesACell()
        {
            var scene = Scene3D.Build([((x, y) => x + y, Blue)], [], Viewport.Default, grid: 10);
            var mesh = scene.Surfaces.Single();
            await Assert.That(mesh.Positions.Count).IsEqualTo(121);
            await Assert.That(mesh.Indices.Count).IsEqualTo(10 * 10 * 6);
            await Assert.That(mesh.Positions.All(p => p.X is >= -1 and <= 1 && p.Y is >= -1 and <= 1 && p.Z is >= -1 and <= 1)).IsTrue();
            await Assert.That(scene.Box.ZMin < -19 && scene.Box.ZMax > 19).IsTrue();

            // a plane: the same normal everywhere, of length 1
            var n = mesh.Normals[60];
            await Assert.That(mesh.Normals.All(m => Vector3.Distance(m, n) < 1e-5f)).IsTrue();
            await Assert.That(n.Length()).IsEqualTo(1).Within(1e-5f);
        }

        [Test]
        public async Task HolesHaveNoTriangles()
        {
            // √ has no value where x<0: the left half of the grid has no triangles
            var scene = Scene3D.Build([((x, y) => Math.Sqrt(x), Blue)], [], Viewport.Default, grid: 10);
            var mesh = scene.Surfaces.Single();
            await Assert.That(mesh.Indices.Count).IsEqualTo(5 * 10 * 6);
            await Assert.That(mesh.Indices.All(i => mesh.Positions[(int)i].X >= -1e-6f)).IsTrue();
        }

        [Test]
        public async Task PoleDoesNotFlattenTheSurface()
        {
            var scene = Scene3D.Build([((x, y) => 1 / (x * x + y * y), Blue)], [], Viewport.Default, grid: 40);
            await Assert.That(scene.Box.ZMax < 10).IsTrue();
            await Assert.That(scene.Surfaces.Single().Positions.Max(p => p.Z)).IsEqualTo(1);
        }

        [Test]
        public async Task CurveWithoutSurfaceSetsTheBox()
        {
            var scene = Scene3D.Build([], [(t => (Math.Cos(t), Math.Sin(t), 30 * t), 0, 2 * Math.PI, Blue)], Viewport.Default);
            await Assert.That(scene.Box.ZMax > 180).IsTrue();
            await Assert.That(scene.Curves.Single().Pieces.Single().Count).IsEqualTo(600);
        }

        [Test]
        public async Task CameraLooksAtTheCenter()
        {
            var camera = OrbitCamera.Default;
            await Assert.That(camera.ToScreen(Vector3.Zero, 800, 600, out var center, out var depth)).IsTrue();
            await Assert.That(Vector2.Distance(center, new Vector2(400, 300))).IsLessThan(1e-3f);
            await Assert.That(depth).IsEqualTo(camera.Distance).Within(1e-4f);

            // the top of the cube is above the center on the screen, and stays so at any yaw
            for (var yaw = 0f; yaw < 6; yaw += 0.7f)
            {
                camera.Rotate(yaw, 0).ToScreen(Vector3.UnitZ, 800, 600, out var top, out _);
                await Assert.That(top.Y).IsLessThan(300);
            }
            await Assert.That(camera.Rotate(0, 10).Pitch).IsLessThan(MathF.PI / 2);
        }

        [Test]
        public async Task PainterDrawsFarthestFirst()
        {
            var scene = Scene3D.Build([((x, y) => Math.Sin(x) * Math.Cos(y), Blue)], [], Viewport.Default, grid: 8);
            var shapes = Painter3D.Paint(scene, OrbitCamera.Default, 640, 480, Vector4.One);
            await Assert.That(shapes.Count(s => s.IsTriangle)).IsEqualTo(8 * 8 * 2);
            await Assert.That(shapes.Count(s => !s.IsTriangle)).IsEqualTo(12);
            for (var k = 1; k < shapes.Count; k++)
                await Assert.That(shapes[k].Depth <= shapes[k - 1].Depth).IsTrue();
        }
    }
}
