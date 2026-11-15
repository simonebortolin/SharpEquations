namespace SharpEquations.Avalonia.MathTools
{
    using System.Globalization;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The math tools, a page each: the scientific keypad around the equation editor, the library of formulas, the
    /// unit converter, the solver of triangles, the graphs, the graphs in space and the worksheet. The desktop window
    /// and the WebAssembly page show it with the services of their platform: where the settings are kept (the page
    /// shown, the equation of the calculator, the unit of angles, the theme), and the computer algebra system.
    /// </summary>
    public sealed class MathToolsViewModel : ObservableObject
    {
        private const int CalculatorTab = 0;
        private const int GraphsTab = 4;
        private const int TabCount = 7;

        private const string TabKey = "tab";
        private const string EquationKey = "equation";
        private const string ThemeKey = "theme";
        private const string AnglesKey = "angles";

        private readonly ISettingsStore settings;
        private int selectedTab;

        public MathToolsViewModel(ISettingsStore? settings = null, ICasEngine? engine = null)
        {
            this.settings = settings ?? new MemorySettingsStore();
            Engine = engine ?? new AngouriMathEngine();
            Calculator = new CalculatorViewModel(Engine);
            FormulaLibrary = new FormulasViewModel();
            Converter = new UnitsViewModel();
            TriangleSolver = new TrianglesViewModel();
            Graphs = new GraphsViewModel(Engine);
            Space = new Graphs3DViewModel(Engine);
            Worksheet = new WorksheetViewModel(Engine, this.settings);

            FormulaLibrary.OpenRequested += (_, equation) =>
            {
                SelectedTab = CalculatorTab;
                Calculator.Open(equation);
            };
            FormulaLibrary.GraphRequested += (_, preset) =>
            {
                Graphs.AddPreset(preset, Engine.Angles);
                SelectedTab = GraphsTab;
            };
            Calculator.Solver.GraphRequested += (_, equation) => ShowGraph(equation);
            Worksheet.GraphRequested += (_, equation) => ShowGraph(equation);

            Restore();
            Calculator.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CalculatorViewModel.Equation))
                    this.settings.Set(EquationKey, Calculator.Equation?.ToString(EquationFormat.UnicodeMath));
            };
            Calculator.Solver.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AlgebraViewModel.AngleIndex))
                    this.settings.Set(AnglesKey, Calculator.Solver.AngleIndex == 1 ? "degrees" : "radians");
            };
        }

        /// <summary>The computer algebra system of the calculator.</summary>
        public ICasEngine Engine { get; }

        /// <summary>The scientific keypad.</summary>
        public CalculatorViewModel Calculator { get; }

        /// <summary>The library of formulas.</summary>
        public FormulasViewModel FormulaLibrary { get; }

        /// <summary>The unit converter.</summary>
        public UnitsViewModel Converter { get; }

        /// <summary>The solver of triangles.</summary>
        public TrianglesViewModel TriangleSolver { get; }

        /// <summary>The graphs.</summary>
        public GraphsViewModel Graphs { get; }

        /// <summary>The graphs in space.</summary>
        public Graphs3DViewModel Space { get; }

        /// <summary>The worksheet, the history of what was computed.</summary>
        public WorksheetViewModel Worksheet { get; }

        /// <summary>The page shown.</summary>
        public int SelectedTab
        {
            get => selectedTab;
            set
            {
                if (value >= 0 && Set(ref selectedTab, value))
                    settings.Set(TabKey, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The theme of the last session: dark, light, or null for the one of the system.</summary>
        public bool? IsDark => settings.Get(ThemeKey) switch
        {
            "dark" => true,
            "light" => false,
            _ => null,
        };

        /// <summary>Keeps the theme chosen for the next session.</summary>
        public void KeepTheme(bool dark) => settings.Set(ThemeKey, dark ? "dark" : "light");

        // the page, the equation and the unit of the angles of the last session
        private void Restore()
        {
            if (int.TryParse(settings.Get(TabKey), NumberStyles.None, CultureInfo.InvariantCulture, out var tab) && tab < TabCount)
                selectedTab = tab;
            if (settings.Get(EquationKey) is { } text && Equation.TryParse(text, EquationFormat.UnicodeMath, new EquationOptions { Display = true }, out var equation))
                Calculator.Equation = equation;
            if (settings.Get(AnglesKey) == "degrees")
                Calculator.Solver.AngleIndex = 1;
        }

        private void ShowGraph(Equation equation)
        {
            Graphs.AddEquation(equation, Engine.Angles, Engine.Definitions);
            SelectedTab = GraphsTab;
        }
    }
}
