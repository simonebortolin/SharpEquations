namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>A display equation wider than its line breaks before its operators, as in Word.</summary>
    [NotInParallel("headless")]
    public class LineBreakTests
    {
        private const string Long = "f(x)=a_0+a_1 x+a_2 x^2+a_3 x^3+a_4 x^4+a_5 x^5+a_6 x^6+a_7 x^7+a_8 x^8+a_9 x^9";

        private static MathLayoutOptions Options(double width) => new MathLayoutOptions { FontSize = 20, LineWidth = width, ShowPlaceholders = true };

        private static MathEditor Editor() => new MathEditor(Equation.Parse(Long, EquationFormat.UnicodeMath, new EquationOptions { Display = true }));

        [Test]
        public async Task BreaksBeforeOperatorsToTheWidth()
        {
            var (one, broken, baselines, starts) = await Headless.Run(() =>
            {
                var editor = Editor();
                var wide = MathLayoutEngine.Layout(editor.Root, MathFonts.Default, Options(5000));
                var narrow = MathLayoutEngine.Layout(editor.Root, MathFonts.Default, Options(300));
                var lines = narrow.Glyphs.Select(g => Math.Round(g.Y)).Distinct().Count(y => narrow.Glyphs.Count(g => Math.Round(g.Y) == y) > 3);
                // the caret positions at the start of each line are before an operator
                var row = editor.Root.Children[0];
                var startsWithOperator = new List<bool>();
                for (var i = 1; i < row.Children.Count; i++)
                {
                    var here = narrow.GetCaretRect(new MathPosition(new[] { 0 }, i))!.Value;
                    var before = narrow.GetCaretRect(new MathPosition(new[] { 0 }, i - 1))!.Value;
                    if (here.Y > before.Y + 1)
                        startsWithOperator.Add(row.Children[i].Name == "mo");
                }
                return ((wide.Width, wide.Height), (narrow.Width, narrow.Height), lines, startsWithOperator);
            });
            await Assert.That(one.Width).IsGreaterThan(300);
            await Assert.That(broken.Width).IsLessThanOrEqualTo(300);
            await Assert.That(broken.Height).IsGreaterThan(one.Height * 2);
            await Assert.That(starts.Count).IsGreaterThanOrEqualTo(2);
            await Assert.That(starts.All(s => s)).IsTrue();
        }

        [Test]
        public async Task InlineOrUnlimitedDoesNotBreak()
        {
            var (inline, off) = await Headless.Run(() =>
            {
                var inlineEditor = new MathEditor(Equation.Parse(Long, EquationFormat.UnicodeMath));
                var a = MathLayoutEngine.Layout(inlineEditor.Root, MathFonts.Default, Options(300)).Width;
                var b = MathLayoutEngine.Layout(Editor().Root, MathFonts.Default, new MathLayoutOptions { FontSize = 20, LineWidth = 300, BreakLines = false }).Width;
                return (a, b);
            });
            await Assert.That(inline).IsGreaterThan(300);
            await Assert.That(off).IsGreaterThan(300);
        }

        /// <summary>Every caret position of the broken equation is found again by a click on it, and Down goes to the next line.</summary>
        [Test]
        public async Task CaretOnTheLines()
        {
            var (failures, movedDown, movedUp) = await Headless.Run(() =>
            {
                var editor = Editor();
                var layout = MathLayoutEngine.Layout(editor.Root, MathFonts.Default, Options(300));
                var failed = new List<string>();
                editor.MoveHome();
                for (var step = 0; step < 300; step++)
                {
                    var caret = editor.Caret;
                    var rect = layout.GetCaretRect(caret)!.Value;
                    var hit = layout.HitTest(rect.X, rect.Y + rect.Height / 2);
                    if (hit == null || layout.GetCaretRect(hit) is not { } h || Math.Abs(h.X - rect.X) > 0.5 || h.Y + h.Height / 2 < rect.Y || h.Y + h.Height / 2 > rect.Bottom)
                        failed.Add(caret + " → " + hit);
                    editor.MoveRight();
                    if (editor.Caret == caret)
                        break;
                }

                editor.MoveHome();
                editor.MoveHome();
                var top = layout.GetCaretRect(editor.Caret)!.Value.Y;
                editor.MoveDown(geometry: layout);
                var down = layout.GetCaretRect(editor.Caret)!.Value.Y;
                editor.MoveUp(geometry: layout);
                var up = layout.GetCaretRect(editor.Caret)!.Value.Y;
                return (failed, down > top + 1, Math.Abs(up - top) < 0.5);
            });
            await Assert.That(failures).IsEmpty();
            await Assert.That(movedDown).IsTrue();
            await Assert.That(movedUp).IsTrue();
        }

        [Test]
        public async Task SelectionOnSeveralLines()
        {
            var rects = await Headless.Run(() =>
            {
                var editor = Editor();
                var layout = MathLayoutEngine.Layout(editor.Root, MathFonts.Default, Options(300));
                editor.SelectAll();
                return layout.GetSelectionRects(editor.Selection!).Count;
            });
            await Assert.That(rects).IsGreaterThanOrEqualTo(2);
        }
    }
}
