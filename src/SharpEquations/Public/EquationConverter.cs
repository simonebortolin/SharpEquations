#nullable enable

namespace SharpEquations
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading;
    using System.Xml.Linq;
    using SharpEquations.Internal;

    /// <summary>
    /// Converts equations from one format to another in one call; <see cref="Equation"/> reads an equation once to
    /// write it in several formats.
    /// </summary>
    /// <example>
    /// <code>
    /// string unicodeMath = EquationConverter.Convert("x^2+1", EquationFormat.LaTeX, EquationFormat.UnicodeMath);
    /// </code>
    /// </example>
    public static class EquationConverter
    {
        /// <summary>Converts an equation given as text: MathML or OMML XML, UnicodeMath or LaTeX.</summary>
        /// <param name="source">The equation.</param>
        /// <param name="sourceFormat">The format of <paramref name="source"/>; not <see cref="EquationFormat.Mtef"/>, which is binary.</param>
        /// <param name="destinationFormat">The format to convert to; MathML and OMML are returned as XML text.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <param name="cancellationToken">Cancels the parsing of long UnicodeMath and LaTeX equations.</param>
        /// <returns>The equation in <paramref name="destinationFormat"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="sourceFormat"/> is binary.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A format is not a format.</exception>
        /// <exception cref="NotSupportedException"><paramref name="destinationFormat"/> cannot be written.</exception>
        /// <exception cref="FormatException"><paramref name="source"/> is not a valid equation in <paramref name="sourceFormat"/>.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        public static string Convert(string source, EquationFormat sourceFormat, EquationFormat destinationFormat,
            EquationOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Equation.Parse(source, sourceFormat, options, cancellationToken).ToString(destinationFormat, options);
        }

        /// <summary>
        /// Converts an equation given as XML: a MathML <c>math</c> element, or an OMML <c>m:oMath</c> or
        /// <c>m:oMathPara</c> element.
        /// </summary>
        /// <param name="source">The element, which is not changed.</param>
        /// <param name="sourceFormat"><see cref="EquationFormat.MathML"/> or <see cref="EquationFormat.Omml"/>.</param>
        /// <param name="destinationFormat">The format to convert to; MathML and OMML are returned as XML text.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="sourceFormat"/> is not an XML format.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A format is not a format.</exception>
        /// <exception cref="NotSupportedException"><paramref name="destinationFormat"/> cannot be written.</exception>
        /// <exception cref="FormatException"><paramref name="source"/> is not an equation in <paramref name="sourceFormat"/>.</exception>
        public static string Convert(XElement source, EquationFormat sourceFormat, EquationFormat destinationFormat, EquationOptions? options = null)
        {
            return Equation.Parse(source, sourceFormat, options).ToString(destinationFormat, options);
        }

        /// <summary>
        /// Converts an equation given as bytes: MTEF, with or without the header of the "Equation Native" stream of its
        /// OLE object.
        /// </summary>
        /// <param name="source">The bytes.</param>
        /// <param name="sourceFormat"><see cref="EquationFormat.Mtef"/>.</param>
        /// <param name="destinationFormat">The format to convert to; MathML and OMML are returned as XML text.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="sourceFormat"/> is not a binary format.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A format is not a format.</exception>
        /// <exception cref="NotSupportedException"><paramref name="destinationFormat"/> cannot be written.</exception>
        /// <exception cref="InvalidDataException"><paramref name="source"/> is not a supported MTEF equation.</exception>
        public static string Convert(ReadOnlySpan<byte> source, EquationFormat sourceFormat, EquationFormat destinationFormat, EquationOptions? options = null)
        {
            return Equation.Parse(source, sourceFormat, options).ToString(destinationFormat, options);
        }

        /// <summary>
        /// Converts an equation given as text, returning false instead of throwing when it is not valid in its format.
        /// </summary>
        /// <param name="source">The equation; null gives false.</param>
        /// <param name="sourceFormat">The format of <paramref name="source"/>.</param>
        /// <param name="destinationFormat">The format to convert to.</param>
        /// <param name="result">The converted equation, or null when it could not be read.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="sourceFormat"/> is binary.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A format is not a format.</exception>
        /// <exception cref="NotSupportedException"><paramref name="destinationFormat"/> cannot be written.</exception>
        public static bool TryConvert([NotNullWhen(true)] string? source, EquationFormat sourceFormat, EquationFormat destinationFormat,
            [NotNullWhen(true)] out string? result, EquationOptions? options = null)
        {
            EquationPipeline.CheckWritable(destinationFormat);
            result = Equation.TryParse(source, sourceFormat, options, out var equation) ? equation.ToString(destinationFormat, options) : null;
            return result is not null;
        }

        /// <summary>Whether equations can be read from a format: all formats.</summary>
        public static bool CanRead(EquationFormat format) => EquationPipeline.IsDefined(format);

        /// <summary>Whether equations can be written in a format: all formats but <see cref="EquationFormat.Mtef"/>.</summary>
        public static bool CanWrite(EquationFormat format) => EquationPipeline.IsDefined(format) && format != EquationFormat.Mtef;
    }
}
