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
    /// are read from a copy of the font file, once (<see cref="OpenTypeMathFont"/>); the glyph typeface of Avalonia is
    /// asked for only to draw, each time (<see cref="GetGlyphTypeface"/>), since one held for long can be released by
    /// Avalonia, its memory given to another font.
    /// </summary>
    public sealed class AvaloniaMathFont : OpenTypeMathFont
    {
        private readonly Dictionary<string, AvaloniaMathFont?> fallbacks = new Dictionary<string, AvaloniaMathFont?>(StringComparer.Ordinal);
        private readonly Dictionary<int, AvaloniaMathFont?> fallbackOf = new Dictionary<int, AvaloniaMathFont?>();
        private readonly string familyName;

        /// <summary>The font of a typeface.</summary>
        /// <exception cref="ArgumentException">The typeface is not installed, or its font file cannot be read.</exception>
        public AvaloniaMathFont(Typeface typeface)
            : this(typeface, Read(typeface))
        {
        }

        private AvaloniaMathFont(Typeface typeface, (FontFile File, string FamilyName) read)
            : base(read.File)
        {
            Typeface = typeface;
            familyName = read.FamilyName;
        }

        /// <summary>The typeface.</summary>
        public Typeface Typeface { get; }

        /// <summary>The glyph typeface of Avalonia to draw with, as Avalonia has it now; null if it no longer has the font.</summary>
        public GlyphTypeface? GetGlyphTypeface()
        {
            MathFonts.Register();
            return FontManager.Current.TryGetGlyphTypeface(Typeface, out var glyphs) ? glyphs : null;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The font Avalonia matches to a code point can change with the fonts it has loaded since: the first one is
        /// kept, so that a character is drawn with one font, whatever was laid out before.
        /// </remarks>
        public override IMathFont? GetFallback(int codePoint)
        {
            lock (fallbacks)
            {
                if (!fallbackOf.TryGetValue(codePoint, out var font))
                    fallbackOf[codePoint] = font = MatchFallback(codePoint);
                return font;
            }
        }

        private AvaloniaMathFont? MatchFallback(int codePoint)
        {
            if (!FontManager.Current.TryMatchCharacter(codePoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, null, null, out var typeface))
                return null;
            var key = typeface.FontFamily.Name;
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

        private static (FontFile File, string FamilyName) Read(Typeface typeface)
        {
            if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphs) || ReadFile(glyphs) is not { } read)
                throw new ArgumentException("The font of the typeface cannot be read: " + typeface.FontFamily, nameof(typeface));
            return (read, glyphs.FamilyName);
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
