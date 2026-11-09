namespace SharpEquations.Avalonia.Tests.Controls
{
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Headless;
    using global::Avalonia.Input;
    using global::Avalonia.Input.Platform;
    using SharpEquations;

    /// <summary>The control in a window of the headless session: typing, keys, MathML in and out.</summary>
    [NotInParallel("headless")]
    public class MathEditBoxTests
    {
        // the editor edits display equations by default, as the equations of Word on a line of their own
        private static Equation Display(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        private static T WithEditor<T>(System.Func<Window, MathEditBox, T> test)
        {
            var editor = new MathEditBox { FontSize = 20 };
            var window = new Window { Content = editor, Width = 600, Height = 200 };
            window.Show();
            editor.Focus();
            try
            {
                return test(window, editor);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public async Task TypingBuildsUp()
        {
            var (equation, events) = await Headless.Run(() => WithEditor((window, editor) =>
            {
                var count = 0;
                editor.EquationChanged += (_, _) => count++;
                window.KeyTextInput("a/b ");
                return (editor.Equation, count);
            }));
            await Assert.That(equation).IsEqualTo(Display("a/b"));
            await Assert.That(events).IsGreaterThan(0);
        }

        [Test]
        public async Task LaTeXInput()
        {
            var equation = await Headless.Run(() => WithEditor((window, editor) =>
            {
                editor.InputFormat = EquationFormat.LaTeX;
                window.KeyTextInput("\\frac{1}{2} ");
                return editor.Equation;
            }));
            await Assert.That(equation).IsEqualTo(Display("1/2"));
        }

        [Test]
        public async Task KeysEditTheEquation()
        {
            var equation = await Headless.Run(() => WithEditor((window, editor) =>
            {
                window.KeyTextInput("x+y");
                window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                window.KeyTextInput("z ");
                return editor.Equation;
            }));
            await Assert.That(equation).IsEqualTo(Display("x+z"));
        }

        [Test]
        public async Task SetMathAndGetMath()
        {
            var (mathML, text) = await Headless.Run(() => WithEditor((_, editor) =>
            {
                editor.SetMath("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msqrt><mi>x</mi></msqrt></math>");
                return (editor.GetMath(), editor.GetText(EquationFormat.UnicodeMath));
            }));
            await Assert.That(mathML).Contains("msqrt");
            await Assert.That(Equation.Parse(text, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("√x", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task BindingTheEquationLoadsIt()
        {
            var text = await Headless.Run(() => WithEditor((_, editor) =>
            {
                editor.Equation = Equation.Parse("a^2", EquationFormat.UnicodeMath);
                return editor.Editor.GetText(EquationFormat.UnicodeMath);
            }));
            await Assert.That(Equation.Parse(text, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a^2", EquationFormat.UnicodeMath));
        }

        [Test]
        public async Task LinearViewMode()
        {
            var (linear, back) = await Headless.Run(() => WithEditor((window, editor) =>
            {
                window.KeyTextInput("a/b ");
                editor.ViewMode = MathViewMode.Linear;
                var isRun = editor.Editor.Root.Children[0].Children[0].IsRun;
                editor.ViewMode = MathViewMode.Professional;
                return (isRun, editor.Equation);
            }));
            await Assert.That(linear).IsTrue();
            await Assert.That(back).IsEqualTo(Display("a/b"));
        }

        [Test]
        public async Task TabMovesThroughTheCells()
        {
            var equation = await Headless.Run(() => WithEditor((window, editor) =>
            {
                editor.Editor.InsertMatrix(1, 2);
                window.KeyTextInput("a");
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyTextInput("b");
                return editor.Equation;
            }));
            await Assert.That(equation).IsEqualTo(Display("■(a&b)"));
        }

        [Test]
        public async Task AccessibleValueIsUnicodeMath()
        {
            var value = await Headless.Run(() => WithEditor((window, editor) =>
            {
                window.KeyTextInput("a/b ");
                var peer = (MathEditBoxAutomationPeer)global::Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(editor);
                var before = peer.Value;
                peer.SetValue("x^2");
                return (before, editor.Equation);
            }));
            await Assert.That(Equation.Parse(value.before, EquationFormat.UnicodeMath)).IsEqualTo(Equation.Parse("a/b", EquationFormat.UnicodeMath));
            await Assert.That(value.Equation).IsEqualTo(Display("x^2"));
        }

        [Test]
        public async Task CopyAndPaste()
        {
            var result = await Headless.RunAsync(async () =>
            {
                var editor = new MathEditBox { FontSize = 20, CopyTextFormat = EquationFormat.LaTeX };
                var window = new Window { Content = editor, Width = 600, Height = 200 };
                window.Show();
                editor.Focus();
                string? copied = null;
                editor.Copying += (_, e) => copied = e.Equation.ToString(EquationFormat.UnicodeMath);
                window.KeyTextInput("a/b ");
                editor.Editor.SelectAll();
                await editor.CopyAsync();
                var text = await window.Clipboard!.TryGetTextAsync();
                editor.Editor.MoveEnd();
                window.KeyTextInput("+");
                await editor.PasteAsync();
                var equation = editor.Equation;
                window.Close();
                return (copied, text, equation);
            });
            await Assert.That(result.copied).IsNotNull();
            await Assert.That(result.text).StartsWith("\\frac");
            await Assert.That(result.equation).IsEqualTo(Display("a/b+a/b"));
        }

        [Test]
        public async Task ClickPlacesTheCaret()
        {
            var caret = await Headless.Run(() => WithEditor((window, editor) =>
            {
                editor.Equation = Equation.Parse("a/b", EquationFormat.UnicodeMath);
                window.UpdateLayout();
                var layout = editor.Layout!;
                // a click below the middle of the equation, in the denominator
                var point = new global::Avalonia.Point(editor.Padding.Left + editor.BorderThickness + layout.Width / 2, 0);
                var origin = editor.TranslatePoint(point, window)!.Value;
                var y = editor.TranslatePoint(new global::Avalonia.Point(0, editor.Bounds.Height / 2 + layout.Height / 4), window)!.Value.Y;
                window.MouseDown(new global::Avalonia.Point(origin.X, y), MouseButton.Left);
                window.MouseUp(new global::Avalonia.Point(origin.X, y), MouseButton.Left);
                return editor.Editor.Caret;
            }));
            await Assert.That(caret.Path).IsEquivalentTo(new[] { 0, 0, 1 });
        }
    }
}
