namespace SharpEquations.Avalonia.Tests.Controls
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Headless;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using SharpEquations;

    /// <summary>
    /// Pictures of equations, written to the directory of SHARPEQUATIONS_SCREENSHOTS when it is set, to look at the
    /// rendering; without it the test only renders them.
    /// </summary>
    [NotInParallel("headless")]
    public class ScreenshotTests
    {
        private static readonly string[] Samples =
        {
            "x=(-b±√(b^2-4ac))/2a",
            "∑_(i=1)^n i^2=(n(n+1)(2n+1))/6",
            "∫_0^∞ e^(-x^2) ⅆx=√π/2",
            "(■(a&b@c&d))^(-1)=1/(ad-bc) (■(d&-b@-c&a))",
            "lim▒_(x→0) sinx/x=1",
            "f(x)={■(x&x≥0@-x&x<0)┤",
            "L:\\hat{a}+\\tilde{b}+\\overline{a+b}+\\vec{x}+\\underline{y}",
            "L:\\sqrt[3]{x^3+y^3}+\\overbrace{a+b+c}^{n}",
            "L:\\binom{n}{k}=\\frac{n!}{k!(n-k)!}",
            "|x|+‖y‖+⌊z⌋+⟨ψ|φ⟩",
            "█(f(x)&=(x+1)^2@&=x^2+2x+1)",
            "E=mc^2#(1)",
        };

        [Test]
        public async Task RenderSamples()
        {
            var directory = Environment.GetEnvironmentVariable("SHARPEQUATIONS_SCREENSHOTS");
            var failures = await Headless.Run(() =>
            {
                var panel = new StackPanel { Spacing = 12, Margin = new global::Avalonia.Thickness(12) };
                foreach (var sample in Samples)
                    panel.Children.Add(new MathView { Equation = Equation.Parse(sample.StartsWith("L:", StringComparison.Ordinal) ? sample.Substring(2) : sample, sample.StartsWith("L:", StringComparison.Ordinal) ? EquationFormat.LaTeX : EquationFormat.UnicodeMath, new EquationOptions { Display = true }), FontSize = 28, Foreground = Brushes.Black });
                var editor = new MathEditBox { FontSize = 28, Foreground = Brushes.Black, BorderBrush = Brushes.SteelBlue, HorizontalAlignment = HorizontalAlignment.Left };
                panel.Children.Add(editor);
                var window = new Window { Content = panel, Width = 900, Height = 1100, Background = Brushes.White };
                window.Show();
                editor.Focus();
                window.KeyTextInput("a/b+\\sqrt(x^2+1) +(");

                // empty slots, and a selection
                var slots = new MathEditBox { FontSize = 28, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left };
                panel.Children.Add(slots);
                var empty = SharpEquations.Editing.MathNode.Row(System.Array.Empty<SharpEquations.Editing.MathNode>());
                slots.Editor.InsertNodes(new[] { SharpEquations.Editing.MathNode.Element("mfrac", new[] { empty, empty }), SharpEquations.Editing.MathNode.Token("mo", "+"), SharpEquations.Editing.MathNode.Element("msup", new[] { empty, empty }) });
                var selected = new MathEditBox { FontSize = 28, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left, Equation = Equation.Parse("x+(a+b)/c", EquationFormat.UnicodeMath) };
                panel.Children.Add(selected);
                selected.Editor.SetCaret(new SharpEquations.Editing.MathPosition(new[] { 0 }, 2));
                selected.Editor.SetCaret(new SharpEquations.Editing.MathPosition(new[] { 0 }, 3), extend: true);
                var linear = new MathEditBox { FontSize = 28, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left, Equation = Equation.Parse("x+(a+b)/c", EquationFormat.UnicodeMath), ViewMode = MathViewMode.Linear };
                panel.Children.Add(linear);
                editor.Focus();
                var frame = window.CaptureRenderedFrame();
                if (directory != null && frame != null)
                {
                    Directory.CreateDirectory(directory);
                    frame.Save(Path.Combine(directory, "samples.png"), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                window.Close();
                return frame == null ? 1 : 0;
            });
            await Assert.That(failures).IsEqualTo(0);
        }
    }
}
