namespace SharpEquations.MathTools.Graphing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Where F(x, y) = 0 across the viewport, by marching squares: the segments where F changes sign along the edges
    /// of a grid of cells, with the sign changes of a pole (F large on both sides, 1/x) left out.
    /// </summary>
    public static class Contours
    {
        /// <summary>The segments of the curve F = 0, on a grid of <paramref name="columns"/> × <paramref name="rows"/> cells.</summary>
        public static IReadOnlyList<((double X, double Y) A, (double X, double Y) B)> ZeroSet(Func<double, double, double> f, Viewport viewport, int columns, int rows)
        {
            var dx = viewport.Width / columns;
            var dy = viewport.Height / rows;
            var values = new double[columns + 1, rows + 1];
            for (var i = 0; i <= columns; i++)
            {
                for (var j = 0; j <= rows; j++)
                    values[i, j] = f(viewport.XMin + i * dx, viewport.YMin + j * dy);
            }

            var segments = new List<((double X, double Y), (double X, double Y))>();
            var crossings = new List<(double X, double Y)>(4);
            for (var i = 0; i < columns; i++)
            {
                for (var j = 0; j < rows; j++)
                {
                    var x0 = viewport.XMin + i * dx;
                    var y0 = viewport.YMin + j * dy;
                    // the corners counterclockwise from the bottom left, and their values
                    var corners = new[] { (x0, y0), (x0 + dx, y0), (x0 + dx, y0 + dy), (x0, y0 + dy) };
                    var v = new[] { values[i, j], values[i + 1, j], values[i + 1, j + 1], values[i, j + 1] };
                    if (Array.Exists(v, z => !double.IsFinite(z)))
                        continue;
                    crossings.Clear();
                    for (var k = 0; k < 4; k++)
                    {
                        var a = v[k];
                        var b = v[(k + 1) % 4];
                        if (a == 0 && b == 0 || (a < 0) == (b < 0) && a != 0)
                            continue;
                        var s = a / (a - b);
                        var (ax, ay) = corners[k];
                        var (bx, by) = corners[(k + 1) % 4];
                        var point = (ax + s * (bx - ax), ay + s * (by - ay));
                        // a pole, not a zero: F at the crossing is far from 0
                        var middle = f(point.Item1, point.Item2);
                        if (!double.IsFinite(middle) || Math.Abs(middle) > Math.Max(Math.Abs(a), Math.Abs(b)))
                            continue;
                        crossings.Add(point);
                    }
                    if (crossings.Count >= 2)
                        segments.Add((crossings[0], crossings[1]));
                    if (crossings.Count == 4)
                        segments.Add((crossings[2], crossings[3]));
                }
            }
            return segments;
        }
    }
}
