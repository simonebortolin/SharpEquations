namespace SharpEquations.Uno.Demo
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using SharpEquations;

    /// <summary>A window with the editor, the equation as MathML, UnicodeMath and LaTeX, and a view of it.</summary>
    public sealed class App : Application
    {
        private Window? window;

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Resources.MergedDictionaries.Add(new XamlControlsResources());
            var editor = new MathEditBox { FontSize = 28, Margin = new Thickness(0, 0, 0, 12) };
            var unicodeMath = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var latex = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var view = new MathView { FontSize = 22, IsDisplay = true };
            editor.EquationChanged += (_, e) =>
            {
                unicodeMath.Text = "UnicodeMath: " + (e.Equation?.ToString(EquationFormat.UnicodeMath) ?? string.Empty);
                latex.Text = "LaTeX: " + (e.Equation?.ToString(EquationFormat.LaTeX) ?? string.Empty);
                view.Equation = e.Equation;
            };
            var panel = new StackPanel { Padding = new Thickness(24), Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "Type UnicodeMath or LaTeX: a space or an operator builds it up.", Opacity = 0.7 });
            panel.Children.Add(editor);
            panel.Children.Add(unicodeMath);
            panel.Children.Add(latex);
            panel.Children.Add(new TextBlock { Text = "MathView:", Opacity = 0.7 });
            panel.Children.Add(view);
            if (System.Environment.GetEnvironmentVariable("SHARPEQUATIONS_DEMO_EQUATION") is { Length: > 0 } initial)
                editor.Equation = Equation.Parse(initial, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            window = new Window { Content = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.White), Children = { panel } } };
            window.Activate();
            editor.Loaded += (_, _) => editor.FocusEditor();
        }
    }
}
