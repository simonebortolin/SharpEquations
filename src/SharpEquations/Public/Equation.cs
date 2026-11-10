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
    /// An equation, read once from any format and written to any format: OMML, MathML, MTEF (read only),
    /// UnicodeMath and LaTeX.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An equation holds its mathematics as normalized presentation MathML, the representation every conversion
    /// goes through: reading a format and writing another never goes back to the source. It is immutable: the
    /// methods that return XML return copies, and two equations are equal when they hold the same MathML.
    /// </para>
    /// <para>
    /// The OMML conversion runs the TEI Stylesheets (BSD-2-Clause), the MTEF reader is a port of jure/mathtype
    /// (MIT) and the UnicodeMath and LaTeX converters a port of UnicodeMathML (MIT); see NOTICE.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var equation = Equation.Parse(@"\frac{a^2+b^2}{c}", EquationFormat.LaTeX);
    /// string unicodeMath = equation.ToString(EquationFormat.UnicodeMath);   // (a^2+b^2)/c
    /// XElement omml = equation.ToOmml();
    /// string text = $"{equation:LaTeX}";
    /// </code>
    /// </example>
    public sealed class Equation : IEquatable<Equation>, IFormattable
    {
        private readonly XElement mathML;
        private string? text;

        private Equation(XElement mathML)
        {
            this.mathML = mathML;
        }

        /// <summary>
        /// Whether the equation is shown on its own line (display mode) rather than inline with the text.
        /// </summary>
        public bool IsDisplay => (string?)mathML.Attribute("display") == "block";

        /// <summary>Reads an equation given as text: MathML or OMML XML, UnicodeMath or LaTeX.</summary>
        /// <param name="source">The equation, such as <c>\frac{a}{b}</c> in LaTeX or <c>a/b</c> in UnicodeMath.</param>
        /// <param name="format">The format of <paramref name="source"/>; not <see cref="EquationFormat.Mtef"/>, which is binary.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <param name="cancellationToken">Cancels the parsing of long UnicodeMath and LaTeX equations.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="format"/> is binary.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a format.</exception>
        /// <exception cref="FormatException"><paramref name="source"/> is not a valid equation in <paramref name="format"/>.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        public static Equation Parse(string source, EquationFormat format, EquationOptions? options = null, CancellationToken cancellationToken = default)
        {
            return new Equation(EquationPipeline.Read(source, format, options ?? EquationOptions.Default, cancellationToken));
        }

        /// <summary>
        /// Reads an equation given as XML: a MathML <c>math</c> element (with or without the MathML namespace), or an
        /// OMML <c>m:oMath</c> or <c>m:oMathPara</c> element. The element is not changed.
        /// </summary>
        /// <param name="source">The element.</param>
        /// <param name="format"><see cref="EquationFormat.MathML"/> or <see cref="EquationFormat.Omml"/>.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="format"/> is not an XML format.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a format.</exception>
        /// <exception cref="FormatException"><paramref name="source"/> is not an equation in <paramref name="format"/>.</exception>
        public static Equation Parse(XElement source, EquationFormat format, EquationOptions? options = null)
        {
            return new Equation(EquationPipeline.Read(source, format, options ?? EquationOptions.Default, CancellationToken.None));
        }

        /// <summary>
        /// Reads an equation given as bytes: MTEF 3 or 5, with or without the header of the "Equation Native" stream of
        /// its OLE object.
        /// </summary>
        /// <param name="source">The bytes.</param>
        /// <param name="format"><see cref="EquationFormat.Mtef"/>.</param>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="format"/> is not a binary format.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a format.</exception>
        /// <exception cref="InvalidDataException"><paramref name="source"/> is not a supported MTEF equation.</exception>
        public static Equation Parse(ReadOnlySpan<byte> source, EquationFormat format, EquationOptions? options = null)
        {
            return new Equation(EquationPipeline.Read(source, format, options ?? EquationOptions.Default));
        }

        /// <summary>Reads an equation given as text, returning false instead of throwing when it is not valid in its format.</summary>
        /// <param name="source">The equation; null gives false.</param>
        /// <param name="format">The format of <paramref name="source"/>.</param>
        /// <param name="equation">The equation, or null when it could not be read.</param>
        /// <exception cref="ArgumentException"><paramref name="format"/> is binary.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a format.</exception>
        public static bool TryParse([NotNullWhen(true)] string? source, EquationFormat format, [NotNullWhen(true)] out Equation? equation)
        {
            return TryParse(source, format, null, out equation);
        }

        /// <inheritdoc cref="TryParse(string?, EquationFormat, out Equation?)"/>
        /// <param name="options">The options, or null for <see cref="EquationOptions.Default"/>.</param>
        public static bool TryParse([NotNullWhen(true)] string? source, EquationFormat format, EquationOptions? options, [NotNullWhen(true)] out Equation? equation)
        {
            equation = null;
            if (source is null)
                return false;
            try
            {
                equation = Parse(source, format, options);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// The same equation in display mode (on its own line) or inline; this equation itself when it already is.
        /// </summary>
        public Equation WithDisplay(bool display)
        {
            if (display == IsDisplay)
                return this;
            var math = new XElement(mathML);
            math.SetAttributeValue("display", display ? "block" : null);
            return new Equation(math);
        }

        /// <summary>
        /// The equation as a MathML <c>math</c> element (a copy); a display equation has <c>display="block"</c>.
        /// </summary>
        public XElement ToMathML() => (XElement)EquationSerializer.Serialize(mathML, EquationFormat.MathML, EquationOptions.Default);

        /// <summary>
        /// The equation as OMML: an <c>m:oMathPara</c> element for a display equation, an <c>m:oMath</c> one otherwise.
        /// </summary>
        public XElement ToOmml() => (XElement)EquationSerializer.Serialize(mathML, EquationFormat.Omml, EquationOptions.Default);

        /// <summary>The equation in a format: MathML and OMML as XML text.</summary>
        /// <param name="format">The format; not <see cref="EquationFormat.Mtef"/>, which cannot be written.</param>
        /// <param name="options">
        /// The options, or null for <see cref="EquationOptions.Default"/>; <see cref="EquationOptions.LaTeXFlavor"/>
        /// chooses the LaTeX.
        /// </param>
        /// <exception cref="NotSupportedException"><paramref name="format"/> cannot be written.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a format.</exception>
        public string ToString(EquationFormat format, EquationOptions? options = null)
        {
            if (format == EquationFormat.MathML)
                return ToString();
            return EquationSerializer.ToText(EquationSerializer.Serialize(mathML, format, options ?? EquationOptions.Default));
        }

        /// <summary>
        /// The equation in the format named by <paramref name="format"/>, the name of an <see cref="EquationFormat"/>
        /// (ignoring case) or one of the short names: "M" (MathML, also for null or empty), "O" (OMML), "U"
        /// (UnicodeMath), "L" (standard LaTeX) and "W" (Word's LaTeX). Interpolated strings use it:
        /// <c>$"{equation:L}"</c>.
        /// </summary>
        /// <exception cref="FormatException"><paramref name="format"/> names no format that can be written.</exception>
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            switch (format)
            {
                case null:
                case "":
                case "M":
                case "G":
                    return ToString();
                case "O":
                    return ToString(EquationFormat.Omml);
                case "U":
                    return ToString(EquationFormat.UnicodeMath);
                case "L":
                    return ToString(EquationFormat.LaTeX);
                case "W":
                    return ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word });
            }
            // the name itself: Enum.TryParse would also take numbers, lists of names and the spaces around them
            foreach (var name in Enum.GetNames(typeof(EquationFormat)))
            {
                if (string.Equals(name, format, StringComparison.OrdinalIgnoreCase) && name != nameof(EquationFormat.Mtef))
                    return ToString((EquationFormat)Enum.Parse(typeof(EquationFormat), name));
            }
            throw new FormatException("Unknown equation format: " + format + ".");
        }

        /// <summary>The equation as MathML text.</summary>
        public override string ToString() => text ??= mathML.ToString(SaveOptions.DisableFormatting);

        /// <summary>Whether the other equation holds the same MathML.</summary>
        public bool Equals(Equation? other) => other is not null && (ReferenceEquals(this, other) || ToString() == other.ToString());

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as Equation);

        /// <inheritdoc/>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ToString());

        /// <summary>Whether two equations hold the same MathML.</summary>
        public static bool operator ==(Equation? left, Equation? right) => left is null ? right is null : left.Equals(right);

        /// <summary>Whether two equations hold different MathML.</summary>
        public static bool operator !=(Equation? left, Equation? right) => !(left == right);
    }
}
