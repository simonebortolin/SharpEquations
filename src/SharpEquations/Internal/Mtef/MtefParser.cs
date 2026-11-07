namespace SharpEquations.Internal.Mtef
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Converts MathType Equation Format (MTEF) equations, the binary format of Equation Editor 3.0
    /// (MTEF 3) and MathType (MTEF 5) OLE objects, to presentation MathML.
    /// </summary>
    /// <remarks>
    /// The MTEF records are read into XML as https://github.com/jure/mathtype does and converted with the
    /// stylesheets of https://github.com/jure/mathtype_to_mathml (MIT, see NOTICE). Conversion is one-way:
    /// MTEF cannot be written. Font-specific private use characters are dropped; the spaces of MathType and of
    /// Equation Editor 3.0 are kept as spaces.
    /// </remarks>
    internal static class MtefParser
    {
        /// <summary>
        /// Converts an MTEF equation (MTEF version 3 or 5, without the "Equation Native" header) to a MathML math element.
        /// </summary>
        /// <exception cref="InvalidDataException">The data is not a supported MTEF equation.</exception>
        public static XElement Parse(ReadOnlySpan<byte> mtef)
        {
            if (HasEquationNativeHeader(mtef))
                return FromEquationNative(mtef);
            var xml = MtefReader.Read(mtef);
            MtefPreprocessor.Prepare(xml);
            var math = EquationStylesheets.Transform("mtef2mml.xsl", xml);
            if (math == null || math.Name.LocalName != "math")
                throw new InvalidDataException("The MTEF equation could not be converted to MathML.");
            foreach (var element in math.DescendantsAndSelf())
                element.Name = MathMLNamespaces.MathML + element.Name.LocalName;
            return math;
        }

        /// <summary>
        /// Whether the data starts with an EQNOLEFILEHDR: its size, 28, as a 16-bit number. An MTEF equation starts
        /// with its version, 2 to 5.
        /// </summary>
        public static bool HasEquationNativeHeader(ReadOnlySpan<byte> data)
        {
            return data.Length >= 10 && data[0] == 28 && data[1] == 0;
        }

        /// <summary>
        /// Converts the content of the "Equation Native" stream of an Equation Editor or MathType OLE object: an
        /// EQNOLEFILEHDR header followed by the MTEF equation. Reading the stream from the OLE object is left to the
        /// caller.
        /// </summary>
        /// <exception cref="InvalidDataException">The data is not a supported MTEF equation.</exception>
        public static XElement FromEquationNative(ReadOnlySpan<byte> equationNative)
        {
            if (equationNative.Length < 10)
                throw new InvalidDataException("The Equation Native stream is too short.");
            var headerSize = equationNative[0] | equationNative[1] << 8;
            var declaredSize = (uint)(equationNative[6] | equationNative[7] << 8 | equationNative[8] << 16 | equationNative[9] << 24);
            var size = (int)Math.Min(declaredSize, (uint)Math.Max(0, equationNative.Length - headerSize));
            if (headerSize >= equationNative.Length || size <= 0)
                throw new InvalidDataException("The Equation Native stream holds no equation.");
            return Parse(equationNative.Slice(headerSize, size));
        }
    }
}
