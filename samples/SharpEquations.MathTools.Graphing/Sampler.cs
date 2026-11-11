namespace SharpEquations.MathTools.Graphing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The points of a curve, for a surface of a size in pixels: closer where the curve bends, and broken where it has
    /// no value or jumps (an asymptote: tan x at π/2), so that no line joins what is apart. The points are of the
    /// plane, with y kept within a few heights of the viewport.
    /// </summary>
    public static class Sampler
    {
        private const int MaxDepth = 10;

        /// <summary>The curve y = f(x) across the viewport, as polylines.</summary>
        public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Function(Func<double, double> f, Viewport viewport, double width, double height)
        {
            var lines = new Lines(viewport);
            var count = Math.Max(16, (int)(width / 4));
            var tolerance = viewport.Height / height * 0.5;
            var x0 = viewport.XMin;
            var y0 = f(x0);
            for (var i = 1; i <= count; i++)
            {
                var x1 = viewport.XMin + viewport.Width * i / count;
                var y1 = f(x1);
                Refine(f, x0, y0, x1, y1, 0, tolerance, lines);
                x0 = x1;
                y0 = y1;
            }
            return lines.Done();
        }

        /// <summary>The curve (x(t), y(t)) for t from <paramref name="min"/> to <paramref name="max"/>, as polylines.</summary>
        public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Parametric(Func<double, (double X, double Y)> f, double min, double max, Viewport viewport, double width, double height)
        {
            var lines = new Lines(viewport);
            var scale = (X: width / viewport.Width, Y: height / viewport.Height);
            const int Count = 400;
            var t0 = min;
            var p0 = f(t0);
            for (var i = 1; i <= Count; i++)
            {
                var t1 = min + (max - min) * i / Count;
                var p1 = f(t1);
                RefinePath(f, t0, p0, t1, p1, 0, scale, lines);
                t0 = t1;
                p0 = p1;
            }
            return lines.Done();
        }

        private static void Refine(Func<double, double> f, double x0, double y0, double x1, double y1, int depth, double tolerance, Lines lines)
        {
            var finite0 = double.IsFinite(y0);
            var finite1 = double.IsFinite(y1);
            if (!finite0 && !finite1)
            {
                lines.Break();
                return;
            }
            var xm = (x0 + x1) / 2;
            var ym = f(xm);
            var bends = !finite0 || !finite1 || !double.IsFinite(ym) || Math.Abs(ym - (y0 + y1) / 2) > tolerance;
            if (bends && depth < MaxDepth)
            {
                Refine(f, x0, y0, xm, ym, depth + 1, tolerance, lines);
                Refine(f, xm, ym, x1, y1, depth + 1, tolerance, lines);
                return;
            }
            // at the finest step a curve moves less than a pixel: a jump of a few pixels is an asymptote or a step, not a line
            if (finite0 && finite1 && Math.Abs(y1 - y0) > 8 * tolerance)
            {
                lines.Add(x0, y0);
                lines.Break();
                lines.Add(x1, y1);
                return;
            }
            if (!finite0 || !finite1)
            {
                lines.Break();
                if (finite1)
                    lines.Add(x1, y1);
                return;
            }
            lines.Add(x0, y0);
            lines.Add(x1, y1);
        }

        private static void RefinePath(Func<double, (double X, double Y)> f, double t0, (double X, double Y) p0, double t1, (double X, double Y) p1, int depth, (double X, double Y) scale, Lines lines)
        {
            var finite0 = double.IsFinite(p0.X) && double.IsFinite(p0.Y);
            var finite1 = double.IsFinite(p1.X) && double.IsFinite(p1.Y);
            if (!finite0 || !finite1)
            {
                lines.Break();
                if (finite1)
                    lines.Add(p1.X, p1.Y);
                return;
            }
            var distance = Math.Max(Math.Abs(p1.X - p0.X) * scale.X, Math.Abs(p1.Y - p0.Y) * scale.Y);
            if (distance > 2 && depth < MaxDepth)
            {
                var tm = (t0 + t1) / 2;
                var pm = f(tm);
                RefinePath(f, t0, p0, tm, pm, depth + 1, scale, lines);
                RefinePath(f, tm, pm, t1, p1, depth + 1, scale, lines);
                return;
            }
            if (distance > 200)
            {
                lines.Break();
                lines.Add(p1.X, p1.Y);
                return;
            }
            lines.Add(p0.X, p0.Y);
            lines.Add(p1.X, p1.Y);
        }

        // polylines built point by point, without repeating the point where one ends and the next starts
        private sealed class Lines
        {
            private readonly List<IReadOnlyList<(double X, double Y)>> done = new List<IReadOnlyList<(double X, double Y)>>();
            private readonly double low;
            private readonly double high;
            private List<(double X, double Y)> current = new List<(double X, double Y)>();

            public Lines(Viewport viewport)
            {
                low = viewport.YMin - 4 * viewport.Height;
                high = viewport.YMax + 4 * viewport.Height;
            }

            public void Add(double x, double y)
            {
                y = Math.Clamp(y, low, high);
                if (current.Count > 0 && current[^1] == (x, y))
                    return;
                current.Add((x, y));
            }

            public void Break()
            {
                if (current.Count > 1)
                    done.Add(current);
                current = new List<(double X, double Y)>();
            }

            public List<IReadOnlyList<(double X, double Y)>> Done()
            {
                Break();
                return done;
            }
        }
    }
}
