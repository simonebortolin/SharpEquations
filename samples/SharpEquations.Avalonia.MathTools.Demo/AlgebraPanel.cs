namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AngouriMath;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using global::Avalonia.Layout;
    using global::Avalonia.Threading;
    using SharpEquations;

    /// <summary>
    /// What the calculator computes from the equation of the editor, with AngouriMath: its value as it is typed, and on a
    /// click its solutions, its simplified, expanded or factored form, its derivative and its integral, each result
    /// drawn and ready to go back to the editor.
    /// </summary>
    public sealed class AlgebraPanel : UserControl
    {
        private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static readonly (string Action, string Face, string Tip)[] Buttons =
        {
            (Algebra.Solve, "Solve", "The solutions of the equation, or where the expression is 0"),
            (Algebra.Simplify, "Simplify", "The expression in a simpler form"),
            (Algebra.Expand, "Expand", "Products and powers multiplied out"),
            (Algebra.Factor, "Factor", "The expression as a product"),
            (Algebra.Differentiate, "d/dx", "The derivative by the letter chosen"),
            (Algebra.Integrate, "∫ dx", "An antiderivative by the letter chosen"),
        };

        private readonly MathEditBox editor;
        private readonly MathView answer = new MathView { FontSize = 26, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly TextBlock answerNote = Ui.Muted("", 12.5);
        private readonly StackPanel results = new StackPanel { Spacing = 12 };
        private readonly DispatcherTimer timer;
        private int version;

        public AlgebraPanel(MathEditBox editor)
        {
            this.editor = editor;
            Letter = new ComboBox { MinWidth = 70 };
            timer = new DispatcherTimer { Interval = Delay };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                await RefreshAnswer().ConfigureAwait(true);
            };
            editor.EquationChanged += (_, _) =>
            {
                timer.Stop();
                timer.Start();
            };

            var buttons = new List<Control>();
            foreach (var (action, face, tip) in Buttons)
            {
                var button = new Button { Content = face, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5) };
                ToolTip.SetTip(button, tip);
                button.Click += async (_, _) => await RunAsync(action).ConfigureAwait(true);
                buttons.Add(button);
            }
            var forLabel = Ui.Muted("by", 12.5);
            buttons.Add(forLabel);
            buttons.Add(Letter);

            var answerFrame = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18, 12),
                MinHeight = 60,
                Child = new StackPanel { Spacing = 4, Children = { answer, answerNote } },
            };
            answerFrame.Bind(Border.BackgroundProperty, answerFrame.GetResourceObservable("CodeBrush"));
            answerFrame.ContextMenu = new ContextMenu();
            answerFrame.ContextMenu.Opening += (_, e) =>
            {
                if (Answer == null)
                {
                    e.Cancel = true;
                    return;
                }
                answerFrame.ContextMenu.ItemsSource = MenuItems(Answer);
            };

            Content = new StackPanel { Spacing = 14, Children = { answerFrame, Ui.Row(buttons.ToArray()), results } };
            ShowAnswer(null, "The value of what is typed shows here.");
        }

        /// <summary>The letter to solve for, and to differentiate and integrate by.</summary>
        public ComboBox Letter { get; }

        /// <summary>The value of the equation of the editor, as it was last computed.</summary>
        public AlgebraResult? Answer { get; private set; }

        /// <summary>The results of the actions, the last first.</summary>
        public IReadOnlyList<AlgebraResult> Results => history;

        private readonly List<AlgebraResult> history = new List<AlgebraResult>();

        /// <summary>Computes the value of the equation of the editor now, as typing does after a pause.</summary>
        public async Task RefreshAnswer()
        {
            timer.Stop();
            var current = ++version;
            if (!TryRead(out var expression, out var error))
            {
                UpdateLetters(null);
                ShowAnswer(null, error);
                return;
            }
            UpdateLetters(expression);
            var (result, message) = await Compute(Algebra.Evaluate, expression!, null).ConfigureAwait(true);
            if (current == version)
                ShowAnswer(result, message);
        }

        /// <summary>Does an action to the equation of the editor, as its button does, and adds the result to the list.</summary>
        public async Task<AlgebraResult?> RunAsync(string action)
        {
            if (!TryRead(out var expression, out var error))
            {
                AddMessage(error!);
                return null;
            }
            var letter = Letter.SelectedItem as string;
            var (result, message) = await Compute(action, expression!, letter).ConfigureAwait(true);
            if (result == null)
            {
                AddMessage(message!);
                return null;
            }
            history.Insert(0, result);
            results.Children.Insert(0, ResultCard(result, Label(action, letter ?? "x")));
            while (results.Children.Count > 8)
                results.Children.RemoveAt(results.Children.Count - 1);
            return result;
        }

        private bool TryRead(out Entity? expression, out string? error)
        {
            expression = null;
            error = null;
            var equation = editor.Equation;
            if (equation == null)
            {
                error = "The value of what is typed shows here.";
                return false;
            }
            if (equation.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror"))
            {
                error = "Not built up yet: finish the argument, or press Build up (Enter).";
                return false;
            }
            try
            {
                expression = MathReader.Read(equation);
                return true;
            }
            catch (NotSupportedException e)
            {
                error = e.Message;
                return false;
            }
        }

        // on another thread, since AngouriMath can take long (it cannot be stopped: a result too late is dropped)
        private static async Task<(AlgebraResult? Result, string? Message)> Compute(string action, Entity expression, string? letter)
        {
            var work = Task.Run(() => Algebra.Run(action, expression, letter));
            if (await Task.WhenAny(work, Task.Delay(Timeout)).ConfigureAwait(true) != work)
                return (null, "It takes too long: AngouriMath found no result in " + Timeout.TotalSeconds + " seconds.");
            try
            {
                return (await work.ConfigureAwait(true), null);
            }
#pragma warning disable CA1031 // what AngouriMath cannot do, it says with exceptions of many kinds
            catch (Exception e)
#pragma warning restore CA1031
            {
                return (null, "AngouriMath cannot do it: " + e.Message);
            }
        }

        private void UpdateLetters(Entity? expression)
        {
            var letters = expression == null ? Array.Empty<string>() : Algebra.Variables(expression);
            var selected = Letter.SelectedItem as string;
            if (Letter.ItemsSource is IReadOnlyList<string> old && old.SequenceEqual(letters))
                return;
            Letter.ItemsSource = letters;
            Letter.SelectedItem = selected != null && letters.Contains(selected) ? selected : letters.Count > 0 ? letters[0] : null;
        }

        private void ShowAnswer(AlgebraResult? result, string? message)
        {
            Answer = result;
            answer.Equation = result?.Math;
            answer.IsVisible = result?.Math != null;
            answerNote.Text = result == null ? message : result.Math == null ? result.Text : null;
            answerNote.IsVisible = !string.IsNullOrEmpty(answerNote.Text);
        }

        private void AddMessage(string message)
        {
            results.Children.Insert(0, Ui.Muted(message, 13));
            while (results.Children.Count > 8)
                results.Children.RemoveAt(results.Children.Count - 1);
        }

        private Border ResultCard(AlgebraResult result, string label)
        {
            Control math = result.Math != null
                ? new MathView { FontSize = 22, Equation = result.Math, HorizontalAlignment = HorizontalAlignment.Left }
                : new SelectableTextBlock { Text = result.Text, FontSize = 14, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            var body = new StackPanel { Spacing = 6, Children = { math } };
            if (result.ApproximateText != null)
            {
                body.Children.Add(result.Approximate != null
                    ? new MathView { FontSize = 18, Equation = result.Approximate, HorizontalAlignment = HorizontalAlignment.Left }
                    : new SelectableTextBlock { Text = result.ApproximateText, FontSize = 14, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            }

            var copy = new Button { Content = "Copy", CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 3) };
            ToolTip.SetTip(copy, "Copy the result, as text and as MathML (right-click for more)");
            copy.Click += async (_, _) => await CopyAsync(result.Math, result.Text, null).ConfigureAwait(true);
            var use = new Button { Content = "To the editor", CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 3), IsEnabled = result.Math != null };
            use.Click += (_, _) => ToEditor(result.Math);
            var top = new DockPanel { Children = { Ui.Docked(use, Dock.Right), Ui.Docked(copy, Dock.Right), Ui.Small(label) } };
            copy.Margin = new Thickness(0, 0, 6, 0);
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 10),
                Child = new StackPanel { Spacing = 8, Children = { top, body } },
                ContextMenu = new ContextMenu { ItemsSource = MenuItems(result) },
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CodeBrush"));
            return card;
        }

        // the right-click menu of a result: copy it as text, LaTeX or MathML, its decimal values, or put it in the editor
        private List<MenuItem> MenuItems(AlgebraResult result)
        {
            var items = new List<MenuItem>
            {
                Item("Copy", () => CopyAsync(result.Math, result.Text, null)),
                Item("Copy as LaTeX", () => CopyAsync(result.Math, result.Text, EquationFormat.LaTeX), result.Math != null),
                Item("Copy as MathML", () => CopyAsync(result.Math, result.Text, EquationFormat.MathML), result.Math != null),
            };
            if (result.ApproximateText != null)
                items.Add(Item("Copy decimal values", () => CopyAsync(result.Approximate, result.ApproximateText, null)));
            items.Add(Item("To the editor", () => { ToEditor(result.Math); return Task.CompletedTask; }, result.Math != null));
            if (result.Approximate != null)
                items.Add(Item("Decimal values to the editor", () => { ToEditor(result.Approximate); return Task.CompletedTask; }));
            items.Add(Item("Paste into the editor", PasteAsync));
            return items;
        }

        private static MenuItem Item(string header, Func<Task> action, bool enabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += async (_, _) => await action().ConfigureAwait(true);
            return item;
        }

        private void ToEditor(Equation? math)
        {
            if (math == null)
                return;
            editor.Equation = math;
            editor.Focus();
        }

        private async Task PasteAsync()
        {
            editor.Focus();
            await editor.PasteAsync().ConfigureAwait(true);
        }

        // the result on the clipboard: as text (UnicodeMath, or the format asked) and as MathML for Word and the editor
        private async Task CopyAsync(Equation? math, string text, EquationFormat? format)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            var item = new DataTransferItem();
            item.SetText(math == null ? text : math.ToString(format ?? EquationFormat.UnicodeMath));
            if (math != null)
                item.Set(MathEditBox.MathMLClipboardFormat, MathClipboard.Encode(math.ToString(EquationFormat.MathML)));
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data).ConfigureAwait(true);
        }

        private static string Label(string action, string letter) => action switch
        {
            Algebra.Solve => "Solve for " + letter,
            Algebra.Differentiate => "Derivative by " + letter,
            Algebra.Integrate => "Antiderivative by " + letter,
            _ => action,
        };
    }
}
