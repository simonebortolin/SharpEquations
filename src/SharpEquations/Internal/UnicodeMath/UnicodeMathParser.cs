namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// Reads UnicodeMath into MathML with <see cref="UnicodeMathTranslator"/>, a port of UnicodeMathML (MIT, see NOTICE)
    /// whose grammar is compiled at build time by Parlot.SourceGenerator.
    /// </summary>
    internal static class UnicodeMathParser
    {
        private static readonly Regex BareAmpersand = new Regex("&(?!#x[0-9A-Fa-f]+;|#[0-9]+;|lt;|gt;|amp;|quot;|apos;)", RegexOptions.CultureInvariant);

        public static XElement Parse(string unicodemath, bool display = false, bool strict = false, CancellationToken cancellationToken = default)
        {
            string mathML;
            try
            {
                mathML = UnicodeMathTranslator.ToMathML(unicodemath, display, true, strict, cancellationToken);
            }
            catch (Exception e) when (e is NullReferenceException or InvalidCastException or ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                // the port of UnicodeMathML assumes what its grammar gives: a structure it does not expect is not UnicodeMath
                throw new FormatException("Not valid UnicodeMath: " + unicodemath, e);
            }
            return FromTranslator(mathML, unicodemath);
        }

        /// <summary>The MathML text of the translator as a math element in the MathML namespace.</summary>
        internal static XElement FromTranslator(string mathML, string source)
        {
            XElement math;
            try
            {
                math = XElement.Parse(BareAmpersand.Replace(mathML, "&amp;"), LoadOptions.PreserveWhitespace);
            }
            catch (XmlException e)
            {
                throw new FormatException("The equation could not be converted to MathML: " + source, e);
            }
            foreach (var element in math.DescendantsAndSelf())
                element.Name = MathMLNamespaces.MathML + element.Name.LocalName;
            return math;
        }
    }
}
