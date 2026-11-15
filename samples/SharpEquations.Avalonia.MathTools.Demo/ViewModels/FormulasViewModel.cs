namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>A subject of the library, or all of them (no name).</summary>
    public sealed record FormulaSubject(string? Name)
    {
        /// <summary>The name shown.</summary>
        public string Title => Name ?? "All";
    }

    /// <summary>A formula of the library as the page shows it.</summary>
    public sealed class FormulaItem : ObservableObject
    {
        private bool isVisible = true;

        internal FormulaItem(Formula formula, bool hasGraph, ICommand openCommand, ICommand graphCommand)
        {
            Formula = formula;
            Caption = formula.Subject.ToUpperInvariant() + "  ·  " + formula.Name;
            Equation = Equation.Parse(formula.UnicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            HasGraph = hasGraph;
            OpenCommand = openCommand;
            GraphCommand = graphCommand;
        }

        public Formula Formula { get; }

        /// <summary>The subject and the name of the formula.</summary>
        public string Caption { get; }

        /// <summary>The formula, to draw.</summary>
        public Equation Equation { get; }

        /// <summary>Whether the formula has a graph the page can draw.</summary>
        public bool HasGraph { get; }

        /// <summary>Opens the formula in the calculator.</summary>
        public ICommand OpenCommand { get; }

        /// <summary>Draws the graph of the formula.</summary>
        public ICommand GraphCommand { get; }

        /// <summary>Whether the subject and the search show the formula.</summary>
        public bool IsVisible
        {
            get => isVisible;
            internal set => Set(ref isVisible, value);
        }
    }

    /// <summary>
    /// The library of formulas, filtered by subject and by name: one opens in the calculator, and the ones with a graph
    /// are drawn on the page of the graphs.
    /// </summary>
    public sealed class FormulasViewModel : ObservableObject
    {
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        private readonly bool graphs;
        private FormulaSubject subject;
        private string? search;

        /// <param name="graphs">Whether the graphs of the formulas can be drawn; without it, no formula has a button for its graph.</param>
        public FormulasViewModel(bool graphs = true)
        {
            this.graphs = graphs;
            Subjects = new[] { new FormulaSubject(null) }.Concat(Formulas.Subjects.Select(name => new FormulaSubject(name))).ToList();
            subject = Subjects[0];
            Items = Formulas.All.Select(formula => new FormulaItem(formula, graphs && formula.Graph != null, new RelayCommand(() => Open(formula)), new RelayCommand(() => Graph(formula)))).ToList();
        }

        /// <summary>Raised with the formula to open in the calculator.</summary>
        public event EventHandler<Equation>? OpenRequested;

        /// <summary>Raised with the graph of a formula to draw.</summary>
        public event EventHandler<GraphPreset>? GraphRequested;

        /// <summary>All the subjects, then each of them.</summary>
        public IReadOnlyList<FormulaSubject> Subjects { get; }

        /// <summary>The subject shown.</summary>
        public FormulaSubject Subject
        {
            get => subject;
            set
            {
                if (value != null && Set(ref subject, value))
                    Filter();
            }
        }

        /// <summary>The text searched: the formulas whose name or subject contain it are shown.</summary>
        public string? Search
        {
            get => search;
            set
            {
                if (Set(ref search, value))
                    Filter();
            }
        }

        /// <summary>The formulas.</summary>
        public IReadOnlyList<FormulaItem> Items { get; }

        /// <summary>The formulas shown, after the subject and the search.</summary>
        public IEnumerable<Formula> Shown => Items.Where(item => item.IsVisible).Select(item => item.Formula);


        /// <summary>Shows the formulas of a subject, or all of them for null.</summary>
        public void ShowSubject(string? name) => Subject = Subjects.Single(s => s.Name == name);

        /// <summary>Opens a formula in the calculator, as a click on it does.</summary>
        public void Open(Formula formula)
        {
            ArgumentNullException.ThrowIfNull(formula);
            OpenRequested?.Invoke(this, Equation.Parse(formula.UnicodeMath, EquationFormat.UnicodeMath, Display));
        }

        /// <summary>Draws the graph of a formula, as its button does; false if it has none or the page draws no graphs.</summary>
        public bool Graph(Formula formula)
        {
            ArgumentNullException.ThrowIfNull(formula);
            if (formula.Graph == null || !graphs)
                return false;
            GraphRequested?.Invoke(this, formula.Graph);
            return true;
        }

        private void Filter()
        {
            var shown = new HashSet<Formula>(Formulas.Find(subject.Name, search));
            foreach (var item in Items)
                item.IsVisible = shown.Contains(item.Formula);
        }
    }
}
