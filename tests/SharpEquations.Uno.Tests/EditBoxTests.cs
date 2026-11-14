namespace SharpEquations.Uno.Tests
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Microsoft.UI.Xaml.Shapes;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using Windows.System;

    /// <summary>The editor of Uno in a window: drawing, typing, keys, pointer, caret, floating bar, context menu.</summary>
    [NotInParallel("ui")]
    public class EditBoxTests
    {
        private static Equation U(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        private static async Task<MathEditBox> Show(string? unicodeMath = null, bool focus = true)
        {
            var editor = new MathEditBox { FontSize = 28 };
            if (unicodeMath != null)
                editor.Equation = U(unicodeMath);
            await Ui.Show(editor);
            if (focus)
            {
                editor.FocusEditor();
                await Ui.Settle();
            }
            return editor;
        }

        [Test]
        public async Task DrawsTheGlyphsAsPathsAndTheBarsAsShapes()
        {
            var (paths, glyphs, rectangles, width, height) = await Ui.Run(async () =>
            {
                var editor = await Show("x=(-b±√(b^2-4ac))/2a", focus: false);
                var layout = editor.Layout!;
                var drawn = layout.Glyphs.Count(g => ((OpenTypeMathFont)g.Font).GetOutline(g.Glyph).Count > 0);
                var children = editor.EquationLayer.Children;
                return (children.OfType<Path>().Count(), drawn, children.OfType<Rectangle>().Count(), editor.ActualWidth, editor.ActualHeight);
            });
            await Assert.That(paths).IsEqualTo(glyphs);
            await Assert.That(glyphs).IsGreaterThan(10);
            await Assert.That(rectangles).IsGreaterThanOrEqualTo(2);
            await Assert.That(width).IsGreaterThan(100);
            await Assert.That(height).IsGreaterThan(60);
        }

        [Test]
        public async Task DrawsInk()
        {
            var dark = await Ui.Run(async () =>
            {
                var editor = await Show("a/b", focus: false);
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(editor);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                var count = 0;
                for (var i = 0; i + 3 < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] > 200 && pixels[i] < 80 && pixels[i + 1] < 80 && pixels[i + 2] < 80)
                        count++;
                }
                return count;
            });
            await Assert.That(dark).IsGreaterThan(50);
        }

        [Test]
        public async Task TypingBuildsUp()
        {
            var (equation, changed, input) = await Ui.Run(async () =>
            {
                var editor = await Show();
                Equation? last = null;
                editor.EquationChanged += (_, e) => last = e.Equation;
                foreach (var ch in "a/b+x^2 ")
                {
                    editor.Type(ch.ToString());
                    await Ui.Settle();
                }
                return (editor.Equation, last, editor.Input.Text);
            });
            await Assert.That(equation).IsEqualTo(U("a/b+x^2"));
            await Assert.That(changed).IsEqualTo(equation);
            await Assert.That(input).IsEmpty();
        }

        [Test]
        public async Task KeysEditAndUndo()
        {
            var (deleted, undone, selected, linear) = await Ui.Run(async () =>
            {
                var editor = await Show();
                editor.Type("x+y");
                await Ui.Settle();
                editor.HandleKey(VirtualKey.Back, MathKeyModifiers.None);
                var afterDelete = editor.Equation;
                var command = System.OperatingSystem.IsMacOS() ? MathKeyModifiers.Meta : MathKeyModifiers.Control;
                editor.HandleKey(VirtualKey.Z, command);
                var afterUndo = editor.Equation;
                editor.HandleKey(VirtualKey.Left, MathKeyModifiers.Shift);
                var selection = editor.Editor.Selection != null;
                editor.HandleKey(VirtualKey.L, command | MathKeyModifiers.Shift);
                return (afterDelete, afterUndo, selection, editor.ViewMode);
            });
            await Assert.That(deleted).IsEqualTo(U("x+"));
            await Assert.That(undone).IsEqualTo(U("x+y"));
            await Assert.That(selected).IsTrue();
            await Assert.That(linear).IsEqualTo(MathViewMode.Linear);
        }

        [Test]
        public async Task CaretOnlyWhenFocusedAndEditable()
        {
            var (unfocused, focused, readOnly) = await Ui.Run(async () =>
            {
                var editor = await Show("a+b", focus: false);
                var before = editor.CaretLayer.Children.Count;
                editor.FocusEditor();
                await Ui.Settle();
                var after = editor.CaretLayer.Children.OfType<Rectangle>().Count();
                editor.IsReadOnly = true;
                await Ui.Settle();
                return (before, after, editor.CaretLayer.Children.Count);
            });
            await Assert.That(unfocused).IsEqualTo(0);
            await Assert.That(focused).IsEqualTo(1);
            await Assert.That(readOnly).IsEqualTo(0);
        }

        [Test]
        public async Task QuickBarCompletesACommand()
        {
            var (open, buttons, equation, closed) = await Ui.Run(async () =>
            {
                var editor = await Show();
                editor.Type("x+\\al");
                await Ui.Settle();
                var wasOpen = editor.IsQuickBarOpen;
                var count = editor.QuickBarPanel.Children.Count;
                var alpha = editor.View.QuickBar.Completions.ToList().FindIndex(c => c.Name == "alpha");
                for (var i = 0; i < alpha; i++)
                    editor.HandleKey(VirtualKey.Down, MathKeyModifiers.None);
                editor.HandleKey(VirtualKey.Tab, MathKeyModifiers.None);
                editor.Type(" ");
                await Ui.Settle();
                return (wasOpen, count, editor.Equation, !editor.IsQuickBarOpen);
            });
            await Assert.That(open).IsTrue();
            await Assert.That(buttons).IsGreaterThan(1);
            await Assert.That(equation).IsEqualTo(U("x+α"));
            await Assert.That(closed).IsTrue();
        }

        [Test]
        public async Task EscapeClosesTheCompletions()
        {
            var (open, closed) = await Ui.Run(async () =>
            {
                var editor = await Show();
                editor.Type("\\su");
                await Ui.Settle();
                var wasOpen = editor.IsQuickBarOpen;
                editor.HandleKey(VirtualKey.Escape, MathKeyModifiers.None);
                await Ui.Settle();
                return (wasOpen, !editor.IsQuickBarOpen);
            });
            await Assert.That(open).IsTrue();
            await Assert.That(closed).IsTrue();
        }

        [Test]
        public async Task StructuresForTheSelection()
        {
            var (open, buttons, equation) = await Ui.Run(async () =>
            {
                var editor = await Show("a+b");
                editor.Editor.SelectAll();
                await Ui.Settle();
                var wasOpen = editor.IsQuickBarOpen;
                var count = editor.QuickBarPanel.Children.Count;
                editor.View.QuickBar.GetItems(editor.QuickSymbols).First(i => i.Text == "√x").Invoke();
                await Ui.Settle();
                return (wasOpen, count, editor.Equation);
            });
            await Assert.That(open).IsTrue();
            await Assert.That(buttons).IsEqualTo(10);
            await Assert.That(equation).IsEqualTo(U("√(a+b)"));
        }

        [Test]
        public async Task PointerPutsTheCaret()
        {
            var (offset, focused) = await Ui.Run(async () =>
            {
                var editor = await Show("a+b", focus: false);
                editor.Editor.MoveHome();
                var start = editor.View.GetCaretRect()!.Value;
                editor.Editor.MoveEnd();
                editor.PressAt(start.X, start.Y + start.Height / 2, false);
                editor.View.PointerReleased();
                await Ui.Settle();
                return (editor.Editor.Caret.Offset, editor.View.IsFocused);
            });
            await Assert.That(offset).IsEqualTo(0);
            await Assert.That(focused).IsTrue();
        }

        [Test]
        public async Task ContextMenuForTheState()
        {
            var (tags, readOnlyTags, undoKey) = await Ui.Run(async () =>
            {
                var editor = new MenuEditBox { FontSize = 28, Equation = U("a+b") };
                await Ui.Show(editor);
                var menu = editor.Menu();
                var all = menu.Items.Select(i => i.Tag as string).OfType<string>().ToList();
                var undo = menu.Items.OfType<MenuFlyoutItem>().First(i => (string)i.Tag == "undo").KeyboardAcceleratorTextOverride;
                editor.IsReadOnly = true;
                var readOnly = editor.Menu().Items.Select(i => i.Tag as string).OfType<string>().ToList();
                return (all, readOnly, undo);
            });
            await Assert.That(tags).Contains("undo");
            await Assert.That(tags).Contains("copy-as");
            await Assert.That(tags).Contains("number");
            await Assert.That(readOnlyTags).IsEquivalentTo(new[] { "copy", "copy-as", "select-all" });
            await Assert.That(undoKey).EndsWith("Z");
        }

        [Test]
        public async Task MathMLInAndOut()
        {
            var (mathML, equation) = await Ui.Run(async () =>
            {
                var editor = await Show(focus: false);
                editor.SetMath("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msqrt><mi>x</mi></msqrt></math>");
                await Ui.Settle();
                return (editor.GetMath(), editor.Equation);
            });
            await Assert.That(mathML).Contains("msqrt");
            await Assert.That(equation).IsEqualTo(Equation.Parse("√x", EquationFormat.UnicodeMath));
        }

        /// <summary>The editor with its context menu at hand.</summary>
        private sealed class MenuEditBox : MathEditBox
        {
            public MenuFlyout Menu() => CreateContextMenu();
        }
    }
}
