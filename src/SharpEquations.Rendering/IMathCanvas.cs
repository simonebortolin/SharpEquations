namespace SharpEquations.Rendering
{
    /// <summary>
    /// What a toolkit paints with: the brush of the foreground, of the selection... A colour given with it
    /// (<c>mathcolor</c>, <c>mathbackground</c>) takes its place when the toolkit reads it.
    /// </summary>
    public enum MathPaint
    {
        /// <summary>The foreground of the equation.</summary>
        Foreground,

        /// <summary>The background of <c>mathbackground</c>: only its colour, nothing without one.</summary>
        Background,

        /// <summary>The box of an empty slot.</summary>
        Placeholder,

        /// <summary>The background of <c>merror</c>, of text that does not parse.</summary>
        Error,

        /// <summary>The selection.</summary>
        Selection,

        /// <summary>The caret.</summary>
        Caret,
    }

    /// <summary>
    /// The surface a toolkit draws an equation on (<see cref="MathLayout.Draw"/>, <see cref="MathEditorView.Render"/>):
    /// rectangles, lines and ellipses with a <see cref="MathPaint"/>, and the glyphs of a layout, which the toolkit
    /// draws with its own glyph runs. Coordinates are those of the control, y down.
    /// </summary>
    public interface IMathCanvas
    {
        /// <summary>Fills a rectangle.</summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="paint">What it is painted with.</param>
        /// <param name="color">The colour of <c>mathcolor</c> or <c>mathbackground</c>, or null.</param>
        void FillRectangle(MathRect rect, MathPaint paint, string? color);

        /// <summary>Draws the outline of a rectangle, dashed for the box of an empty slot.</summary>
        void DrawRectangle(MathRect rect, double thickness, bool dashed, MathPaint paint, string? color);

        /// <summary>Draws a line.</summary>
        void DrawLine(double x1, double y1, double x2, double y2, double thickness, MathPaint paint, string? color);

        /// <summary>Draws the outline of the ellipse in a rectangle.</summary>
        void DrawEllipse(MathRect rect, double thickness, MathPaint paint, string? color);

        /// <summary>
        /// Draws the glyphs of a layout (<see cref="MathLayout.Glyphs"/>), each in its colour or the foreground, with the
        /// origin of the layout at a point.
        /// </summary>
        void DrawGlyphs(MathLayout layout, double x, double y);
    }
}
