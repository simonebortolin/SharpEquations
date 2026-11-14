namespace SharpEquations.MathTools.Graphing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// The box of space a graph shows, from (<see cref="XMin"/>, <see cref="YMin"/>, <see cref="ZMin"/>) to
    /// (<see cref="XMax"/>, <see cref="YMax"/>, <see cref="ZMax"/>), and how its points map to the cube −1..1 the camera looks at.
    /// </summary>
    public sealed record Box3(double XMin, double XMax, double YMin, double YMax, double ZMin, double ZMax)
    {
        /// <summary>A point of the box in the cube −1..1.</summary>
        public Vector3 ToCube(double x, double y, double z) => new(
            (float)(2 * (x - XMin) / (XMax - XMin) - 1),
            (float)(2 * (y - YMin) / (YMax - YMin) - 1),
            (float)(2 * (z - ZMin) / (ZMax - ZMin) - 1));

        /// <summary>A point of the cube −1..1 in the box.</summary>
        public (double X, double Y, double Z) FromCube(Vector3 p) => (
            XMin + (p.X + 1) / 2 * (XMax - XMin),
            YMin + (p.Y + 1) / 2 * (YMax - YMin),
            ZMin + (p.Z + 1) / 2 * (ZMax - ZMin));
    }

    /// <summary>A surface of a scene, with its points in the cube −1..1: two triangles a cell of a grid, colored by height.</summary>
    public sealed class Mesh3D
    {
        internal Mesh3D(Vector3[] positions, Vector3[] normals, Vector4[] colors, uint[] indices)
        {
            Positions = positions;
            Normals = normals;
            Colors = colors;
            Indices = indices;
        }

        public IReadOnlyList<Vector3> Positions { get; }

        public IReadOnlyList<Vector3> Normals { get; }

        /// <summary>The color of each point, red, green, blue and alpha from 0 to 1.</summary>
        public IReadOnlyList<Vector4> Colors { get; }

        /// <summary>Three indices of <see cref="Positions"/> a triangle; no triangle has a point where the function has no value.</summary>
        public IReadOnlyList<uint> Indices { get; }
    }

    /// <summary>A curve of a scene, with its points in the cube −1..1, in pieces where the curve has no value.</summary>
    public sealed record Curve3D(IReadOnlyList<IReadOnlyList<Vector3>> Pieces, Vector4 Color);

    /// <summary>What a graph in space draws: its box, its surfaces and its curves, sampled once and placed in the cube −1..1.</summary>
    public sealed class Scene3D
    {
        private Scene3D(Box3 box, IReadOnlyList<Mesh3D> surfaces, IReadOnlyList<Curve3D> curves)
        {
            Box = box;
            Surfaces = surfaces;
            Curves = curves;
        }

        /// <summary>A scene with nothing in it, the box −10..10 on all three axes.</summary>
        public static Scene3D Empty { get; } = new(new Box3(-10, 10, -10, 10, -10, 10), [], []);

        public Box3 Box { get; }

        public IReadOnlyList<Mesh3D> Surfaces { get; }

        public IReadOnlyList<Curve3D> Curves { get; }

        /// <summary>
        /// The scene of <paramref name="surfaces"/> z=f(x,y) over <paramref name="domain"/>, on a grid of
        /// <paramref name="grid"/> × <paramref name="grid"/> cells, and of <paramref name="curves"/> of t, with
        /// <paramref name="steps"/> points each. The heights shown go from the 2nd to the 98th percentile of the values, a
        /// tenth more on both sides, so that a pole does not flatten the rest; higher and lower points are clamped.
        /// </summary>
        public static Scene3D Build(
            IEnumerable<(Func<double, double, double> F, Vector4 Color)> surfaces,
            IEnumerable<(Func<double, (double X, double Y, double Z)> F, double Min, double Max, Vector4 Color)> curves,
            Viewport domain,
            int grid = 60,
            int steps = 600)
        {
            if (!domain.IsValid)
                throw new ArgumentException("The domain of a graph in space has a size on both axes.", nameof(domain));
            if (grid < 1 || steps < 2)
                throw new ArgumentOutOfRangeException(nameof(grid), "A graph in space samples at least one cell, and two points of a curve.");
            var grids = surfaces.Select(s => (Values: Sample(s.F, domain, grid), s.Color)).ToList();
            var paths = curves.Select(c => (Points: Sample(c.F, c.Min, c.Max, steps), c.Color)).ToList();

            // the box: the domain across, and in height what the surfaces reach (or the curves, without surfaces)
            var heights = grids.SelectMany(g => g.Values).Where(double.IsFinite).ToList();
            double zMin, zMax;
            if (heights.Count > 0)
            {
                (zMin, zMax) = Spread(heights);
            }
            else
            {
                (zMin, zMax) = (domain.YMin, domain.YMax);
                var points = paths.SelectMany(p => p.Points).Where(Finite).ToList();
                if (points.Count > 0)
                {
                    (zMin, zMax) = Spread(points.Select(p => p.Z).ToList());
                    domain = new Viewport(
                        Math.Min(domain.XMin, points.Min(p => p.X)), Math.Max(domain.XMax, points.Max(p => p.X)),
                        Math.Min(domain.YMin, points.Min(p => p.Y)), Math.Max(domain.YMax, points.Max(p => p.Y)));
                }
            }
            var box = new Box3(domain.XMin, domain.XMax, domain.YMin, domain.YMax, zMin, zMax);
            return new Scene3D(
                box,
                grids.Select(g => Mesh(g.Values, grid, box, g.Color)).ToList(),
                paths.Select(p => new Curve3D(Pieces(p.Points, box), p.Color)).ToList());
        }

        /// <summary>The 12 edges of the box, two points of the cube −1..1 each.</summary>
        public static IEnumerable<(Vector3 A, Vector3 B)> Edges()
        {
            for (var axis = 0; axis < 3; axis++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var u = (i & 1) == 0 ? -1f : 1f;
                    var v = (i & 2) == 0 ? -1f : 1f;
                    yield return axis switch
                    {
                        0 => (new Vector3(-1, u, v), new Vector3(1, u, v)),
                        1 => (new Vector3(u, -1, v), new Vector3(u, 1, v)),
                        _ => (new Vector3(u, v, -1), new Vector3(u, v, 1)),
                    };
                }
            }
        }

        private static double[] Sample(Func<double, double, double> f, Viewport domain, int grid)
        {
            var values = new double[(grid + 1) * (grid + 1)];
            for (var j = 0; j <= grid; j++)
            {
                var y = domain.YMin + domain.Height * j / grid;
                for (var i = 0; i <= grid; i++)
                    values[j * (grid + 1) + i] = Safe(() => f(domain.XMin + domain.Width * i / grid, y));
            }
            return values;
        }

        private static (double X, double Y, double Z)[] Sample(Func<double, (double X, double Y, double Z)> f, double min, double max, int steps)
        {
            var points = new (double, double, double)[steps];
            for (var k = 0; k < steps; k++)
            {
                var t = min + (max - min) * k / (steps - 1);
                points[k] = SafePoint(() => f(t));
            }
            return points;
        }

        private static (double Min, double Max) Spread(List<double> values)
        {
            values.Sort();
            var low = values[(int)Math.Floor(0.02 * (values.Count - 1))];
            var high = values[(int)Math.Ceiling(0.98 * (values.Count - 1))];
            if (high - low < 1e-9 * Math.Max(1, Math.Abs(low)))
                return (low - 1, high + 1);
            var margin = (high - low) / 10;
            return (low - margin, high + margin);
        }

        private static Mesh3D Mesh(double[] values, int grid, Box3 box, Vector4 color)
        {
            var side = grid + 1;
            var positions = new Vector3[values.Length];
            var colors = new Vector4[values.Length];
            for (var j = 0; j < side; j++)
            {
                for (var i = 0; i < side; i++)
                {
                    var k = j * side + i;
                    var z = Math.Clamp(values[k], box.ZMin, box.ZMax);
                    positions[k] = new Vector3(2f * i / grid - 1, 2f * j / grid - 1, double.IsFinite(z) ? box.ToCube(0, 0, z).Z : 0);
                    colors[k] = Shade(color, (positions[k].Z + 1) / 2);
                }
            }

            var indices = new List<uint>(grid * grid * 6);
            var normals = new Vector3[values.Length];
            for (var j = 0; j < grid; j++)
            {
                for (var i = 0; i < grid; i++)
                {
                    var a = (uint)(j * side + i);
                    var b = a + 1;
                    var c = a + (uint)side;
                    var d = c + 1;
                    Triangle(a, b, d);
                    Triangle(a, d, c);
                }
            }
            for (var k = 0; k < normals.Length; k++)
                normals[k] = normals[k] == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(normals[k]);
            return new Mesh3D(positions, normals, colors, indices.ToArray());

            void Triangle(uint p, uint q, uint r)
            {
                if (!double.IsFinite(values[p]) || !double.IsFinite(values[q]) || !double.IsFinite(values[r]))
                    return;
                indices.Add(p);
                indices.Add(q);
                indices.Add(r);
                var n = Vector3.Cross(positions[q] - positions[p], positions[r] - positions[p]);
                normals[p] += n;
                normals[q] += n;
                normals[r] += n;
            }
        }

        private static List<IReadOnlyList<Vector3>> Pieces((double X, double Y, double Z)[] points, Box3 box)
        {
            var pieces = new List<IReadOnlyList<Vector3>>();
            var piece = new List<Vector3>();
            foreach (var p in points)
            {
                if (Finite(p))
                {
                    piece.Add(Vector3.Clamp(box.ToCube(p.X, p.Y, p.Z), -Vector3.One, Vector3.One));
                    continue;
                }
                if (piece.Count > 1)
                    pieces.Add(piece);
                piece = [];
            }
            if (piece.Count > 1)
                pieces.Add(piece);
            return pieces;
        }

        /// <summary>The color of a surface at a height from 0 (bottom of the box) to 1 (top): darker below, lighter above.</summary>
        private static Vector4 Shade(Vector4 color, float height)
        {
            var rgb = new Vector3(color.X, color.Y, color.Z);
            var shaded = height < 0.5f
                ? Vector3.Lerp(rgb * 0.45f, rgb, height * 2)
                : Vector3.Lerp(rgb, Vector3.Lerp(rgb, Vector3.One, 0.55f), (height - 0.5f) * 2);
            return new Vector4(shaded, color.W);
        }

        private static bool Finite((double X, double Y, double Z) p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);

#pragma warning disable CA1031 // a function without a value at a point (a domain error, an overflow) is a hole in the graph
        private static double Safe(Func<double> f)
        {
            try
            {
                return f();
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private static (double X, double Y, double Z) SafePoint(Func<(double, double, double)> f)
        {
            try
            {
                return f();
            }
            catch (Exception)
            {
                return (double.NaN, double.NaN, double.NaN);
            }
        }
#pragma warning restore CA1031
    }
}
