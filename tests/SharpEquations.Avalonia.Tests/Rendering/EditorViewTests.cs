namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>The view of an editor without a toolkit: layout, pointer, caret, floating bar, context menu, clipboard.</summary>
    public class EditorViewTests
    {
        private static readonly Lazy<OpenTypeMathFont> Libertinus = new Lazy<OpenTypeMathFont>(() => new OpenTypeMathFont(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SharpEquations.Avalonia", "Assets", "Fonts", "LibertinusMath-Regular.otf"))));

        private static Equation U(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        private static MathEditorView View(string? unicodeMath = null)
        {
            var view = new MathEditorView(new MathEditor(unicodeMath == null ? null : U(unicodeMath)) { IsDisplay = true })
            {
                Font = Libertinus.Value,
                FontSize = 20,
                Padding = new MathThickness(6, 4, 6, 4),
                BorderThickness = 1,
            };
            var size = view.Measure(double.PositiveInfinity);
            view.Arrange(size.Width, size.Height + 40);
            return view;
        }

        [Test]
        public async Task FontReadWithoutAToolkit()
        {
            await Assert.That(Libertinus.Value.Math).IsNotNull();
            await Assert.That(Libertinus.Value.TryGetGlyph('x', out _)).IsTrue();
            await Assert.That(Libertinus.Value.GetFallback('x')).IsNull();
        }

        [Test]
        public async Task MeasureAndPlaceInTheMiddle()
        {
            var view = View("a+b");
            var layout = view.Layout!;
            var size = view.Measure(double.PositiveInfinity);
            await Assert.That(size.Width).IsEqualTo(layout.Width + 12 + 2);
            await Assert.That(view.ContentX).IsEqualTo(7);
            await Assert.That(view.ContentY).IsEqualTo(5 + (size.Height + 40 - 10 - layout.Height) / 2).Within(1e-9);
            var empty = new MathEditorView(new MathEditor()) { Font = Libertinus.Value, FontSize = 20 }.Measure(double.PositiveInfinity);
            await Assert.That(empty.Height).IsGreaterThanOrEqualTo(24);
        }

        [Test]
        public async Task ChangesAskForMeasureOrRender()
        {
            var view = View("a+b");
            var changes = new List<MathEditorViewChanges>();
            view.Changed += (_, e) => changes.Add(e.Changes);
            view.Render(new Canvas());
            view.Editor.SelectAll();
            view.Editor.Insert("c");
            await Assert.That(changes[0]).IsEqualTo(MathEditorViewChanges.Render | MathEditorViewChanges.Caret | MathEditorViewChanges.QuickBar);
            await Assert.That(changes[1] & MathEditorViewChanges.Measure).IsEqualTo(MathEditorViewChanges.Measure);
        }

        [Test]
        public async Task PointerMovesTheCaretAndDragSelects()
        {
            var view = View("a+b");
            view.Editor.MoveHome();
            var start = view.GetCaretRect()!.Value;
            view.Editor.MoveEnd();
            var end = view.GetCaretRect()!.Value;
            view.PointerPressed(start.X, start.Y + start.Height / 2, false);
            await Assert.That(view.Editor.Selection).IsNull();
            await Assert.That(view.Editor.Caret.Offset).IsEqualTo(0);
            view.PointerMoved(end.X, end.Y + end.Height / 2);
            await Assert.That(view.Editor.Selection).IsNotNull();
            await Assert.That(view.IsInSelection((start.X + end.X) / 2, start.Y + start.Height / 2)).IsTrue();
            await Assert.That(view.PointerReleased()).IsTrue();
            await Assert.That(view.PointerReleased()).IsFalse();
            view.PointerMoved(start.X, start.Y + start.Height / 2);
            await Assert.That(view.Editor.Selection).IsNotNull();
        }

        [Test]
        public async Task DrawsTheSelectionUnderTheEquation()
        {
            var view = View("a+b");
            view.Editor.SelectAll();
            var canvas = new Canvas();
            view.Render(canvas);
            await Assert.That(canvas.Calls[0]).StartsWith("fill Selection");
            await Assert.That(canvas.Calls[^1]).IsEqualTo("glyphs 7 " + view.ContentY);
        }

        [Test]
        public async Task CaretOnlyFocusedShownAndEditable()
        {
            var view = View("a+b");
            var canvas = new Canvas();
            view.RenderCaret(canvas);
            await Assert.That(canvas.Calls).IsEmpty();
            view.IsFocused = true;
            view.RenderCaret(canvas);
            await Assert.That(canvas.Calls.Single()).StartsWith("fill Caret");
            view.BlinkCaret();
            await Assert.That(view.IsCaretVisible).IsFalse();
            view.Editor.MoveEnd();
            await Assert.That(view.IsCaretVisible).IsTrue();
            view.IsReadOnly = true;
            canvas.Calls.Clear();
            view.RenderCaret(canvas);
            await Assert.That(canvas.Calls).IsEmpty();
        }

        [Test]
        public async Task QuickBarUnderTheEquationWhenFocused()
        {
            var view = View();
            view.Editor.Insert("x+\\al");
            view.Measure(double.PositiveInfinity);
            view.UpdateQuickBar();
            await Assert.That(view.QuickBarAnchor).IsNull();
            view.IsFocused = true;
            await Assert.That(view.UpdateQuickBar()).IsTrue();
            await Assert.That(view.QuickBar.Mode).IsEqualTo(MathQuickBarMode.Completions);
            var anchor = view.QuickBarAnchor!.Value;
            await Assert.That(anchor.Y).IsEqualTo(view.ContentY);
            await Assert.That(anchor.Bottom).IsGreaterThanOrEqualTo(view.ContentY + view.Layout!.Height);
            await Assert.That(view.ProcessQuickBarKey(MathKey.Escape, MathKeyModifiers.None)).IsTrue();
            view.UpdateQuickBar();
            await Assert.That(view.QuickBarAnchor).IsNull();
            await Assert.That(view.ProcessKey(MathKey.Home, MathKeyModifiers.None)).IsTrue();
            await Assert.That(view.Editor.Caret.Offset).IsEqualTo(0);
        }

        [Test]
        public async Task ContextMenuForTheState()
        {
            var view = View("a+b");
            var commands = view.GetContextMenu().Where(i => !i.IsSeparator).Select(i => i.Command).ToList();
            await Assert.That(commands).Contains("undo");
            await Assert.That(commands).Contains("number");
            await Assert.That(commands).DoesNotContain("table");
            view.IsReadOnly = true;
            var readOnly = view.GetContextMenu().Select(i => i.Command!).ToList();
            await Assert.That(readOnly).IsEquivalentTo(new[] { "copy", "copy-as", "select-all" });
            var copyAs = view.GetContextMenu().Single(i => i.Command == "copy-as");
            await Assert.That(copyAs.Items.Select(i => MathEditorView.CopyAsFormat(i.Command!))).IsEquivalentTo(new EquationFormat?[] { EquationFormat.MathML, EquationFormat.UnicodeMath, EquationFormat.LaTeX, EquationFormat.Omml });
            await Assert.That(view.ExecuteCommand("number")).IsTrue();
            await Assert.That(view.Editor.EquationNumber).IsEqualTo("(1)");
            await Assert.That(view.ExecuteCommand("copy")).IsFalse();
            await Assert.That(view.ExecuteCommand("view")).IsFalse();
        }

        [Test]
        public async Task ContextMenuAtAPointMovesTheCaret()
        {
            var view = View("a+b");
            view.Editor.MoveHome();
            var start = view.GetCaretRect()!.Value;
            view.Editor.MoveEnd();
            var at = view.PrepareContextMenu(start.X, start.Y + 1);
            await Assert.That(view.Editor.Caret.Offset).IsEqualTo(0);
            await Assert.That(at!.Value.Width).IsEqualTo(1);
            await Assert.That(view.PrepareContextMenu(null, null)!.Value.Height).IsEqualTo(start.Height);
        }

        [Test]
        public async Task CopyAndPaste()
        {
            var view = View("a+b");
            await Assert.That(view.TryGetCopy(null, out _, out _, out _)).IsFalse();
            view.Editor.SelectAll();
            await Assert.That(view.TryGetCopy(EquationFormat.LaTeX, out var equation, out var text, out var mathML)).IsTrue();
            await Assert.That(equation).IsEqualTo(U("a+b"));
            await Assert.That(text).IsEqualTo(view.Editor.GetSelectedText(EquationFormat.LaTeX));
            await Assert.That(mathML).StartsWith("<math");
            view.Editor.MoveEnd();
            view.PasteText(mathML);
            await Assert.That(view.Editor.ToEquation()).IsEqualTo(U("a+ba+b"));
            view.IsReadOnly = true;
            view.PasteText("c");
            await Assert.That(view.PasteMathML(mathML)).IsFalse();
            await Assert.That(view.Editor.ToEquation()).IsEqualTo(U("a+ba+b"));
            await Assert.That(view.GetCopyAsText(EquationFormat.UnicodeMath)).IsEqualTo(view.Editor.GetText(EquationFormat.UnicodeMath));
        }

        /// <summary>A canvas that writes down what is drawn on it.</summary>
        private sealed class Canvas : IMathCanvas
        {
            public List<string> Calls { get; } = new List<string>();

            public void FillRectangle(MathRect rect, MathPaint paint, string? color) => Calls.Add($"fill {paint} {rect}");

            public void DrawRectangle(MathRect rect, double thickness, bool dashed, MathPaint paint, string? color) => Calls.Add($"rectangle {paint} {rect}");

            public void DrawLine(double x1, double y1, double x2, double y2, double thickness, MathPaint paint, string? color) => Calls.Add($"line {paint}");

            public void DrawEllipse(MathRect rect, double thickness, MathPaint paint, string? color) => Calls.Add($"ellipse {paint} {rect}");

            public void DrawGlyphs(MathLayout layout, double x, double y) => Calls.Add($"glyphs {x} {y}");
        }
    }
}
