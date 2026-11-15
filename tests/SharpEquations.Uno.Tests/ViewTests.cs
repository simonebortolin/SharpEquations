namespace SharpEquations.Uno.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Shapes;
    using SharpEquations.Rendering;

    /// <summary>The view of an equation, and the brushes of the colours of MathML.</summary>
    [NotInParallel("ui")]
    public class ViewTests
    {
        [Test]
        public async Task ShowsAnEquation()
        {
            var (paths, width, height, empty) = await Ui.Run(async () =>
            {
                var view = new MathView { FontSize = 24, IsDisplay = true, MathML = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mfrac><mi>a</mi><mi>b</mi></mfrac></math>" };
                await Ui.Show(view);
                var canvas = (Canvas)view.Content;
                var count = canvas.Children.OfType<Path>().Count();
                var size = (view.ActualWidth, view.ActualHeight);
                view.Equation = null;
                await Ui.Settle();
                return (count, size.ActualWidth, size.ActualHeight, canvas.Children.Count);
            });
            await Assert.That(paths).IsEqualTo(2);
            await Assert.That(width).IsGreaterThan(5);
            await Assert.That(height).IsGreaterThan(20);
            await Assert.That(empty).IsEqualTo(0);
        }

        [Test]
        public async Task ColorsOfMathColor()
        {
            var colors = await Ui.Run(() => new[] { "#f00", "#00ff00", "#800000FF", "red", "Navy", "nonsense", "#12" }
                .Select(c => MathCanvas.Brush(c) is SolidColorBrush b ? $"{b.Color.A:X2}{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : null).ToArray());
            await Assert.That(colors).IsEquivalentTo(new string?[] { "FFFF0000", "FF00FF00", "800000FF", "FFFF0000", "FF000080", null, null });
        }

        [Test]
        public async Task EmbeddedFontHasAMathTable()
        {
            await Assert.That(MathFonts.Default.Math).IsNotNull();
            await Assert.That(MathFonts.Default.TryGetGlyph('∑', out _)).IsTrue();
        }
    }
}
