namespace SharpEquations.Rendering
{
    /// <summary>What a <see cref="MathPathCommand"/> draws.</summary>
    public enum MathPathCommandKind
    {
        /// <summary>Starts a contour at (X, Y).</summary>
        MoveTo,

        /// <summary>A line to (X, Y).</summary>
        LineTo,

        /// <summary>A quadratic curve to (X, Y), with the control (X1, Y1).</summary>
        QuadraticTo,

        /// <summary>A cubic curve to (X, Y), with the controls (X1, Y1) and (X2, Y2).</summary>
        CubicTo,

        /// <summary>Closes the contour.</summary>
        Close,
    }

    /// <summary>A command of the outline of a glyph (<see cref="OpenTypeMathFont.GetOutline"/>), in design units with y up.</summary>
    public readonly struct MathPathCommand
    {
        /// <summary>A command.</summary>
        public MathPathCommand(MathPathCommandKind kind, double x1, double y1, double x2, double y2, double x, double y)
        {
            Kind = kind;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            X = x;
            Y = y;
        }

        /// <summary>What the command draws.</summary>
        public MathPathCommandKind Kind { get; }

        /// <summary>The x of the first control of a curve.</summary>
        public double X1 { get; }

        /// <summary>The y of the first control of a curve.</summary>
        public double Y1 { get; }

        /// <summary>The x of the second control of a cubic curve.</summary>
        public double X2 { get; }

        /// <summary>The y of the second control of a cubic curve.</summary>
        public double Y2 { get; }

        /// <summary>The x of the end.</summary>
        public double X { get; }

        /// <summary>The y of the end.</summary>
        public double Y { get; }
    }
}
