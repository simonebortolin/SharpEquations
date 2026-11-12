namespace SharpEquations.Editing
{
    /// <summary>What the floating bar at the caret shows (<see cref="MathEditor.GetQuickBarMode"/>).</summary>
    public enum MathQuickBarMode
    {
        /// <summary>Nothing: the bar is closed.</summary>
        Hidden,

        /// <summary>The commands that complete the one typed before the caret (<c>\al</c>: <c>\alpha</c>, <c>\aleph</c>...).</summary>
        Completions,

        /// <summary>The structures that take the selection (a fraction of it, its root, brackets around it...).</summary>
        Selection,

        /// <summary>The commands of the table of the caret: rows and columns.</summary>
        Table,

        /// <summary>The quick symbols, in an empty slot.</summary>
        Symbols,
    }
}
