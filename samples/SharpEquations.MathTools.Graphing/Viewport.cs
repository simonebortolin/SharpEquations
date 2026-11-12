namespace SharpEquations.MathTools.Graphing
{
    using System;

    /// <summary>
    /// The region of the plane a graph shows, from (<see cref="XMin"/>, <see cref="YMin"/>) to
    /// (<see cref="XMax"/>, <see cref="YMax"/>), and how it moves: panned by a fraction of its size, zoomed around a point.
    /// </summary>
    public sealed record Viewport(double XMin, double XMax, double YMin, double YMax)
    {
        /// <summary>The region of a new graph: −10 to 10 on both axes.</summary>
        public static Viewport Default { get; } = new Viewport(-10, 10, -10, 10);

        public double Width => XMax - XMin;

        public double Height => YMax - YMin;

        /// <summary>Whether the region has a size on both axes, and finite bounds.</summary>
        public bool IsValid => Width > 0 && Height > 0 && double.IsFinite(Width) && double.IsFinite(Height);

        /// <summary>The region moved by a fraction of its width and height (positive: right and up).</summary>
        public Viewport Pan(double dx, double dy) => this with
        {
            XMin = XMin + dx * Width,
            XMax = XMax + dx * Width,
            YMin = YMin + dy * Height,
            YMax = YMax + dy * Height,
        };

        /// <summary>The region scaled by <paramref name="factor"/> around (<paramref name="x"/>, <paramref name="y"/>), which stays where it is: below 1 zooms in.</summary>
        public Viewport Zoom(double factor, double x, double y)
        {
            if (!(factor > 0) || double.IsInfinity(factor))
                throw new ArgumentOutOfRangeException(nameof(factor), factor, "The factor of a zoom is a positive number.");
            return new Viewport(x + (XMin - x) * factor, x + (XMax - x) * factor, y + (YMin - y) * factor, y + (YMax - y) * factor);
        }

        /// <summary>The region scaled around its center.</summary>
        public Viewport Zoom(double factor) => Zoom(factor, (XMin + XMax) / 2, (YMin + YMax) / 2);

        /// <summary>A point of the plane on a surface of <paramref name="width"/> × <paramref name="height"/> pixels, y down.</summary>
        public (double X, double Y) ToScreen(double x, double y, double width, double height) =>
            ((x - XMin) / Width * width, (YMax - y) / Height * height);

        /// <summary>The point of the plane at a pixel of a surface of <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
        public (double X, double Y) FromScreen(double px, double py, double width, double height) =>
            (XMin + px / width * Width, YMax - py / height * Height);
    }
}
