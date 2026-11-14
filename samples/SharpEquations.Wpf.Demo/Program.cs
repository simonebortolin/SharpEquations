namespace SharpEquations.Wpf.Demo
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using SharpEquations;

    /// <summary>A window with the editor, the equation as UnicodeMath and LaTeX, and a view of it.</summary>
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var editor = new MathEditBox { FontSize = 28, Margin = new Thickness(0, 0, 0, 12) };
            var unicodeMath = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var latex = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var view = new MathView { FontSize = 22, IsDisplay = true, HorizontalAlignment = HorizontalAlignment.Left };
            editor.EquationChanged += (_, e) =>
            {
                unicodeMath.Text = "UnicodeMath: " + (e.Equation?.ToString(EquationFormat.UnicodeMath) ?? string.Empty);
                latex.Text = "LaTeX: " + (e.Equation?.ToString(EquationFormat.LaTeX) ?? string.Empty);
                view.Equation = e.Equation;
            };
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Type UnicodeMath or LaTeX: a space or an operator builds it up.", Opacity = 0.7, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(editor);
            panel.Children.Add(unicodeMath);
            panel.Children.Add(latex);
            panel.Children.Add(new TextBlock { Text = "MathView:", Opacity = 0.7, Margin = new Thickness(0, 8, 0, 4) });
            panel.Children.Add(view);
            if (Environment.GetEnvironmentVariable("SHARPEQUATIONS_DEMO_EQUATION") is { Length: > 0 } initial)
                editor.Equation = Equation.Parse(initial, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            var window = new Window
            {
                Title = "SharpEquations for WPF",
                Width = 800,
                Height = 500,
                Background = Brushes.White,
                Content = panel,
            };
            editor.Loaded += (_, _) => editor.Focus();
            new Application().Run(window);
        }
    }
}
