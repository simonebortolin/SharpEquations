namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Immutable;
    using SharpEquations.MathTools;
    using SharpEquations.MathTools.Graphing;

    /// <summary>A graph drawn by <see cref="GraphView"/>: the plot of an equation and its color.</summary>
    public sealed record GraphCurve(Equation Equation, GraphPlot Plot, Color Color);

    /// <summary>
    /// The graphs of equations on a plane with its grid and axes: dragged to move, the wheel to zoom where the pointer
    /// is; the point under the pointer is shown in a corner.
    /// </summary>
    public sealed class GraphView : Control
    {
        private Point? dragged;
        private Point? pointer;
        private Viewport viewport = Viewport.Default;
        private Size sized;

        public GraphView()
        {
            ClipToBounds = true;
            Focusable = true;
            Curves.CollectionChanged += (_, _) => InvalidateVisual();
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        }

        /// <summary>The graphs shown.</summary>
        public ObservableCollection<GraphCurve> Curves { get; } = new ObservableCollection<GraphCurve>();

        /// <summary>The region of the plane shown; the y range follows the shape of the control, so that a circle is round.</summary>
        public Viewport Viewport
        {
            get => viewport;
            set
            {
                viewport = value;
                InvalidateVisual();
            }
        }

        /// <summary>The region of a new graph, with the shape of the control.</summary>
        public void Reset()
        {
            sized = default;
            viewport = Viewport.Default;
            Square();
            InvalidateVisual();
        }

        /// <summary>Draws again, after a parameter changed.</summary>
        public void Redraw() => InvalidateVisual();

        public override void Render(DrawingContext context)
        {
            var size = Bounds.Size;
            if (size.Width < 2 || size.Height < 2)
                return;
            if (size != sized)
                Square();

            context.FillRectangle(Brush("GraphBrush", Brushes.White), new Rect(size));
            DrawGrid(context, size);
            foreach (var curve in Curves)
                DrawCurve(context, curve, size);
            if (pointer is { } p)
            {
                var (x, y) = viewport.FromScreen(p.X, p.Y, size.Width, size.Height);
                var text = Text($"({Number(x)}, {Number(y)})", 12, Brush("AxisBrush", Brushes.Gray));
                context.DrawText(text, new Point(size.Width - text.Width - 10, size.Height - text.Height - 8));
            }
        }

        // the y range with the scale of the x range, around the same center
        private void Square()
        {
            var size = Bounds.Size;
            if (size.Width < 2 || size.Height < 2)
                return;
            sized = size;
            var center = (viewport.YMin + viewport.YMax) / 2;
            var half = viewport.Width * size.Height / size.Width / 2;
            viewport = viewport with { YMin = center - half, YMax = center + half };
        }

        private void DrawGrid(DrawingContext context, Size size)
        {
            var grid = new Pen(Brush("GridBrush", Brushes.LightGray), 1);
            var axis = new Pen(Brush("AxisBrush", Brushes.Gray), 1.4);
            var label = Brush("AxisBrush", Brushes.Gray);
            var xs = Ticks.Values(viewport.XMin, viewport.XMax, Math.Max(2, (int)(size.Width / 90)));
            var ys = Ticks.Values(viewport.YMin, viewport.YMax, Math.Max(2, (int)(size.Height / 70)));
            foreach (var x in xs)
            {
                var px = Snap(viewport.ToScreen(x, 0, size.Width, size.Height).X);
                context.DrawLine(grid, new Point(px, 0), new Point(px, size.Height));
            }
            foreach (var y in ys)
            {
                var py = Snap(viewport.ToScreen(0, y, size.Width, size.Height).Y);
                context.DrawLine(grid, new Point(0, py), new Point(size.Width, py));
            }

            // the axes, or the edge they are beyond, and the numbers along them
            var (ox, oy) = viewport.ToScreen(0, 0, size.Width, size.Height);
            var axisX = Math.Clamp(Snap(oy), 0, size.Height - 18);
            var axisY = Math.Clamp(Snap(ox), 0, size.Width - 1);
            if (oy >= 0 && oy <= size.Height)
                context.DrawLine(axis, new Point(0, axisX), new Point(size.Width, axisX));
            if (ox >= 0 && ox <= size.Width)
                context.DrawLine(axis, new Point(axisY, 0), new Point(axisY, size.Height));
            foreach (var x in xs.Where(x => x != 0))
            {
                var text = Text(Number(x), 11, label);
                var px = viewport.ToScreen(x, 0, size.Width, size.Height).X;
                context.DrawText(text, new Point(px - text.Width / 2, axisX + 3));
            }
            foreach (var y in ys.Where(y => y != 0))
            {
                var text = Text(Number(y), 11, label);
                var py = viewport.ToScreen(0, y, size.Width, size.Height).Y;
                var left = axisY + 4 + text.Width > size.Width ? axisY - text.Width - 4 : axisY + 4;
                context.DrawText(text, new Point(left, py - text.Height / 2));
            }
        }

        private void DrawCurve(DrawingContext context, GraphCurve curve, Size size)
        {
            var pen = new Pen(new ImmutableSolidColorBrush(curve.Color), 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var plot = curve.Plot;
            if (plot.Kind == GraphKind.Implicit)
            {
                var segments = Contours.ZeroSet(plot.F, viewport, Math.Max(20, (int)(size.Width / 4)), Math.Max(20, (int)(size.Height / 4)));
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    foreach (var (a, b) in segments)
                    {
                        g.BeginFigure(Screen(a, size), false);
                        g.LineTo(Screen(b, size));
                        g.EndFigure(false);
                    }
                }
                context.DrawGeometry(null, pen, geometry);
                return;
            }

            var lines = plot.Kind == GraphKind.Function
                ? Sampler.Function(plot.Y, viewport, size.Width, size.Height)
                : Sampler.Parametric(plot.Point, plot.Range.Min, plot.Range.Max, viewport, size.Width, size.Height);
            var path = new StreamGeometry();
            using (var g = path.Open())
            {
                foreach (var line in lines)
                {
                    g.BeginFigure(Screen(line[0], size), false);
                    foreach (var point in line.Skip(1))
                        g.LineTo(Screen(point, size));
                    g.EndFigure(false);
                }
            }
            context.DrawGeometry(null, pen, path);
        }

        private Point Screen((double X, double Y) p, Size size)
        {
            var (x, y) = viewport.ToScreen(p.X, p.Y, size.Width, size.Height);
            return new Point(x, y);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.ClickCount == 2)
            {
                Reset();
                return;
            }
            dragged = e.GetPosition(this);
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var position = e.GetPosition(this);
            pointer = position;
            if (dragged is { } from)
            {
                var size = Bounds.Size;
                viewport = viewport.Pan(-(position.X - from.X) / size.Width, (position.Y - from.Y) / size.Height);
                dragged = position;
            }
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            dragged = null;
            e.Pointer.Capture(null);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            pointer = null;
            InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var p = e.GetPosition(this);
            var (x, y) = viewport.FromScreen(p.X, p.Y, Bounds.Width, Bounds.Height);
            viewport = viewport.Zoom(Math.Pow(0.85, e.Delta.Y), x, y);
            InvalidateVisual();
            e.Handled = true;
        }

        private IBrush Brush(string key, IBrush fallback) =>
            this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;

        private static FormattedText Text(string text, double size, IBrush brush) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

        private static string Number(double value) =>
            Math.Abs(value) < 1e-12 ? "0" : value.ToString(Math.Abs(value) >= 1e5 || Math.Abs(value) < 1e-3 ? "0.###E0" : "0.####", CultureInfo.InvariantCulture);

        private static double Snap(double v) => Math.Round(v) + 0.5;
    }
}
