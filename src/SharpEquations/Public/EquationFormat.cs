namespace SharpEquations
{
    /// <summary>
    /// The formats <see cref="EquationConverter"/> reads and writes, and the format an equation is stored in by its document.
    /// </summary>
    public enum EquationFormat
    {
        /// <summary>
        /// Office Math Markup Language, the equations of .docx files (<c>m:oMath</c> and <c>m:oMathPara</c> elements).
        /// </summary>
        Omml,
        /// <summary>
        /// Presentation MathML (a <c>math</c> element), the equations of OpenDocument files and the representation
        /// every conversion goes through.
        /// </summary>
        MathML,
        /// <summary>
        /// MathType Equation Format, the binary format of Equation Editor 3.0 and MathType OLE objects (MTEF 3 and 5,
        /// with or without the header of the "Equation Native" stream). It can be read, not written.
        /// </summary>
        Mtef,
        /// <summary>
        /// UnicodeMath, the linear format of Word equations (Unicode Technical Note 28), such as <c>a^2+b^2=c^2</c>.
        /// </summary>
        UnicodeMath,
        /// <summary>
        /// LaTeX math, such as <c>\frac{a}{b}</c>; how it is written is chosen by <see cref="EquationOptions.LaTeXFlavor"/>.
        /// </summary>
        LaTeX,
    }
}
