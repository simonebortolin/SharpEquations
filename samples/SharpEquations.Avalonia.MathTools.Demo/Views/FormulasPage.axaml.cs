namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia.Controls;

    /// <summary>The page of the library of formulas (<see cref="FormulasViewModel"/>)</summary>
    public sealed partial class FormulasPage : UserControl
    {
        public FormulasPage() => InitializeComponent();

        /// <summary> each one drawn, filtered by subject and by name.:The view model: the formulas, the subject and the search.</summary>
        public FormulasViewModel? ViewModel => DataContext as FormulasViewModel;
    }
}
