namespace SharpEquations.Avalonia.MathTools
{
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Browser;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using global::Avalonia.Media;

    [SupportedOSPlatform("browser")]
    public static class Program
    {
        public static Task Main() => BuildAvaloniaApp().StartBrowserAppAsync("out");

        // A page has no fonts of its own: Inter for the text, and the embedded Libertinus Math for the symbols it lacks.
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "fonts:Inter#Inter",
                FontFallbacks = new[] { new FontFallback { FontFamily = new FontFamily("fonts:SharpEquations#Libertinus Math") } },
            });
    }

    public sealed class App : Application
    {
        public override void Initialize() => MathToolsTheme.Apply(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is ISingleViewApplicationLifetime view)
                view.MainView = new MathToolsView(new BrowserSettingsStore());
            base.OnFrameworkInitializationCompleted();
        }
    }
}
