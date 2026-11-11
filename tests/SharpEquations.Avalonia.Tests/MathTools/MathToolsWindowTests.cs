#if NET10_0_OR_GREATER
namespace SharpEquations.Avalonia.Tests.MathTools
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Headless;
    using global::Avalonia.VisualTree;
    using SharpEquations;
    using SharpEquations.Avalonia.MathTools;

    /// <summary>The window of the math tools opens, and its pages work (a picture of each with SHARPEQUATIONS_SCREENSHOTS).</summary>
    [NotInParallel("headless")]
    public class MathToolsWindowTests
    {
        private static void Save(Window window, string name)
        {
            window.UpdateLayout();
            if (Environment.GetEnvironmentVariable("SHARPEQUATIONS_SCREENSHOTS") is { } directory && window.CaptureRenderedFrame() is { } frame)
            {
                Directory.CreateDirectory(directory);
                frame.Save(Path.Combine(directory, name + ".png"), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        }

        [Test]
        public async Task KeypadTypesIntoTheEditor()
        {
            var (equation, unicodeMath) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                var page = window.GetVisualDescendants().OfType<CalculatorPage>().Single();
                foreach (var face in new[] { "sin", "x", ")", "x^2", "+", "cos", "x", ")", "x^2", "=", "1" })
                    page.Press(CalculatorKeys.Functions.Concat(CalculatorKeys.Numbers).First(k => k.Face == face));
                page.Press(CalculatorKeys.Editing.Single(k => k.Face == "Build up"));
                Save(window, "math-tools-calculator");
                var texts = window.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsReadOnly).Select(t => t.Text).ToList();
                var result = page.Editor.Equation;
                window.Close();
                return (result, texts.FirstOrDefault());
            });
            await Assert.That(equation).IsEqualTo(Equation.Parse("sin(x)^2+cos(x)^2=1", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            await Assert.That(unicodeMath).IsNotNull();
        }

        [Test]
        public async Task FormulaOpensInTheCalculator()
        {
            var (shown, equation, tab) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                var view = window.GetVisualDescendants().OfType<MathToolsView>().Single();
                view.Tabs.SelectedIndex = 1;
                view.FormulaLibrary.Search.Text = "law";
                Save(window, "math-tools-formulas");
                var names = view.FormulaLibrary.Shown.Select(f => f.Name).ToList();
                view.FormulaLibrary.Open(Formulas.All.Single(f => f.Name == "Law of cosines"));
                var result = (names, view.Calculator.Editor.Equation, view.Tabs.SelectedIndex);
                window.Close();
                return result;
            });
            await Assert.That(shown).Contains("Law of sines");
            await Assert.That(shown).DoesNotContain("Pythagorean theorem");
            await Assert.That(equation).IsEqualTo(Equation.Parse("c^2=a^2+b^2-2ab cos⁡γ", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            await Assert.That(tab).IsEqualTo(0);
        }
    }
}
#endif
