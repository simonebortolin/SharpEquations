namespace SharpEquations
{
    /// <summary>
    /// The format an equation is stored in by its document.
    /// </summary>
    public enum EquationFormat
    {
        /// <summary>
        /// Office Math Markup Language, used by .docx (and Office 2007+ in general).
        /// </summary>
        Omml,

        /// <summary>
        /// MathML, used by OpenDocument formulas.
        /// </summary>
        MathML,

        /// <summary>
        /// MathType Equation Format, the binary format of Equation Editor 3.0 and MathType OLE objects.
        /// </summary>
        Mtef,
    }
}
