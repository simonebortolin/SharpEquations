namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia.Controls;

    /// <summary>The page of the unit converter (<see cref="UnitsViewModel"/>).</summary>
    public sealed partial class UnitsPage : UserControl
    {
        public UnitsPage() => InitializeComponent();

        /// <summary>The view model: the value, the units and the results.</summary>
        public UnitsViewModel? ViewModel => DataContext as UnitsViewModel;
    }
}
