namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia.Controls;
    using SharpEquations.MathTools;

    /// <summary>The window of the desktop app: the view the WebAssembly page shows too, with the settings kept where the store says (none by default).</summary>
    public sealed class MainWindow : Window
    {
        public MainWindow(ISettingsStore? settings = null)
        {
            Title = "SharpEquations Math Tools";
            Width = 1180;
            Height = 860;
            Content = new MathToolsView(settings);
        }
    }
}
