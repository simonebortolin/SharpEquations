namespace SharpEquations
{
    using System.Xml.Linq;

    /// <summary>
    /// XML namespaces of the equation formats.
    /// </summary>
    public static class MathMLNamespaces
    {
        /// <summary>
        /// MathML, used by OpenDocument formulas and as the common representation of equations.
        /// </summary>
        public static readonly XNamespace MathML = "http://www.w3.org/1998/Math/MathML";

        /// <summary>
        /// Office Math Markup Language (OMML).
        /// </summary>
        public static readonly XNamespace Omml = "http://schemas.openxmlformats.org/officeDocument/2006/math";

        /// <summary>
        /// WordprocessingML, used by the run properties inside OMML.
        /// </summary>
        public static readonly XNamespace WordprocessingML = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    }
}
