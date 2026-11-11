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
    }

    /// <summary>
    /// A computer algebra system behind the math tools: it reads what an equation of the editor means and computes with
    /// it. <see cref="AngouriMathEngine"/> is the one of every platform; others can stand beside it (to compare results).
    /// </summary>
    public interface ICasEngine
    {
        /// <summary>The name of the engine, as the app shows it.</summary>
        string Name { get; }

        /// <summary>The letters of an equation, <c>x</c> first when there is one.</summary>
        /// <exception cref="NotSupportedException">The equation has something the engine cannot read, as the message says.</exception>
        IReadOnlyList<string> Variables(Equation equation);

        /// <summary>Does an action to an equation; <paramref name="variable"/> is the letter to solve for, or to differentiate and integrate by (the first of <see cref="Variables"/> if null).</summary>
        /// <exception cref="NotSupportedException">The equation has something the engine cannot read, as the message says.</exception>
        /// <remarks>It can take long, and throw exceptions of other kinds for what the engine cannot do.</remarks>
        CasResult Run(CasAction action, Equation equation, string? variable = null);
    }
}
