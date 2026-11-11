namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using global::Avalonia.Media;
    using SharpEquations.Rendering;
    using SharpEquations.Rendering.OpenType;

    /// <summary>
    /// A font of Avalonia for the layout of equations. Its glyphs, advances, ink boxes, MATH table and script variants
    /// are read from a copy of the font file, once; the glyph typeface of Avalonia is asked for only to draw, each time
    /// (<see cref="GetGlyphTypeface"/>), since one held for long can be released by Avalonia, its memory given to another
    /// font.
    /// </summary>
    public sealed class AvaloniaMathFont : IMathFont
    {
        private readonly Dictionary<string, AvaloniaMathFont?> fallbacks = new Dictionary<string, AvaloniaMathFont?>(StringComparer.Ordinal);
        private readonly FontFile file;
        private readonly Lazy<MathTable?> math;
        private readonly Lazy<GlyphOutlines?> outlines;
        private readonly Lazy<ScriptStyles?> scriptStyles;
        private readonly string familyName;

        /// <summary>The font of a typeface.</summary>
        /// <exception cref="ArgumentException">The typeface is not installed, or its font file cannot be read.</exception>
        public AvaloniaMathFont(Typeface typeface)
        {
            Typeface = typeface;
            if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphs) || ReadFile(glyphs) is not { } read)
                throw new ArgumentException("The font of the typeface cannot be read: " + typeface.FontFamily, nameof(typeface));
            file = read;
            familyName = glyphs.FamilyName;
            UnitsPerEm = file.UnitsPerEm;
            math = new Lazy<MathTable?>(() => file.Table("MATH") is { } table ? TryParse(table) : null);
            scriptStyles = new Lazy<ScriptStyles?>(() => ScriptStyles.Parse(file.Table("GSUB")));
            outlines = new Lazy<GlyphOutlines?>(() => GlyphOutlines.Create(file.Table));
        }

        /// <summary>The typeface.</summary>
        public Typeface Typeface { get; }

        /// <inheritdoc/>
        public double UnitsPerEm { get; }

        /// <inheritdoc/>
        public MathTable? Math => math.Value;

        /// <summary>The glyph typeface of Avalonia to draw with, as Avalonia has it now; null if it no longer has the font.</summary>
        public GlyphTypeface? GetGlyphTypeface()
        {
            MathFonts.Register();
            return FontManager.Current.TryGetGlyphTypeface(Typeface, out var glyphs) ? glyphs : null;
        }

        /// <inheritdoc/>
        public bool TryGetGlyph(int codePoint, out ushort glyph) => file.TryGetGlyph(codePoint, out glyph);

        /// <inheritdoc/>
        public double GetAdvance(ushort glyph) => file.Advance(glyph);

        /// <inheritdoc/>
        public GlyphBounds GetBounds(ushort glyph) => outlines.Value?.Bounds(glyph) ?? LineBounds(glyph);

        /// <summary>
        /// The box of a glyph of a font without outlines (the bitmaps of a color emoji font, <c>CBDT</c>), which has no
        /// ink box to read: its advance across, and the ascent and descent of the font.
        /// </summary>
        private GlyphBounds LineBounds(ushort glyph)
        {
            if (file.Table("hhea") is not { Length: >= 8 } hhea)
                return default;
            var ascent = (short)(hhea[4] << 8 | hhea[5]);
            var descent = (short)(hhea[6] << 8 | hhea[7]);
            return new GlyphBounds(0, descent, file.Advance(glyph), ascent);
        }

        /// <inheritdoc/>
        public ushort GetScriptVariant(ushort glyph, int scriptLevel) => scriptStyles.Value?.Variant(glyph, scriptLevel) ?? glyph;

        /// <inheritdoc/>
        public IMathFont? GetFallback(int codePoint)
        {
            if (!FontManager.Current.TryMatchCharacter(codePoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, null, null, out var typeface))
                return null;
            var key = typeface.FontFamily.Name;
            lock (fallbacks)
            {
                if (!fallbacks.TryGetValue(key, out var font))
                {
                    try
                    {
                        font = new AvaloniaMathFont(typeface);
                        if (font.familyName == familyName)
                            font = null;
                    }
                    catch (ArgumentException)
                    {
                        font = null;
                    }
                    fallbacks[key] = font;
                }
                return font;
            }
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

        private static FontFile? ReadFile(GlyphTypeface glyphs)
        {
            if (!glyphs.PlatformTypeface.TryGetStream(out var stream) || stream == null)
                return null;
            using (stream)
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                try
                {
                    return FontFile.Parse(copy.ToArray(), glyphs.GlyphCount);
                }
                catch (FormatException)
                {
                    return null;
                }
            }
        }
    }
}
