namespace SharpEquations.Wpf
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Media;
    using SharpEquations.Rendering;

    /// <summary>The brushes an equation is drawn with, one for each <see cref="MathPaint"/>.</summary>
    internal readonly struct MathBrushes
    {
        public MathBrushes(Brush? foreground, Brush? placeholder = null, Brush? error = null, Brush? selection = null, Brush? caret = null)
        {
            Foreground = foreground;
            Placeholder = placeholder;
            Error = error;
            Selection = selection;
            Caret = caret;
        }

        public Brush? Foreground { get; }

        public Brush? Placeholder { get; }

        public Brush? Error { get; }

        public Brush? Selection { get; }

        public Brush? Caret { get; }
    }

    /// <summary>
    /// A <see cref="DrawingContext"/> of WPF as the canvas of the views of Rendering: the shapes drawn as they are, and the
    /// glyphs as geometries from their outlines (<see cref="OpenTypeMathFont.GetOutline"/>), made once for each glyph of a
    /// font in the units of its design and scaled where they are drawn.
    /// </summary>
    internal sealed class MathCanvas : IMathCanvas
    {
        private static readonly ConditionalWeakTable<OpenTypeMathFont, Dictionary<ushort, Geometry?>> Geometries = new ConditionalWeakTable<OpenTypeMathFont, Dictionary<ushort, Geometry?>>();
        private static readonly Dictionary<string, Brush?> ColorBrushes = new Dictionary<string, Brush?>(StringComparer.OrdinalIgnoreCase);
        private static readonly DashStyle Dashes = CreateDashes();
        private readonly DrawingContext context;
        private readonly MathBrushes brushes;

        public MathCanvas(DrawingContext context, MathBrushes brushes)
        {
            this.context = context;
            this.brushes = brushes;
        }

        public void FillRectangle(MathRect rect, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.DrawRectangle(brush, null, new Rect(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height)));
        }

        public void DrawRectangle(MathRect rect, double thickness, bool dashed, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is not { } brush)
                return;
            var pen = new Pen(brush, thickness);
            if (dashed)
                pen.DashStyle = Dashes;
            var half = thickness / 2;
            context.DrawRectangle(null, pen, new Rect(rect.X + half, rect.Y + half, Math.Max(0, rect.Width - thickness), Math.Max(0, rect.Height - thickness)));
        }

        public void DrawLine(double x1, double y1, double x2, double y2, double thickness, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.DrawLine(new Pen(brush, thickness), new Point(x1, y1), new Point(x2, y2));
        }

        public void DrawEllipse(MathRect rect, double thickness, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.DrawEllipse(null, new Pen(brush, thickness), new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2);
        }

        public void DrawGlyphs(MathLayout layout, double x, double y)
        {
            foreach (var glyph in layout.Glyphs)
            {
                if (glyph.Font is not OpenTypeMathFont font || (Brush(glyph.Color) ?? brushes.Foreground) is not { } brush || Geometry(font, glyph.Glyph) is not { } geometry)
                    continue;
                var scale = glyph.Size / font.UnitsPerEm;
                context.PushTransform(new MatrixTransform(scale, 0, 0, -scale, x + glyph.X, y + glyph.Y));
                context.DrawGeometry(brush, null, geometry);
                context.Pop();
            }
        }

        /// <summary>The geometry of a glyph in the units of the design of its font, y up; null for a glyph with no outline.</summary>
        internal static Geometry? Geometry(OpenTypeMathFont font, ushort glyph)
        {
            var cache = Geometries.GetValue(font, _ => new Dictionary<ushort, Geometry?>());
            lock (cache)
            {
                if (!cache.TryGetValue(glyph, out var geometry))
                    cache[glyph] = geometry = CreateGeometry(font.GetOutline(glyph));
                return geometry;
            }
        }

        private static StreamGeometry? CreateGeometry(IReadOnlyList<MathPathCommand> outline)
        {
            if (outline.Count == 0)
                return null;
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var sink = geometry.Open())
            {
                foreach (var command in outline)
                {
                    switch (command.Kind)
                    {
                        case MathPathCommandKind.MoveTo:
                            sink.BeginFigure(new Point(command.X, command.Y), true, true);
                            break;
                        case MathPathCommandKind.LineTo:
                            sink.LineTo(new Point(command.X, command.Y), true, false);
                            break;
                        case MathPathCommandKind.QuadraticTo:
                            sink.QuadraticBezierTo(new Point(command.X1, command.Y1), new Point(command.X, command.Y), true, false);
                            break;
                        case MathPathCommandKind.CubicTo:
                            sink.BezierTo(new Point(command.X1, command.Y1), new Point(command.X2, command.Y2), new Point(command.X, command.Y), true, false);
                            break;
                    }
                }
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>The brush of a colour of <c>mathcolor</c> or <c>mathbackground</c> (<c>#rgb</c>, <c>#rrggbb</c>, <c>#aarrggbb</c>, a name); null otherwise.</summary>
        public static Brush? Brush(string? color)
        {
            if (color == null)
                return null;
            lock (ColorBrushes)
            {
                if (!ColorBrushes.TryGetValue(color, out var brush))
                {
                    brush = ParseColor(color.Trim()) is { } c ? new SolidColorBrush(c) : null;
                    brush?.Freeze();
                    ColorBrushes[color] = brush;
                }
                return brush;
            }
        }

        private static Color? ParseColor(string text)
        {
            // #rgb, which WPF reads as #argb
            if (text.Length == 4 && text[0] == '#' && int.TryParse(text.Substring(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return Color.FromRgb((byte)((v >> 8 & 0xF) * 17), (byte)((v >> 4 & 0xF) * 17), (byte)((v & 0xF) * 17));
            try
            {
                return ColorConverter.ConvertFromString(text) as Color?;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static DashStyle CreateDashes()
        {
            var dashes = new DashStyle(new DoubleCollection { 2, 2 }, 0);
            dashes.Freeze();
            return dashes;
        }

        private Brush? Brush(MathPaint paint, string? color) => Brush(color) ?? paint switch
        {
            MathPaint.Foreground => brushes.Foreground,
            MathPaint.Placeholder => brushes.Placeholder,
            MathPaint.Error => brushes.Error,
            MathPaint.Selection => brushes.Selection,
            MathPaint.Caret => brushes.Caret,
            _ => null,
        };
    }
}
