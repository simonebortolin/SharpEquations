namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Interactivity;
    using global::Avalonia.Layout;
    using global::Avalonia.Styling;
    using SharpEquations.MathTools;

    /// <summary>
    /// The view of the math tools (<see cref="MathToolsViewModel"/>): a header and a page for each tool. The desktop
    /// window and the WebAssembly page show the same view.
    /// </summary>
    public sealed partial class MathToolsView : UserControl
    {
        private const string RepositoryUrl = "https://github.com/simonebortolin/SharpEquations";

        public MathToolsView() => InitializeComponent();

        /// <param name="settings">Where the settings are kept; in memory by default.</param>
        /// <param name="engine">The computer algebra system; AngouriMath by default.</param>
        public MathToolsView(ISettingsStore? settings, ICasEngine? engine = null)
            : this()
        {
            DataContext = new MathToolsViewModel(settings, engine);
        }

        /// <summary>The view model: the tools, and the settings of the session.</summary>
        public MathToolsViewModel? ViewModel => DataContext as MathToolsViewModel;

        /// <summary>The tabs of the pages.</summary>
        public TabControl Tabs => Pages;

        /// <summary>The page of the scientific keypad.</summary>
        public CalculatorPage Calculator => CalculatorView;

        /// <summary>The page of the library of formulas.</summary>
        public FormulasPage FormulaLibrary => FormulasView;

        /// <summary>The page of the unit converter.</summary>
        public UnitsPage Converter => UnitsView;

        /// <summary>The page of the solver of triangles.</summary>
        public TrianglePage TriangleSolver => TrianglesView;

        /// <summary>The page of the graphs.</summary>
        public GraphsPage Graphs => GraphsView;

        /// <summary>The page of the graphs in space.</summary>
        public Graphs3DPage Space => SpaceView;

        /// <summary>The page of the worksheet.</summary>
        public WorksheetPage Worksheet => WorksheetView;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            // the theme of the last session
            if (Application.Current is { } app && ViewModel?.IsDark is { } dark)
                app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private void OnThemeClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is not { } app)
                return;
            var dark = app.ActualThemeVariant != ThemeVariant.Dark;
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            ViewModel?.KeepTheme(dark);
        }

        private async void OnGitHubClick(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
                await launcher.LaunchUriAsync(new Uri(RepositoryUrl)).ConfigureAwait(true);
        }

        // a narrow header has no formula, and its buttons under the title
        private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var narrow = e.NewSize.Width < 640;
            Hero.IsVisible = e.NewSize.Width > 900;
            DockPanel.SetDock(HeaderButtons, narrow ? Dock.Bottom : Dock.Right);
            HeaderButtons.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            HeaderButtons.Margin = narrow ? new Thickness(0, 14, 0, 0) : default;
        }
    }
}
