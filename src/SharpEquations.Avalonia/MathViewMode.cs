namespace SharpEquations.Avalonia
{
    using System;

    /// <summary>How <see cref="MathEditBox"/> shows the equation.</summary>
    public enum MathViewMode
    {
        /// <summary>Built up, in two dimensions, as typed text builds up (Word's professional format).</summary>
        Professional,

        /// <summary>As one line of UnicodeMath or LaTeX, edited as text (Word's linear format).</summary>
        Linear,
    }

    /// <summary>The arguments of <see cref="MathEditBox.EquationChanged"/>.</summary>
    public sealed class EquationChangedEventArgs : EventArgs
    {
        /// <summary>Arguments with the new equation.</summary>
        public EquationChangedEventArgs(Equation? equation) => Equation = equation;

        /// <summary>The equation, or null when the editor is empty.</summary>
        public Equation? Equation { get; }
    }
}
