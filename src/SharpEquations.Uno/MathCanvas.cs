namespace SharpEquations.Uno
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Shapes;
    using SharpEquations.Rendering;
    using Windows.Foundation;
    using Windows.UI;

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
    /// A <see cref="Canvas"/> of XAML as the canvas of the views of Rendering: rectangles, lines and ellipses as shapes,
    /// and the glyphs as paths from their outlines, so that it draws alike on every renderer of Uno and on WinUI. The
    /// canvas is moved to the origin of the layout (<see cref="OriginX"/>, <see cref="OriginY"/>), so that a change of
    /// the size of the control only moves it.
    /// </summary>
    internal sealed class MathCanvas : IMathCanvas
    {
        private static readonly Dictionary<string, Brush?> ColorBrushes = new Dictionary<string, Brush?>(StringComparer.OrdinalIgnoreCase);
        private readonly Canvas canvas;
        private readonly MathBrushes brushes;

        public MathCanvas(Canvas canvas, MathBrushes brushes, double originX, double originY)
        {
            this.canvas = canvas;
            this.brushes = brushes;
            OriginX = originX;
            OriginY = originY;
            canvas.Children.Clear();
            canvas.RenderTransform = new TranslateTransform { X = originX, Y = originY };
        }

        public double OriginX { get; }

        public double OriginY { get; }

        public void FillRectangle(MathRect rect, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                Add(new Rectangle { Width = rect.Width, Height = rect.Height, Fill = brush }, rect.X, rect.Y);
        }

        public void DrawRectangle(MathRect rect, double thickness, bool dashed, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is not { } brush)
                return;
            var shape = new Rectangle { Width = rect.Width, Height = rect.Height, Stroke = brush, StrokeThickness = thickness };
            if (dashed)
                shape.StrokeDashArray = new DoubleCollection { 2, 2 };
            Add(shape, rect.X, rect.Y);
        }

        public void DrawLine(double x1, double y1, double x2, double y2, double thickness, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                canvas.Children.Add(new Line { X1 = x1 - OriginX, Y1 = y1 - OriginY, X2 = x2 - OriginX, Y2 = y2 - OriginY, Stroke = brush, StrokeThickness = thickness });
        }

        public void DrawEllipse(MathRect rect, double thickness, MathPaint paint, string? color)
        {
            if (Brush(paint, color) is { } brush)
                Add(new Ellipse { Width = rect.Width, Height = rect.Height, Stroke = brush, StrokeThickness = thickness }, rect.X, rect.Y);
        }

        public void DrawGlyphs(MathLayout layout, double x, double y)
        {
            foreach (var glyph in layout.Glyphs)
            {
                if (glyph.Font is not OpenTypeMathFont font || (Brush(glyph.Color) ?? brushes.Foreground) is not { } brush)
                    continue;
                var outline = font.GetOutline(glyph.Glyph);
                if (outline.Count == 0)
                    continue;
                var scale = glyph.Size / font.UnitsPerEm;
                var ox = x + glyph.X - OriginX;
                var oy = y + glyph.Y - OriginY;
                Point P(double px, double py) => new Point(ox + px * scale, oy - py * scale);
                var geometry = new PathGeometry { FillRule = FillRule.Nonzero };
                PathFigure? figure = null;
                double cx = 0, cy = 0;
                foreach (var command in outline)
                {
                    switch (command.Kind)
                    {
                        case MathPathCommandKind.MoveTo:
                            figure = new PathFigure { StartPoint = P(command.X, command.Y), IsClosed = true, IsFilled = true };
                            geometry.Figures.Add(figure);
                            break;
                        case MathPathCommandKind.LineTo:
                            figure?.Segments.Add(new LineSegment { Point = P(command.X, command.Y) });
                            break;
                        case MathPathCommandKind.QuadraticTo:
                            // as a cubic, which every renderer draws
                            figure?.Segments.Add(new BezierSegment
                            {
                                Point1 = P(cx + 2.0 / 3 * (command.X1 - cx), cy + 2.0 / 3 * (command.Y1 - cy)),
                                Point2 = P(command.X + 2.0 / 3 * (command.X1 - command.X), command.Y + 2.0 / 3 * (command.Y1 - command.Y)),
                                Point3 = P(command.X, command.Y),
                            });
                            break;
                        case MathPathCommandKind.CubicTo:
                            figure?.Segments.Add(new BezierSegment { Point1 = P(command.X1, command.Y1), Point2 = P(command.X2, command.Y2), Point3 = P(command.X, command.Y) });
                            break;
                        case MathPathCommandKind.Close:
                            figure = null;
                            break;
                    }
                    if (command.Kind != MathPathCommandKind.Close)
                    {
                        cx = command.X;
                        cy = command.Y;
                    }
                }
                canvas.Children.Add(new Path { Data = geometry, Fill = brush });
            }
        }

        /// <summary>The brush of a colour of <c>mathcolor</c> or <c>mathbackground</c>: <c>#rgb</c>, <c>#rrggbb</c>, <c>#aarrggbb</c> or a name of HTML; null otherwise.</summary>
        public static Brush? Brush(string? color)
        {
            if (color == null)
                return null;
            lock (ColorBrushes)
            {
                if (!ColorBrushes.TryGetValue(color, out var brush))
                    ColorBrushes[color] = brush = ParseColor(color) is { } c ? new SolidColorBrush(c) : null;
                return brush;
            }
        }

        private static Color? ParseColor(string text)
        {
            var s = text.Trim();
            if (s.Length > 1 && s[0] == '#' && uint.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            {
                switch (s.Length)
                {
                    case 4:
                        return Color.FromArgb(0xFF, (byte)((v >> 8 & 0xF) * 17), (byte)((v >> 4 & 0xF) * 17), (byte)((v & 0xF) * 17));
                    case 7:
                        return Color.FromArgb(0xFF, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
                    case 9:
                        return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
                }
                return null;
            }
            return s.ToLowerInvariant() switch
            {
                "black" => Color.FromArgb(0xFF, 0, 0, 0),
                "silver" => Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0),
                "gray" or "grey" => Color.FromArgb(0xFF, 0x80, 0x80, 0x80),
                "white" => Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                "maroon" => Color.FromArgb(0xFF, 0x80, 0, 0),
                "red" => Color.FromArgb(0xFF, 0xFF, 0, 0),
                "purple" => Color.FromArgb(0xFF, 0x80, 0, 0x80),
                "fuchsia" or "magenta" => Color.FromArgb(0xFF, 0xFF, 0, 0xFF),
                "green" => Color.FromArgb(0xFF, 0, 0x80, 0),
                "lime" => Color.FromArgb(0xFF, 0, 0xFF, 0),
                "olive" => Color.FromArgb(0xFF, 0x80, 0x80, 0),
                "yellow" => Color.FromArgb(0xFF, 0xFF, 0xFF, 0),
                "navy" => Color.FromArgb(0xFF, 0, 0, 0x80),
                "blue" => Color.FromArgb(0xFF, 0, 0, 0xFF),
                "teal" => Color.FromArgb(0xFF, 0, 0x80, 0x80),
                "aqua" or "cyan" => Color.FromArgb(0xFF, 0, 0xFF, 0xFF),
                "orange" => Color.FromArgb(0xFF, 0xFF, 0xA5, 0),
                "transparent" => Color.FromArgb(0, 0, 0, 0),
                _ => null,
            };
        }

        private void Add(Shape shape, double x, double y)
        {
            Canvas.SetLeft(shape, x - OriginX);
            Canvas.SetTop(shape, y - OriginY);
            canvas.Children.Add(shape);
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
