namespace SharpEquations.Wpf.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Media;

    /// <summary>The view of an equation, and the colours of MathML.</summary>
    [NotInParallel("ui")]
    public class ViewTests
    {
        [Test]
        public async Task ShowsAnEquation()
        {
            var (width, height, glyphs) = await Ui.Run(async () =>
            {
                var view = new MathView { FontSize = 24, Equation = Equation.Parse("a/b", EquationFormat.UnicodeMath) };
                await Ui.Show(view);
                return (view.ActualWidth, view.ActualHeight, view.Layout!.Glyphs.Count);
            });
            await Assert.That(width).IsGreaterThan(5);
            await Assert.That(height).IsGreaterThan(20);
            await Assert.That(glyphs).IsEqualTo(2);
        }

        [Test]
        public async Task ColorsOfMathColor()
        {
            var colors = await Ui.Run(() => new[] { "#f00", "#ff0000", "#ffff0000", "red", "nonsense" }
                .Select(c => (MathCanvas.Brush(c) as SolidColorBrush)?.Color).ToArray());
            await Assert.That(colors[0]).IsEqualTo(Colors.Red);
            await Assert.That(colors[1]).IsEqualTo(Colors.Red);
            await Assert.That(colors[2]).IsEqualTo(Colors.Red);
            await Assert.That(colors[3]).IsEqualTo(Colors.Red);
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
