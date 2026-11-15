namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>A field of the solver of triangles: a side or an angle, with its letter and what is typed in it.</summary>
    public sealed class TriangleInput : ObservableObject
    {
        private readonly Action changed;
        private string? text;

        internal TriangleInput(string name, string unit, Action changed)
        {
            Name = Equation.Parse(name, EquationFormat.UnicodeMath);
            Unit = unit;
            this.changed = changed;
        }

        /// <summary>The letter of the side or of the angle.</summary>
        public Equation Name { get; }

        /// <summary>The unit written after the field: ° for an angle.</summary>
        public string Unit { get; }

        /// <summary>What is typed.</summary>
        public string? Text
        {
            get => text;
            set
            {
                if (Set(ref text, value))
                    changed();
            }
        }
    }

    /// <summary>Values that fill the fields of the solver.</summary>
    public sealed record TriangleExample(string Name, IReadOnlyList<string> Values, ICommand FillCommand);

    /// <summary>A measure of a triangle found: its name and its value.</summary>
    public sealed record TriangleMeasure(string Name, Equation Value);

    /// <summary>A triangle found: its sides, its angles and its measures, and the triangle to draw.</summary>
    public sealed record TriangleResult(string? Title, Equation Sides, Equation Angles, IReadOnlyList<TriangleMeasure> Measures, Triangle Triangle);

    /// <summary>
    /// The solver of triangles: three of the sides and angles give the others, the area, the perimeter and the radii of
    /// the circles, with the laws used, written in UnicodeMath for the view to draw.
    /// </summary>
    public sealed class TrianglesViewModel : ObservableObject
    {
        private static readonly string[] Empty = { "", "", "", "", "", "" };

        private TriangleSolution? solution;
        private IReadOnlyList<TriangleResult> results = Array.Empty<TriangleResult>();
        private IReadOnlyList<Equation> steps = Array.Empty<Equation>();
        private bool updating;

        public TrianglesViewModel()
        {
            Sides = Triangles.SideNames.Select(name => new TriangleInput(name, "", Solve)).ToList();
            Angles = Triangles.AngleNames.Select(name => new TriangleInput(name, "°", Solve)).ToList();
            Inputs = Sides.Concat(Angles).ToList();
            ClearCommand = new RelayCommand(() => Fill(Empty));
            Examples = new[]
            {
                Example("3, 4, 5", "3", "4", "5", "", "", ""),
                Example("SAS", "7", "", "9", "", "60", ""),
                Example("ASA", "", "", "10", "45", "75", ""),
                Example("Two triangles", "6", "8", "", "40", "", ""),
                Example("No triangle", "1", "2", "4", "", "", ""),
            };
            Fill(Examples[0].Values);
        }

        private TriangleExample Example(string name, params string[] values) => new TriangleExample(name, values, new RelayCommand(() => Fill(values)));

        /// <summary>The fields of a, b and c.</summary>
        public IReadOnlyList<TriangleInput> Sides { get; }

        /// <summary>The fields of α, β and γ.</summary>
        public IReadOnlyList<TriangleInput> Angles { get; }

        /// <summary>The fields of a, b, c, α, β and γ.</summary>
        public IReadOnlyList<TriangleInput> Inputs { get; }

        /// <summary>The examples that fill the fields.</summary>
        public IReadOnlyList<TriangleExample> Examples { get; }

        /// <summary>Empties the fields.</summary>
        public ICommand ClearCommand { get; }

        /// <summary>The solution of the values in the fields.</summary>
        public TriangleSolution? Solution
        {
            get => solution;
            private set
            {
                if (!Set(ref solution, value))
                    return;
                OnPropertyChanged(nameof(Error));
                OnPropertyChanged(nameof(HasTwoTriangles));
                OnPropertyChanged(nameof(Case));
                OnPropertyChanged(nameof(HasNoSteps));
            }
        }

        /// <summary>Why there is no triangle, or null.</summary>
        public string? Error => solution?.Error;

        /// <summary>Whether two triangles have the values given.</summary>
        public bool HasTwoTriangles => solution?.Triangles.Count == 2;

        /// <summary>The case solved, with its name, or null.</summary>
        public string? Case => solution?.Case is { } kind ? "Case " + kind + " (" + CaseName(kind) + ")" : null;

        /// <summary>Whether no law was used: too few values.</summary>
        public bool HasNoSteps => solution == null || solution.Steps.Count == 0;

        /// <summary>The triangles found.</summary>
        public IReadOnlyList<TriangleResult> Results
        {
            get => results;
            private set => Set(ref results, value);
        }

        /// <summary>The laws that give the values found.</summary>
        public IReadOnlyList<Equation> Steps
        {
            get => steps;
            private set => Set(ref steps, value);
        }

        /// <summary>Fills the fields, as the examples do.</summary>
        public void Fill(IReadOnlyList<string> values)
        {
            updating = true;
            for (var i = 0; i < Inputs.Count; i++)
                Inputs[i].Text = values[i];
            updating = false;
            Solve();
        }

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

        private void Show(TriangleSolution found)
        {
            Results = found.Triangles.Select((triangle, index) => Result(triangle, found.Triangles.Count == 2 ? "Triangle " + (index + 1) : null)).ToList();
            Steps = found.Steps.Select(Math).ToList();
            Solution = found;
        }

        private static TriangleResult Result(Triangle triangle, string? title)
        {
            var (sides, angles) = Triangles.Describe(triangle);
            return new TriangleResult(title, Math(sides), Math(angles), new[]
            {
                new TriangleMeasure("Area", Math(Units.Format(triangle.Area))),
                new TriangleMeasure("Perimeter", Math(Units.Format(triangle.Perimeter))),
                new TriangleMeasure("Circumradius", Math("R=" + Units.Format(triangle.Circumradius))),
                new TriangleMeasure("Inradius", Math("r=" + Units.Format(triangle.Inradius))),
            }, triangle);
        }

        private static Equation Math(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);

        private static string CaseName(string kind) => kind switch
        {
            "SSS" => "three sides",
            "SAS" => "two sides and the angle between them",
            "SSA" => "two sides and an angle not between them",
            "ASA" => "two angles and the side between them",
            _ => "two angles and a side not between them",
        };
    }
}
