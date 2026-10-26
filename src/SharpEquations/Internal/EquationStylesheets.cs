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

        public static XElement Transform(string name, XElement input)
        {
            var stylesheet = Compiled.GetOrAdd(name, n => new Lazy<XslCompiledTransform>(() => Load(n))).Value;
            var result = new XDocument();
            using (var reader = new XDocument(input).CreateReader())
            using (var writer = result.CreateWriter())
            {
                stylesheet.Transform(reader, writer);
            }
            return result.Root;
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
