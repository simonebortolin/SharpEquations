namespace SharpEquations.Avalonia.Demo
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using global::Avalonia.Themes.Fluent;

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
    }

    public sealed class App : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
