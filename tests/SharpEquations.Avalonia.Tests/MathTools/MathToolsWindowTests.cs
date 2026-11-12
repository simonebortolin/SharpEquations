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
    using SharpEquations.MathTools;

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

        /// <summary>The page and the equation of the last session come back from the settings, and the new ones go there.</summary>
        [Test]
        public async Task SettingsAreKept()
        {
            var settings = new MemorySettingsStore();
            settings.Set("tab", "3");
            settings.Set("equation", "x^2-1");
            var (tab, equation, savedTab, savedEquation) = await Headless.Run(() =>
            {
                var window = new MainWindow(settings);
                window.Show();
                var view = window.GetVisualDescendants().OfType<MathToolsView>().Single();
                var restored = (view.Tabs.SelectedIndex, view.Calculator.Editor.Equation);
                view.Tabs.SelectedIndex = 1;
                view.Calculator.Editor.Equation = Equation.Parse("a+b", EquationFormat.UnicodeMath);
                window.Close();
                return (restored.SelectedIndex, restored.Equation, settings.Get("tab"), settings.Get("equation"));
            });
            await Assert.That(tab).IsEqualTo(3);
            await Assert.That(equation).IsEqualTo(Equation.Parse("x^2-1", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            await Assert.That(savedTab).IsEqualTo("1");
            await Assert.That(Equation.Parse(savedEquation!, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a+b", EquationFormat.UnicodeMath));
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

        [Test]
        public async Task UnitsConvert()
        {
            var (miles, swapped, celsius, invalid) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                var view = window.GetVisualDescendants().OfType<MathToolsView>().Single();
                view.Tabs.SelectedIndex = 2;
                var page = view.Converter;
                page.Value.Text = "42.195";
                page.From.SelectedItem = Units.Get("Length")["km"];
                page.To.SelectedItem = Units.Get("Length")["mi"];
                window.UpdateLayout();
                var first = page.Result;
                Save(window, "math-tools-units");
                page.Swap();
                var second = page.Result;
                page.Quantity.SelectedItem = Units.Get("Temperature");
                page.Value.Text = "98.6";
                page.From.SelectedItem = Units.Get("Temperature")["°F"];
                page.To.SelectedItem = Units.Get("Temperature")["°C"];
                var third = page.Result;
                page.Value.Text = "abc";
                var fourth = page.Result;
                window.Close();
                return (first, second, third, fourth);
            });
            await Assert.That(miles).IsEqualTo("42.195 \"\u00A0km\"=26.2188 \"\u00A0mi\"");
            await Assert.That(swapped).IsEqualTo("42.195 \"\u00A0mi\"=67.9063 \"\u00A0km\"");
            await Assert.That(celsius).IsEqualTo("98.6 \"\u00A0°F\"=37 \"\u00A0°C\"");
            await Assert.That(invalid).IsNull();
        }

        [Test]
        public async Task TrianglesSolve()
        {
            var (right, two, error) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                var view = window.GetVisualDescendants().OfType<MathToolsView>().Single();
                view.Tabs.SelectedIndex = 3;
                var page = view.TriangleSolver;
                window.UpdateLayout();
                var first = page.Solution;
                page.Fill(new[] { "6", "8", "", "40", "", "" });
                window.UpdateLayout();
                Save(window, "math-tools-triangles");
                var second = page.Solution;
                page.Inputs[2].Text = "x";
                var third = page.Solution;
                window.Close();
                return (first, second, third);
            });
            await Assert.That(right!.Case).IsEqualTo("SSS");
            await Assert.That(right.Triangles.Single().Gamma).IsEqualTo(90).Within(1e-9);
            await Assert.That(two!.Case).IsEqualTo("SSA");
            await Assert.That(two.Triangles.Count).IsEqualTo(2);
            await Assert.That(error!.Error).IsEqualTo("“x” is not a number.");
        }

        [Test]
        public async Task CalculatorComputes()
        {
            var (value, solved, derivative, unreadable) = await Headless.RunAsync(async () =>
            {
                var window = new MainWindow();
                window.Show();
                var page = window.GetVisualDescendants().OfType<CalculatorPage>().Single();
                var display = new EquationOptions { Display = true };
                page.Editor.Equation = Equation.Parse("√2+1/3", EquationFormat.UnicodeMath, display);
                await page.Solver.RefreshAnswer();
                var first = page.Solver.Answer;
                page.Editor.Equation = Equation.Parse("x^2-5x+6=0", EquationFormat.UnicodeMath, display);
                await page.Solver.RefreshAnswer();
                var second = await page.Solver.RunAsync(CasAction.Solve);
                page.Editor.Equation = Equation.Parse("x^3 sin x", EquationFormat.UnicodeMath, display);
                await page.Solver.RefreshAnswer();
                var third = await page.Solver.RunAsync(CasAction.Differentiate);
                window.Height = 1500;
                Save(window, "math-tools-compute");
                page.Editor.Equation = Equation.Parse("\"text\"", EquationFormat.UnicodeMath, display);
                await page.Solver.RefreshAnswer();
                var fourth = page.Solver.Answer;
                window.Close();
                return (first, second, third, fourth);
            });
            await Assert.That(value!.Text).EndsWith("≈ 1.747546896");
            await Assert.That(value.Math).IsNotNull();
            await Assert.That(solved!.Text).IsEqualTo("x ∈ { 2, 3 }");
            await Assert.That(solved.Math).IsNotNull();
            await Assert.That(derivative!.Math).IsNotNull();
            await Assert.That(unreadable).IsNull();
        }

        /// <summary>A letter defined stands for its value in what is computed next; in degrees, sin 30 is ½.</summary>
        [Test]
        public async Task CalculatorDefinesLettersAndTakesDegrees()
        {
            var (defined, value, sine, angles) = await Headless.RunAsync(async () =>
            {
                var settings = new MemorySettingsStore();
                var window = new MainWindow(settings);
                window.Show();
                var page = window.GetVisualDescendants().OfType<CalculatorPage>().Single();
                var display = new EquationOptions { Display = true };
                page.Editor.Equation = Equation.Parse("a≔3", EquationFormat.UnicodeMath, display);
                await page.Solver.RunAsync(CasAction.Define);
                var names = page.Solver.Engine.Definitions.Keys.ToList();
                page.Editor.Equation = Equation.Parse("a^2+1", EquationFormat.UnicodeMath, display);
                await page.Solver.RefreshAnswer();
                var first = page.Solver.Answer;
                page.Solver.Angles.SelectedIndex = 1;
                page.Editor.Equation = Equation.Parse("sin 30", EquationFormat.UnicodeMath, display);
                await page.Solver.RefreshAnswer();
                var second = page.Solver.Answer;
                window.Height = 1500;
                Save(window, "math-tools-define");
                window.Close();
                return (names, first, second, settings.Get("angles"));
            });
            await Assert.That(defined).IsEquivalentTo(new[] { "a" });
            await Assert.That(value!.Text).IsEqualTo("10");
            await Assert.That(sine!.Text).StartsWith("1/2");
            await Assert.That(angles).IsEqualTo("degrees");
        }

        /// <summary>Graphs of each kind are drawn together, with a slider for each parameter; the calculator sends its equation.</summary>
        [Test]
        public async Task GraphsDraw()
        {
            var (curves, parameters, kinds, none, error, sent, tab) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                var view = window.GetVisualDescendants().OfType<MathToolsView>().Single();
                view.Tabs.SelectedIndex = 4;
                var page = view.Graphs;
                var display = new EquationOptions { Display = true };
                page.Clear();
                page.AddEquation(Equation.Parse("y=a x^2", EquationFormat.UnicodeMath, display));
                page.AddEquation(Equation.Parse("x^2+y^2=25", EquationFormat.UnicodeMath, display));
                page.AddEquation(Equation.Parse("r=1+cos θ", EquationFormat.UnicodeMath, display));
                var slider = page.Sliders.Single();
                slider.Value.Value = 0.5;
                var value = page.Graph.Curves[0].Plot[slider.Key.Parameter];
                var failed = page.AddEquation(Equation.Parse("2=3", EquationFormat.UnicodeMath, display));
                window.Height = 1300;
                Save(window, "math-tools-graphs");
                var count = page.Graph.Curves.Count;
                var shown = page.Graph.Curves.Select(c => c.Plot.Kind).ToList();
                var message = page.Error;
                view.Tabs.SelectedIndex = 0;
                view.Calculator.Editor.Equation = Equation.Parse("y=sin x", EquationFormat.UnicodeMath, display);
                var requested = view.Calculator.Solver.RequestGraph();
                var result = (count, (slider.Key.Parameter, value), shown, failed, message, (requested, page.Graph.Curves.Count), view.Tabs.SelectedIndex);
                window.Close();
                return result;
            });
            await Assert.That(curves).IsEqualTo(3);
            await Assert.That(parameters.Parameter).IsEqualTo("a");
            await Assert.That(parameters.value).IsEqualTo(0.5);
            await Assert.That(kinds).IsEquivalentTo(new[] { GraphKind.Function, GraphKind.Implicit, GraphKind.Polar });
            await Assert.That(none).IsFalse();
            await Assert.That(error).IsNotNull();
            await Assert.That(sent.requested).IsTrue();
            await Assert.That(sent.Count).IsEqualTo(4);
            await Assert.That(tab).IsEqualTo(4);
        }
    }
}
#endif
