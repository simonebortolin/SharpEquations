namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using SharpEquations;

    /// <summary>What a computer algebra system does to an equation of the editor.</summary>
    public enum CasAction
    {
        /// <summary>The value: exact, and as a decimal number when it differs.</summary>
        Evaluate,

        /// <summary>The solutions of an equation, or where an expression is 0.</summary>
        Solve,

        Simplify,

        /// <summary>Products and powers multiplied out.</summary>
        Expand,

        /// <summary>The expression as a product.</summary>
        Factor,

        /// <summary>The derivative by a letter.</summary>
        Differentiate,

        /// <summary>An antiderivative by a letter.</summary>
        Integrate,

        /// <summary>A definition, <c>a≔5</c>: the letter stands for its value in what is computed next.</summary>
        Define,
    }

    /// <summary>The unit of the angles of the trigonometric functions.</summary>
    public enum AngleUnit
    {
        Radians,
        Degrees,
    }

    /// <summary>A result of an <see cref="ICasEngine"/>: what was done, the result as an equation (null if it cannot be drawn) and as text.</summary>
    public sealed record CasResult(CasAction Action, Equation? Math, string Text)
    {
        /// <summary>
        /// The result one value at a time, for what compares it with another engine or with the reference tests: the
        /// exact value of <see cref="CasAction.Evaluate"/>, each solution of <see cref="CasAction.Solve"/> (none when
        /// there is none, or when they are not a finite set), the expression of the other actions.
        /// </summary>
        public IReadOnlyList<Equation> Values { get; init; } = Array.Empty<Equation>();

        /// <summary>The solutions as decimal numbers (<c>a+bi</c> for complex ones), when they are not plain numbers already; null otherwise.</summary>
        public Equation? Approximate { get; init; }

        /// <summary>The text of <see cref="Approximate"/>.</summary>
        public string? ApproximateText { get; init; }
    }

    /// <summary>
    /// A computer algebra system behind the math tools: it reads what an equation of the editor means and computes with
    /// it. <see cref="AngouriMathEngine"/> is the one of every platform; others can stand beside it (to compare results).
    /// </summary>
    public interface ICasEngine
    {
        /// <summary>The name of the engine, as the app shows it.</summary>
        string Name { get; }

        /// <summary>The unit of the angles of the trigonometric functions (radians by default).</summary>
        AngleUnit Angles { get; set; }

        /// <summary>The letters defined (<see cref="CasAction.Define"/>), with their values as equations.</summary>
        IReadOnlyDictionary<string, Equation> Definitions { get; }

        /// <summary>Forgets the definitions.</summary>
        void ClearDefinitions();

        /// <summary>The letters of an equation that are not defined, <c>x</c> first when there is one.</summary>
        /// <exception cref="NotSupportedException">The equation has something the engine cannot read, as the message says.</exception>
        IReadOnlyList<string> Variables(Equation equation);

        /// <summary>
        /// Does an action to an equation, or to a system of equations for <see cref="CasAction.Solve"/>, with the letters
        /// defined replaced by their values; <paramref name="variable"/> is the letter to solve for, or to differentiate and
        /// integrate by (the first of <see cref="Variables"/> if null; a system is solved for all its letters).
        /// </summary>
        /// <exception cref="NotSupportedException">The equation has something the engine cannot read, as the message says.</exception>
        /// <remarks>It can take long, and throw exceptions of other kinds for what the engine cannot do.</remarks>
        CasResult Run(CasAction action, Equation equation, string? variable = null);
    }
}
