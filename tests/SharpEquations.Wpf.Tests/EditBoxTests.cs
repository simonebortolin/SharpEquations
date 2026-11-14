namespace SharpEquations.Wpf.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Automation;
    using System.Windows.Automation.Peers;
    using System.Windows.Automation.Provider;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>The editor of WPF in a window: drawing, typing, keys, pointer, caret, floating bar, context menu.</summary>
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

        private static int DarkPixels(System.Windows.FrameworkElement element)
        {
            var bitmap = new RenderTargetBitmap((int)System.Math.Ceiling(element.ActualWidth), (int)System.Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            var count = 0;
            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
                if (pixels[i + 3] > 200 && pixels[i] < 80 && pixels[i + 1] < 80 && pixels[i + 2] < 80)
                    count++;
            }
            return count;
        }

        [Test]
        public async Task DrawsInk()
        {
            var (size, dark, empty) = await Ui.Run(async () =>
            {
                var editor = await Show("x=(-b±√(b^2-4ac))/2a", focus: false);
                var (size, ink) = (editor.RenderSize, DarkPixels(editor));
                var blank = await Show(focus: false);
                return (size, ink, DarkPixels(blank));
            });
            await Assert.That(size.Width).IsGreaterThan(100);
            await Assert.That(size.Height).IsGreaterThan(60);
            await Assert.That(dark).IsGreaterThan(200);
            await Assert.That(empty).IsLessThan(dark / 4);
        }

        [Test]
        public async Task GlyphsAreGeometriesOfTheirOutlines()
        {
            var (x, space) = await Ui.Run(() =>
            {
                var font = MathFonts.Default;
                font.TryGetGlyph('x', out var xGlyph);
                font.TryGetGlyph(' ', out var spaceGlyph);
                return (MathCanvas.Geometry(font, xGlyph)!.Bounds, MathCanvas.Geometry(font, spaceGlyph));
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
                editor.HandleKey(Key.Back, MathKeyModifiers.None);
                var afterDelete = editor.Equation;
                editor.HandleKey(Key.Z, MathKeyModifiers.Control);
                var afterUndo = editor.Equation;
                editor.HandleKey(Key.Left, MathKeyModifiers.Shift);
                var selection = editor.Editor.Selection != null;
                editor.HandleKey(Key.L, MathKeyModifiers.Control | MathKeyModifiers.Shift);
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
                var after = editor.IsKeyboardFocused && editor.View.IsFocused && editor.View.IsCaretVisible;
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
            var (open, buttons, equation, closed) = await Ui.Run(async () =>
            {
                var editor = await Show();
                editor.Type("x+\\al");
                await Ui.Settle();
                var wasOpen = editor.IsQuickBarOpen;
                var count = editor.QuickBarPanel.Children.Count;
                var alpha = editor.View.QuickBar.Completions.ToList().FindIndex(c => c.Name == "alpha");
                for (var i = 0; i < alpha; i++)
                    editor.HandleKey(Key.Down, MathKeyModifiers.None);
                editor.HandleKey(Key.Tab, MathKeyModifiers.None);
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
                editor.HandleKey(Key.Escape, MathKeyModifiers.None);
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
            var (tags, readOnlyTags, undoKey, cancelled) = await Ui.Run(async () =>
            {
                var editor = new MenuEditBox { FontSize = 28, Equation = U("a+b") };
                await Ui.Show(editor);
                var menu = editor.Menu();
                var all = menu.Items.OfType<Control>().Select(i => i.Tag as string).Where(t => t != null).ToList();
                var undo = menu.Items.OfType<MenuItem>().First(i => (string)i.Tag == "undo").InputGestureText;
                editor.IsReadOnly = true;
                var readOnly = editor.Menu().Items.OfType<Control>().Select(i => i.Tag as string).Where(t => t != null).ToList();
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
            var (type, value, set) = await Ui.Run(async () =>
            {
                var editor = await Show("a/b", focus: false);
                var peer = (MathEditBoxAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(editor);
                var provider = (IValueProvider)peer.GetPattern(PatternInterface.Value);
                var before = provider.Value;
                provider.SetValue("x^2");
                return (peer.GetAutomationControlType(), before, editor.Equation);
            });
            await Assert.That(type).IsEqualTo(AutomationControlType.Edit);
            await Assert.That(Equation.Parse(value, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a/b", EquationFormat.UnicodeMath));
            await Assert.That(set).IsEqualTo(U("x^2"));
        }

        /// <summary>The editor with its context menu at hand.</summary>
        private sealed class MenuEditBox : MathEditBox
        {
            public ContextMenu Menu() => CreateContextMenu();
        }
    }
}
