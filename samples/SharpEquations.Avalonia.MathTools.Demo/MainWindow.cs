namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia.Controls;

    /// <summary>The window of the desktop app: the view the WebAssembly page shows too.</summary>
    public sealed class MainWindow : Window
    {
        public MainWindow()
        {
            Title = "SharpEquations Math Tools";
            Width = 1180;
            Height = 860;
            Content = new MathToolsView();
        }
    }
}
