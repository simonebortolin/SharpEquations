namespace SharpEquations.Wpf
{
    using System;
    using System.IO;
    using SharpEquations.Rendering;

    /// <summary>
    /// The math font of the controls: Libertinus Math (SIL Open Font License), embedded in this assembly and read from its
    /// file, so that it is the same on every system; its glyphs are drawn as geometries from their outlines.
    /// </summary>
    public static class MathFonts
    {
        private static readonly Lazy<OpenTypeMathFont> Libertinus = new Lazy<OpenTypeMathFont>(() =>
        {
            using var stream = typeof(MathFonts).Assembly.GetManifestResourceStream("SharpEquations.Wpf.LibertinusMath-Regular.otf")
                ?? throw new InvalidOperationException("The embedded Libertinus Math font is missing.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return new OpenTypeMathFont(copy.ToArray());
        });

        /// <summary>The embedded Libertinus Math.</summary>
        public static OpenTypeMathFont LibertinusMath => Libertinus.Value;

        /// <summary>The default math font: Libertinus Math.</summary>
        public static OpenTypeMathFont Default => Libertinus.Value;

        /// <summary>A math font read from the bytes of its file (an OpenType font with a MATH table), for <c>MathFont</c>.</summary>
        /// <exception cref="FormatException">The bytes are not an OpenType font, or it has no MATH table.</exception>
        public static OpenTypeMathFont FromFile(byte[] data)
        {
            var font = new OpenTypeMathFont(data);
            return font.Math != null ? font : throw new FormatException("The font has no MATH table.");
        }

        /// <summary>
        /// A math font read from its file (an OpenType font with a MATH table), for <c>MathFont</c>; of a collection, the
        /// face with the MATH table: Cambria Math of <c>C:\Windows\Fonts\cambria.ttc</c>.
        /// </summary>
        /// <exception cref="FormatException">The file is not an OpenType font, or it has no MATH table.</exception>
        public static OpenTypeMathFont FromFile(string path) => FromFile(File.ReadAllBytes(path));
    }
}
