namespace SharpEquations.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpEquations.Editing;

    /// <summary>A rectangle, with y down from the top of the equation.</summary>
    public readonly struct MathRect
    {
        /// <summary>A rectangle.</summary>
        public MathRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>The left.</summary>
        public double X { get; }

        /// <summary>The top.</summary>
        public double Y { get; }

        /// <summary>The width.</summary>
        public double Width { get; }

        /// <summary>The height.</summary>
        public double Height { get; }

        /// <summary>The right.</summary>
        public double Right => X + Width;

        /// <summary>The bottom.</summary>
        public double Bottom => Y + Height;

        /// <inheritdoc/>
        public override string ToString() => $"{X:0.##},{Y:0.##} {Width:0.##}×{Height:0.##}";
    }

    /// <summary>A glyph to draw: its font, its origin on the baseline (y down from the top) and its size.</summary>
    public readonly struct MathGlyph
    {
        internal MathGlyph(IMathFont font, ushort glyph, double x, double y, double size, string? color)
        {
            Font = font;
            Glyph = glyph;
            X = x;
            Y = y;
            Size = size;
            Color = color;
        }

        /// <summary>The font of the glyph.</summary>
        public IMathFont Font { get; }

        /// <summary>The glyph.</summary>
        public ushort Glyph { get; }

        /// <summary>The x of the origin of the glyph.</summary>
        public double X { get; }

        /// <summary>The y of the baseline of the glyph.</summary>
        public double Y { get; }

        /// <summary>The size of the em, in the units of the layout.</summary>
        public double Size { get; }

        /// <summary>The colour of <c>mathcolor</c>, or null for the foreground.</summary>
        public string? Color { get; }
    }

    /// <summary>The kind of a <see cref="MathShape"/>.</summary>
    public enum MathShapeKind
    {
        /// <summary>A filled rectangle: a fraction bar, the bar of a radical, an underbar.</summary>
        Fill,

        /// <summary>The outline of a rectangle.</summary>
        Outline,

        /// <summary>A line from (X, Y) to (X2, Y2).</summary>
        Line,

        /// <summary>The outline of the ellipse in the rectangle.</summary>
        Ellipse,
    }

    /// <summary>What a <see cref="MathShape"/> is, so that a toolkit draws it with the right brush.</summary>
    public enum MathShapeRole
    {
        /// <summary>Part of the equation, drawn with the foreground.</summary>
        Ink,

        /// <summary>The background of <c>mathbackground</c>, with its <see cref="MathShape.Color"/>.</summary>
        Background,

        /// <summary>The box of an empty slot in an editor.</summary>
        Placeholder,

        /// <summary>The background of <c>merror</c>.</summary>
        Error,
    }

    /// <summary>A shape to draw.</summary>
    public readonly struct MathShape
    {
        internal MathShape(MathShapeKind kind, MathShapeRole role, double x, double y, double width, double height, double x2, double y2, double thickness, string? color)
        {
            Kind = kind;
            Role = role;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            X2 = x2;
            Y2 = y2;
            Thickness = thickness;
            Color = color;
        }

        /// <summary>The kind of the shape.</summary>
        public MathShapeKind Kind { get; }

        /// <summary>What the shape is.</summary>
        public MathShapeRole Role { get; }

        /// <summary>The left of the rectangle, or the x of the start of the line.</summary>
        public double X { get; }

        /// <summary>The top of the rectangle, or the y of the start of the line.</summary>
        public double Y { get; }

        /// <summary>The width of the rectangle.</summary>
        public double Width { get; }

        /// <summary>The height of the rectangle.</summary>
        public double Height { get; }

        /// <summary>The x of the end of the line.</summary>
        public double X2 { get; }

        /// <summary>The y of the end of the line.</summary>
        public double Y2 { get; }

        /// <summary>The thickness of an outline or a line.</summary>
        public double Thickness { get; }

        /// <summary>The colour of <c>mathcolor</c> or <c>mathbackground</c>, or null.</summary>
        public string? Color { get; }
    }

    /// <summary>
    /// A laid out equation: what to draw, its size, and the geometry of the caret and the selection for an editor.
    /// </summary>
    public sealed class MathLayout : IMathGeometry
    {
        private readonly List<RowGeometry> rows;
        private readonly Dictionary<string, RowGeometry> byPath;

        internal MathLayout(double width, double height, double baseline, IReadOnlyList<MathGlyph> glyphs, IReadOnlyList<MathShape> shapes, List<RowGeometry> rows)
        {
            Width = width;
            Height = height;
            Baseline = baseline;
            Glyphs = glyphs;
            Shapes = shapes;
            this.rows = rows;
            byPath = new Dictionary<string, RowGeometry>(StringComparer.Ordinal);
            foreach (var row in rows)
                byPath[Key(row.Path)] = row;
        }

        /// <summary>The width of the equation.</summary>
        public double Width { get; }

        /// <summary>The height of the equation, from the top of its ink to the bottom.</summary>
        public double Height { get; }

        /// <summary>The y of the baseline.</summary>
        public double Baseline { get; }

        /// <summary>The glyphs to draw.</summary>
        public IReadOnlyList<MathGlyph> Glyphs { get; }

        /// <summary>The shapes to draw, after the backgrounds and before the glyphs.</summary>
        public IReadOnlyList<MathShape> Shapes { get; }

        /// <summary>The rectangle of the caret at a position, or null if the position is not laid out.</summary>
        public MathRect? GetCaretRect(MathPosition position)
        {
            if (!byPath.TryGetValue(Key(position.Path), out var row) || position.Offset >= row.CaretX.Length)
                return null;
            return new MathRect(row.CaretX[position.Offset], row.Top, 0, row.Bottom - row.Top);
        }

        /// <summary>The rectangles to highlight for a selection.</summary>
        public IReadOnlyList<MathRect> GetSelectionRects(MathSelection selection)
        {
            if (!byPath.TryGetValue(Key(selection.Path), out var row) || selection.End >= row.CaretX.Length)
                return Array.Empty<MathRect>();
            var x0 = row.CaretX[selection.Start];
            var x1 = row.CaretX[selection.End];
            if (!row.IsRun)
            {
                // the extent of the children selected, which may stand out of the row's own box
                var top = row.Top;
                var bottom = row.Bottom;
                foreach (var other in rows)
                {
                    if (other.Path.Length > row.Path.Length && other.Path.Take(row.Path.Length).SequenceEqual(row.Path)
                        && other.Path[row.Path.Length] >= selection.Start && other.Path[row.Path.Length] < selection.End)
                    {
                        top = Math.Min(top, other.Top);
                        bottom = Math.Max(bottom, other.Bottom);
                    }
                }
                return new[] { new MathRect(Math.Min(x0, x1), top, Math.Abs(x1 - x0), bottom - top) };
            }
            return new[] { new MathRect(Math.Min(x0, x1), row.Top, Math.Abs(x1 - x0), row.Bottom - row.Top) };
        }

        /// <summary>The position of the caret nearest to a point.</summary>
        public MathPosition? HitTest(double x, double y)
        {
            RowGeometry? best = null;
            var bestDistance = double.MaxValue;
            foreach (var row in rows)
            {
                var dx = x < row.Left ? row.Left - x : x > row.Right ? x - row.Right : 0;
                var dy = y < row.Top ? row.Top - y : y > row.Bottom ? y - row.Bottom : 0;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (best == null || distance < bestDistance - 0.01
                    || Math.Abs(distance - bestDistance) <= 0.01 && (row.Path.Length > best.Path.Length || row.Path.Length == best.Path.Length && row.Area < best.Area))
                {
                    best = row;
                    bestDistance = distance;
                }
            }
            if (best == null)
                return null;
            return best.IsRun ? new MathPosition(best.Path, Nearest(best.CaretX, x)) : GetNearestPosition(best.Path, x);
        }

        /// <inheritdoc/>
        public double? GetCaretX(MathPosition position) =>
            byPath.TryGetValue(Key(position.Path), out var row) && position.Offset < row.CaretX.Length ? row.CaretX[position.Offset] : null;

        /// <inheritdoc/>
        public MathPosition? GetNearestPosition(IReadOnlyList<int> rowPath, double x)
        {
            if (!byPath.TryGetValue(Key(rowPath), out var row))
                return null;
            foreach (var run in rows)
            {
                if (run.IsRun && run.Path.Length == rowPath.Count + 1 && run.Path.Take(rowPath.Count).SequenceEqual(rowPath)
                    && x > run.Left && x < run.Right)
                {
                    return new MathPosition(run.Path, Nearest(run.CaretX, x));
                }
            }
            return new MathPosition(rowPath, Nearest(row.CaretX, x));
        }

        private static int Nearest(double[] xs, double x)
        {
            var best = 0;
            for (var i = 1; i < xs.Length; i++)
            {
                if (Math.Abs(xs[i] - x) < Math.Abs(xs[best] - x))
                    best = i;
            }
            return best;
        }

        private static string Key(IEnumerable<int> path) => string.Join("/", path);
    }

    /// <summary>The geometry of a row or a run, in the coordinates of the layout.</summary>
    internal sealed class RowGeometry
    {
        public RowGeometry(int[] path, bool isRun, double[] caretX, double top, double bottom, double left, double right)
        {
            Path = path;
            IsRun = isRun;
            CaretX = caretX;
            Top = top;
            Bottom = bottom;
            Left = left;
            Right = right;
        }

        public int[] Path { get; }

        public bool IsRun { get; }

        public double[] CaretX { get; }

        public double Top { get; }

        public double Bottom { get; }

        public double Left { get; }

        public double Right { get; }

        public double Area => (Right - Left) * (Bottom - Top);
    }
}
