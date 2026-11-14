namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using SharpEquations.Rendering;
    using SharpEquations.Rendering.OpenType;

    /// <summary>The outlines of the glyphs, for the toolkits that draw glyphs as paths: their extremes are the ink boxes.</summary>
    public class OutlineTests
    {
        private static readonly string Libertinus = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SharpEquations.Avalonia", "Assets", "Fonts", "LibertinusMath-Regular.otf");

        [Test]
        public async Task CffOutlinesHaveTheInkBoxes()
        {
            var mismatches = Mismatches(new OpenTypeMathFont(File.ReadAllBytes(Libertinus)), 1.0, out var drawn);
            await Assert.That(drawn).IsGreaterThan(1000);
            await Assert.That(mismatches).IsEmpty();
        }

        [Test]
        public async Task TrueTypeOutlinesHaveTheInkBoxes()
        {
            var path = "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf";
            if (!File.Exists(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cambria.ttc");
            if (!File.Exists(path))
                return;
            var mismatches = Mismatches(new OpenTypeMathFont(File.ReadAllBytes(path)), 0.5, out var drawn);
            await Assert.That(drawn).IsGreaterThan(500);
            await Assert.That(mismatches).IsEmpty();
        }

        [Test]
        public async Task ContoursAreClosed()
        {
            var font = new OpenTypeMathFont(File.ReadAllBytes(Libertinus));
            await Assert.That(font.TryGetGlyph('x', out var x)).IsTrue();
            var outline = font.GetOutline(x);
            await Assert.That(outline[0].Kind).IsEqualTo(MathPathCommandKind.MoveTo);
            await Assert.That(outline[outline.Count - 1].Kind).IsEqualTo(MathPathCommandKind.Close);
            await Assert.That(font.TryGetGlyph(' ', out var space)).IsTrue();
            await Assert.That(font.GetOutline(space)).IsEmpty();
        }

        /// <summary>The glyphs whose outline, sampled along its curves, does not reach the ink box of the font.</summary>
        private static List<string> Mismatches(OpenTypeMathFont font, double tolerance, out int drawn)
        {
            var failed = new List<string>();
            drawn = 0;
            var count = Math.Min(font.File.GlyphCount, 65535);
            for (var g = 0; g < count; g++)
            {
                var glyph = (ushort)g;
                var outline = font.GetOutline(glyph);
                var box = font.GetBounds(glyph);
                if (outline.Count == 0)
                {
                    if (box.XMax > box.XMin && font.File.Table("CFF ") != null)
                        failed.Add(g + ": no outline");
                    continue;
                }
                drawn++;
                double xMin = double.MaxValue, yMin = double.MaxValue, xMax = double.MinValue, yMax = double.MinValue;
                void Add(double px, double py)
                {
                    xMin = Math.Min(xMin, px);
                    xMax = Math.Max(xMax, px);
                    yMin = Math.Min(yMin, py);
                    yMax = Math.Max(yMax, py);
                }
                double cx = 0, cy = 0;
                foreach (var c in outline)
                {
                    switch (c.Kind)
                    {
                        case MathPathCommandKind.MoveTo or MathPathCommandKind.LineTo:
                            Add(c.X, c.Y);
                            break;
                        case MathPathCommandKind.QuadraticTo:
                            for (var t = 0.0; t <= 1.0; t += 1.0 / 64)
                                Add((1 - t) * (1 - t) * cx + 2 * (1 - t) * t * c.X1 + t * t * c.X, (1 - t) * (1 - t) * cy + 2 * (1 - t) * t * c.Y1 + t * t * c.Y);
                            break;
                        case MathPathCommandKind.CubicTo:
                            for (var t = 0.0; t <= 1.0; t += 1.0 / 64)
                            {
                                var u = 1 - t;
                                Add(u * u * u * cx + 3 * u * u * t * c.X1 + 3 * u * t * t * c.X2 + t * t * t * c.X,
                                    u * u * u * cy + 3 * u * u * t * c.Y1 + 3 * u * t * t * c.Y2 + t * t * t * c.Y);
                            }
                            break;
                    }
                    if (c.Kind != MathPathCommandKind.Close)
                    {
                        cx = c.X;
                        cy = c.Y;
                    }
                }
                // the box of a TrueType glyph is that of its points, controls included, so the curves can stay inside it
                var trueType = font.File.Table("CFF ") == null;
                if (Math.Abs(xMin - box.XMin) > tolerance && !(trueType && xMin > box.XMin)
                    || Math.Abs(xMax - box.XMax) > tolerance && !(trueType && xMax < box.XMax)
                    || Math.Abs(yMin - box.YMin) > tolerance && !(trueType && yMin > box.YMin)
                    || Math.Abs(yMax - box.YMax) > tolerance && !(trueType && yMax < box.YMax))
                {
                    failed.Add(FormattableString.Invariant($"{g}: {xMin:0.#},{yMin:0.#} {xMax:0.#},{yMax:0.#} for {box.XMin},{box.YMin} {box.XMax},{box.YMax}"));
                }
            }
            return failed;
        }
    }
}
