namespace SharpEquations.WinForms
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;
    using SharpEquations.Editing;
    using SkiaSharp;

    /// <summary>
    /// The floating bar of the editor: a window of its own that never takes the focus (so that typing goes on in the
    /// editor), its items drawn with Skia in the user interface font, with a fallback font for the symbols it lacks.
    /// </summary>
    internal sealed class QuickBar : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const float TextSize = 12f * 96 / 72;
        private static readonly SKColor BackgroundColor = new SKColor(0xFB, 0xFB, 0xFD);
        private static readonly SKColor BorderColor = new SKColor(0xA0, 0xA0, 0xA0);
        private static readonly SKColor HighlightColor = new SKColor(0x33, 0x99, 0xFF, 0x40);
        private static readonly SKColor HoverColor = new SKColor(0x80, 0x80, 0x80, 0x30);
        private static readonly SKTypeface UiTypeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;
        private readonly MathSurface surface = new MathSurface();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly List<(MathQuickBarItem Item, RectangleF Bounds)> items = new List<(MathQuickBarItem, RectangleF)>();
        private int hovered = -1;
        private int tipped = -1;
        private bool column;

        public QuickBar()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        }

        /// <summary>The items shown.</summary>
        public int Count => items.Count;

        /// <summary>The item that Tab or Enter takes, highlighted; -1 for none.</summary>
        public int Selected { get; private set; } = -1;

        /// <inheritdoc/>
        protected override bool ShowWithoutActivation => true;

        /// <inheritdoc/>
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return parameters;
            }
        }

        private float DpiScale => DeviceDpi / 96f;

        /// <summary>Puts items in the bar, in a column (the completions) or in a row, and sizes it for them.</summary>
        public void Fill(IReadOnlyList<MathQuickBarItem> list, bool column, float minWidth)
        {
            items.Clear();
            this.column = column;
            hovered = -1;
            Selected = -1;
            using var font = new SKFont(UiTypeface, TextSize);
            float x = 4, y = 4, width = 0;
            var height = TextSize * 1.6f;
            foreach (var item in list)
            {
                var w = Math.Max(minWidth, Measure(item.Text, font) + 12);
                if (column)
                {
                    items.Add((item, new RectangleF(4, y, w, height)));
                    y += height + 2;
                    width = Math.Max(width, w);
                }
                else
                {
                    items.Add((item, new RectangleF(x, 4, w, height)));
                    x += w + 2;
                }
            }
            if (column)
            {
                for (var i = 0; i < items.Count; i++)
                    items[i] = (items[i].Item, new RectangleF(4, items[i].Bounds.Y, width, height));
            }
            var size = column ? new SizeF(width + 8, y + 2) : new SizeF(x + 2, height + 8);
            Size = new Size((int)Math.Ceiling(size.Width * DpiScale), (int)Math.Ceiling(size.Height * DpiScale));
            Invalidate();
        }

        /// <summary>Highlights an item; -1 for none.</summary>
        public void Highlight(int index)
        {
            if (index == Selected)
                return;
            Selected = index;
            Invalidate();
        }

        /// <summary>Invokes the item at a point of the bar, in pixels, as a click does.</summary>
        internal bool ClickAt(Point point)
        {
            var index = IndexAt(point);
            if (index < 0)
                return false;
            items[index].Item.Invoke();
            return true;
        }

        /// <inheritdoc/>
        protected override void WndProc(ref Message m)
        {
            // a click on the bar invokes an item, and leaves the focus in the editor
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            var scale = DpiScale;
            using (var canvas = surface.Begin(ClientSize, BackgroundColor, scale))
            using (var paint = new SKPaint { IsAntialias = true })
            using (var font = new SKFont(UiTypeface, TextSize) { Edging = SKFontEdging.SubpixelAntialias })
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.Color = BorderColor;
                canvas.DrawRect(0.5f, 0.5f, ClientSize.Width / scale - 1, ClientSize.Height / scale - 1, paint);
                paint.Style = SKPaintStyle.Fill;
                for (var i = 0; i < items.Count; i++)
                {
                    var (item, bounds) = items[i];
                    var rect = SKRect.Create(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    if (i == Selected || i == hovered)
                    {
                        paint.Color = i == Selected ? HighlightColor : HoverColor;
                        canvas.DrawRoundRect(rect, 3, 3, paint);
                    }
                    paint.Color = SKColors.Black;
                    var width = Measure(item.Text, font);
                    var x = column ? rect.Left + 6 : rect.MidX - width / 2;
                    font.GetFontMetrics(out var metrics);
                    Draw(canvas, item.Text, x, rect.MidY - (metrics.Ascent + metrics.Descent) / 2, font, paint);
                }
            }
            surface.Present(e.Graphics);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var index = IndexAt(e.Location);
            if (index != hovered)
            {
                hovered = index;
                Invalidate();
            }
            if (index != tipped)
            {
                tipped = index;
                if (index >= 0 && items[index].Item.ToolTip is { Length: > 0 } tip)
                    toolTip.Show(tip, this, e.X, e.Y + (int)(TextSize * 2 * DpiScale), 3000);
                else
                    toolTip.Hide(this);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = tipped = -1;
            toolTip.Hide(this);
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
                ClickAt(e.Location);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                surface.Dispose();
                toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>The width of a text, each code point in the user interface font or the first font that has it.</summary>
        private static float Measure(string text, SKFont font)
        {
            var width = 0f;
            foreach (var (run, typeface) in Runs(text))
            {
                font.Typeface = typeface;
                width += font.MeasureText(run);
            }
            font.Typeface = UiTypeface;
            return width;
        }

        private static void Draw(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
        {
            foreach (var (run, typeface) in Runs(text))
            {
                font.Typeface = typeface;
                canvas.DrawText(run, x, y, font, paint);
                x += font.MeasureText(run);
            }
            font.Typeface = UiTypeface;
        }

        /// <summary>The text in runs of one typeface: the user interface font where it has the glyph, a fallback elsewhere.</summary>
        private static IEnumerable<(string Run, SKTypeface Typeface)> Runs(string text)
        {
            var start = 0;
            SKTypeface? current = null;
            for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            {
                var codePoint = char.ConvertToUtf32(text, i);
                var typeface = UiTypeface.ContainsGlyph(codePoint) ? UiTypeface : SKFontManager.Default.MatchCharacter(codePoint) ?? UiTypeface;
                if (current == null)
                {
                    current = typeface;
                }
                else if (typeface.FamilyName != current.FamilyName)
                {
                    yield return (text.Substring(start, i - start), current);
                    start = i;
                    current = typeface;
                }
            }
            if (current != null)
                yield return (text.Substring(start), current);
        }

        private int IndexAt(Point point)
        {
            var scale = DpiScale;
            var p = new PointF(point.X / scale, point.Y / scale);
            return items.FindIndex(i => i.Bounds.Contains(p));
        }
    }
}
