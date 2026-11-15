namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Specialized;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Input.Platform;
    using global::Avalonia.Interactivity;
    using global::Avalonia.Media.Imaging;

    /// <summary>
    /// The page of the graphs (<see cref="GraphsViewModel"/>): the equations of the view model drawn together on a
    /// plane, each in its color, with a slider for each of their parameters.
    /// </summary>
    public sealed partial class GraphsPage : UserControl
    {
        private GraphsViewModel? shown;
        private RenderTargetBitmap? copied;

        public GraphsPage() => InitializeComponent();

        /// <summary>The view model: the graphs and their parameters.</summary>
        public GraphsViewModel? ViewModel => DataContext as GraphsViewModel;

        /// <summary>The editor of the equation to add.</summary>
        public MathEditBox Editor => EquationEditor;

        /// <summary>The plane the graphs are drawn on.</summary>
        public GraphView Graph => Plane;

        /// <summary>Copies the graphs to the clipboard as an image.</summary>
        public async Task CopyImageAsync()
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            // the clipboard may read the image later: the last one copied is kept until the next
            copied?.Dispose();
            copied = Plane.Snapshot();
            await clipboard.SetBitmapAsync(copied).ConfigureAwait(true);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (shown != null)
            {
                shown.Items.CollectionChanged -= OnItemsChanged;
                shown.RedrawRequested -= OnRedrawRequested;
                shown.ResetRequested -= OnResetRequested;
            }
            shown = ViewModel;
            if (shown != null)
            {
                shown.Items.CollectionChanged += OnItemsChanged;
                shown.RedrawRequested += OnRedrawRequested;
                shown.ResetRequested += OnResetRequested;
            }
            Draw();
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Draw();

        private void OnRedrawRequested(object? sender, EventArgs e) => Plane.Redraw();

        private void OnResetRequested(object? sender, EventArgs e) => Plane.Reset();

        private void OnResetViewClick(object? sender, RoutedEventArgs e) => Plane.Reset();

        private async void OnCopyImageClick(object? sender, RoutedEventArgs e) => await CopyImageAsync().ConfigureAwait(true);

        // the curves of the plane are the graphs of the view model, in their order
        private void Draw()
        {
            Plane.Curves.Clear();
            if (shown == null)
                return;
            foreach (var item in shown.Items)
                Plane.Curves.Add((GraphCurve)item.Graph);
        }
    }
}
