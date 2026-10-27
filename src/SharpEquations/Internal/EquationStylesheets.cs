namespace SharpEquations
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Xsl;

    /// <summary>
    /// The XSLT 1.0 stylesheets embedded under Resources/Equations, compiled once on first use.
    /// </summary>
    internal static class EquationStylesheets
    {
        private const string ResourcePrefix = "SharpEquations.Resources.";

        private static readonly ConcurrentDictionary<string, Lazy<XslCompiledTransform>> Compiled =
            new ConcurrentDictionary<string, Lazy<XslCompiledTransform>>(StringComparer.Ordinal);

        /// <summary>
        /// Runs the stylesheet and parses its output, or returns null when the output is empty.
        /// The output goes through text so that disable-output-escaping, used by mtef2mml.xsl
        /// to write character references, takes effect.
        /// </summary>
        public static XElement Transform(string name, XElement input)
        {
            var stylesheet = Compiled.GetOrAdd(name, n => new Lazy<XslCompiledTransform>(() => Load(n))).Value;
            var output = new StringWriter();
            using (var reader = new XDocument(input).CreateReader())
            {
                stylesheet.Transform(reader, null, output);
            }
            var text = output.ToString();
            if (text.Trim().Length == 0)
                return null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(text), settings))
                return XElement.Load(reader, LoadOptions.PreserveWhitespace);
        }

        private static XslCompiledTransform Load(string name)
        {
            using (var stream = typeof(EquationStylesheets).Assembly.GetManifestResourceStream(ResourcePrefix + name))
            {
                if (stream == null)
                    throw new FileNotFoundException("Embedded stylesheet not found.", name);
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(stream, settings))
                {
                    var stylesheet = new XslCompiledTransform();
                    stylesheet.Load(reader, XsltSettings.Default, null);
                    return stylesheet;
                }
            }
        }
    }
}
