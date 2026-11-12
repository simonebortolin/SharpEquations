namespace SharpEquations.Avalonia.Tests.Controls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Headless;
    using global::Avalonia.Input;
    using global::Avalonia.Input.Platform;
    using global::Avalonia.Media;
    using global::Avalonia.Threading;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The context menu of the editor: its items for the state of the editor, and their commands.</summary>
    [NotInParallel("headless")]
    public class ContextMenuTests
    {
        private static Equation Display(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        private static async Task<T> WithEditor<T>(Func<Window, MathEditBox, Task<T>> test, string? picture = null)
        {
            var editor = new MathEditBox { FontSize = 28 };
            var window = new Window { Content = new StackPanel { Children = { editor } }, Width = 700, Height = 500, Background = Brushes.White };
            window.Show();
            editor.Focus();
            try
            {
                var result = await test(window, editor);
                if (picture != null && Environment.GetEnvironmentVariable("SHARPEQUATIONS_SCREENSHOTS") is { } directory && window.CaptureRenderedFrame() is { } frame)
                {
                    Directory.CreateDirectory(directory);
                    frame.Save(Path.Combine(directory, picture), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                return result;
            }
            finally
            {
                editor.OpenContextMenu?.Close();
                window.Close();
            }
        }

        private static void Settle(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        private static MenuItem? Find(ContextMenu menu, string tag) => Items(menu.Items).FirstOrDefault(i => (i.Tag as string) == tag);

        private static IEnumerable<MenuItem> Items(IEnumerable<object?> items)
        {
            foreach (var item in items.OfType<MenuItem>())
            {
                yield return item;
                foreach (var child in Items(item.Items))
                    yield return child;
            }
        }

        private static Point Center(MathEditBox editor, MathPosition position)
        {
            var rect = editor.Layout!.GetCaretRect(position)!.Value;
            var origin = editor.TranslatePoint(new Point(0, 0), editor)!.Value;
            return new Point(origin.X + editor.Padding.Left + editor.BorderThickness + rect.X, editor.Bounds.Height / 2);
        }

        private static void Click(MenuItem item) => item.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        [Test]
        public async Task OpensWithTheItemsOfTheState()
        {
            var (open, cut, copy, undo, paste, table) = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                window.KeyTextInput("a/b ");
                Settle(window);
                editor.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
                Settle(window);
                var menu = editor.OpenContextMenu;
                return Task.FromResult((menu != null, Find(menu!, "cut")!.IsEnabled, Find(menu!, "copy")!.IsEnabled,
                    Find(menu!, "undo")!.IsEnabled, Find(menu!, "paste")!.IsEnabled, Find(menu!, "table") != null));
            }, "context-menu.png"));
            await Assert.That(open).IsTrue();
            await Assert.That(cut).IsFalse();
            await Assert.That(copy).IsFalse();
            await Assert.That(undo).IsTrue();
            await Assert.That(paste).IsTrue();
            await Assert.That(table).IsFalse();
        }

        [Test]
        public async Task CopyAsLaTeX()
        {
            var text = await Headless.RunAsync(() => WithEditor(async (window, editor) =>
            {
                editor.Equation = Display("a/b");
                Settle(window);
                editor.ShowContextMenu();
                Click(Find(editor.OpenContextMenu!, "copy-as-latex")!);
                Settle(window);
                return await window.Clipboard!.TryGetTextAsync();
            }));
            await Assert.That(text).StartsWith("\\frac");
            await Assert.That(Equation.Parse(text!, EquationFormat.LaTeX, new EquationOptions { Display = true })).IsEqualTo(Display("a/b"));
        }

        [Test]
        public async Task CommandsOfTheMenu()
        {
            var (numbered, lines, linear) = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                editor.Equation = Display("x=1");
                Settle(window);
                editor.ShowContextMenu();
                Click(Find(editor.OpenContextMenu!, "number")!);
                var number = editor.EquationNumber;
                editor.Editor.SetEquationNumber(null);
                editor.ShowContextMenu();
                Click(Find(editor.OpenContextMenu!, "new-line")!);
                var count = editor.Editor.LineCount;
                editor.ShowContextMenu();
                Click(Find(editor.OpenContextMenu!, "view")!);
                return Task.FromResult((number, count, editor.ViewMode));
            }));
            await Assert.That(numbered).IsEqualTo("(1)");
            await Assert.That(lines).IsEqualTo(2);
            await Assert.That(linear).IsEqualTo(MathViewMode.Linear);
        }

        [Test]
        public async Task TableItemsInATable()
        {
            var (present, equation) = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                editor.Editor.InsertMatrix(2, 2, "(", ")");
                Settle(window);
                editor.ShowContextMenu();
                var menu = editor.OpenContextMenu!;
                var found = Find(menu, "table") != null;
                Click(Find(menu, "row-below")!);
                return Task.FromResult((found, editor.Editor.GetText(EquationFormat.UnicodeMath)));
            }));
            await Assert.That(present).IsTrue();
            await Assert.That(equation.Count(c => c == '@')).IsEqualTo(2);
        }

        /// <summary>A right click outside the selection moves the caret there; inside, it keeps the selection.</summary>
        [Test]
        public async Task RightClickMovesTheCaretOutsideTheSelection()
        {
            var (kept, moved) = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                editor.Equation = Display("a+b+c");
                Settle(window);
                editor.Editor.SetCaret(new MathPosition(new[] { 0 }, 0));
                editor.Editor.SetCaret(new MathPosition(new[] { 0 }, 2), extend: true);
                Settle(window);
                editor.ShowContextMenu(Center(editor, new MathPosition(new[] { 0 }, 1)));
                var stillSelected = editor.Editor.Selection != null;
                editor.OpenContextMenu?.Close();
                editor.ShowContextMenu(Center(editor, new MathPosition(new[] { 0 }, 5)));
                return Task.FromResult((stillSelected, editor.Editor.Selection == null && editor.Editor.Caret.Offset == 5));
            }));
            await Assert.That(kept).IsTrue();
            await Assert.That(moved).IsTrue();
        }

        /// <summary>The menu opens on a right click of the mouse, and on the Menu key or Shift+F10.</summary>
        [Test]
        public async Task OpensOnRightClickAndKeys()
        {
            var (mouse, menuKey, shiftF10) = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                editor.Equation = Display("a+b");
                Settle(window);
                var point = editor.TranslatePoint(new Point(20, editor.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Right);
                window.MouseUp(point, MouseButton.Right);
                Settle(window);
                var byMouse = editor.OpenContextMenu != null;
                editor.OpenContextMenu?.Close();
                editor.Focus();
                window.KeyPress(Key.Apps, RawInputModifiers.None, PhysicalKey.ContextMenu, null);
                window.KeyRelease(Key.Apps, RawInputModifiers.None, PhysicalKey.ContextMenu, null);
                Settle(window);
                var byMenuKey = editor.OpenContextMenu != null;
                editor.OpenContextMenu?.Close();
                editor.Focus();
                window.KeyPress(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
                window.KeyRelease(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
                Settle(window);
                return Task.FromResult((byMouse, byMenuKey, editor.OpenContextMenu != null));
            }));
            await Assert.That(mouse).IsTrue().Because("mouse");
            await Assert.That(menuKey).IsTrue().Because("menu key");
            await Assert.That(shiftF10).IsTrue().Because("shift f10");
        }

        [Test]
        public async Task ReadOnlyHasCopyAndSelectAll()
        {
            var tags = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                editor.Equation = Display("a/b");
                editor.IsReadOnly = true;
                Settle(window);
                editor.ShowContextMenu();
                return Task.FromResult(editor.OpenContextMenu!.Items.OfType<MenuItem>().Select(i => (string)i.Tag!).ToList());
            }));
            await Assert.That(tags).IsEquivalentTo(new[] { "copy", "copy-as", "select-all" });
        }

        [Test]
        public async Task AnApplicationChangesOrDisablesTheMenu()
        {
            var (added, disabled) = await Headless.RunAsync(() => WithEditor((window, editor) =>
            {
                editor.ContextMenuOpening += (_, e) => e.Menu.Items.Add(new MenuItem { Header = "Mine", Tag = "mine" });
                editor.ShowContextMenu();
                var mine = Find(editor.OpenContextMenu!, "mine") != null;
                editor.OpenContextMenu!.Close();
                editor.IsContextMenuEnabled = false;
                return Task.FromResult((mine, !editor.ShowContextMenu()));
            }));
            await Assert.That(added).IsTrue();
            await Assert.That(disabled).IsTrue();
        }
    }
}
