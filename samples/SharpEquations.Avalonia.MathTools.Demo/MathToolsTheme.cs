namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;
    using global::Avalonia.Themes.Fluent;

    /// <summary>The Fluent theme of the app, with a teal accent in the light and the dark variant.</summary>
    public static class MathToolsTheme
    {
        public static void Apply(Application application)
        {
            application.Styles.Add(new FluentTheme
            {
                Palettes =
                {
                    [ThemeVariant.Light] = new ColorPaletteResources { Accent = Color.FromRgb(0x0F, 0x76, 0x6E) },
                    [ThemeVariant.Dark] = new ColorPaletteResources { Accent = Color.FromRgb(0x2D, 0xD4, 0xBF) },
                },
            });
        }
    }
}
