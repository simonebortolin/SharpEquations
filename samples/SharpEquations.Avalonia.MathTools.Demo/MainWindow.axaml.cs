namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia.Controls;
    using SharpEquations.MathTools;

    /// <summary>The window of the desktop app: the view the WebAssembly page shows too, with the settings kept where the store says.</summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
            : this(null)
        {
        }

        /// <param name="settings">Where the settings are kept; in memory by default.</param>
        public MainWindow(ISettingsStore? settings)
        {
            InitializeComponent();
            DataContext = new MathToolsViewModel(settings);
        }
    }
}
