namespace SharpEquations.Avalonia
{
    using System;
    using System.Runtime.InteropServices;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Fonts;

    /// <summary>
    /// The math fonts of the controls: Cambria Math on Windows, where it is a font of the system, and Libertinus Math
    /// (SIL Open Font License), embedded in this assembly, everywhere else.
    /// </summary>
    public static class MathFonts
    {
        private const string CollectionKey = "fonts:SharpEquations";
        private static readonly object Gate = new object();
        private static bool registered;
        private static AvaloniaMathFont? defaultFont;

        /// <summary>The family of the embedded Libertinus Math.</summary>
        public static FontFamily LibertinusMath
        {
            get
            {
                Register();
                return new FontFamily(CollectionKey + "#Libertinus Math");
            }
        }

        /// <summary>The default math font: Cambria Math on Windows when it is installed, Libertinus Math otherwise.</summary>
        public static AvaloniaMathFont Default
        {
            get
            {
                lock (Gate)
                {
                    if (defaultFont != null)
                        return defaultFont;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TryGet(new FontFamily("Cambria Math"), out var cambria))
                        defaultFont = cambria;
                    else if (TryGet(LibertinusMath, out var libertinus))
                        defaultFont = libertinus;
                    else
                        throw new InvalidOperationException("The embedded Libertinus Math font could not be loaded.");
                    return defaultFont!;
                }
            }
        }

        /// <summary>The math font of a family, if it is installed and has an OpenType MATH table.</summary>
        public static bool TryGet(FontFamily family, out AvaloniaMathFont? font)
        {
            font = null;
            if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyphs))
                return false;
            var candidate = new AvaloniaMathFont(glyphs);
            if (candidate.Math == null)
                return false;
            font = candidate;
            return true;
        }

        /// <summary>The math font of a family, or the default one if the family is null or not a math font.</summary>
        public static AvaloniaMathFont Get(FontFamily? family) => family != null && TryGet(family, out var font) ? font! : Default;

        private static void Register()
        {
            lock (Gate)
            {
                if (registered)
                    return;
                FontManager.Current.AddFontCollection(new EmbeddedFontCollection(
                    new Uri(CollectionKey, UriKind.Absolute),
                    new Uri("avares://SharpEquations.Avalonia/Assets/Fonts", UriKind.Absolute)));
                registered = true;
            }
        }
    }
}
