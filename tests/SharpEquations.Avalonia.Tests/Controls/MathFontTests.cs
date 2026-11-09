namespace SharpEquations.Avalonia.Tests.Controls
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Media;
    using SharpEquations;

    /// <summary>The math font of the controls follows <c>MathFontFamily</c>, from the default font to another and back.</summary>
    [NotInParallel("headless")]
    public class MathFontTests
    {
        private static readonly Equation Quadratic = Equation.Parse("x=(-b±√(b^2-4ac))/2a", EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        private static bool Windows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static IEnumerable<(FontFamily? Family, string Expected)> Families()
        {
            var system = Windows ? "Cambria Math" : "Libertinus Math";
            yield return (null, system);
            yield return (MathFonts.LibertinusMath, "Libertinus Math");
            if (Windows)
                yield return (new FontFamily("Cambria Math"), "Cambria Math");
            yield return (null, system);
        }

        [Test]
        public async Task EditorAndViewFollowTheFont()
        {
            var results = await Headless.Run(() =>
            {
                var editor = new MathEditBox { FontSize = 30, Equation = Quadratic };
                var view = new MathView { FontSize = 30, Equation = Quadratic };
                var window = new Window { Content = new StackPanel { Children = { editor, view } }, Width = 800, Height = 400 };
                window.Show();
                var list = new List<(string Expected, string Editor, string View, double Width)>();
                foreach (var (family, expected) in Families())
                {
                    editor.MathFontFamily = view.MathFontFamily = family;
                    window.UpdateLayout();
                    list.Add((expected, Drawn(editor.Layout), Drawn(view.Layout), editor.Layout!.Width));
                }
                window.Close();
                return list;
            });

            var widths = new HashSet<double>();
            foreach (var (expected, editorFont, viewFont, width) in results)
            {
                await Assert.That($"{editorFont} / {viewFont}").IsEqualTo($"{expected} / {expected}");
                widths.Add(width);
            }
            await Assert.That(results[0].Width).IsEqualTo(results[^1].Width);
            await Assert.That(widths.Count).IsEqualTo(Windows ? 2 : 1);
        }

        private static string Drawn(SharpEquations.Rendering.MathLayout? layout)
        {
            foreach (var glyph in layout!.Glyphs)
                return ((AvaloniaMathFont)glyph.Font).GetGlyphTypeface()!.FamilyName;
            return string.Empty;
        }
    }
}
