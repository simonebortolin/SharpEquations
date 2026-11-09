namespace SharpEquations.Avalonia.Demo
{
    using global::Avalonia;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;
    using global::Avalonia.Themes.Fluent;

    /// <summary>The Fluent theme of the demos, with an indigo accent in the light and the dark variant.</summary>
    public static class DemoTheme
    {
        public static void Apply(Application application)
        {
            application.Styles.Add(new FluentTheme
            {
                Palettes =
                {
                    [ThemeVariant.Light] = new ColorPaletteResources { Accent = Color.FromRgb(0x4F, 0x46, 0xE5) },
                    [ThemeVariant.Dark] = new ColorPaletteResources { Accent = Color.FromRgb(0x81, 0x8C, 0xF8) },
                },
            });
        }
    }
}
