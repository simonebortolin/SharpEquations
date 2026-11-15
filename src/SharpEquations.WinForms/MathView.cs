namespace SharpEquations.WinForms
{
    using System;
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Forms;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using SkiaSharp;

    /// <summary>
    /// Shows an equation, laid out after MathML Core with a math font and drawn with Skia, its glyphs as paths from their
    /// outlines. With <see cref="Control.AutoSize"/> (the default) the control takes the size of the equation.
    /// </summary>
    public class MathView : Control
    {
        private static readonly SKColor ErrorColor = new SKColor(0xFF, 0xE4, 0xE1);
        private readonly MathLayoutCache layoutCache = new MathLayoutCache();
        private readonly MathSurface surface = new MathSurface();
        private (Equation Equation, MathNode Root)? tree;
        private Equation? equation;
        private bool isDisplay;
        private OpenTypeMathFont? mathFont;
        private double? fontSize;

        /// <summary>A view with no equation.</summary>
        public MathView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            base.AutoSize = true;
        }

        /// <summary>The equation shown.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Equation? Equation
        {
            get => equation;
            set
            {
                if (ReferenceEquals(equation, value))
                    return;
                equation = value;
                Relayout();
            }
        }

        /// <summary>The MathML of the equation shown; setting it reads the MathML.</summary>
        /// <exception cref="FormatException">The MathML is not valid.</exception>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? MathML
        {
            get => Equation?.ToString(EquationFormat.MathML);
            set => Equation = string.IsNullOrEmpty(value) ? null : Equation.Parse(value!, EquationFormat.MathML);
        }

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        [DefaultValue(false)]
        public bool IsDisplay
        {
            get => isDisplay;
            set
            {
                if (isDisplay == value)
                    return;
                isDisplay = value;
                Relayout();
            }
        }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OpenTypeMathFont? MathFont
        {
            get => mathFont;
            set
            {
                mathFont = value;
                Relayout();
            }
        }

        /// <summary>The size of the font, in device-independent pixels; by default that of <see cref="Control.Font"/>.</summary>
        public double FontSize
        {
            get => fontSize ?? Font.SizeInPoints * 96 / 72;
            set
            {
                if (!(value > 0))
                    throw new ArgumentOutOfRangeException(nameof(value), "The size of the font is positive.");
                fontSize = value;
                Relayout();
            }
        }

        /// <summary>Whether the control takes the size of the equation; true by default.</summary>
        [DefaultValue(true)]
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override bool AutoSize
        {
            get => base.AutoSize;
            set
            {
                base.AutoSize = value;
                Relayout();
            }
        }

        /// <summary>The layout of the equation, in device-independent pixels; null without an equation.</summary>
        [Browsable(false)]
        public MathLayout? EquationLayout { get; private set; }

        /// <summary>The scale of the device-independent pixels of the layout to the pixels of the screen.</summary>
        internal float DpiScale => DeviceDpi / 96f;

        internal MathSurface Surface => surface;

        /// <inheritdoc/>
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (UpdateLayout(proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : (int?)null) is not { } layout)
                return Size.Empty;
            return new Size((int)Math.Ceiling(layout.Width * DpiScale), (int)Math.Ceiling(layout.Height * DpiScale));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                surface.Dispose();
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            var canvas = surface.Begin(ClientSize, MathCanvas.ToSkia(BackColor), DpiScale);
            using (canvas)
            {
                if ((EquationLayout ?? UpdateLayout(AutoSize ? null : ClientSize.Width)) is { } layout)
                {
                    using var math = new MathCanvas(canvas, new MathColors(MathCanvas.ToSkia(ForeColor), error: ErrorColor));
                    layout.Draw(math, 0, 0);
                }
            }
            surface.Present(e.Graphics);
        }

        /// <inheritdoc/>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Relayout();
        }

        /// <inheritdoc/>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            Relayout();
        }

        /// <inheritdoc/>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            tree = null;
            EquationLayout = null;
            layoutCache.Clear();
        }

        /// <inheritdoc/>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!AutoSize)
                EquationLayout = null;
        }

        private bool ShouldSerializeFontSize() => fontSize.HasValue;

        private void ResetFontSize()
        {
            fontSize = null;
            Relayout();
        }

        private void Relayout()
        {
            EquationLayout = null;
            if (AutoSize)
            {
                var size = GetPreferredSize(Size.Empty);
                if (size != Size)
                    Size = size;
            }
            Invalidate();
        }

        /// <summary>Lays out the equation for a width in pixels, or for no limit.</summary>
        private MathLayout? UpdateLayout(int? width)
        {
            if (Equation is not { } current)
                return EquationLayout = null;
            if (tree is not { } cached || !ReferenceEquals(cached.Equation, current))
                tree = cached = (current, MathNode.FromMathML(current.ToMathML()));
            var options = new MathLayoutOptions { FontSize = FontSize, Display = IsDisplay, LineWidth = width / (double)DpiScale };
            return EquationLayout = MathLayoutEngine.Layout(cached.Root, MathFont ?? MathFonts.Default, options, layoutCache);
        }
    }
}
