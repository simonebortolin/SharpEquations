namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using global::Avalonia;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Immutable;
    using global::Avalonia.Media.TextFormatting;
    using SharpEquations.Rendering;

    /// <summary>The brushes an equation is drawn with.</summary>
    internal readonly struct MathBrushes
    {
        public MathBrushes(IBrush? foreground, IBrush? placeholder, IBrush? error)
        {
            Foreground = foreground;
            Placeholder = placeholder;
            Error = error;
        }

        public IBrush? Foreground { get; }

        public IBrush? Placeholder { get; }

        public IBrush? Error { get; }
    }

    /// <summary>A laid out equation with the glyph runs of Avalonia it is drawn with.</summary>
    internal sealed class MathDrawing : IDisposable
    {
        private static readonly Dictionary<string, IBrush?> ColorBrushes = new Dictionary<string, IBrush?>(StringComparer.OrdinalIgnoreCase);
        private readonly List<(GlyphRun Run, string? Color)> runs = new List<(GlyphRun, string?)>();

        public MathDrawing(MathLayout layout)
        {
            Layout = layout;
            foreach (var glyph in layout.Glyphs)
            {
                if (glyph.Font is not AvaloniaMathFont font)
                    continue;
                var info = new[] { new GlyphInfo(glyph.Glyph, 0, font.GetAdvance(glyph.Glyph) * glyph.Size / font.UnitsPerEm) };
                runs.Add((new GlyphRun(font.Typeface, glyph.Size, "￼".AsMemory(), info, new Point(glyph.X, glyph.Y)), glyph.Color));
            }
        }

        public MathLayout Layout { get; }

        public void Draw(DrawingContext context, Point origin, MathBrushes brushes)
        {
            using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                foreach (var shape in Layout.Shapes)
                    DrawShape(context, shape, brushes);
                foreach (var (run, color) in runs)
                {
                    var brush = Brush(color) ?? brushes.Foreground;
                    if (brush != null)
                        context.DrawGlyphRun(brush, run);
                }
            }
        }

        private static void DrawShape(DrawingContext context, MathShape shape, MathBrushes brushes)
        {
            var brush = shape.Role switch
            {
                MathShapeRole.Placeholder => brushes.Placeholder,
                MathShapeRole.Error => brushes.Error,
                MathShapeRole.Background => Brush(shape.Color),
                _ => Brush(shape.Color) ?? brushes.Foreground,
            };
            if (brush == null)
                return;

            var rect = new Rect(shape.X, shape.Y, Math.Max(0, shape.Width), Math.Max(0, shape.Height));
            switch (shape.Kind)
            {
                case MathShapeKind.Fill:
                    context.FillRectangle(brush, rect);
                    break;
                case MathShapeKind.Outline:
                    var pen = new Pen(brush, shape.Thickness, shape.Role == MathShapeRole.Placeholder ? new DashStyle(new double[] { 2, 2 }, 0) : null);
                    context.DrawRectangle(null, pen, rect);
                    break;
                case MathShapeKind.Line:
                    context.DrawLine(new Pen(brush, shape.Thickness), new Point(shape.X, shape.Y), new Point(shape.X2, shape.Y2));
                    break;
                case MathShapeKind.Ellipse:
                    context.DrawEllipse(null, new Pen(brush, shape.Thickness), rect);
                    break;
            }
        }

        private static IBrush? Brush(string? color)
        {
            if (color == null)
                return null;
            lock (ColorBrushes)
            {
                if (!ColorBrushes.TryGetValue(color, out var brush))
                {
                    brush = Color.TryParse(color, out var c) ? new ImmutableSolidColorBrush(c) : null;
                    ColorBrushes[color] = brush;
                }
                return brush;
            }
        }

        public void Dispose()
        {
            foreach (var (run, _) in runs)
                run.Dispose();
            runs.Clear();
        }
    }
}
