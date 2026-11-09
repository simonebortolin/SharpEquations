namespace SharpEquations.Avalonia.Demo
{
    using System;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Fonts;

    /// <summary>The fonts embedded in the demo: STIX Two Math (SIL Open Font License, Assets/Fonts/OFL.txt).</summary>
    public static class DemoFonts
    {
        private const string CollectionKey = "fonts:SharpEquationsDemo";
        private static FontManager? registeredOn;

        public static FontFamily StixTwoMath
        {
            get
            {
                var manager = FontManager.Current;
                if (!ReferenceEquals(registeredOn, manager))
                {
                    var assembly = typeof(DemoFonts).Assembly.GetName().Name;
                    manager.AddFontCollection(new EmbeddedFontCollection(
                        new Uri(CollectionKey, UriKind.Absolute),
                        new Uri($"avares://{assembly}/Assets/Fonts", UriKind.Absolute)));
                    registeredOn = manager;
                }
                return new FontFamily(CollectionKey + "#STIX Two Math");
            }
        }
    }
}
