namespace SharpEquations.Avalonia.Demo
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using global::Avalonia.Themes.Fluent;
    using SharpEquations;

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

    /// <summary>
    /// The editor, with what it holds in every format: type UnicodeMath (or LaTeX) and it builds up as in Word;
    /// Ctrl+Shift+L shows it as linear text and back.
    /// </summary>
    public sealed class MainWindow : Window
    {
        public MainWindow()
        {
            Title = "SharpEquations — MathEditBox";
            Width = 900;
            Height = 640;

            var editor = new MathEditBox { FontSize = 30, MinHeight = 80, BorderBrush = Brushes.SteelBlue, Padding = new Thickness(12) };
            var format = new ComboBox { ItemsSource = new[] { "UnicodeMath", "LaTeX" }, SelectedIndex = 0 };
            format.SelectionChanged += (_, _) => editor.InputFormat = format.SelectedIndex == 1 ? EquationFormat.LaTeX : EquationFormat.UnicodeMath;
            var linear = new CheckBox { Content = "Linear (Ctrl+Shift+L)" };
            linear.IsCheckedChanged += (_, _) => editor.ViewMode = linear.IsChecked == true ? MathViewMode.Linear : MathViewMode.Professional;
            var display = new CheckBox { Content = "Display", IsChecked = true };
            display.IsCheckedChanged += (_, _) => editor.IsDisplay = display.IsChecked == true;

            TextBox Output() => new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("monospace"), MinHeight = 32 };
            var mathML = Output();
            var unicodeMath = Output();
            var latex = Output();
            var view = new MathView { FontSize = 22 };

            editor.EquationChanged += (_, e) =>
            {
                linear.IsChecked = editor.ViewMode == MathViewMode.Linear;
                mathML.Text = e.Equation?.ToString(EquationFormat.MathML);
                unicodeMath.Text = e.Equation?.ToString(EquationFormat.UnicodeMath);
                latex.Text = e.Equation == null ? null : $"{e.Equation:L}";
                view.Equation = e.Equation;
            };

            Control Labelled(string label, Control control) => new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontWeight = FontWeight.SemiBold }, control } };

            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { format, linear, display } },
                        editor,
                        new TextBlock { Text = "Type a/b, x^2, \\sqrt(x), \\alpha, (a+b)/c, \\sum_(i=1)^n i, then a space or an operator to build up. Backspace selects a structure, then deletes it.", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 },
                        Labelled("MathView", view),
                        Labelled("MathML", mathML),
                        Labelled("UnicodeMath", unicodeMath),
                        Labelled("LaTeX", latex),
                    },
                },
            };
            Opened += (_, _) => editor.Focus();
        }
    }
}
