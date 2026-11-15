namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Windows.Input;
    using global::Avalonia.Media;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>A parameter of a graph, a letter that is not one of its variables: the slider of the view moves its value.</summary>
    public sealed class PlotParameter : ObservableObject
    {
        private readonly Action<PlotParameter> changed;
        private double minimum = -10;
        private double maximum = 10;
        private double value;

        internal PlotParameter(string name, double value, Action<PlotParameter> changed)
        {
            Name = name;
            this.value = value;
            this.changed = changed;
        }

        public string Name { get; }

        public double Minimum
        {
            get => minimum;
            set => Set(ref minimum, value);
        }

        public double Maximum
        {
            get => maximum;
            set => Set(ref maximum, value);
        }

        public double Value
        {
            get => value;
            set
            {
                if (!Set(ref this.value, value))
                    return;
                OnPropertyChanged(nameof(Text));
                changed(this);
            }
        }

        /// <summary>The parameter and its value: a = 0.5.</summary>
        public string Text => Name + " = " + value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>A graph drawn: its equation, its color and its parameters.</summary>
    public sealed class PlotItem
    {
        internal PlotItem(object graph, Equation equation, Color color, IReadOnlyList<PlotParameter> parameters, ICommand removeCommand)
        {
            Graph = graph;
            Equation = equation;
            Color = color;
            Parameters = parameters;
            RemoveCommand = removeCommand;
        }

        /// <summary>What the view draws: a <see cref="GraphCurve"/> in the plane, a <see cref="SurfaceGraph"/> in space.</summary>
        public object Graph { get; }

        public Equation Equation { get; }

        public Color Color { get; }

        /// <summary>The color, to fill the mark of the graph.</summary>
        public IBrush Brush => new global::Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color);

        public IReadOnlyList<PlotParameter> Parameters { get; }

        /// <summary>Removes the graph.</summary>
        public ICommand RemoveCommand { get; }

        /// <summary>The parameter of a letter.</summary>
        public PlotParameter this[string name] => Parameters.Single(p => p.Name == name);
    }

    /// <summary>
    /// Graphs drawn together: equations typed in an editor (or sent by the calculator), each in its color, with a
    /// parameter for each of their letters that are not variables of the graph.
    /// </summary>
    public abstract class PlotsViewModel : ObservableObject
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

        private readonly ICasEngine? engine;
        private readonly string noGraph;
        private Equation? equation;
        private bool sharedParameters;
        private string? error;
        private int added;
        private bool sharing;

        /// <param name="engine">The computer algebra system whose unit of angles and letters defined the Add command takes.</param>
        /// <param name="initial">The equation of the editor at first, as UnicodeMath.</param>
        /// <param name="noGraph">What is told of an equation that has no graph.</param>
        protected PlotsViewModel(ICasEngine? engine, string initial, string noGraph)
        {
            this.engine = engine;
            this.noGraph = noGraph;
            equation = Equation.Parse(initial, EquationFormat.UnicodeMath, Display);
            AddCommand = new RelayCommand(() =>
            {
                if (Equation is { } typed)
                    AddEquation(typed, engine?.Angles ?? AngleUnit.Radians, engine?.Definitions);
            });
            ClearCommand = new RelayCommand(Clear);
            ResetCommand = new RelayCommand(() => ResetRequested?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>Raised when the graphs are to be drawn again: a parameter changed.</summary>
        public event EventHandler? RedrawRequested;

        /// <summary>Raised when the view is to show the region around the origin again.</summary>
        public event EventHandler? ResetRequested;

        /// <summary>The equation of the editor, the one to add.</summary>
        public Equation? Equation
        {
            get => equation;
            set => Set(ref equation, value);
        }

        /// <summary>The graphs drawn.</summary>
        public ObservableCollection<PlotItem> Items { get; } = new ObservableCollection<PlotItem>();

        /// <summary>Whether a letter is one parameter for every graph with it (its values move together) or one per graph.</summary>
        public bool SharedParameters
        {
            get => sharedParameters;
            set
            {
                if (!Set(ref sharedParameters, value) || !value)
                    return;
                foreach (var first in Items.SelectMany(i => i.Parameters).GroupBy(p => p.Name).Select(g => g.First()).ToList())
                    Share(first.Name, first.Value);
            }
        }

        /// <summary>Why the last equation was not drawn, or null.</summary>
        public string? Error
        {
            get => error;
            private set => Set(ref error, value);
        }

        /// <summary>Draws the equation of the editor.</summary>
        public ICommand AddCommand { get; }

        /// <summary>Removes every graph.</summary>
        public ICommand ClearCommand { get; }

        /// <summary>Shows the region around the origin again.</summary>
        public ICommand ResetCommand { get; }

        protected static EquationOptions Display { get; } = new EquationOptions { Display = true };

        /// <summary>Draws an equation; false, with <see cref="Error"/>, when it has no graph.</summary>
        public bool AddEquation(Equation equation, AngleUnit angles = AngleUnit.Radians, IReadOnlyDictionary<string, Equation>? definitions = null)
        {
            ArgumentNullException.ThrowIfNull(equation);
            var color = Palette[added % Palette.Length];
            object graph;
            IReadOnlyList<string> names;
            Func<string, double> get;
            Action<string, double> set;
            try
            {
                (graph, names, get, set) = Create(equation, angles, definitions, color);
            }
#pragma warning disable CA1031 // what the equation cannot draw is told, whatever it is
            catch (Exception e)
#pragma warning restore CA1031
            {
                Error = e is NotSupportedException ? e.Message : noGraph;
                return false;
            }

            Error = null;
            added++;
            var parameters = names.Select(name => new PlotParameter(name, get(name), parameter =>
            {
                set(parameter.Name, parameter.Value);
                if (!sharing && sharedParameters)
                    Share(parameter.Name, parameter.Value);
                RedrawRequested?.Invoke(this, EventArgs.Empty);
            })).ToList();
            PlotItem? item = null;
            item = new PlotItem(graph, equation, color, parameters, new RelayCommand(() => Items.Remove(item!)));
            if (sharedParameters)
            {
                // a letter already drawn keeps its value
                foreach (var parameter in parameters)
                {
                    if (Items.SelectMany(i => i.Parameters).FirstOrDefault(p => p.Name == parameter.Name) is { } other)
                        parameter.Value = other.Value;
                }
            }
            Items.Add(item);
            return true;
        }

        /// <summary>Removes every graph.</summary>
        public void Clear()
        {
            Items.Clear();
            added = 0;
        }

        /// <summary>Asks the view to draw the graphs again.</summary>
        protected void Redraw() => RedrawRequested?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Reads an equation as a graph in a color: what the view draws, the letters that are its parameters, and how
        /// the value of one is read and changed. It throws when the equation has no graph.
        /// </summary>
        protected abstract (object Graph, IReadOnlyList<string> Parameters, Func<string, double> Get, Action<string, double> Set) Create(
            Equation equation, AngleUnit angles, IReadOnlyDictionary<string, Equation>? definitions, Color color);

        // the parameters of a letter, on every graph, to a value
        private void Share(string name, double value)
        {
            sharing = true;
            try
            {
                foreach (var parameter in Items.SelectMany(i => i.Parameters).Where(p => p.Name == name))
                    parameter.Value = value;
            }
            finally
            {
                sharing = false;
            }
        }
    }

    /// <summary>The graphs in the plane.</summary>
    public sealed class GraphsViewModel : PlotsViewModel
    {
        public GraphsViewModel(ICasEngine? engine = null)
            : base(engine, "y=sin(a x)", "This equation has no graph.")
        {
        }

        /// <summary>
        /// Draws the graph of a formula of the library, with its parameters at their starting values; false, with
        /// <see cref="PlotsViewModel.Error"/>, when it has no graph.
        /// </summary>
        public bool AddPreset(GraphPreset preset, AngleUnit angles = AngleUnit.Radians)
        {
            ArgumentNullException.ThrowIfNull(preset);
            if (!AddEquation(Equation.Parse(preset.UnicodeMath, EquationFormat.UnicodeMath, Display), angles))
                return false;
            var item = Items[^1];
            var curve = (GraphCurve)item.Graph;
            foreach (var (parameter, value) in preset.Values)
            {
                var name = MathReader.Symbol(parameter);
                if (item.Parameters.FirstOrDefault(p => p.Name == name) is { } slider)
                {
                    slider.Minimum = Math.Min(slider.Minimum, value);
                    slider.Maximum = Math.Max(slider.Maximum, value);
                    slider.Value = value;
                }
                curve.Plot[name] = value;
            }
            Redraw();
            return true;
        }

        protected override (object Graph, IReadOnlyList<string> Parameters, Func<string, double> Get, Action<string, double> Set) Create(
            Equation equation, AngleUnit angles, IReadOnlyDictionary<string, Equation>? definitions, Color color)
        {
            var plot = GraphPlot.FromEquation(equation, angles, definitions);
            return (new GraphCurve(equation, plot, color), plot.Parameters, name => plot[name], (name, value) => plot[name] = value);
        }
    }

    /// <summary>The graphs in space.</summary>
    public sealed class Graphs3DViewModel : PlotsViewModel
    {
        public Graphs3DViewModel(ICasEngine? engine = null)
            : base(engine, "z=sin x cos y", "This equation has no graph in space.")
        {
        }

        protected override (object Graph, IReadOnlyList<string> Parameters, Func<string, double> Get, Action<string, double> Set) Create(
            Equation equation, AngleUnit angles, IReadOnlyDictionary<string, Equation>? definitions, Color color)
        {
            var plot = SurfacePlot.FromEquation(equation, angles, definitions);
            return (new SurfaceGraph(equation, plot, color), plot.Parameters, name => plot[name], (name, value) => plot[name] = value);
        }
    }
}
