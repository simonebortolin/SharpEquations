namespace SharpEquations
{
    /// <summary>The LaTeX that <see cref="EquationConverter"/> writes.</summary>
    public enum LaTeXFlavor
    {
        /// <summary>
        /// Standard LaTeX math (amsmath, amssymb), which LaTeX, KaTeX and MathJax read: letters with their font
        /// commands (\mathbf{x}), Greek letters and symbols as commands (\alpha, \le), \left and \right around tall
        /// content, \tag for equation numbers.
        /// </summary>
        Standard,
        /// <summary>
        /// The LaTeX of Word's LaTeX math mode: math alphanumeric characters (𝑥), and UnicodeMath for the constructs
        /// that have no LaTeX command.
        /// </summary>
        Word,
    }
}
