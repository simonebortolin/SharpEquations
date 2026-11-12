namespace SharpEquations.MathTools.Graphing
{
    using System;
    using System.Numerics;

    /// <summary>
    /// A camera that turns around the cube −1..1, z up: <see cref="Yaw"/> around the vertical, <see cref="Pitch"/> above the
    /// horizon, at <see cref="Distance"/> from the center. Its matrices take row vectors, as <see cref="Vector4.Transform(Vector4, Matrix4x4)"/>
    /// does, so that OpenGL reads them as the column matrices it multiplies on the left.
    /// </summary>
    public sealed record OrbitCamera(float Yaw, float Pitch, float Distance)
    {
        private const float Limit = 89 * MathF.PI / 180;

        /// <summary>The camera of a new graph: from the front right, a little above.</summary>
        public static OrbitCamera Default { get; } = new(-MathF.PI / 3, MathF.PI / 6, 6.5f);

        /// <summary>The angle of view, top to bottom.</summary>
        public float FieldOfView { get; init; } = MathF.PI / 5;

        /// <summary>Where the camera is.</summary>
        public Vector3 Eye => Distance * new Vector3(MathF.Cos(Pitch) * MathF.Cos(Yaw), MathF.Cos(Pitch) * MathF.Sin(Yaw), MathF.Sin(Pitch));

        /// <summary>The camera turned: <paramref name="yaw"/> more around the vertical, <paramref name="pitch"/> more up, never past the poles.</summary>
        public OrbitCamera Rotate(float yaw, float pitch) => this with
        {
            Yaw = (Yaw + yaw) % (2 * MathF.PI),
            Pitch = Math.Clamp(Pitch + pitch, -Limit, Limit),
        };

        public Matrix4x4 View => Matrix4x4.CreateLookAt(Eye, Vector3.Zero, Vector3.UnitZ);

        public Matrix4x4 Projection(float aspect) => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspect > 0 ? aspect : 1, 0.1f, 100);

        public Matrix4x4 ViewProjection(float aspect) => View * Projection(aspect);

        /// <summary>
        /// A point of the cube on a surface of <paramref name="width"/> × <paramref name="height"/> pixels, y down, with its
        /// depth (larger is farther); false if the point is behind the camera.
        /// </summary>
        public bool ToScreen(Vector3 point, float width, float height, out Vector2 screen, out float depth) =>
            ToScreen(point, ViewProjection(width / height), width, height, out screen, out depth);

        internal static bool ToScreen(Vector3 point, Matrix4x4 viewProjection, float width, float height, out Vector2 screen, out float depth)
        {
            var clip = Vector4.Transform(new Vector4(point, 1), viewProjection);
            if (clip.W <= 1e-6f)
            {
                screen = default;
                depth = float.PositiveInfinity;
                return false;
            }
            screen = new Vector2((clip.X / clip.W + 1) / 2 * width, (1 - clip.Y / clip.W) / 2 * height);
            depth = clip.W;
            return true;
        }
    }
}
