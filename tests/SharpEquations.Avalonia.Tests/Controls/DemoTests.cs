#if NET10_0_OR_GREATER
namespace SharpEquations.Avalonia.Tests.Controls
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Headless;
    using global::Avalonia.VisualTree;
    using SharpEquations;
    using SharpEquations.Avalonia.Demo;

    /// <summary>The demo window opens, edits and shows the equation in every format (a picture with SHARPEQUATIONS_SCREENSHOTS).</summary>
    [NotInParallel("headless")]
    public class DemoTests
    {
        [Test]
        public async Task DemoWindowWorks()
        {
            var (equation, outputs) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                var editor = window.GetVisualDescendants().OfType<MathEditBox>().Single();
                editor.Equation = null;
                editor.Focus();
                window.KeyTextInput("x=");
                editor.Editor.InsertStructure("mfrac");
                window.KeyTextInput("-b±\\sqrt(b^2-4ac) ");
                editor.Editor.MoveDown();
                window.KeyTextInput("2a ");
                editor.EquationNumber = "(1)";
                window.UpdateLayout();
                var frame = window.CaptureRenderedFrame();
                if (Environment.GetEnvironmentVariable("SHARPEQUATIONS_SCREENSHOTS") is { } directory && frame != null)
                {
                    Directory.CreateDirectory(directory);
                    frame.Save(Path.Combine(directory, "demo.png"), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                var texts = window.GetVisualDescendants().OfType<TextBox>().Count(t => t.IsReadOnly && !string.IsNullOrEmpty(t.Text));
                var result = editor.Equation;
                window.Close();
                return (result, texts);
            });
            await Assert.That(equation).IsEqualTo(Equation.Parse("x=(-b±√(b^2-4ac))/2a#(1)", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            await Assert.That(outputs).IsGreaterThanOrEqualTo(1);
        }

        [Test]
        public async Task FontChangesTheLayout()
        {
            var (before, after, family) = await Headless.Run(() =>
            {
                var window = new MainWindow();
                window.Show();
                window.UpdateLayout();
                var editor = window.GetVisualDescendants().OfType<MathEditBox>().Single();
                var width = editor.Layout!.Width;
                var fonts = window.GetVisualDescendants().OfType<ComboBox>()
                    .Single(c => c.ItemsSource is System.Collections.IEnumerable items && items.Cast<object>().Contains("STIX Two Math"));
                fonts.SelectedItem = "STIX Two Math";
                window.UpdateLayout();
                var result = (width, editor.Layout!.Width, ((AvaloniaMathFont)MathFonts.Get(editor.MathFontFamily)).Typeface.FontFamily.Name);
                window.Close();
                return result;
            });
            await Assert.That(family).Contains("STIX Two Math");
            await Assert.That(after).IsNotEqualTo(before);
        }
    }
}
#endif
