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
            var i = position.Offset;
            return new MathRect(row.CaretX[i], row.CaretTop[i], 0, row.CaretBottom[i] - row.CaretTop[i]);
        }

        /// <summary>The rectangles to highlight for a selection: one for each line it is on.</summary>
        public IReadOnlyList<MathRect> GetSelectionRects(MathSelection selection)
        {
            if (!byPath.TryGetValue(Key(selection.Path), out var row) || selection.End >= row.CaretX.Length)
                return Array.Empty<MathRect>();

            var rects = new List<MathRect>();
            var first = row.CaretLine[selection.Start];
            var last = row.CaretLine[selection.End];
            for (var line = first; line <= last; line++)
            {
                var from = line == first ? selection.Start : Array.IndexOf(row.CaretLine, line);
                var x0 = row.CaretX[from];
                var x1 = line == last ? row.CaretX[selection.End] : row.LineRight[line];
                var top = row.CaretTop[from];
                var bottom = row.CaretBottom[from];
                if (!row.IsRun)
                {
                    // the extent of the children selected on the line, which may stand out of the line of the row
                    var to = line == last ? selection.End : Array.LastIndexOf(row.CaretLine, line) + 1;
                    foreach (var other in rows)
                    {
                        if (other.Path.Length > row.Path.Length && other.Path.Take(row.Path.Length).SequenceEqual(row.Path)
                            && other.Path[row.Path.Length] >= from && other.Path[row.Path.Length] < to)
                        {
                            top = Math.Min(top, other.Top);
                            bottom = Math.Max(bottom, other.Bottom);
                        }
                    }
                }
                if (x1 > x0)
                    rects.Add(new MathRect(x0, top, x1 - x0, bottom - top));
            }
            return rects;
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
            if (best.IsRun)
                return new MathPosition(best.Path, best.Nearest(x, y));
            var index = best.Nearest(x, y);
            return RunAt(best, best.CaretLine[index], x) ?? new MathPosition(best.Path, index);
        }

        /// <inheritdoc/>
        public double? GetCaretX(MathPosition position) =>
            byPath.TryGetValue(Key(position.Path), out var row) && position.Offset < row.CaretX.Length ? row.CaretX[position.Offset] : null;

        /// <inheritdoc/>
        public MathPosition? GetNearestPosition(IReadOnlyList<int> rowPath, double x)
        {
            if (!byPath.TryGetValue(Key(rowPath), out var row))
                return null;
            return RunAt(row, 0, x) ?? new MathPosition(rowPath, row.NearestOnLine(x, 0));
        }

        /// <inheritdoc/>
        public MathPosition? GetPositionOnAdjacentLine(MathPosition position, int direction)
        {
            // the innermost row broken into lines that holds the position, and the line of the position in it
            for (var length = position.Path.Count; length >= 0; length--)
            {
                var prefix = position.Path.Take(length).ToArray();
                if (!byPath.TryGetValue(Key(prefix), out var row) || row.Lines < 2 || row.IsRun)
                    continue;
                var index = length == position.Path.Count ? position.Offset : position.Path[length];
                if (index >= row.CaretX.Length || GetCaretX(position) is not { } x)
                    return null;
                var line = row.CaretLine[index] + direction;
                if (line < 0 || line >= row.Lines)
                    return null;
                return RunAt(row, line, x) ?? new MathPosition(prefix, row.NearestOnLine(x, line));
            }
            return null;
        }

        /// <summary>The position in a run of the row, on the line, under the x; null when no run is there.</summary>
        private MathPosition? RunAt(RowGeometry row, int line, double x)
        {
            foreach (var run in rows)
            {
                if (run.IsRun && run.Path.Length == row.Path.Length + 1 && run.Path.Take(row.Path.Length).SequenceEqual(row.Path)
                    && x > run.Left && x < run.Right && run.Path[row.Path.Length] < row.CaretLine.Length && row.CaretLine[run.Path[row.Path.Length]] == line)
                {
                    return new MathPosition(run.Path, run.NearestOnLine(x, 0));
                }
            }
            return null;
        }

        private static string Key(IEnumerable<int> path) => string.Join("/", path);
    }

    /// <summary>The geometry of a row or a run, in the coordinates of the layout; a row broken into lines has several.</summary>
    internal sealed class RowGeometry
    {
        public RowGeometry(int[] path, bool isRun, double[] caretX, double[] caretTop, double[] caretBottom, int[] caretLine, double[] lineRight, double left, double top, double bottom)
        {
            Path = path;
            IsRun = isRun;
            CaretX = caretX;
            CaretTop = caretTop;
            CaretBottom = caretBottom;
            CaretLine = caretLine;
            LineRight = lineRight;
            Left = left;
            Top = Math.Min(top, caretTop.DefaultIfEmpty(top).Min());
            Bottom = Math.Max(bottom, caretBottom.DefaultIfEmpty(bottom).Max());
            Right = Math.Max(left + 1, lineRight.Max());
        }

        public int[] Path { get; }

        public bool IsRun { get; }

        public double[] CaretX { get; }

        public double[] CaretTop { get; }

        public double[] CaretBottom { get; }

        /// <summary>The line of each caret position.</summary>
        public int[] CaretLine { get; }

        /// <summary>The x of the end of each line.</summary>
        public double[] LineRight { get; }

        public int Lines => LineRight.Length;

        public double Top { get; }

        public double Bottom { get; }

        public double Left { get; }

        public double Right { get; }

        public double Area => (Right - Left) * (Bottom - Top);

        /// <summary>The caret position nearest to a point: on the nearest line, the nearest x.</summary>
        public int Nearest(double x, double y)
        {
            var bestLine = 0;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < CaretX.Length; i++)
            {
                var d = y < CaretTop[i] ? CaretTop[i] - y : y > CaretBottom[i] ? y - CaretBottom[i] : 0;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestLine = CaretLine[i];
                }
            }
            return NearestOnLine(x, bestLine);
        }

        public int NearestOnLine(double x, int line)
        {
            var best = -1;
            for (var i = 0; i < CaretX.Length; i++)
            {
                if (CaretLine[i] == line && (best < 0 || Math.Abs(CaretX[i] - x) < Math.Abs(CaretX[best] - x)))
                    best = i;
            }
            return Math.Max(best, 0);
        }
    }
}
