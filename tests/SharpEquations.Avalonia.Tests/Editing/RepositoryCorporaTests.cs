namespace SharpEquations.Avalonia.Tests.Editing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using SharpEquations.Tests;

    /// <summary>
    /// The editor and the layout on the other corpora of the repository: the LaTeX of latex2mathml, the OMML of Word
    /// documents, the MathML of mml2tex and the MTEF of the fixtures.
    /// </summary>
    [NotInParallel("headless")]
    public class RepositoryCorporaTests
    {
        private static readonly EquationOptions WordLaTeX = new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word };

        private static List<(string Name, string LaTeX, Equation Equation)>? latex;
        private static List<(string Name, Equation Equation)>? omml;

        private static List<(string Name, string LaTeX, Equation Equation)> LaTeX() => latex ??= Load("latex2mathml-converter.json")
            .Concat(Load("latex2mathml-macros.json"))
            .ToList();

        private static IEnumerable<(string, string, Equation)> Load(string file)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(TestData.Path("latex", file)));
            foreach (var e in json.RootElement.EnumerateArray())
            {
                var source = e.GetProperty("latex").GetString()!;
                var display = e.TryGetProperty("display", out var d) && d.GetBoolean();
                if (Equation.TryParse(source, EquationFormat.LaTeX, new EquationOptions { Display = display }, out var equation) && equation!.ToMathML().HasElements)
                    yield return (e.GetProperty("id").GetString()!, source, equation);
            }
        }

        private static List<(string Name, Equation Equation)> Omml() => omml ??= Directory.GetFiles(TestData.Path("omml"), "*.omml")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), Equation.Parse(File.ReadAllText(f), EquationFormat.Omml)))
            .Where(e => e.Item2.ToMathML().HasElements)
            .ToList();

        /// <summary>The MathML of two equations is the same but for the whitespace between elements.</summary>
        private static bool Same(Equation? a, Equation? b) => a != null && b != null && Canonical(a) == Canonical(b);

        private static string Canonical(Equation equation)
        {
            var math = equation.ToMathML();
            foreach (var text in math.DescendantNodes().OfType<XText>().Where(t => t.Parent!.HasElements && string.IsNullOrWhiteSpace(t.Value)).ToList())
                text.Remove();
            return math.ToString(SaveOptions.DisableFormatting);
        }

        [Test]
        public async Task CorporaAreLarge()
        {
            await Assert.That(LaTeX()).Count().IsGreaterThan(300);
            await Assert.That(Omml()).Count().IsGreaterThan(180);
        }

        [Test]
        public async Task LoadedEquationIsUnchanged()
        {
            var failures = LaTeX().Select(e => (e.Name, e.Equation)).Concat(Omml())
                .Where(e => !Same(new MathEditor(e.Equation).ToEquation(), e.Equation))
                .Select(e => e.Name)
                .ToList();
            await Assert.That(failures).IsEmpty();
        }

        /// <summary>
        /// Linear and back, in UnicodeMath and in the LaTeX of Word: what the format cannot hold is lost the first time
        /// only, so the equation is that of its linear text read again.
        /// </summary>
        [Test]
        [Arguments(EquationFormat.UnicodeMath)]
        [Arguments(EquationFormat.LaTeX)]
        public async Task BuiltDownAndUpIsTheLinearText(EquationFormat format)
        {
            var failures = new List<string>();
            foreach (var (name, equation) in LaTeX().Select(e => (e.Name, e.Equation)).Concat(Omml()))
            {
                var text = format == EquationFormat.LaTeX ? equation.ToString(format, WordLaTeX) : equation.ToString(format);
                if (!Equation.TryParse(text, format, new EquationOptions { Display = equation.IsDisplay }, out var once))
                    continue;
                var editor = new MathEditor(equation) { InputFormat = format };
                editor.SetLinear(true);
                editor.SetLinear(false);
                if (!Same(editor.ToEquation(), once))
                    failures.Add(name + ": " + text);
            }
            // \lvert alone, which UnicodeMath writes as a bar that reads back as text in a row of its own
            await Assert.That(failures.Count).IsLessThanOrEqualTo(1).Because(string.Join("\n", failures));
        }

        /// <summary>
        /// The LaTeX of latex2mathml typed one character at a time, building up at each space and operator: for 90% of it,
        /// the equation of the whole text. The others are commands that take the text after them as their argument
        /// (<c>\over</c>, <c>\rm</c>, <c>\mathcal {...}</c> with a space) or are not equations (comments, spaces alone).
        /// </summary>
        [Test]
        public async Task TypedLaTeXIsMostlyTheEquationOfTheText()
        {
            var same = 0;
            var errors = new List<string>();
            foreach (var (name, source, equation) in LaTeX())
            {
                var editor = new MathEditor { InputFormat = EquationFormat.LaTeX, IsDisplay = equation.IsDisplay };
                try
                {
                    editor.Insert(source);
                    if (Same(editor.ToEquation(), equation))
                        same++;
                }
                catch (Exception e)
                {
                    errors.Add(name + ": " + e.Message);
                }
            }
            await Assert.That(errors).IsEmpty();
            await Assert.That(same).IsGreaterThanOrEqualTo(LaTeX().Count * 88 / 100);
        }

        [Test]
        public async Task CorporaLayOut()
        {
            var equations = LaTeX().Select(e => (e.Name, e.Equation)).Concat(Omml()).ToList();
            foreach (var file in Directory.GetFiles(TestData.Path("mtef"), "*.bin", SearchOption.AllDirectories).Concat(Directory.GetFiles(TestData.Path("mtef"), "*.eqn", SearchOption.AllDirectories)))
            {
                if (!file.Contains("invalid", StringComparison.Ordinal))
                {
                    try
                    {
                        equations.Add((Path.GetFileName(file), Equation.Parse(File.ReadAllBytes(file).AsSpan(), EquationFormat.Mtef)));
                    }
                    catch (FormatException)
                    {
                    }
                }
            }
            using (var json = JsonDocument.Parse(File.ReadAllText(TestData.Path("mathml", "mml2tex.json"))))
            {
                foreach (var e in json.RootElement.EnumerateArray())
                    equations.Add((e.GetProperty("label").GetString()!, Equation.Parse(e.GetProperty("mathml").GetString()!, EquationFormat.MathML)));
            }

            var failures = await Headless.Run(() =>
            {
                var failed = new List<string>();
                foreach (var (name, equation) in equations)
                {
                    try
                    {
                        var editor = new MathEditor(equation);
                        var layout = MathLayoutEngine.Layout(editor.Root, MathFonts.Default, new MathLayoutOptions { FontSize = 20, ShowPlaceholders = true, LineWidth = 800 });
                        // \vphantom has no width and \smash no height, but nothing has a negative or infinite size
                        if (!(layout.Width >= 0) || !(layout.Height >= 0) || layout.Width + layout.Height <= 0 || double.IsInfinity(layout.Width + layout.Height)
                            || layout.GetCaretRect(editor.Caret) == null)
                            failed.Add(name + ": " + layout.Width + "×" + layout.Height);
                    }
                    catch (Exception e)
                    {
                        failed.Add(name + ": " + e.GetType().Name + " " + e.Message);
                    }
                }
                return failed;
            });
            await Assert.That(equations.Count).IsGreaterThan(500);
            await Assert.That(failures).IsEmpty();
        }
    }
}
