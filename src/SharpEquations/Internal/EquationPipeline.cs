#nullable enable

namespace SharpEquations.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Xml.Linq;
    using System.Xml.Xsl;
    using SharpEquations.Internal.LaTeX;
    using SharpEquations.Internal.MathML;
    using SharpEquations.Internal.Mtef;
    using SharpEquations.Internal.Omml;
    using SharpEquations.Internal.UnicodeMath;

    /// <summary>
    /// Reads any source into the normalized MathML an <see cref="Equation"/> holds: the parser of the source format
    /// first, then <see cref="MathMLParser.Normalize"/>.
    /// </summary>
    internal static class EquationPipeline
    {
        public static bool IsDefined(EquationFormat format) => format is >= EquationFormat.Omml and <= EquationFormat.LaTeX;

        public static bool IsXml(EquationFormat format) => format is EquationFormat.MathML or EquationFormat.Omml;

        public static void CheckWritable(EquationFormat format)
        {
            CheckDefined(format);
            if (format == EquationFormat.Mtef)
                throw new NotSupportedException("MTEF equations cannot be written.");
        }

        /// <param name="source">A string or an XElement.</param>
        public static XElement Read(object? source, EquationFormat format, EquationOptions options, CancellationToken cancellationToken)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            CheckDefined(format);
            cancellationToken.ThrowIfCancellationRequested();
            var math = (format, source) switch
            {
                (EquationFormat.MathML, XElement element) => element,
                (EquationFormat.MathML, string text) => MathMLParser.ParseXml(text, format),
                (EquationFormat.Omml, XElement element) => ReadOmml(element, options),
                (EquationFormat.Omml, string text) => ReadOmml(MathMLParser.ParseXml(text, format), options),
                (EquationFormat.UnicodeMath, string text) => UnicodeMathParser.Parse(text, options.Display, options.Strict, cancellationToken),
                (EquationFormat.LaTeX, string text) => LaTeXParser.Parse(text, options.Display, LaTeXParser.Macros(options.LaTeXMacros), options.Strict, cancellationToken),
                (EquationFormat.Mtef, _) => throw new ArgumentException("MTEF equations are binary: pass them as bytes.", nameof(format)),
                _ => throw new ArgumentException(format + " equations are text, not XML: pass them as strings.", nameof(format)),
            };
            return MathMLParser.Normalize(math, options, format == EquationFormat.MathML);
        }

        /// <summary>Reads a binary source, MTEF, without copying it.</summary>
        public static XElement Read(ReadOnlySpan<byte> source, EquationFormat format, EquationOptions options)
        {
            CheckDefined(format);
            if (format != EquationFormat.Mtef)
                throw new ArgumentException(format + " equations are not binary: pass them as " + (IsXml(format) ? "text or XML." : "text."), nameof(format));
            return MathMLParser.Normalize(MtefParser.Parse(source), options, false);
        }

        private static void CheckDefined(EquationFormat format)
        {
            if (!IsDefined(format))
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown equation format.");
        }

        private static XElement ReadOmml(XElement omml, EquationOptions options)
        {
            if (options.Strict)
                OmmlParser.Validate(omml);
            try
            {
                return OmmlParser.Parse(omml);
            }
            catch (Exception e) when (e is ArgumentException or XsltException)
            {
                throw new FormatException(e.Message, e);
            }
        }
    }
}
