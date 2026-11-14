namespace SharpEquations.WinForms
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using SharpEquations.Rendering;
    using SkiaSharp;

    /// <summary>The colours an equation is drawn with, one for each <see cref="MathPaint"/>; null draws nothing.</summary>
    internal readonly struct MathColors
    {
        public MathColors(SKColor? foreground, SKColor? placeholder = null, SKColor? error = null, SKColor? selection = null, SKColor? caret = null)
        {
            Foreground = foreground;
            Placeholder = placeholder;
            Error = error;
            Selection = selection;
            Caret = caret;
        }

        public SKColor? Foreground { get; }

        public SKColor? Placeholder { get; }

        public SKColor? Error { get; }

        public SKColor? Selection { get; }

        public SKColor? Caret { get; }
    }

    /// <summary>
    /// An <see cref="SKCanvas"/> as the canvas of the views of Rendering: the shapes drawn as they are, and the glyphs as
    /// paths from their outlines (<see cref="OpenTypeMathFont.GetOutline"/>), made once for each glyph of a font in the
    /// units of its design and scaled where they are drawn.
    /// </summary>
    internal sealed class MathCanvas : IMathCanvas, IDisposable
    {
        private static readonly ConditionalWeakTable<OpenTypeMathFont, Dictionary<ushort, SKPath?>> Paths = new ConditionalWeakTable<OpenTypeMathFont, Dictionary<ushort, SKPath?>>();
        private static readonly Dictionary<string, SKColor?> NamedColors = new Dictionary<string, SKColor?>(StringComparer.OrdinalIgnoreCase);
        private static readonly float[] Dashes = { 2f, 2f };
        private readonly SKCanvas canvas;
        private readonly MathColors colors;
        private readonly SKPaint paint = new SKPaint { IsAntialias = true };

        public MathCanvas(SKCanvas canvas, MathColors colors)
        {
            this.canvas = canvas;
            this.colors = colors;
        }

        public void Dispose() => paint.Dispose();

        public void FillRectangle(MathRect rect, MathPaint paint, string? color)
        {
            if (Color(paint, color) is { } c)
                canvas.DrawRect(SKRect.Create((float)rect.X, (float)rect.Y, (float)Math.Max(0, rect.Width), (float)Math.Max(0, rect.Height)), Fill(c));
        }

        public void DrawRectangle(MathRect rect, double thickness, bool dashed, MathPaint paint, string? color)
        {
            if (Color(paint, color) is not { } c)
                return;
            var half = thickness / 2;
            using var dashes = dashed ? SKPathEffect.CreateDash(Dashes, 0) : null;
            var stroke = Stroke(c, thickness);
            stroke.PathEffect = dashes;
            canvas.DrawRect(SKRect.Create((float)(rect.X + half), (float)(rect.Y + half), (float)Math.Max(0, rect.Width - thickness), (float)Math.Max(0, rect.Height - thickness)), stroke);
            stroke.PathEffect = null;
        }

        public void DrawLine(double x1, double y1, double x2, double y2, double thickness, MathPaint paint, string? color)
        {
            if (Color(paint, color) is { } c)
                canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2, Stroke(c, thickness));
        }

        public void DrawEllipse(MathRect rect, double thickness, MathPaint paint, string? color)
        {
            if (Color(paint, color) is { } c)
                canvas.DrawOval(SKRect.Create((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height), Stroke(c, thickness));
        }

        public void DrawGlyphs(MathLayout layout, double x, double y)
        {
            foreach (var glyph in layout.Glyphs)
            {
                if (glyph.Font is not OpenTypeMathFont font || (Parse(glyph.Color) ?? colors.Foreground) is not { } c || Path(font, glyph.Glyph) is not { } path)
                    continue;
                var scale = (float)(glyph.Size / font.UnitsPerEm);
                canvas.Save();
                canvas.Translate((float)(x + glyph.X), (float)(y + glyph.Y));
                canvas.Scale(scale, -scale);
                canvas.DrawPath(path, Fill(c));
                canvas.Restore();
            }
        }

        /// <summary>The path of a glyph in the units of the design of its font, y up; null for a glyph with no outline.</summary>
        internal static SKPath? Path(OpenTypeMathFont font, ushort glyph)
        {
            var cache = Paths.GetValue(font, _ => new Dictionary<ushort, SKPath?>());
            lock (cache)
            {
                if (!cache.TryGetValue(glyph, out var path))
                    cache[glyph] = path = CreatePath(font.GetOutline(glyph));
                return path;
            }
        }

        /// <summary>The colour of <c>mathcolor</c> or <c>mathbackground</c> (<c>#rgb</c>, <c>#rrggbb</c>, <c>#aarrggbb</c>, a name); null otherwise.</summary>
        internal static SKColor? Parse(string? color)
        {
            if (color == null)
                return null;
            lock (NamedColors)
            {
                if (!NamedColors.TryGetValue(color, out var parsed))
                    NamedColors[color] = parsed = ParseColor(color.Trim());
                return parsed;
            }
        }

        internal static SKColor ToSkia(System.Drawing.Color color) => new SKColor(color.R, color.G, color.B, color.A);

        private static SKPath? CreatePath(IReadOnlyList<MathPathCommand> outline)
        {
            if (outline.Count == 0)
                return null;
            using var builder = new SKPathBuilder { FillType = SKPathFillType.Winding };
            var open = false;
            foreach (var command in outline)
            {
                switch (command.Kind)
                {
                    case MathPathCommandKind.MoveTo:
                        if (open)
                            builder.Close();
                        builder.MoveTo((float)command.X, (float)command.Y);
                        open = true;
                        break;
                    case MathPathCommandKind.LineTo:
                        builder.LineTo((float)command.X, (float)command.Y);
                        break;
                    case MathPathCommandKind.QuadraticTo:
                        builder.QuadTo((float)command.X1, (float)command.Y1, (float)command.X, (float)command.Y);
                        break;
                    case MathPathCommandKind.CubicTo:
                        builder.CubicTo((float)command.X1, (float)command.Y1, (float)command.X2, (float)command.Y2, (float)command.X, (float)command.Y);
                        break;
                }
            }
            if (open)
                builder.Close();
            return builder.Detach();
        }

        private static SKColor? ParseColor(string text)
        {
            if (text.Length > 0 && text[0] == '#')
            {
                var hex = text.Substring(1);
                if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
                    return null;
                return hex.Length switch
                {
                    3 => new SKColor((byte)((v >> 8 & 0xF) * 17), (byte)((v >> 4 & 0xF) * 17), (byte)((v & 0xF) * 17)),
                    6 => new SKColor((byte)(v >> 16), (byte)(v >> 8), (byte)v),
                    8 => new SKColor((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)(v >> 24)),
                    _ => null,
                };
            }
            var known = System.Drawing.Color.FromName(text);
            return known.IsKnownColor ? ToSkia(known) : null;
        }

        private SKPaint Fill(SKColor color)
        {
            paint.Style = SKPaintStyle.Fill;
            paint.Color = color;
            return paint;
        }

        private SKPaint Stroke(SKColor color, double thickness)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = (float)thickness;
            paint.Color = color;
            return paint;
        }

        private SKColor? Color(MathPaint paint, string? color) => Parse(color) ?? paint switch
        {
            MathPaint.Foreground => colors.Foreground,
            MathPaint.Placeholder => colors.Placeholder,
            MathPaint.Error => colors.Error,
            MathPaint.Selection => colors.Selection,
            MathPaint.Caret => colors.Caret,
            _ => null,
        };
    }
}
