namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Immutable;
    using global::Avalonia.Styling;
    using SharpEquations;

    /// <summary>The pieces of the pages: cards, labels, rows, and the brushes of the light and the dark theme.</summary>
    internal static class Ui
    {
        public static void AddBrushes(IResourceDictionary resources)
        {
            resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
            {
                ["PageBrush"] = Solid(0xF2F6F5),
                ["CardBrush"] = Solid(0xFFFFFF),
                ["CardBorderBrush"] = Solid(0xDDE7E5),
                ["MutedBrush"] = Solid(0x64748B),
                ["EditorBrush"] = Solid(0xFBFEFD),
                ["CodeBrush"] = Solid(0xF4F8F7),
                ["KeyBrush"] = Solid(0xF1F5F4),
                ["DigitBrush"] = Solid(0xFFFFFF),
                ["EditorBorderBrush"] = Solid(0x14B8A6),
            };
            resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
            {
                ["PageBrush"] = Solid(0x0B1113),
                ["CardBrush"] = Solid(0x141C1F),
                ["CardBorderBrush"] = Solid(0x24302F),
                ["MutedBrush"] = Solid(0x94A3B8),
                ["EditorBrush"] = Solid(0x0F1618),
                ["CodeBrush"] = Solid(0x0F1517),
                ["KeyBrush"] = Solid(0x1A2427),
                ["DigitBrush"] = Solid(0x222E31),
                ["EditorBorderBrush"] = Solid(0x0D9488),
            };
        }

        public static Border Card(string title, string subtitle, Control content)
        {
            var heading = new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold };
            var caption = Muted(subtitle, 12.5);
            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22, 18, 22, 22),
                BoxShadow = BoxShadows.Parse("0 6 28 0 #12000000"),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children = { new StackPanel { Spacing = 2, Children = { heading, caption } }, content },
                },
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CardBrush"));
            card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("CardBorderBrush"));
            return card;
        }

        public static TextBlock Muted(string text, double size = 12)
        {
            var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
            block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("MutedBrush"));
            return block;
        }

        public static TextBlock Small(string text) => new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeight.SemiBold };

        public static MathView Math(string unicodeMath, double size, bool display = false) => new MathView
        {
            FontSize = size,
            IsHitTestVisible = false,
            Equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = display }),
        };

        public static Control Docked(Control control, Dock dock)
        {
            DockPanel.SetDock(control, dock);
            return control;
        }

        public static WrapPanel Row(params Control[] children)
        {
            var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
            foreach (var child in children)
            {
                child.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(child);
            }
            return row;
        }

        public static ImmutableSolidColorBrush Solid(uint rgb) => new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000 | rgb));
    }
}
