namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using global::Avalonia.Controls;
    using global::Avalonia.Input;

    /// <summary>
    /// The page of the worksheet (<see cref="WorksheetViewModel"/>): the entries one after the other as on paper, and
    /// the editor of the next one.
    /// </summary>
    public sealed partial class WorksheetPage : UserControl
    {
        private WorksheetViewModel? shown;

        public WorksheetPage() => InitializeComponent();

        /// <summary>The view model: the entries, and what is done to the equation of the editor.</summary>
        public WorksheetViewModel? ViewModel => DataContext as WorksheetViewModel;

        /// <summary>The editor of the next entry.</summary>
        public MathEditBox Editor => EquationEditor;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (shown != null)
                shown.FocusRequested -= OnFocusRequested;
            shown = ViewModel;
            if (shown != null)
                shown.FocusRequested += OnFocusRequested;
        }

        private void OnFocusRequested(object? sender, EventArgs e) => EquationEditor.Focus();

        // Ctrl+Enter adds the entry, as the Enter button does
        private void OnEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && ViewModel is { } model)
            {
                e.Handled = true;
                model.EnterCommand.Execute(null);
            }
        }
    }
}
