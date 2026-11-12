namespace SharpEquations.Avalonia.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using SharpEquations.Tests;

    /// <summary>The equations of the UnicodeMathML corpus (tests/data/unicodemath) that are valid UnicodeMath and not empty (a comment alone).</summary>
    internal static class Corpus
    {
        private static List<(string Input, bool Display, Equation Equation)>? examples;

        public static List<(string Input, bool Display, Equation Equation)> Examples() => examples ??= Load();

        private static List<(string, bool, Equation)> Load()
        {
            using var json = JsonDocument.Parse(File.ReadAllText(TestData.Path("unicodemath", "unicodemathml-examples.json")));
            return json.RootElement.EnumerateArray()
                .Select(e => (Input: e.GetProperty("input").GetString()!, Display: e.GetProperty("display").GetBoolean()))
                .Select(e => (e.Input, e.Display, Equation: Equation.TryParse(e.Input, EquationFormat.UnicodeMath, new EquationOptions { Display = e.Display }, out var equation) ? equation : null))
                .Where(e => e.Equation != null && e.Equation.ToMathML().HasElements)
                .Select(e => (e.Input, e.Display, e.Equation!))
                .ToList();
        }
    }
}
