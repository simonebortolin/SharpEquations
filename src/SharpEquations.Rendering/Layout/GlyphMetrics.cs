namespace SharpEquations.Rendering.Layout
{
    using System.Collections.Generic;

    /// <summary>
    /// The glyphs of the code points, and the advances, ink boxes and script variants of the glyphs, of a font and its
    /// fallbacks, each asked of the font once. They are in design units, so they serve every size.
    /// </summary>
    internal sealed class GlyphMetrics
    {
        private readonly Dictionary<IMathFont, FontMetrics> fonts = new Dictionary<IMathFont, FontMetrics>();
        private readonly Dictionary<int, (IMathFont Font, ushort Glyph, bool Found)> glyphs = new Dictionary<int, (IMathFont, ushort, bool)>();
        private FontMetrics? last;

        public GlyphMetrics(IMathFont font) => Font = font;

        /// <summary>The font the code points are looked up in first.</summary>
        public IMathFont Font { get; }

        /// <summary>The glyph of a code point in the font, or else in its fallback; false (and glyph 0 of the font) if neither has one.</summary>
        public bool Glyph(int codePoint, out IMathFont f, out ushort glyph)
        {
            if (!glyphs.TryGetValue(codePoint, out var found))
            {
                if (Font.TryGetGlyph(codePoint, out var g) && g != 0)
                    found = (Font, g, true);
                else if (Font.GetFallback(codePoint) is { } fallback && fallback.TryGetGlyph(codePoint, out g) && g != 0)
                    found = (fallback, g, true);
                else
                    found = (Font, 0, false);
                glyphs[codePoint] = found;
            }
            f = found.Font;
            glyph = found.Glyph;
            return found.Found;
        }

        public double Advance(IMathFont f, ushort glyph) => Of(f).Glyph(glyph).Advance;

        public GlyphBounds Bounds(IMathFont f, ushort glyph) => Of(f).Glyph(glyph).Bounds;

        public ushort Variant(IMathFont f, ushort glyph, int level)
        {
            var metrics = Of(f);
            var key = (glyph, level);
            if (!metrics.Variants.TryGetValue(key, out var variant))
                metrics.Variants[key] = variant = f.GetScriptVariant(glyph, level);
            return variant;
        }

        private FontMetrics Of(IMathFont f)
        {
            if (last != null && ReferenceEquals(last.Font, f))
                return last;
            if (!fonts.TryGetValue(f, out var metrics))
                fonts[f] = metrics = new FontMetrics(f);
            return last = metrics;
        }

        private sealed class FontMetrics
        {
            private readonly Dictionary<ushort, (double Advance, GlyphBounds Bounds)> glyphs = new Dictionary<ushort, (double, GlyphBounds)>();

            public FontMetrics(IMathFont font) => Font = font;

            public IMathFont Font { get; }

            public Dictionary<(ushort Glyph, int Level), ushort> Variants { get; } = new Dictionary<(ushort, int), ushort>();

            public (double Advance, GlyphBounds Bounds) Glyph(ushort glyph)
            {
                if (!glyphs.TryGetValue(glyph, out var metrics))
                    glyphs[glyph] = metrics = (Font.GetAdvance(glyph), Font.GetBounds(glyph));
                return metrics;
            }
        }
    }
}
