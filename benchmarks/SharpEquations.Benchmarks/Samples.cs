namespace SharpEquations.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Xml.Linq;

    /// <summary>The equations the benchmarks read: two written here, and the corpora of the tests copied next to them.</summary>
    internal static class Samples
    {
        private static readonly Dictionary<string, string> UnicodeMath = new()
        {
            ["quadratic"] = "x=(-b±√(b^2-4ac))/2a",
            ["fourier"] = "f(x)=a_0+∑_(n=1)^∞ (a_n cos⁡〖nπx/L〗+b_n sin⁡〖nπx/L〗)",
        };

        public static IEnumerable<string> Names => UnicodeMath.Keys;

        private static string DataPath(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

        /// <summary>The same equation in UnicodeMath, MathML, OMML and LaTeX, the last three written by the library.</summary>
        public static Dictionary<EquationFormat, string> InEveryFormat(string name)
        {
            var equation = Equation.Parse(UnicodeMath[name], EquationFormat.UnicodeMath);
            return new Dictionary<EquationFormat, string>
            {
                [EquationFormat.UnicodeMath] = UnicodeMath[name],
                [EquationFormat.MathML] = equation.ToString(EquationFormat.MathML),
                [EquationFormat.Omml] = equation.ToString(EquationFormat.Omml),
                [EquationFormat.LaTeX] = equation.ToString(EquationFormat.LaTeX),
            };
        }

        public static string[] UnicodeMathCorpus()
        {
            using var json = JsonDocument.Parse(File.ReadAllText(DataPath("unicodemath", "unicodemathml-examples.json")));
            return json.RootElement.EnumerateArray().Select(example => example.GetProperty("input").GetString()!).ToArray();
        }

        public static byte[][] MtefCorpus() =>
            Directory.GetFiles(DataPath("mtef"), "*.*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllBytes).ToArray();

        public static XElement[] ContentMathMLCorpus() =>
            Directory.GetFiles(DataPath("contentmathml"), "*.mml").OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(path => XElement.Load(path, LoadOptions.PreserveWhitespace).DescendantsAndSelf().Where(e => e.Name.LocalName == "math")).ToArray();

        public static string Stylesheet(string name) => DataPath("xsl", name);

        public static XElement[] OmmlCorpus() =>
            Directory.GetFiles(DataPath("omml"), "*.omml").OrderBy(path => path, StringComparer.Ordinal).Select(path => XElement.Load(path)).ToArray();
    }
}
