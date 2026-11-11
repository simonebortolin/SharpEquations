namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>A layout with a cache of the layouts before is the layout without it, to the glyph and the caret.</summary>
    [NotInParallel("headless")]
    public class LayoutCacheTests
    {
        private static MathLayoutOptions Options(double? width = null) => new MathLayoutOptions { FontSize = 20, ShowPlaceholders = true, LineWidth = width };

        /// <summary>The corpus, laid out twice with one cache (the second time from it) and without one.</summary>
        [Test]
        public async Task CorpusLaysOutAsWithoutCache()
        {
            var failures = await Headless.Run(() =>
            {
                var failed = new List<string>();
                var cache = new MathLayoutCache();
                foreach (var pass in new[] { 1, 2 })
                {
                    foreach (var (input, _, equation) in Corpus.Examples())
                    {
                        var root = new MathEditor(equation).Root;
                        if (Difference(root, Options(), cache) is { } difference)
                            failed.Add(pass + " " + input + ": " + difference);
                    }
                }
                return failed;
            });
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>
        /// Long equations typed one character at a time, laid out after every keystroke with one cache, on a line that
        /// breaks them, and after undoing it all.
        /// </summary>
        [Test]
        public async Task TypingLaysOutAsWithoutCache()
        {
            var inputs = new[]
            {
                "f(x)=a_0+a_1 x+a_2 x^2+a_3 x^3+a_4 x^4+a_5 x^5+a_6 x^6+a_7 x^7+a_8 x^8+a_9 x^9 ",
                "(■(a_11&a_12&a_13@a_21&a_22&a_23@a_31&a_32&a_33)) ",
                "█(x&=(-b±√(b^2-4ac))/2a@y&=∑_(i=1)^n i^2/(i+1)) ",
                "a+\\phantom(b)+c ∫_0^∞ e^(-x^2) dx=√π/2 ",
            };
            var failures = await Headless.Run(() =>
            {
                var failed = new List<string>();
                foreach (var width in new double?[] { null, 300 })
                {
                    foreach (var input in inputs)
                    {
                        var cache = new MathLayoutCache();
                        var editor = new MathEditor { IsDisplay = true };
                        foreach (var rune in input.EnumerateRunes())
                        {
                            editor.Insert(rune.ToString());
                            if (Difference(editor.Root, Options(width), cache) is { } difference)
                                failed.Add(input + " at " + editor.Root + ": " + difference);
                        }
                        while (editor.CanUndo)
                        {
                            editor.Undo();
                            if (Difference(editor.Root, Options(width), cache) is { } difference)
                                failed.Add(input + " undone to " + editor.Root + ": " + difference);
                        }
                    }
                }
                return failed;
            });
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>
        /// Fractions typed at every position of equations with fences, scripts, accents, radicals and tables: what is
        /// around them grows, and the nodes the edit did not replace are laid out again in another style or stretch.
        /// </summary>
        [Test]
        public async Task EditsInsideLayOutAsWithoutCache()
        {
            var inputs = new[] { "(■(a&b@c&d))+[x+y]^2", "|a+b|+‖c‖+⟨d⟩", "∑_(k=1)^n (k+1)+√(x+1)+x̂+\\overbrace(a+b)", "█(x&=1@y&=2)" };
            var failures = await Headless.Run(() =>
            {
                var failed = new List<string>();
                foreach (var width in new double?[] { null, 120 })
                {
                    foreach (var input in inputs)
                    {
                        var cache = new MathLayoutCache();
                        var editor = new MathEditor(Equation.Parse(input, EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
                        editor.MoveHome();
                        for (var step = 0; step < 60; step++)
                        {
                            var caret = editor.Caret;
                            editor.Insert("1/(2/3) ");
                            if (Difference(editor.Root, Options(width), cache) is { } difference)
                                failed.Add(input + " at " + editor.Root + ": " + difference);
                            editor.MoveRight();
                            if (editor.Caret == caret)
                                break;
                        }
                    }
                }
                return failed;
            });
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>What differs between the layout with the cache and the layout without it, or null.</summary>
        private static string? Difference(MathNode root, MathLayoutOptions options, MathLayoutCache cache)
        {
            var expected = MathLayoutEngine.Layout(root, MathFonts.Default, options);
            var actual = MathLayoutEngine.Layout(root, MathFonts.Default, options, cache);
            if (expected.Width != actual.Width || expected.Height != actual.Height || expected.Baseline != actual.Baseline)
                return "size";
            if (!expected.Glyphs.Select(Describe).SequenceEqual(actual.Glyphs.Select(Describe)))
                return "glyphs";
            if (!expected.Shapes.Select(Describe).SequenceEqual(actual.Shapes.Select(Describe)))
                return "shapes";
            foreach (var position in Positions(root, Array.Empty<int>()))
            {
                if (!Equals(expected.GetCaretRect(position), actual.GetCaretRect(position)))
                    return "caret at " + position;
            }
            return null;
        }

        private static string Describe(MathGlyph g) => FormattableString.Invariant($"{g.Glyph} {g.X:R} {g.Y:R} {g.Size:R} {g.Color} {g.Font.GetHashCode()}");

        private static string Describe(MathShape s) =>
            FormattableString.Invariant($"{s.Kind} {s.Role} {s.X:R} {s.Y:R} {s.Width:R} {s.Height:R} {s.X2:R} {s.Y2:R} {s.Thickness:R} {s.Color}");

        /// <summary>Every position of the caret in a node: the offsets of its runs and rows, and of those under it.</summary>
        private static IEnumerable<MathPosition> Positions(MathNode node, int[] path)
        {
            if (node.IsRun || node.IsRow)
            {
                var count = node.IsRun ? node.Text.EnumerateRunes().Count() : node.Children.Count;
                for (var offset = 0; offset <= count; offset++)
                    yield return new MathPosition(path, offset);
            }
            for (var i = 0; i < node.Children.Count; i++)
            {
                foreach (var position in Positions(node.Children[i], path.Concat(new[] { i }).ToArray()))
                    yield return position;
            }
        }
    }
}
