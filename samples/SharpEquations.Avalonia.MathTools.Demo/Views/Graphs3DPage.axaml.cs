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
    /// The page of the graphs in space (<see cref="Graphs3DViewModel"/>): the equations of the view model drawn together in a
    /// box the camera turns around, each in its color, with a slider for each of their parameters.
    /// </summary>
    public sealed partial class Graphs3DPage : UserControl
    {
        private Graphs3DViewModel? shown;
        private RenderTargetBitmap? copied;

        public Graphs3DPage()
        {
            InitializeComponent();
            Box.ForceSoftware = OperatingSystem.IsBrowser();
        }

        /// <summary>The view model: the graphs and their parameters.</summary>
        public Graphs3DViewModel? ViewModel => DataContext as Graphs3DViewModel;

        /// <summary>The editor of the equation to add.</summary>
        public MathEditBox Editor => EquationEditor;

        /// <summary>The box the graphs are drawn in.</summary>
        public Graph3DView Space => Box;

        /// <summary>Copies the graphs to the clipboard as an image.</summary>
        public async Task CopyImageAsync()
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            // the clipboard may read the image later: the last one copied is kept until the next
            copied?.Dispose();
            copied = Box.Snapshot();
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

        // sampling the surfaces again is slow: it waits for the slider to stop
        private void OnRedrawRequested(object? sender, EventArgs e) => Box.RedrawSoon();

        private void OnResetRequested(object? sender, EventArgs e) => Box.Reset();

        private void OnResetViewClick(object? sender, RoutedEventArgs e) => Box.Reset();

        private async void OnCopyImageClick(object? sender, RoutedEventArgs e) => await CopyImageAsync().ConfigureAwait(true);

        // the surfaces and curves of the box are the graphs of the view model, in their order
        private void Draw()
        {
            Box.Surfaces.Clear();
            if (shown == null)
                return;
            foreach (var item in shown.Items)
                Box.Surfaces.Add((SurfaceGraph)item.Graph);
        }
    }
}
