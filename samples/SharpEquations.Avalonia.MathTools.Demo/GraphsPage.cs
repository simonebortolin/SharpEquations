namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Input.Platform;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Imaging;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The graphs: equations typed in an editor (or sent by the calculator) drawn together, each in its color, with a
    /// slider for each of their parameters, the letters that are not variables of the graph.
    /// </summary>
    public sealed class GraphsPage : UserControl
    {
        private static readonly Color[] Palette =
        {
            Color.FromRgb(0x25, 0x63, 0xEB),
            Color.FromRgb(0xDC, 0x26, 0x26),
            Color.FromRgb(0x05, 0x96, 0x69),
            Color.FromRgb(0xD9, 0x77, 0x06),
            Color.FromRgb(0x7C, 0x3A, 0xED),
            Color.FromRgb(0xDB, 0x27, 0x77),
        };

        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        private readonly StackPanel list = new StackPanel { Spacing = 10 };
        private readonly TextBlock message = Ui.Muted("", 12.5);
        private readonly ICasEngine? engine;
        private int added;
        private bool sharing;
        private RenderTargetBitmap? copied;

        /// <param name="engine">The computer algebra system whose unit of angles and letters defined the Add button takes.</param>
        public GraphsPage(ICasEngine? engine = null)
        {
            this.engine = engine;
            Editor = new MathEditBox
            {
                FontSize = 26,
                MinHeight = 70,
                Padding = new Thickness(16, 10),
                BorderThickness = 0,
                Equation = Equation.Parse("y=sin(a x)", EquationFormat.UnicodeMath, Display),
            };
            Editor.Bind(MathEditBox.BackgroundProperty, Editor.GetResourceObservable("EditorBrush"));
            var frame = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1.5),
                ClipToBounds = true,
                Child = Editor,
            };
            frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("EditorBorderBrush"));

            var add = new Button { Content = "Add", CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 6) };
            ToolTip.SetTip(add, "Draws the equation of the editor");
            add.Click += (_, _) =>
            {
                if (Editor.Equation is { } equation)
                    AddEquation(equation, engine?.Angles ?? AngleUnit.Radians, engine?.Definitions);
            };
            var clear = new Button { Content = "Clear", CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 6) };
            ToolTip.SetTip(clear, "Removes every graph");
            clear.Click += (_, _) => Clear();
            var reset = new Button { Content = "Reset view", CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 6) };
            ToolTip.SetTip(reset, "Shows the region around the origin (a double click on the graph does the same)");

            var copy = new Button { Content = "Copy image", CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 6) };
            ToolTip.SetTip(copy, "Copies the graphs as an image");
            copy.Click += async (_, _) => await CopyImageAsync().ConfigureAwait(true);
            SharedParameters = new CheckBox { Content = "Same parameters for every graph", IsChecked = false };
            ToolTip.SetTip(SharedParameters, "A letter is one parameter for all the graphs with it, or one per graph");
            SharedParameters.IsCheckedChanged += (_, _) =>
            {
                if (SharedParameters.IsChecked == true)
                {
                    foreach (var name in Sliders.Keys.Select(k => k.Parameter).Distinct().ToList())
                        Share(name, Sliders.First(s => s.Key.Parameter == name).Value.Value);
                }
            };

            Graph = new GraphView { Height = 480 };
            reset.Click += (_, _) => Graph.Reset();
            var graphFrame = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), ClipToBounds = true, Child = Graph };
            graphFrame.ContextMenu = new ContextMenu
            {
                ItemsSource = new[]
                {
                    Item("Copy image", CopyImageAsync),
                    Item("Reset view", () =>
                    {
                        Graph.Reset();
                        return Task.CompletedTask;
                    }),
                },
            };
            graphFrame.Bind(Border.BorderBrushProperty, graphFrame.GetResourceObservable("CardBorderBrush"));

            Content = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    Ui.Card("Graphs", Ui.Caption("$y=f(x)$, $r=f(θ)$, $x=f(t)$ and $y=g(t)$ on two lines, or a relation of x and y such as $x^2+y^2=25$: the other letters are parameters, with a slider each"), new StackPanel
                    {
                        Spacing = 12,
                        Children = { frame, Ui.Row(add, clear, reset, copy, SharedParameters), message, list },
                    }),
                    Ui.Card("Plane", "Drag to move, the wheel to zoom where the pointer is, a double click to start again", graphFrame),
                },
            };
        }

        /// <summary>The editor of the equation to add.</summary>
        public MathEditBox Editor { get; }

        /// <summary>The plane the graphs are drawn on.</summary>
        public GraphView Graph { get; }

        /// <summary>Whether a letter is one parameter for every graph with it (its sliders move together) or one per graph.</summary>
        public CheckBox SharedParameters { get; }

        /// <summary>Why the last equation was not drawn, or null.</summary>
        public string? Error { get; private set; }

        /// <summary>The sliders of the parameters, by graph and parameter.</summary>
        public IDictionary<(GraphCurve Curve, string Parameter), Slider> Sliders { get; } = new Dictionary<(GraphCurve, string), Slider>();

        /// <summary>Draws an equation; false, with <see cref="Error"/>, when it has no graph.</summary>
        public bool AddEquation(Equation equation, AngleUnit angles = AngleUnit.Radians, IReadOnlyDictionary<string, Equation>? definitions = null)
        {
            GraphPlot plot;
            try
            {
                plot = GraphPlot.FromEquation(equation, angles, definitions);
            }
#pragma warning disable CA1031 // what the equation cannot draw is told, whatever it is
            catch (Exception e)
#pragma warning restore CA1031
            {
                Error = e is NotSupportedException ? e.Message : "This equation has no graph.";
                message.Text = Error;
                return false;
            }

            Error = null;
            message.Text = "";
            var curve = new GraphCurve(equation, plot, Palette[added++ % Palette.Length]);
            Graph.Curves.Add(curve);
            list.Children.Add(Row(curve));
            if (SharedParameters.IsChecked == true)
            {
                // a letter already drawn keeps its value
                foreach (var name in plot.Parameters)
                {
                    if (Sliders.FirstOrDefault(s => s.Key.Parameter == name && s.Key.Curve != curve).Value is { } other)
                        Sliders[(curve, name)].Value = other.Value;
                }
            }
            return true;
        }

        /// <summary>
        /// Draws the graph of a formula of the library, with its parameters (and their sliders) at their starting values;
        /// false, with <see cref="Error"/>, when it has no graph.
        /// </summary>
        public bool AddPreset(GraphPreset preset, AngleUnit angles = AngleUnit.Radians)
        {
            ArgumentNullException.ThrowIfNull(preset);
            if (!AddEquation(Equation.Parse(preset.UnicodeMath, EquationFormat.UnicodeMath, Display), angles))
                return false;
            var curve = Graph.Curves[^1];
            foreach (var (parameter, value) in preset.Values)
            {
                var name = MathReader.Symbol(parameter);
                if (Sliders.TryGetValue((curve, name), out var slider))
                {
                    slider.Minimum = Math.Min(slider.Minimum, value);
                    slider.Maximum = Math.Max(slider.Maximum, value);
                    slider.Value = value;
                }
                curve.Plot[name] = value;
            }
            Graph.Redraw();
            return true;
        }

        /// <summary>Removes every graph.</summary>
        public void Clear()
        {
            Graph.Curves.Clear();
            list.Children.Clear();
            Sliders.Clear();
            added = 0;
        }

        private StackPanel Row(GraphCurve curve)
        {
            var swatch = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(curve.Color), VerticalAlignment = VerticalAlignment.Center };
            var view = new MathView { FontSize = 18, Equation = curve.Equation, VerticalAlignment = VerticalAlignment.Center };
            var remove = new Button { Content = "✕", CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(remove, "Removes this graph");

            var parameters = new StackPanel { Spacing = 4, Margin = new Thickness(28, 0, 0, 0) };
            foreach (var name in curve.Plot.Parameters)
            {
                var value = Ui.Muted(Value(name, curve.Plot[name]), 12.5);
                value.MinWidth = 90;
                value.VerticalAlignment = VerticalAlignment.Center;
                var slider = new Slider { Minimum = -10, Maximum = 10, Value = curve.Plot[name], SmallChange = 0.1, LargeChange = 1, MinWidth = 240 };
                var parameter = name;
                slider.PropertyChanged += (_, e) =>
                {
                    if (e.Property != Slider.ValueProperty)
                        return;
                    curve.Plot[parameter] = slider.Value;
                    value.Text = Value(parameter, slider.Value);
                    if (!sharing && SharedParameters.IsChecked == true)
                        Share(parameter, slider.Value);
                    Graph.Redraw();
                };
                Sliders[(curve, name)] = slider;
                parameters.Children.Add(new DockPanel { Children = { Ui.Docked(value, Dock.Left), slider } });
            }

            var row = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { swatch, view, remove } },
                    parameters,
                },
            };
            remove.Click += (_, _) =>
            {
                Graph.Curves.Remove(curve);
                list.Children.Remove(row);
                foreach (var name in curve.Plot.Parameters)
                    Sliders.Remove((curve, name));
            };
            return row;
        }

        // the sliders of a letter, on every graph, to a value
        private void Share(string parameter, double value)
        {
            sharing = true;
            try
            {
                foreach (var slider in Sliders.Where(s => s.Key.Parameter == parameter).Select(s => s.Value))
                {
                    if (slider.Value != value)
                        slider.Value = value;
                }
            }
            finally
            {
                sharing = false;
            }
        }

        /// <summary>Copies the graphs to the clipboard as an image.</summary>
        public async Task CopyImageAsync()
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            // the clipboard may read the image later: the last one copied is kept until the next
            copied?.Dispose();
            copied = Graph.Snapshot();
            await clipboard.SetBitmapAsync(copied).ConfigureAwait(true);
        }

        private static MenuItem Item(string header, Func<Task> action)
        {
            var item = new MenuItem { Header = header };
            item.Click += async (_, _) => await action().ConfigureAwait(true);
            return item;
        }

        private static string Value(string name, double value) => name + " = " + value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
