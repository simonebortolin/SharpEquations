namespace SharpEquations.Avalonia.Demo
{
    using global::Avalonia;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using global::Avalonia.Markup.Xaml;

    /// <summary>The app of the desktop demo: the theme the WebAssembly demo has too, and <see cref="MainWindow"/>.</summary>
    public sealed partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            DemoTheme.Apply(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
