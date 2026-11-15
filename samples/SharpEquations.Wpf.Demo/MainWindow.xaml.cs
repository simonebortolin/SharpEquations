namespace SharpEquations.Wpf.Demo
{
    using System;
    using System.Windows;
    using SharpEquations;

    /// <summary>A window with the editor, the equation as UnicodeMath and LaTeX, and a view of it.</summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (Environment.GetEnvironmentVariable("SHARPEQUATIONS_DEMO_EQUATION") is { Length: > 0 } initial)
                Editor.Equation = Equation.Parse(initial, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            Editor.Loaded += (_, _) => Editor.Focus();
        }

        private void OnEquationChanged(object? sender, EquationChangedEventArgs e)
        {
            UnicodeMath.Text = "UnicodeMath: " + (e.Equation?.ToString(EquationFormat.UnicodeMath) ?? string.Empty);
            LaTeX.Text = "LaTeX: " + (e.Equation?.ToString(EquationFormat.LaTeX) ?? string.Empty);
        }
    }
}
