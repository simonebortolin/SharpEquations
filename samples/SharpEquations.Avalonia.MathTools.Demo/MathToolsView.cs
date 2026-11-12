namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Globalization;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The math tools, a page each: the scientific keypad around the equation editor, the library of formulas, the
    /// unit converter, the solver of triangles, the graphs and the worksheet. The desktop window and the WebAssembly page show the same view, each
    /// with the services of its platform: where the settings are kept (the page shown, the equation of the calculator,
    /// the unit of angles, the theme), and the computer algebra system.
    /// </summary>
    public sealed class MathToolsView : UserControl
    {
        private const string RepositoryUrl = "https://github.com/simonebortolin/SharpEquations";

        private const int GraphsTab = 4;

        private const string TabKey = "tab";
        private const string EquationKey = "equation";
        private const string ThemeKey = "theme";
        private const string AnglesKey = "angles";

        private readonly ISettingsStore settings;

        public MathToolsView(ISettingsStore? settings = null, ICasEngine? engine = null)
        {
            this.settings = settings ?? new MemorySettingsStore();
            Engine = engine ?? new AngouriMathEngine();
            Ui.AddBrushes(Resources);
            Calculator = new CalculatorPage(Engine);
            Converter = new UnitsPage();
            TriangleSolver = new TrianglePage();
            Graphs = new GraphsPage(Engine);
            Worksheet = new WorksheetPage(Engine, this.settings);
            Tabs = new TabControl { Margin = new Thickness(20, 12, 20, 0) };
            FormulaLibrary = new FormulasPage(equation =>
            {
                Calculator.Editor.Equation = equation;
                Tabs.SelectedIndex = 0;
                Calculator.Editor.Focus();
            }, preset =>
            {
                Graphs.AddPreset(preset, Engine.Angles);
                Tabs.SelectedIndex = GraphsTab;
            });
            Tabs.ItemsSource = new[]
            {
                Tab("Calculator", Calculator),
                Tab("Formulas", FormulaLibrary),
                Tab("Units", Converter),
                Tab("Triangles", TriangleSolver),
                Tab("Graphs", Graphs),
                Tab("Worksheet", Worksheet),
            };
            Calculator.Solver.GraphRequested += (_, equation) => ShowGraph(equation);
            Worksheet.GraphRequested += (_, equation) => ShowGraph(equation);

            Restore();
            Tabs.SelectionChanged += (_, _) => this.settings.Set(TabKey, Tabs.SelectedIndex.ToString(CultureInfo.InvariantCulture));
            Calculator.Solver.Angles.SelectionChanged += (_, _) => this.settings.Set(AnglesKey, Calculator.Solver.Angles.SelectedIndex == 1 ? "degrees" : "radians");
            Calculator.Editor.EquationChanged += (_, _) => this.settings.Set(EquationKey, Calculator.Editor.Equation?.ToString(EquationFormat.UnicodeMath));

            var root = new DockPanel { Children = { Ui.Docked(Header(), Dock.Top), Tabs } };
            root.Bind(Panel.BackgroundProperty, root.GetResourceObservable("PageBrush"));
            Content = root;
        }

        /// <summary>The worksheet, the history of what was computed.</summary>
        public WorksheetPage Worksheet { get; }

        /// <summary>The computer algebra system of the calculator.</summary>
        public ICasEngine Engine { get; }

        /// <summary>The page of the scientific keypad.</summary>
        public CalculatorPage Calculator { get; }

        /// <summary>The page of the library of formulas.</summary>
        public FormulasPage FormulaLibrary { get; }

        /// <summary>The page of the unit converter.</summary>
        public UnitsPage Converter { get; }

        /// <summary>The page of the solver of triangles.</summary>
        public TrianglePage TriangleSolver { get; }

        /// <summary>The page of the graphs.</summary>
        public GraphsPage Graphs { get; }

        /// <summary>The tabs of the pages.</summary>
        public TabControl Tabs { get; }

        // the page, the equation and the theme of the last session
        private void Restore()
        {
            if (int.TryParse(settings.Get(TabKey), NumberStyles.None, CultureInfo.InvariantCulture, out var tab) && tab < Tabs.ItemCount)
                Tabs.SelectedIndex = tab;
            if (settings.Get(EquationKey) is { } text && Equation.TryParse(text, EquationFormat.UnicodeMath, new EquationOptions { Display = true }, out var equation))
                Calculator.Editor.Equation = equation;
            if (settings.Get(AnglesKey) == "degrees")
                Calculator.Solver.Angles.SelectedIndex = 1;
            if (Application.Current is { } app && settings.Get(ThemeKey) is { } theme)
                app.RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private void ShowGraph(Equation equation)
        {
            Graphs.AddEquation(equation, Engine.Angles, Engine.Definitions);
            Tabs.SelectedIndex = GraphsTab;
        }

        private static TabItem Tab(string header, Control page) => new TabItem
        {
            Header = new TextBlock { Text = header, FontSize = 15, FontWeight = FontWeight.SemiBold },
            Content = new ScrollViewer
            {
                Content = new Border { Padding = new Thickness(0, 12, 0, 32), MaxWidth = 1180, Child = page },
                HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            },
        };

        private Border Header()
        {
            var title = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "SharpEquations Math Tools", FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                    new TextBlock
                    {
                        Text = "A scientific keypad, formulas, units, triangles, graphs and a worksheet around a real-time equation editor in .NET.",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.White, 0.85),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            };

            var hero = new MathView
            {
                FontSize = 24,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24, 0),
                Equation = Equation.Parse("∑_(k=1)^∞ 1/k^2=π^2/6", EquationFormat.UnicodeMath, new EquationOptions { Display = true }),
            };

            var theme = HeaderButton("Theme", "Light or dark theme");
            theme.Click += (_, _) =>
            {
                if (Application.Current is { } app)
                {
                    var dark = app.ActualThemeVariant != ThemeVariant.Dark;
                    app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
                    settings.Set(ThemeKey, dark ? "dark" : "light");
                }
            };
            var github = HeaderButton("GitHub ↗", RepositoryUrl);
            github.Click += async (_, _) =>
            {
                if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
                    await launcher.LaunchUriAsync(new Uri(RepositoryUrl)).ConfigureAwait(true);
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { theme, github } };
            var bar = new DockPanel { Children = { Ui.Docked(buttons, Dock.Right), Ui.Docked(hero, Dock.Right), title } };
            bar.SizeChanged += (_, e) =>
            {
                var narrow = e.NewSize.Width < 640;
                hero.IsVisible = e.NewSize.Width > 900;
                DockPanel.SetDock(buttons, narrow ? Dock.Bottom : Dock.Right);
                buttons.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                buttons.Margin = narrow ? new Thickness(0, 14, 0, 0) : default;
            };

            return new Border
            {
                Padding = new Thickness(28, 20),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(0x0F, 0x76, 0x6E), 0),
                        new GradientStop(Color.FromRgb(0x08, 0x91, 0xB2), 0.55),
                        new GradientStop(Color.FromRgb(0x25, 0x63, 0xEB), 1),
                    },
                },
                Child = bar,
            };
        }

        private static Button HeaderButton(string text, string tip)
        {
            var button = new Button
            {
                Content = text,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Colors.White, 0.16),
                BorderBrush = new SolidColorBrush(Colors.White, 0.35),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14, 6),
            };
            ToolTip.SetTip(button, tip);
            return button;
        }
    }
}
