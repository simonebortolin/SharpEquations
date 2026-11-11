namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Shapes;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using SharpEquations;

    /// <summary>
    /// The solver of triangles: three of the sides and angles give the others, the area, the perimeter and the radii of
    /// the circles, with the laws used, written in UnicodeMath and drawn by <see cref="MathView"/>s, and the triangle
    /// drawn to scale.
    /// </summary>
    public sealed class TrianglePage : UserControl
    {
        private const double DrawingWidth = 300;
        private const double DrawingHeight = 210;

        private static readonly Color Accent = Color.FromRgb(0x0D, 0x94, 0x88);

        private static readonly (string Name, string[] Values)[] Examples =
        {
            ("3, 4, 5", new[] { "3", "4", "5", "", "", "" }),
            ("SAS", new[] { "7", "", "9", "", "60", "" }),
            ("ASA", new[] { "", "", "10", "45", "75", "" }),
            ("Two triangles", new[] { "6", "8", "", "40", "", "" }),
            ("No triangle", new[] { "1", "2", "4", "", "", "" }),
        };

        private static readonly string[] Empty = { "", "", "", "", "", "" };

        private readonly StackPanel results = new StackPanel { Spacing = 18 };
        private readonly StackPanel steps = new StackPanel { Spacing = 10 };
        private bool updating;

        public TrianglePage()
        {
            Inputs = Triangles.SideNames.Concat(Triangles.AngleNames).Select(_ => new TextBox { Width = 92, PlaceholderText = "?" }).ToArray();
            foreach (var input in Inputs)
            {
                input.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBox.TextProperty)
                        Solve();
                };
            }

            var clear = new Button { Content = "Clear", CornerRadius = new CornerRadius(8) };
            clear.Click += (_, _) => Fill(Empty);
            var examples = new List<Control> { Ui.Muted("Examples", 12.5) };
            foreach (var (name, values) in Examples)
            {
                var button = new Button { Content = name, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 4) };
                button.Click += (_, _) => Fill(values);
                examples.Add(button);
            }
            examples.Add(clear);

            var sides = Ui.Row(Enumerable.Range(0, 3).Select(i => Field(Triangles.SideNames[i], Inputs[i], "")).ToArray());
            var angles = Ui.Row(Enumerable.Range(0, 3).Select(i => Field(Triangles.AngleNames[i], Inputs[i + 3], "°")).ToArray());

            Content = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    Ui.Card("Triangles", "Three of the sides and angles, at least one of them a side; each angle is opposite the side of its letter", new StackPanel
                    {
                        Spacing = 14,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                                RowDefinitions = new RowDefinitions("Auto,Auto"),
                                ColumnSpacing = 14,
                                RowSpacing = 10,
                                Children = { Label("Sides", 0), At(sides, 0, 1), Label("Angles", 1), At(angles, 1, 1) },
                            },
                            Ui.Row(examples.ToArray()),
                        },
                    }),
                    Ui.Card("Solution", "The values found, the area, the perimeter and the radii of the circles through the vertices and inside", results),
                    Ui.Card("How", "The laws that give the values found", steps),
                },
            };
            Fill(Examples[0].Values);
        }

        /// <summary>The fields of a, b, c, α, β and γ.</summary>
        public IReadOnlyList<TextBox> Inputs { get; }

        /// <summary>The solution of the values in the fields.</summary>
        public TriangleSolution? Solution { get; private set; }

        /// <summary>Fills the fields, as the examples do.</summary>
        public void Fill(IReadOnlyList<string> values)
        {
            updating = true;
            for (var i = 0; i < Inputs.Count; i++)
                Inputs[i].Text = values[i];
            updating = false;
            Solve();
        }

        /// <summary>The values of a triangle as UnicodeMath: <c>a=3, b=4, c=5</c> and <c>α=36.8699°, β=53.1301°, γ=90°</c>.</summary>
        public static (string Sides, string Angles) Describe(Triangle triangle) => (
            string.Join(",  ", Enumerable.Range(0, 3).Select(i => Triangles.SideNames[i] + "=" + Units.Format(triangle.Side(i)))),
            string.Join(",  ", Enumerable.Range(0, 3).Select(i => Triangles.AngleNames[i] + "=" + Units.Format(triangle.Angle(i)) + "°")));

        private void Solve()
        {
            if (updating)
                return;

            var values = new double?[Inputs.Count];
            for (var i = 0; i < Inputs.Count; i++)
            {
                var text = Inputs[i].Text;
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                if (!Units.TryParse(text!.TrimEnd('°', ' '), out var value))
                {
                    Show(new TriangleSolution(null, Array.Empty<Triangle>(), Array.Empty<string>(), "“" + text + "” is not a number."));
                    return;
                }
                values[i] = value;
            }
            Show(Triangles.Solve(values[0], values[1], values[2], values[3], values[4], values[5]));
        }

        private void Show(TriangleSolution solution)
        {
            Solution = solution;
            results.Children.Clear();
            steps.Children.Clear();

            if (solution.Error != null)
                results.Children.Add(Ui.Muted(solution.Error, 14));
            if (solution.Triangles.Count == 2)
                results.Children.Add(Ui.Muted("The angle given is opposite the shorter side: two triangles have these values.", 13));
            foreach (var (triangle, index) in solution.Triangles.Select((t, i) => (t, i)))
                results.Children.Add(Result(triangle, solution.Triangles.Count == 2 ? "Triangle " + (index + 1) : null));

            if (solution.Case != null)
                steps.Children.Add(Ui.Muted("Case " + solution.Case + " (" + CaseName(solution.Case) + ")", 13));
            foreach (var step in solution.Steps)
            {
                var math = Ui.Math(step, 19);
                math.HorizontalAlignment = HorizontalAlignment.Left;
                steps.Children.Add(math);
            }
            if (solution.Steps.Count == 0)
                steps.Children.Add(Ui.Muted("Give three values, at least one of them a side.", 13));
        }

        private static Grid Result(Triangle triangle, string? title)
        {
            var (sides, angles) = Describe(triangle);
            var values = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            if (title != null)
                values.Children.Add(Ui.Small(title));
            values.Children.Add(Left(Ui.Math(sides, 21)));
            values.Children.Add(Left(Ui.Math(angles, 21)));
            values.Children.Add(Measures(triangle));

            var drawing = Drawing(triangle);
            Grid.SetColumn(values, 1);
            return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 24, Children = { drawing, values } };
        }

        private static Grid Measures(Triangle triangle)
        {
            var rows = new (string Name, string Value)[]
            {
                ("Area", Units.Format(triangle.Area)),
                ("Perimeter", Units.Format(triangle.Perimeter)),
                ("Circumradius", "R=" + Units.Format(triangle.Circumradius)),
                ("Inradius", "r=" + Units.Format(triangle.Inradius)),
            };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*"), RowSpacing = 8, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var (row, (name, value)) in rows.Select((r, i) => (i, r)))
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var label = Ui.Muted(name, 13);
                label.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(At(label, row, 0));
                grid.Children.Add(At(Left(Ui.Math(value, 17)), row, 1));
            }
            return grid;
        }

        /// <summary>The triangle to scale: A at the left of the base c, B at its right, C above.</summary>
        private static Border Drawing(Triangle triangle)
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
            var shape = new Polygon
            {
                Points = screen,
                StrokeThickness = 2,
                StrokeJoin = PenLineJoin.Round,
                Stroke = new SolidColorBrush(Accent),
                Fill = new SolidColorBrush(Accent, 0.14),
            };
            canvas.Children.Add(shape);

            var center = new Point(screen.Average(p => p.X), screen.Average(p => p.Y));
            for (var i = 0; i < 3; i++)
            {
                // the vertex, away from the center, and the side opposite it, at its middle, away from the center too
                Label(canvas, ((char)('A' + i)).ToString(), Away(screen[i], center, 14), FontWeight.SemiBold);
                var middle = (screen[(i + 1) % 3] + screen[(i + 2) % 3]) / 2;
                Label(canvas, Triangles.SideNames[i], Away(middle, center, 12), FontWeight.Normal);
            }

            var frame = new Border { CornerRadius = new CornerRadius(12), Child = canvas, VerticalAlignment = VerticalAlignment.Top };
            frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable("CodeBrush"));
            return frame;
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

        private static string CaseName(string kind) => kind switch
        {
            "SSS" => "three sides",
            "SAS" => "two sides and the angle between them",
            "SSA" => "two sides and an angle not between them",
            "ASA" => "two angles and the side between them",
            _ => "two angles and a side not between them",
        };

        private static StackPanel Field(string name, TextBox input, string unit)
        {
            var label = Ui.Math(name, 19);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Width = 18;
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { label, input } };
            if (unit.Length > 0)
                panel.Children.Add(new TextBlock { Text = unit, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-4, 0, 6, 0) });
            else
                panel.Margin = new Thickness(0, 0, 12, 0);
            return panel;
        }

        private static TextBlock Label(string text, int row)
        {
            var label = Ui.Small(text);
            label.VerticalAlignment = VerticalAlignment.Center;
            return At(label, row, 0);
        }

        private static T Left<T>(T control)
            where T : Control
        {
            control.HorizontalAlignment = HorizontalAlignment.Left;
            return control;
        }

        private static T At<T>(T control, int row, int column)
            where T : Control
        {
            Grid.SetRow(control, row);
            Grid.SetColumn(control, column);
            return control;
        }
    }
}
