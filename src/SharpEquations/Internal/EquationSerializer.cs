#nullable enable

namespace SharpEquations.Internal
{
    using System.Xml.Linq;
    using SharpEquations.Internal.LaTeX;
    using SharpEquations.Internal.MathML;
    using SharpEquations.Internal.Omml;
    using SharpEquations.Internal.UnicodeMath;

    /// <summary>Writes the MathML of an <see cref="Equation"/> with the serializer of the target format.</summary>
    internal static class EquationSerializer
    {
        /// <returns>An XElement for the XML formats, a string for the others.</returns>
        public static object Serialize(XElement math, EquationFormat format, EquationOptions options)
        {
            EquationPipeline.CheckWritable(format);
            return format switch
            {
                EquationFormat.MathML => MathMLSerializer.Serialize(math),
                EquationFormat.Omml => OmmlSerializer.Serialize(math),
                EquationFormat.UnicodeMath => UnicodeMathSerializer.Serialize(math),
                _ => LaTeXSerializer.Serialize(math, options.LaTeXFlavor == LaTeXFlavor.Standard),
            };
        }

        public static string ToText(object serialized) =>
            serialized is XElement element ? element.ToString(SaveOptions.DisableFormatting) : (string)serialized;
    }
}
