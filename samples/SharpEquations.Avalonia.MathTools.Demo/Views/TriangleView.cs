namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Shapes;
    using global::Avalonia.Media;
    using SharpEquations.MathTools;

    /// <summary>A triangle drawn to scale: A at the left of the base c, B at its right, C above.</summary>
    public sealed class TriangleView : Decorator
    {
        public static readonly StyledProperty<Triangle?> TriangleProperty =
            AvaloniaProperty.Register<TriangleView, Triangle?>(nameof(Triangle));

        private const double DrawingWidth = 300;
        private const double DrawingHeight = 210;

        private static readonly Color Accent = Color.FromRgb(0x0D, 0x94, 0x88);

        public Triangle? Triangle
        {
            get => GetValue(TriangleProperty);
            set => SetValue(TriangleProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TriangleProperty)
                Child = Triangle is { } triangle ? Drawing(triangle) : null;
        }

        private static Canvas Drawing(Triangle triangle)
        {
            var alpha = Triangles.Radians(triangle.Alpha);
            var points = new[] { (X: 0.0, Y: 0.0), (X: triangle.C, Y: 0.0), (X: triangle.B * Math.Cos(alpha), Y: triangle.B * Math.Sin(alpha)) };
            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X), maxY = points.Max(p => p.Y);
            const double margin = 26;
            var scale = Math.Min((DrawingWidth - 2 * margin) / (maxX - minX), (DrawingHeight - 2 * margin) / maxY);
            var offsetX = (DrawingWidth - (maxX - minX) * scale) / 2;
            var offsetY = (DrawingHeight + maxY * scale) / 2;
            var screen = points.Select(p => new Point(offsetX + (p.X - minX) * scale, offsetY - p.Y * scale)).ToArray();

            var canvas = new Canvas { Width = DrawingWidth, Height = DrawingHeight };
            canvas.Children.Add(new Polygon
            {
                Points = screen,
                StrokeThickness = 2,
                StrokeJoin = PenLineJoin.Round,
                Stroke = new SolidColorBrush(Accent),
                Fill = new SolidColorBrush(Accent, 0.14),
            });

            var center = new Point(screen.Average(p => p.X), screen.Average(p => p.Y));
            for (var i = 0; i < 3; i++)
            {
                // the vertex, away from the center, and the side opposite it, at its middle, away from the center too
                Label(canvas, ((char)('A' + i)).ToString(), Away(screen[i], center, 14), FontWeight.SemiBold);
                var middle = (screen[(i + 1) % 3] + screen[(i + 2) % 3]) / 2;
                Label(canvas, Triangles.SideNames[i], Away(middle, center, 12), FontWeight.Normal);
            }
            return canvas;
        }

        private static Point Away(Point point, Point center, double distance)
        {
            var direction = point - center;
            var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            return length < 1e-9 ? point : point + direction * (distance / length);
        }

        private static void Label(Canvas canvas, string text, Point at, FontWeight weight)
        {
            var label = new TextBlock { Text = text, FontStyle = FontStyle.Italic, FontSize = 15, FontWeight = weight };
            label.Measure(Size.Infinity);
            Canvas.SetLeft(label, at.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, at.Y - label.DesiredSize.Height / 2);
            canvas.Children.Add(label);
        }
    }
}
