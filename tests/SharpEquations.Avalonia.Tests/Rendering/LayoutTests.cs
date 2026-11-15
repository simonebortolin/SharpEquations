namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>The layout of the corpus with the default math font, in the headless session.</summary>
    [NotInParallel("headless")]
    public class LayoutTests
    {
        private static readonly MathLayoutOptions Options = new MathLayoutOptions { FontSize = 20, ShowPlaceholders = true };

        [Test]
        public async Task FontHasMathTable()
        {
            var hasMath = await Headless.Run(() => MathFonts.Default.Math != null);
            await Assert.That(hasMath).IsTrue();
        }

        [Test]
        public async Task CorpusLaysOut()
        {
            var failures = await Headless.Run(() =>
            {
                var failed = new List<string>();
                foreach (var (input, _, equation) in Corpus.Examples())
                {
                    try
                    {
                        var layout = MathLayoutEngine.Layout(equation, MathFonts.Default, Options);
                        if (!(layout.Width > 0) || !(layout.Height > 0) || double.IsInfinity(layout.Width) || double.IsInfinity(layout.Height)
                            || layout.Glyphs.Any(g => double.IsNaN(g.X) || double.IsNaN(g.Y)))
                        {
                            failed.Add(input + ": " + layout.Width + "×" + layout.Height);
                        }
                    }
                    catch (Exception e)
                    {
                        failed.Add(input + ": " + e.GetType().Name + " " + e.Message);
                    }
                }
                return failed;
            });
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>
        /// The limits of an integral in display, a large operator, go to its top and its bottom (TeX, rule 13), not to
        /// the place of the scripts of a letter: the lower limit is not halfway down the integral.
        /// </summary>
        [Test]
        public async Task LimitsOfALargeIntegralAtItsEnds()
        {
            var (integral, letter, height) = await Headless.Run(() =>
            {
                double Spread(string unicodeMath, out double operatorHeight)
                {
                    var layout = MathLayoutEngine.Layout(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true }), MathFonts.Default, Options);
                    var size = layout.Glyphs.Max(g => g.Size);
                    var scripts = layout.Glyphs.Where(g => g.Size < size).ToList();
                    operatorHeight = layout.Height;
                    return scripts.Max(g => g.Y) - scripts.Min(g => g.Y);
                }

                var i = Spread("∫_0^1", out var h);
                return (i, Spread("x_0^1", out _), h);
            });
            await Assert.That(integral).IsGreaterThan(letter * 1.5);
            await Assert.That(integral).IsGreaterThan(height * 0.5);
        }

        [Test]
        public async Task FractionIsTallerThanItsParts()
        {
            var (fraction, letter) = await Headless.Run(() =>
                (MathLayoutEngine.Layout(Equation.Parse("a/b", EquationFormat.UnicodeMath), MathFonts.Default, Options).Height,
                 MathLayoutEngine.Layout(Equation.Parse("a", EquationFormat.UnicodeMath), MathFonts.Default, Options).Height));
            await Assert.That(fraction).IsGreaterThan(letter * 1.8);
        }

        [Test]
        public async Task DelimitersStretch()
        {
            var (tall, plain) = await Headless.Run(() =>
                (MathLayoutEngine.Layout(Equation.Parse("(a/b)", EquationFormat.UnicodeMath, new EquationOptions { Display = true }), MathFonts.Default, Options).Height,
                 MathLayoutEngine.Layout(Equation.Parse("a/b", EquationFormat.UnicodeMath, new EquationOptions { Display = true }), MathFonts.Default, Options).Height));
            await Assert.That(tall).IsGreaterThanOrEqualTo(plain);
        }

        /// <summary>A click on the caret of every position of an equation gives back that position (or one at the same place).</summary>
        [Test]
        public async Task HitTestFindsEveryCaret()
        {
            var failures = await Headless.Run(() =>
            {
                var failed = new List<string>();
                foreach (var input in new[] { "x=(-b±√(b^2-4ac))/2a", "∑_(i=1)^n i^2", "(■(a&b@c&d))", "e^(iπ)+1=0" })
                {
                    var editor = new MathEditor(Equation.Parse(input, EquationFormat.UnicodeMath));
                    var layout = MathLayoutEngine.Layout(editor.Root, MathFonts.Default, Options);
                    editor.MoveHome();
                    editor.MoveHome();
                    for (var step = 0; step < 200; step++)
                    {
                        var caret = editor.Caret;
                        var rect = layout.GetCaretRect(caret);
                        if (rect is not { } r)
                        {
                            failed.Add(input + " " + caret + ": no caret");
                            break;
                        }
                        var hit = layout.HitTest(r.X, r.Y + r.Height / 2);
                        if (hit == null || layout.GetCaretRect(hit) is not { } h || Math.Abs(h.X - r.X) > 0.5)
                            failed.Add(input + " " + caret + " → " + hit);
                        editor.MoveRight();
                        if (editor.Caret == caret)
                            break;
                    }
                }
                return failed;
            });
            await Assert.That(failures).IsEmpty();
        }
    }
}
