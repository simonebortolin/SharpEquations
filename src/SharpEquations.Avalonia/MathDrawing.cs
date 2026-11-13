namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using global::Avalonia;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Immutable;
    using global::Avalonia.Media.TextFormatting;
    using SharpEquations.Rendering;

    /// <summary>The brushes an equation is drawn with, one for each <see cref="MathPaint"/>.</summary>
    internal readonly struct MathBrushes
    {
        public MathBrushes(IBrush? foreground, IBrush? placeholder, IBrush? error, IBrush? selection = null, IBrush? caret = null)
        {
            Foreground = foreground;
            Placeholder = placeholder;
            Error = error;
            Selection = selection;
            Caret = caret;
        }

        public IBrush? Foreground { get; }

        public IBrush? Placeholder { get; }

        public IBrush? Error { get; }

        public IBrush? Selection { get; }

        public IBrush? Caret { get; }
    }

    /// <summary>The glyph runs of Avalonia a laid out equation is drawn with, made once for the layout.</summary>
    internal sealed class MathDrawing : IDisposable
    {
        private static readonly Dictionary<string, IBrush?> ColorBrushes = new Dictionary<string, IBrush?>(StringComparer.OrdinalIgnoreCase);
        private static readonly IBrush LightError = new ImmutableSolidColorBrush(Colors.MistyRose);
        private static readonly IBrush DarkError = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0x5C, 0x24, 0x24));
        private readonly List<(GlyphRun Run, string? Color)> runs = new List<(GlyphRun, string?)>();

        public MathDrawing(MathLayout layout)
        {
            Layout = layout;
            var typefaces = new Dictionary<AvaloniaMathFont, GlyphTypeface?>();
            foreach (var glyph in layout.Glyphs)
            {
                if (glyph.Font is not AvaloniaMathFont font)
                    continue;
                if (!typefaces.TryGetValue(font, out var typeface))
                    typefaces[font] = typeface = font.GetGlyphTypeface();
                if (typeface == null)
                    continue;
                var info = new[] { new GlyphInfo(glyph.Glyph, 0, font.GetAdvance(glyph.Glyph) * glyph.Size / font.UnitsPerEm) };
                runs.Add((new GlyphRun(typeface, glyph.Size, ReadOnlyMemory<char>.Empty, info, new Point(glyph.X, glyph.Y)), glyph.Color));
            }
        }

        public MathLayout Layout { get; }

        /// <summary>The background of merror, of text that does not parse: light or dark with the theme.</summary>
        public static IBrush ErrorBrush(global::Avalonia.Styling.ThemeVariant? theme) => theme == global::Avalonia.Styling.ThemeVariant.Dark ? DarkError : LightError;

        /// <summary>Draws the layout, its origin at a point.</summary>
        public void Draw(DrawingContext context, Point origin, MathBrushes brushes) => Layout.Draw(new MathCanvas(context, brushes, this), origin.X, origin.Y);

        public void DrawGlyphs(DrawingContext context, Point origin, IBrush? foreground)
        {
            using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                foreach (var (run, color) in runs)
                {
                    var brush = Brush(color) ?? foreground;
                    if (brush != null)
                        context.DrawGlyphRun(brush, run);
                }
            }
        }

        /// <summary>The brush of a colour of <c>mathcolor</c> or <c>mathbackground</c>, or null.</summary>
        public static IBrush? Brush(string? color)
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

    /// <summary>A <see cref="DrawingContext"/> of Avalonia as the canvas of the views of Rendering.</summary>
    internal sealed class MathCanvas : IMathCanvas
    {
        private static readonly DashStyle Dashes = new DashStyle(new double[] { 2, 2 }, 0);
        private readonly DrawingContext context;
        private readonly MathBrushes brushes;
        private readonly MathDrawing? drawing;

        public MathCanvas(DrawingContext context, MathBrushes brushes, MathDrawing? drawing)
        {
            this.context = context;
            this.brushes = brushes;
            this.drawing = drawing;
        }

        public void FillRectangle(MathRect rect, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.FillRectangle(brush, Rect(rect));
        }

        public void DrawRectangle(MathRect rect, double thickness, bool dashed, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.DrawRectangle(null, new Pen(brush, thickness, dashed ? Dashes : null), Rect(rect));
        }

        public void DrawLine(double x1, double y1, double x2, double y2, double thickness, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.DrawLine(new Pen(brush, thickness), new Point(x1, y1), new Point(x2, y2));
        }

        public void DrawEllipse(MathRect rect, double thickness, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                context.DrawEllipse(null, new Pen(brush, thickness), Rect(rect));
        }

        public void DrawGlyphs(MathLayout layout, double x, double y)
        {
            if (drawing != null && ReferenceEquals(drawing.Layout, layout))
            {
                drawing.DrawGlyphs(context, new Point(x, y), brushes.Foreground);
                return;
            }
            new MathDrawing(layout).DrawGlyphs(context, new Point(x, y), brushes.Foreground);
        }

        private static Rect Rect(MathRect rect) => new Rect(rect.X, rect.Y, rect.Width, rect.Height);

        private IBrush? Brush(MathPaint paint, string? color) => MathDrawing.Brush(color) ?? paint switch
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
