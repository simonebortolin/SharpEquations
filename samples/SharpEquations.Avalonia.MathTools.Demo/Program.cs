namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using SharpEquations.MathTools;

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
    }

    public sealed class App : Application
    {
        public override void Initialize() => MathToolsTheme.Apply(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow(new FileSettingsStore(FileSettingsStore.DefaultPath));
            base.OnFrameworkInitializationCompleted();
        }
    }
}
