namespace SharpEquations.Rendering
{
    using System;
    using System.Collections.Generic;
    using SharpEquations.Rendering.OpenType;

    /// <summary>
    /// A math font read from its OpenType file, without a toolkit: its glyphs, advances, ink boxes (from the outlines),
    /// MATH table and script variants are read from a copy of the file, once. A toolkit derives from it to draw with its
    /// own fonts and to find fallback fonts (<see cref="GetFallback"/>).
    /// </summary>
    public class OpenTypeMathFont : IMathFont
    {
        private readonly Lazy<MathTable?> math;
        private readonly Lazy<GlyphOutlines?> outlines;
        private readonly Lazy<ScriptStyles?> scriptStyles;

        /// <summary>The font of the bytes of a font file (a collection: its first font with a MATH table).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="FormatException">The bytes are not an OpenType font.</exception>
        public OpenTypeMathFont(byte[] data)
            : this(FontFile.Parse(data ?? throw new ArgumentNullException(nameof(data))))
        {
        }

        /// <summary>The font of a font file.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="file"/> is null.</exception>
        protected OpenTypeMathFont(FontFile file)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
            UnitsPerEm = file.UnitsPerEm;
            math = new Lazy<MathTable?>(() => file.Table("MATH") is { } table ? TryParse(table) : null);
            scriptStyles = new Lazy<ScriptStyles?>(() => ScriptStyles.Parse(file.Table("GSUB")));
            outlines = new Lazy<GlyphOutlines?>(() => GlyphOutlines.Create(file.Table));
        }

        /// <summary>The font file.</summary>
        public FontFile File { get; }

        /// <inheritdoc/>
        public double UnitsPerEm { get; }

        /// <inheritdoc/>
        public MathTable? Math => math.Value;

        /// <inheritdoc/>
        public bool TryGetGlyph(int codePoint, out ushort glyph) => File.TryGetGlyph(codePoint, out glyph);

        /// <inheritdoc/>
        public double GetAdvance(ushort glyph) => File.Advance(glyph);

        /// <inheritdoc/>
        public GlyphBounds GetBounds(ushort glyph) => outlines.Value?.Bounds(glyph) ?? LineBounds(glyph);

        /// <summary>
        /// The outline of a glyph in design units, y up (<see cref="GlyphOutlines.Outline"/>), for a toolkit that draws
        /// glyphs as paths; empty for a glyph without outline.
        /// </summary>
        public IReadOnlyList<MathPathCommand> GetOutline(ushort glyph) => outlines.Value?.Outline(glyph) ?? Array.Empty<MathPathCommand>();

        /// <inheritdoc/>
        public ushort GetScriptVariant(ushort glyph, int scriptLevel) => scriptStyles.Value?.Variant(glyph, scriptLevel) ?? glyph;

        /// <inheritdoc/>
        /// <remarks>None here: a toolkit finds them among its fonts.</remarks>
        public virtual IMathFont? GetFallback(int codePoint) => null;

        /// <summary>
        /// The box of a glyph of a font without outlines (the bitmaps of a color emoji font, <c>CBDT</c>), which has no
        /// ink box to read: its advance across, and the ascent and descent of the font.
        /// </summary>
        private GlyphBounds LineBounds(ushort glyph)
        {
            if (File.Table("hhea") is not { Length: >= 8 } hhea)
                return default;
            var ascent = (short)(hhea[4] << 8 | hhea[5]);
            var descent = (short)(hhea[6] << 8 | hhea[7]);
            return new GlyphBounds(0, descent, File.Advance(glyph), ascent);
        }

        private static MathTable? TryParse(byte[] table)
        {
            try
            {
                return MathTable.Parse(table);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
