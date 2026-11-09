namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Fonts;
    using SharpEquations.Rendering;
    using SharpEquations.Rendering.OpenType;

    /// <summary>A font of Avalonia for the layout of equations: its glyphs, metrics and OpenType MATH table.</summary>
    public sealed class AvaloniaMathFont : IMathFont
    {
        private readonly Dictionary<ushort, double> advances = new Dictionary<ushort, double>();
        private readonly Dictionary<string, AvaloniaMathFont?> fallbacks = new Dictionary<string, AvaloniaMathFont?>(StringComparer.Ordinal);
        private readonly Lazy<MathTable?> math;
        private readonly Lazy<GlyphOutlines?> outlines;
        private readonly Lazy<FontFile?> file;

        /// <summary>A font of a glyph typeface.</summary>
        public AvaloniaMathFont(GlyphTypeface typeface)
        {
            Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
            file = new Lazy<FontFile?>(ReadFile);
            math = new Lazy<MathTable?>(ReadMath);
            outlines = new Lazy<GlyphOutlines?>(() => GlyphOutlines.Create(Table));
        }

        /// <summary>The glyph typeface.</summary>
        public GlyphTypeface Typeface { get; }

        /// <inheritdoc/>
        public double UnitsPerEm => Typeface.Metrics.DesignEmHeight;

        /// <inheritdoc/>
        public MathTable? Math => math.Value;

        /// <inheritdoc/>
        public bool TryGetGlyph(int codePoint, out ushort glyph) => Typeface.CharacterToGlyphMap.TryGetGlyph(codePoint, out glyph) && glyph != 0;

        /// <inheritdoc/>
        public double GetAdvance(ushort glyph)
        {
            if (!advances.TryGetValue(glyph, out var advance))
            {
                advance = Typeface.TryGetHorizontalGlyphAdvance(glyph, out var a) ? a : 0;
                advances[glyph] = advance;
            }
            return advance;
        }

        /// <inheritdoc/>
        public GlyphBounds GetBounds(ushort glyph) => outlines.Value?.Bounds(glyph) ?? default;

        /// <inheritdoc/>
        public IMathFont? GetFallback(int codePoint)
        {
            if (!FontManager.Current.TryMatchCharacter(codePoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, null, null, out var typeface))
                return null;
            var key = typeface.FontFamily.Name;
            if (!fallbacks.TryGetValue(key, out var font))
            {
                font = FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphs) && glyphs.FamilyName != Typeface.FamilyName
                    ? new AvaloniaMathFont(glyphs)
                    : null;
                fallbacks[key] = font;
            }
            return font;
        }

        /// <summary>
        /// A table of the font, from a copy of the font file: the memory of the tables that Avalonia gives is not valid
        /// for long. The ink boxes come from the outlines of the font rather than from the rasterizer, whose bounds of a
        /// glyph run can be empty (and the metrics of Avalonia are, for CFF outlines).
        /// </summary>
        private byte[]? Table(string tag) => file.Value?.Table(tag);

        private FontFile? ReadFile()
        {
            if (!Typeface.PlatformTypeface.TryGetStream(out var stream) || stream == null)
                return null;
            using (stream)
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                try
                {
                    return FontFile.Parse(copy.ToArray(), Typeface.GlyphCount);
                }
                catch (FormatException)
                {
                    return null;
                }
            }
        }

        private MathTable? ReadMath()
        {
            if (Table("MATH") is not { } table)
                return null;
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
