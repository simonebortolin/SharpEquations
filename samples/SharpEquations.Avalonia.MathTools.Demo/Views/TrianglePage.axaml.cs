namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia.Controls;

    /// <summary>The page of the solver of triangles (<see cref="TrianglesViewModel"/>), with each triangle drawn to scale.</summary>
    public sealed partial class TrianglePage : UserControl
    {
        public TrianglePage() => InitializeComponent();

        /// <summary>The view model: the fields and the solution.</summary>
        public TrianglesViewModel? ViewModel => DataContext as TrianglesViewModel;
    }
}
