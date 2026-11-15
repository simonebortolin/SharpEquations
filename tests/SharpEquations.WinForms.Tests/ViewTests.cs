namespace SharpEquations.WinForms.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using SkiaSharp;

    /// <summary>The view of an equation, and the colours of MathML.</summary>
    [NotInParallel("ui")]
    public class ViewTests
    {
        [Test]
        public async Task ShowsAnEquation()
        {
            var (width, height, glyphs, dark) = await Ui.Run(async () =>
            {
                var view = new MathView { FontSize = 24, Equation = Equation.Parse("a/b", EquationFormat.UnicodeMath) };
                await Ui.Show(view);
                view.Refresh();
                return (view.Width, view.Height, view.EquationLayout!.Glyphs.Count, EditBoxTests.DarkPixels(view.Surface.Pixels));
            });
            await Assert.That(width).IsGreaterThan(5);
            await Assert.That(height).IsGreaterThan(20);
            await Assert.That(glyphs).IsEqualTo(2);
            await Assert.That(dark).IsGreaterThan(10);
        }

        [Test]
        public async Task ColorsOfMathColor()
        {
            var colors = new[] { "#f00", "#ff0000", "#ffff0000", "red", "nonsense" }.Select(MathCanvas.Parse).ToArray();
            await Assert.That(colors[0]).IsEqualTo(SKColors.Red);
            await Assert.That(colors[1]).IsEqualTo(SKColors.Red);
            await Assert.That(colors[2]).IsEqualTo(SKColors.Red);
            await Assert.That(colors[3]).IsEqualTo(SKColors.Red);
            await Assert.That(colors[4]).IsNull();
        }

        [Test]
        public async Task EmbeddedFontHasAMathTable()
        {
            await Assert.That(MathFonts.Default.Math).IsNotNull();
            await Assert.That(MathFonts.Default.TryGetGlyph('∑', out _)).IsTrue();
        }
    }
}
