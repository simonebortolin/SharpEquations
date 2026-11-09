namespace SharpEquations.Avalonia.Tests.Controls
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Headless;
    using global::Avalonia.Input;
    using global::Avalonia.Media;
    using global::Avalonia.Threading;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>The floating bar at the caret: completions, structures for the selection, table commands, symbols.</summary>
    [NotInParallel("headless")]
    public class QuickBarTests
    {
        private static T WithEditor<T>(Func<Window, MathEditBox, T> test, string? picture = null)
        {
            var editor = new MathEditBox { FontSize = 28, Foreground = Brushes.Black };
            var window = new Window { Content = new StackPanel { Children = { editor } }, Width = 700, Height = 360, Background = Brushes.White };
            window.Show();
            editor.Focus();
            try
            {
                var result = test(window, editor);
                if (picture != null && Environment.GetEnvironmentVariable("SHARPEQUATIONS_SCREENSHOTS") is { } directory && window.CaptureRenderedFrame() is { } frame)
                {
                    Directory.CreateDirectory(directory);
                    frame.Save(Path.Combine(directory, picture), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                return result;
            }
            finally
            {
                window.Close();
            }
        }

        private static void Settle(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        [Test]
        public async Task CompletionsOfACommand()
        {
            var (mode, count, equation) = await Headless.Run(() => WithEditor((window, editor) =>
            {
                window.KeyTextInput("x+\\al");
                Settle(window);
                var shown = (editor.QuickBar.Mode, editor.QuickBar.Completions.Count);
                var index = 0;
                for (var i = 0; i < editor.QuickBar.Completions.Count; i++)
                {
                    if (editor.QuickBar.Completions[i].Name == "alpha")
                        index = i;
                }
                for (var i = 0; i < index; i++)
                    window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
                Settle(window);
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyTextInput(" ");
                Settle(window);
                return (shown.Mode, shown.Count, editor.Equation);
            }, "quickbar-completions.png"));
            await Assert.That(mode).IsEqualTo(MathQuickBarMode.Completions);
            await Assert.That(count).IsGreaterThan(1);
            await Assert.That(equation).IsEqualTo(Equation.Parse("x+α", EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
        }

        [Test]
        public async Task EscapeClosesTheCompletions()
        {
            var (open, closed) = await Headless.Run(() => WithEditor((window, editor) =>
            {
                window.KeyTextInput("\\su");
                Settle(window);
                var wasOpen = editor.IsQuickBarOpen;
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Settle(window);
                return (wasOpen, !editor.IsQuickBarOpen);
            }));
            await Assert.That(open).IsTrue();
            await Assert.That(closed).IsTrue();
        }

        [Test]
        public async Task StructuresForTheSelectionAndTableCommands()
        {
            var (selection, table, symbols) = await Headless.Run(() => WithEditor((window, editor) =>
            {
                editor.Equation = Equation.Parse("a+b", EquationFormat.UnicodeMath, new EquationOptions { Display = true });
                editor.Editor.SelectAll();
                Settle(window);
                var onSelection = editor.QuickBar.Mode;
                editor.Editor.MoveEnd();
                editor.Editor.InsertMatrix(2, 2, "(", ")");
                Settle(window);
                var inTable = editor.QuickBar.Mode;
                editor.Editor.Undo();
                editor.Editor.InsertStructure("mfrac");
                Settle(window);
                return (onSelection, inTable, editor.QuickBar.Mode);
            }, "quickbar-slot.png"));
            await Assert.That(selection).IsEqualTo(MathQuickBarMode.Selection);
            await Assert.That(table).IsEqualTo(MathQuickBarMode.Table);
            await Assert.That(symbols).IsEqualTo(MathQuickBarMode.Symbols);
        }

        [Test]
        public async Task PictureOfTheCompletions()
        {
            var open = await Headless.Run(() => WithEditor((window, editor) =>
            {
                editor.Equation = Equation.Parse("a+b", EquationFormat.UnicodeMath, new EquationOptions { Display = true });
                editor.Editor.MoveEnd();
                editor.Editor.InsertStructure("mfrac");
                window.KeyTextInput("\\sq");
                Settle(window);
                Settle(window);
                return editor.IsQuickBarOpen;
            }, "quickbar-open.png"));
            await Assert.That(open).IsTrue();
        }

        [Test]
        public async Task DisabledBarStaysClosed()
        {
            var open = await Headless.Run(() => WithEditor((window, editor) =>
            {
                editor.IsQuickBarEnabled = false;
                window.KeyTextInput("\\al");
                Settle(window);
                return editor.IsQuickBarOpen;
            }));
            await Assert.That(open).IsFalse();
        }
    }
}
