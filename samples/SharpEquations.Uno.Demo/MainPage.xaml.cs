namespace SharpEquations.Uno.Demo
{
    using Microsoft.UI.Xaml.Controls;
    using SharpEquations;

    /// <summary>A page with the editor, the equation as UnicodeMath and LaTeX, and a view of it.</summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            if (System.Environment.GetEnvironmentVariable("SHARPEQUATIONS_DEMO_EQUATION") is { Length: > 0 } initial)
                Editor.Equation = Equation.Parse(initial, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            Editor.Loaded += (_, _) => Editor.FocusEditor();
        }

        private void OnEquationChanged(object? sender, EquationChangedEventArgs e)
        {
            UnicodeMath.Text = "UnicodeMath: " + (e.Equation?.ToString(EquationFormat.UnicodeMath) ?? string.Empty);
            LaTeX.Text = "LaTeX: " + (e.Equation?.ToString(EquationFormat.LaTeX) ?? string.Empty);
        }
    }
}
