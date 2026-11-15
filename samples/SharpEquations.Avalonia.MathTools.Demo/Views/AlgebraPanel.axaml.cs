namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using global::Avalonia.Interactivity;
    using SharpEquations;
    using SharpEquations.MathTools;
    using SharpEquations.Rendering;

    /// <summary>
    /// What the calculator computes from the equation of the editor (<see cref="AlgebraViewModel"/>): its value, and the
    /// results of the actions, each ready to be copied or to go back to the editor.
    /// </summary>
    public sealed partial class AlgebraPanel : UserControl
    {
        public AlgebraPanel() => InitializeComponent();

        /// <summary>The view model: the value and the results.</summary>
        public AlgebraViewModel? ViewModel => DataContext as AlgebraViewModel;

        /// <summary>The editor the results are pasted into.</summary>
        public MathEditBox? Editor { get; set; }

        private void OnUseClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: AlgebraItem item })
                ViewModel?.Use(item.Math);
        }

        private async void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: AlgebraItem { Result: { } result } })
                await CopyAsync(result.Math, result.Text, null).ConfigureAwait(true);
        }

        private void OnAnswerMenuOpening(object? sender, CancelEventArgs e)
        {
            if (sender is not ContextMenu menu || ViewModel?.Answer is not { } answer)
            {
                e.Cancel = true;
                return;
            }
            menu.ItemsSource = MenuItems(answer);
        }

        private void OnResultMenuOpening(object? sender, CancelEventArgs e)
        {
            if (sender is not ContextMenu menu || (menu.PlacementTarget?.DataContext ?? menu.DataContext) is not AlgebraItem { Result: { } result })
            {
                e.Cancel = true;
                return;
            }
            menu.ItemsSource = MenuItems(result);
        }

        // the right-click menu of a result: copy it as text, LaTeX or MathML, its decimal values, or put it in the editor
        private List<MenuItem> MenuItems(CasResult result)
        {
            var items = new List<MenuItem>
            {
                Item("Copy", () => CopyAsync(result.Math, result.Text, null)),
                Item("Copy as LaTeX", () => CopyAsync(result.Math, result.Text, EquationFormat.LaTeX), result.Math != null),
                Item("Copy as MathML", () => CopyAsync(result.Math, result.Text, EquationFormat.MathML), result.Math != null),
            };
            if (result.ApproximateText != null)
                items.Add(Item("Copy decimal values", () => CopyAsync(result.Approximate, result.ApproximateText, null)));
            items.Add(Item("To the editor", () => Use(result.Math), result.Math != null));
            if (result.Approximate != null)
                items.Add(Item("Decimal values to the editor", () => Use(result.Approximate)));
            items.Add(Item("Paste into the editor", PasteAsync));
            return items;
        }

        private static MenuItem Item(string header, Func<Task> action, bool enabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += async (_, _) => await action().ConfigureAwait(true);
            return item;
        }

        private Task Use(Equation? math)
        {
            ViewModel?.Use(math);
            return Task.CompletedTask;
        }

        private async Task PasteAsync()
        {
            if (Editor == null)
                return;
            Editor.Focus();
            await Editor.PasteAsync().ConfigureAwait(true);
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
    }
}
