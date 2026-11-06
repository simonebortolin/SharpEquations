namespace SharpEquations.Internal.MathML
{
    using System.Xml.Linq;

    /// <summary>Writes the MathML of an equation: a copy, so that the equation stays unchanged.</summary>
    internal static class MathMLSerializer
    {
        public static XElement Serialize(XElement math) => new XElement(math);
    }
}
