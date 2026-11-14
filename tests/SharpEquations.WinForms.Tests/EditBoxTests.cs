namespace SharpEquations.WinForms.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using SkiaSharp;

    /// <summary>The editor of Windows Forms in a form: drawing, typing, keys, pointer, caret, floating bar, context menu.</summary>
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
                editor.Focus();
                await Ui.Settle();
            }
            return editor;
        }

        /// <summary>The dark pixels Skia drew for the control, in its last paint.</summary>
        internal static int DarkPixels(SKBitmap? pixels)
        {
            if (pixels == null)
                return 0;
            var count = 0;
            for (var y = 0; y < pixels.Height; y++)
            {
                for (var x = 0; x < pixels.Width; x++)
                {
                    var c = pixels.GetPixel(x, y);
                    if (c.Alpha > 200 && c.Red < 80 && c.Green < 80 && c.Blue < 80)
                        count++;
                }
            }
            return count;
        }

        [Test]
        public async Task DrawsInk()
        {
            var (width, height, dark, empty) = await Ui.Run(async () =>
            {
                var editor = await Show("x=(-b±√(b^2-4ac))/2a", focus: false);
                editor.Refresh();
                var (w, h, ink) = (editor.Width, editor.Height, DarkPixels(editor.Surface.Pixels));
                var blank = await Show(focus: false);
                blank.Refresh();
                return (w, h, ink, DarkPixels(blank.Surface.Pixels));
            });
            await Assert.That(width).IsGreaterThan(100);
            await Assert.That(height).IsGreaterThan(60);
            await Assert.That(dark).IsGreaterThan(200);
            await Assert.That(empty).IsLessThan(dark / 4);
        }

        [Test]
        public async Task HeightFollowsTheEquation()
        {
            var (flat, tall) = await Ui.Run(async () =>
            {
                var editor = await Show("x", focus: false);
                var before = editor.Height;
                editor.Equation = U("(a/b)/(c/d)");
                await Ui.Settle();
                return (before, editor.Height);
            });
            await Assert.That(tall).IsGreaterThan(flat);
        }

        [Test]
        public async Task GlyphsArePathsOfTheirOutlines()
        {
            var (x, space) = await Ui.Run(() =>
            {
                var font = MathFonts.Default;
                font.TryGetGlyph('x', out var xGlyph);
                font.TryGetGlyph(' ', out var spaceGlyph);
                return (MathCanvas.Path(font, xGlyph)!.Bounds, MathCanvas.Path(font, spaceGlyph));
            });
            await Assert.That(x.Width).IsGreaterThan(100);
            await Assert.That(x.Bottom).IsGreaterThan(300);
            await Assert.That(space).IsNull();
        }

        [Test]
        public async Task TypingBuildsUp()
        {
            var (equation, changed) = await Ui.Run(async () =>
            {
                var editor = await Show();
                Equation? last = null;
                editor.EquationChanged += (_, e) => last = e.Equation;
                foreach (var ch in "a/b+x^2 ")
                    editor.Type(ch.ToString());
                await Ui.Settle();
                return (editor.Equation, last);
            });
            await Assert.That(equation).IsEqualTo(U("a/b+x^2"));
            await Assert.That(changed).IsEqualTo(equation);
        }

        [Test]
        public async Task ControlCharactersAreKeys()
        {
            var (backspace, text) = await Ui.Run(async () =>
            {
                var editor = await Show();
                return (editor.Type("\b"), editor.Type("x"));
            });
            await Assert.That(backspace).IsFalse();
            await Assert.That(text).IsTrue();
        }

        [Test]
        public async Task KeysEditAndUndo()
        {
            var (deleted, undone, selected, linear) = await Ui.Run(async () =>
            {
                var editor = await Show();
                editor.Type("x+y");
                editor.HandleKey(Keys.Back, MathKeyModifiers.None);
                var afterDelete = editor.Equation;
                editor.HandleKey(Keys.Z, MathKeyModifiers.Control);
                var afterUndo = editor.Equation;
                editor.HandleKey(Keys.Left, MathKeyModifiers.Shift);
                var selection = editor.Editor.Selection != null;
                editor.HandleKey(Keys.L, MathKeyModifiers.Control | MathKeyModifiers.Shift);
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
                var before = editor.View.IsFocused;
                editor.Focus();
                await Ui.Settle();
                var after = editor.Focused && editor.View.IsFocused && editor.View.IsCaretVisible;
                editor.IsReadOnly = true;
                return (before, after, editor.View.IsReadOnly);
            });
            await Assert.That(unfocused).IsFalse();
            await Assert.That(focused).IsTrue();
            await Assert.That(readOnly).IsTrue();
        }

        [Test]
        public async Task QuickBarCompletesACommand()
        {
            var (open, items, focusKept, equation, closed) = await Ui.Run(async () =>
            {
                var editor = await Show();
                editor.Type("x+\\al");
                await Ui.Settle();
                var wasOpen = editor.IsQuickBarOpen;
                var count = editor.QuickBarWindow?.Count ?? 0;
                var focus = editor.Focused;
                var alpha = editor.View.QuickBar.Completions.ToList().FindIndex(c => c.Name == "alpha");
                for (var i = 0; i < alpha; i++)
                    editor.HandleKey(Keys.Down, MathKeyModifiers.None);
                editor.HandleKey(Keys.Tab, MathKeyModifiers.None);
                editor.Type(" ");
                await Ui.Settle();
                return (wasOpen, count, focus, editor.Equation, !editor.IsQuickBarOpen);
            });
            await Assert.That(open).IsTrue();
            await Assert.That(items).IsGreaterThan(1);
            await Assert.That(focusKept).IsTrue();
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
                editor.HandleKey(Keys.Escape, MathKeyModifiers.None);
                await Ui.Settle();
                return (wasOpen, !editor.IsQuickBarOpen);
            });
            await Assert.That(open).IsTrue();
            await Assert.That(closed).IsTrue();
        }

        [Test]
        public async Task StructuresForTheSelection()
        {
            var (open, items, equation) = await Ui.Run(async () =>
            {
                var editor = await Show("a+b");
                editor.Editor.SelectAll();
                await Ui.Settle();
                var wasOpen = editor.IsQuickBarOpen;
                var count = editor.QuickBarWindow?.Count ?? 0;
                editor.View.QuickBar.GetItems(editor.QuickSymbols).First(i => i.Text == "√x").Invoke();
                await Ui.Settle();
                return (wasOpen, count, editor.Equation);
            });
            await Assert.That(open).IsTrue();
            await Assert.That(items).IsEqualTo(10);
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
            var (tags, readOnlyTags, undoKey, cancelled) = await Ui.Run(async () =>
            {
                var editor = new MenuEditBox { FontSize = 28, Equation = U("a+b") };
                await Ui.Show(editor);
                using var menu = editor.Menu();
                var all = menu.Items.Cast<ToolStripItem>().Select(i => i.Tag as string).OfType<string>().ToList();
                var undo = menu.Items.OfType<ToolStripMenuItem>().First(i => (string?)i.Tag == "undo").ShortcutKeyDisplayString;
                editor.IsReadOnly = true;
                using var readOnlyMenu = editor.Menu();
                var readOnly = readOnlyMenu.Items.Cast<ToolStripItem>().Select(i => i.Tag as string).OfType<string>().ToList();
                editor.MathContextMenuOpening += (_, e) => e.Cancel = true;
                return (all, readOnly, undo, !editor.ShowContextMenu());
            });
            await Assert.That(tags).Contains("undo");
            await Assert.That(tags).Contains("copy-as");
            await Assert.That(tags).Contains("number");
            await Assert.That(readOnlyTags).IsEquivalentTo(new[] { "copy", "copy-as", "select-all" });
            await Assert.That(undoKey).IsEqualTo("Ctrl+Z");
            await Assert.That(cancelled).IsTrue();
        }

        [Test]
        public async Task CopyPutsTextAndMathML()
        {
            var (text, mathML, pasted) = await Ui.Run(async () =>
            {
                var editor = await Show("a/b");
                editor.Editor.SelectAll();
                editor.Copy();
                var data = Clipboard.GetDataObject()!;
                var copiedText = Clipboard.GetText();
                var bytes = ((System.IO.MemoryStream)data.GetData(MathEditBox.MathMLClipboardFormat)!).ToArray();
                editor.Editor.SelectAll();
                editor.Editor.DeleteSelection();
                editor.Paste();
                return (copiedText, MathClipboard.Decode(bytes), editor.Equation);
            });
            await Assert.That(Equation.Parse(text, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a/b", EquationFormat.UnicodeMath));
            await Assert.That(mathML).Contains("mfrac");
            await Assert.That(pasted).IsEqualTo(U("a/b"));
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

        [Test]
        public async Task ScreenReadersReadUnicodeMath()
        {
            var (role, value, set) = await Ui.Run(async () =>
            {
                var editor = await Show("a/b", focus: false);
                var accessible = editor.AccessibilityObject;
                var before = accessible.Value;
                accessible.Value = "x^2";
                return (accessible.Role, before, editor.Equation);
            });
            await Assert.That(role).IsEqualTo(AccessibleRole.Text);
            await Assert.That(Equation.Parse(value!, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a/b", EquationFormat.UnicodeMath));
            await Assert.That(set).IsEqualTo(U("x^2"));
        }

        /// <summary>The editor with its context menu at hand.</summary>
        private sealed class MenuEditBox : MathEditBox
        {
            public ContextMenuStrip Menu() => CreateContextMenu();
        }
    }
}
