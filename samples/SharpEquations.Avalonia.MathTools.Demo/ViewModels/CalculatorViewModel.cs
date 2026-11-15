namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The scientific keypad: the keys the view types into its equation editor, the equation as UnicodeMath and LaTeX,
    /// and what is computed from it.
    /// </summary>
    public sealed class CalculatorViewModel : ObservableObject
    {
        private Equation? equation;
        private string? unicodeMath;
        private string? latex;

        public CalculatorViewModel(ICasEngine engine)
        {
            ArgumentNullException.ThrowIfNull(engine);
            Solver = new AlgebraViewModel(engine);
            Solver.EditorRequested += (_, math) => Open(math);
            ComputeCaption = "With " + engine.Name + ": the value of what is typed, and its solutions, simpler forms, derivative and antiderivative";
        }

        /// <summary>Raised when the editor should take the focus: an equation was put in it.</summary>
        public event EventHandler? FocusRequested;

        /// <summary>The functions, powers, structures and constants.</summary>
        public IReadOnlyList<CalculatorKey> Functions { get; } = CalculatorKeys.Functions;

        /// <summary>The digits, the operators, the brackets and the variables.</summary>
        public IReadOnlyList<CalculatorKey> Numbers { get; } = CalculatorKeys.Numbers;

        /// <summary>The keys that move the caret, delete, undo and build up.</summary>
        public IReadOnlyList<CalculatorKey> Editing { get; } = CalculatorKeys.Editing;

        /// <summary>What is computed from the equation.</summary>
        public AlgebraViewModel Solver { get; }

        /// <summary>What the computer algebra system does, with its name.</summary>
        public string ComputeCaption { get; }

        /// <summary>The equation of the editor.</summary>
        public Equation? Equation
        {
            get => equation;
            set
            {
                if (!Set(ref equation, value))
                    return;
                // text that is not built up yet (sin( waiting for its argument) is an merror of the equation
                var complete = value != null && !value.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror");
                UnicodeMath = complete ? value!.ToString(EquationFormat.UnicodeMath) : null;
                LaTeX = complete ? value!.ToString(EquationFormat.LaTeX) : null;
                Solver.Equation = value;
            }
        }

        /// <summary>The equation as UnicodeMath, or null while it is not built up.</summary>
        public string? UnicodeMath
        {
            get => unicodeMath;
            private set => Set(ref unicodeMath, value);
        }

        /// <summary>The equation as LaTeX, or null while it is not built up.</summary>
        public string? LaTeX
        {
            get => latex;
            private set => Set(ref latex, value);
        }

        /// <summary>Puts an equation in the editor, which takes the focus.</summary>
        public void Open(Equation? math)
        {
            Equation = math;
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
