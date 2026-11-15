namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using global::Avalonia.Markup.Xaml;
    using SharpEquations.MathTools;

    /// <summary>The desktop app: the theme the WebAssembly page has too, and <see cref="MainWindow"/> with its settings in a file.</summary>
    public sealed partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            MathToolsTheme.Apply(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow(new FileSettingsStore(FileSettingsStore.DefaultPath));
            base.OnFrameworkInitializationCompleted();
        }
    }
}
