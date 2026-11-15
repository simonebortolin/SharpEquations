namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using global::Avalonia.Controls;
    using global::Avalonia.Interactivity;

    /// <summary>
    /// The page of the scientific keypad (<see cref="CalculatorViewModel"/>): the keys type into the equation editor,
    /// which the keyboard types into too.
    /// </summary>
    public sealed partial class CalculatorPage : UserControl
    {
        private CalculatorViewModel? shown;

        public CalculatorPage()
        {
            InitializeComponent();
            SolverView.Editor = EquationEditor;
        }

        /// <summary>The view model: the keys, the equation and what is computed from it.</summary>
        public CalculatorViewModel? ViewModel => DataContext as CalculatorViewModel;

        /// <summary>The equation editor the keys type into.</summary>
        public MathEditBox Editor => EquationEditor;

        /// <summary>What is computed from the equation of the editor.</summary>
        public AlgebraPanel Solver => SolverView;

        /// <summary>Presses a key, as a click on it does.</summary>
        public void Press(CalculatorKey key)
        {
            ArgumentNullException.ThrowIfNull(key);
            key.Press(EquationEditor.Editor);
            EquationEditor.Focus();
        }

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

        private void OnKeyClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: CalculatorKey key })
                Press(key);
        }
    }
}
