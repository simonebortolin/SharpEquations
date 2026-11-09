namespace SharpEquations.Avalonia.Demo
{
    using global::Avalonia.Controls;

    /// <summary>The window of the desktop demo: the view the WebAssembly demo shows too.</summary>
    public sealed class MainWindow : Window
    {
        public MainWindow()
        {
            Title = "SharpEquations — equation editor";
            Width = 1240;
            Height = 900;
            Content = new DemoView();
        }
    }
}
