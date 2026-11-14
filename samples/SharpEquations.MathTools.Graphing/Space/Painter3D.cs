namespace SharpEquations.MathTools.Graphing
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>What the painter draws: a triangle (three points) or a segment (two), on the screen, in its color.</summary>
    public readonly record struct Shape3D(Vector2 A, Vector2 B, Vector2 C, bool IsTriangle, Vector4 Color, float Depth);

    /// <summary>
    /// Draws a scene without a graphics card: each triangle and segment projected on the screen, lit, and sorted from the
    /// farthest to the nearest, so that drawing them in order hides what is behind (the painter's algorithm).
    /// </summary>
    public static class Painter3D
    {
        /// <summary>The light, from above the camera, in the cube: the direction it comes from.</summary>
        public static Vector3 Light(OrbitCamera camera) => Vector3.Normalize(camera.Eye + 3 * Vector3.UnitZ);

        /// <summary>How much light a side facing <paramref name="normal"/> gets: from 0.35 in the shade to 1; both sides of a surface are lit.</summary>
        public static float Lambert(Vector3 normal, Vector3 light) => 0.35f + 0.65f * MathF.Abs(Vector3.Dot(normal, light));

        /// <summary>The shapes of <paramref name="scene"/>, and of its box in <paramref name="edges"/>, in the order to draw them.</summary>
        public static IReadOnlyList<Shape3D> Paint(Scene3D scene, OrbitCamera camera, float width, float height, Vector4 edges)
        {
            var shapes = new List<Shape3D>();
            if (!(width > 0) || !(height > 0))
                return shapes;
            var matrix = camera.ViewProjection(width / height);
            var light = Light(camera);
            foreach (var mesh in scene.Surfaces)
            {
                var screen = new Vector2[mesh.Positions.Count];
                var depths = new float[mesh.Positions.Count];
                var seen = new bool[mesh.Positions.Count];
                for (var k = 0; k < screen.Length; k++)
                    seen[k] = OrbitCamera.ToScreen(mesh.Positions[k], matrix, width, height, out screen[k], out depths[k]);
                for (var t = 0; t + 2 < mesh.Indices.Count; t += 3)
                {
                    int a = (int)mesh.Indices[t], b = (int)mesh.Indices[t + 1], c = (int)mesh.Indices[t + 2];
                    if (!seen[a] || !seen[b] || !seen[c])
                        continue;
                    var normal = Vector3.Cross(mesh.Positions[b] - mesh.Positions[a], mesh.Positions[c] - mesh.Positions[a]);
                    var lit = normal == Vector3.Zero ? 1 : Lambert(Vector3.Normalize(normal), light);
                    var color = (mesh.Colors[a] + mesh.Colors[b] + mesh.Colors[c]) / 3;
                    shapes.Add(new Shape3D(screen[a], screen[b], screen[c], true, new Vector4(new Vector3(color.X, color.Y, color.Z) * lit, color.W), (depths[a] + depths[b] + depths[c]) / 3));
                }
            }
            foreach (var curve in scene.Curves)
            {
                foreach (var piece in curve.Pieces)
                {
                    for (var k = 1; k < piece.Count; k++)
                        Segment(piece[k - 1], piece[k], curve.Color, -0.01f);
                }
            }
            foreach (var (a, b) in Scene3D.Edges())
                Segment(a, b, edges, 0);
            shapes.Sort((p, q) => q.Depth.CompareTo(p.Depth));
            return shapes;

            // a segment a little nearer than it is, so that a curve on a surface is drawn over it
            void Segment(Vector3 a, Vector3 b, Vector4 color, float bias)
            {
                if (OrbitCamera.ToScreen(a, matrix, width, height, out var p, out var dp) && OrbitCamera.ToScreen(b, matrix, width, height, out var q, out var dq))
                    shapes.Add(new Shape3D(p, q, q, false, color, (dp + dq) / 2 + bias));
            }
        }
    }
}
