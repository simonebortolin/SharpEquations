namespace SharpEquations.XsltGenerator
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Text;

    /// <summary>
    /// Each XSLT 1.0 stylesheet among the additional files (<c>*.xsl</c>) compiled to a C# class of
    /// SharpEquations.Internal.Xslt, named after the file (omml2mml.xsl: <c>Omml2mmlStylesheet</c>), whose
    /// <c>Transform(XElement)</c> runs it over the runtime of that namespace.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class XsltGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor CannotCompile = new DiagnosticDescriptor(
            "XSLT001", "The stylesheet cannot be compiled", "{0}", "SharpEquations.Xslt", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var stylesheets = context.AdditionalTextsProvider
                .Where(static file => file.Path.EndsWith(".xsl", StringComparison.OrdinalIgnoreCase))
                .Select(static (file, cancellationToken) => (file.Path, Text: file.GetText(cancellationToken)?.ToString()));
            context.RegisterSourceOutput(stylesheets, static (output, file) => Generate(output, file.Path, file.Text));
        }

        public static string ClassName(string path)
        {
            var name = new StringBuilder();
            foreach (var c in Path.GetFileNameWithoutExtension(path))
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    name.Append(name.Length == 0 ? char.ToUpperInvariant(c) : c);
            }
            return name + "Stylesheet";
        }

        private static void Generate(SourceProductionContext output, string path, string? text)
        {
            if (text == null)
                return;
            var fileName = Path.GetFileName(path);
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                XDocument document;
                using (var reader = XmlReader.Create(new StringReader(text), settings))
                    document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
                var className = ClassName(path);
                // compiled again with what each compilation learns of the templates, until nothing is new
                var facts = new Facts();
                string source;
                while (true)
                {
                    var compiler = new StylesheetCompiler(document.Root!, className, fileName, facts);
                    source = compiler.Compile();
                    if (!facts.Add(compiler.Learnt))
                        break;
                }
                output.AddSource(className + ".g.cs", SourceText.From(source, Encoding.UTF8));
            }
            catch (XsltException e)
            {
                output.ReportDiagnostic(Diagnostic.Create(CannotCompile, Location(path, e.Line, e.Column), e.Message));
            }
            catch (XmlException e)
            {
                output.ReportDiagnostic(Diagnostic.Create(CannotCompile, Location(path, e.LineNumber, e.LinePosition), e.Message));
            }
        }

        private static Location Location(string path, int line, int column)
        {
            var position = new LinePosition(Math.Max(0, line - 1), Math.Max(0, column - 1));
            return Microsoft.CodeAnalysis.Location.Create(path, default, new LinePositionSpan(position, position));
        }
    }
}
