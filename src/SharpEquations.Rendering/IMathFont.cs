namespace SharpEquations.Rendering
{
    using SharpEquations.Rendering.OpenType;

    /// <summary>The ink box of a glyph, in design units, with y up from the baseline.</summary>
    public readonly struct GlyphBounds
    {
        /// <summary>A box.</summary>
        public GlyphBounds(double xMin, double yMin, double xMax, double yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        /// <summary>The left of the ink.</summary>
        public double XMin { get; }

        /// <summary>The bottom of the ink (negative below the baseline).</summary>
        public double YMin { get; }

        /// <summary>The right of the ink.</summary>
        public double XMax { get; }

        /// <summary>The top of the ink.</summary>
        public double YMax { get; }
    }

    /// <summary>
    /// A font as the layout needs it, which a toolkit implements over its own fonts: glyphs, advances, ink boxes, and
    /// the OpenType MATH table of a math font.
    /// </summary>
    public interface IMathFont
    {
        /// <summary>The units of the design of the font in an em.</summary>
        double UnitsPerEm { get; }

        /// <summary>The MATH table of the font, or null for a font that has none (a fallback font).</summary>
        MathTable? Math { get; }

        /// <summary>The glyph of a code point, if the font has one.</summary>
        bool TryGetGlyph(int codePoint, out ushort glyph);

        /// <summary>The advance of a glyph, in design units.</summary>
        double GetAdvance(ushort glyph);

        /// <summary>The ink box of a glyph, in design units.</summary>
        GlyphBounds GetBounds(ushort glyph);

        /// <summary>
        /// The variant of a glyph drawn for a script level (1 for scripts, 2 for scripts of scripts): the <c>ssty</c>
        /// feature of a math font (<see cref="ScriptStyles"/>); the glyph itself without one.
        /// </summary>
        ushort GetScriptVariant(ushort glyph, int scriptLevel);

        /// <summary>A font for a code point that the font has no glyph for, or null.</summary>
        IMathFont? GetFallback(int codePoint);
    }
}
