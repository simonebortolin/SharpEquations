namespace SharpEquations.Tests
{
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using SharpEquations;
    using SharpEquations.Internal.LaTeX;
    using SharpEquations.Internal.UnicodeMath;

    public class UnicodeMathCorpusTests
    {
        internal sealed class Example
        {
            public string input { get; set; }
            public bool display { get; set; }
            public string mathml { get; set; }
        }

        internal static List<Example> Examples()
        {
            var path = TestData.Path("unicodemath", "unicodemathml-examples.json");
            return JsonSerializer.Deserialize<List<Example>>(File.ReadAllText(path));
        }

        private static object Parse(string unicodemath)
        {
            var input = UnicodeMathInput.MapToPrivate(UnicodeMathInput.ResolveControlWords(unicodemath.Trim()));
            return UnicodeMathGrammar.TryParse(input, out var ast) ? UnicodeMathInput.AstMapFromPrivate(ast) : null;
        }

        [Test]
        public async Task SimpleExpressions()
        {
            await Assert.That(Js.Stringify(Parse("a+b"))).IsEqualTo("{\"unicodemath\":{\"content\":[{\"expr\":[[{\"atoms\":[{\"chars\":\"a\"}]}],{\"operator\":\"+\"},[{\"atoms\":[{\"chars\":\"b\"}]}]]}],\"eqnumber\":null}}");
            await Assert.That(Js.Stringify(Parse("1/2"))).Contains("\"fraction\"");
            await Assert.That(Js.Stringify(Parse("∑_(i=1)^n▒i"))).Contains("\"nary\"");
        }

        [Test]
        public async Task NestedBracketsParseInLinearTime()
        {
            foreach (var (open, close) in new[] { ("(", ")"), ("|", "|"), ("〖", "〗"), ("⏟(", ")_(a)") })
            {
                var input = string.Concat(Enumerable.Repeat(open + "x_1^2 + ", 12)) + "y" + string.Concat(Enumerable.Repeat(close, 12));
                using (var cts = new System.Threading.CancellationTokenSource(5000))
                    await Assert.That(UnicodeMathGrammar.TryParse(UnicodeMathInput.MapToPrivate(input), cts.Token, out _)).IsTrue().Because(input);
            }
        }

        [Test]
        public async Task CorpusConvertsToMathML()
        {
            // macros apply in the equation that defines them, not in the next ones (\f+r, \h ab), the cached UnicodeMathML
            // output of "x = 0, y = 2" predates its current spacing, and UnicodeMathML strikes \cancel (╱, from bottom left to
            // top right) the other way (downdiagonalstrike)
            var different = new[] { "x = 0, y = 2", "\\f+r", "\\h ab",
                "\\xcancel(a)\\rect(207&a+b)\\rect(a+b)\\cancel(f+g)\\bcancel(u+i)" };
            var invalid = new[] { "a+{(1]/4⟩ 📌+1 Jⁱ⁼⁵ |_a", "|_〖|_a〗^b", "𝐸=𝑚𝑐^2#(1)\n𝐸=𝑚𝑐^2\n𝐸=𝑚𝑐^2#(3)" };
            var failed = new List<string>();
            foreach (var e in Examples().Where(e => !e.input.StartsWith("$") && !e.input.StartsWith("\\[") && !e.input.StartsWith("\\(") && !invalid.Contains(e.input)))
            {
                var math = UnicodeMathParser.Parse(e.input, e.display);
                await Assert.That(math.Name.LocalName).IsEqualTo("math").Because(e.input);
                var expected = System.Text.RegularExpressions.Regex.Replace(e.mathml, " xmlns=\"[^\"]*\"", "").Replace("'", "\"");
                if (UnicodeMathTranslator.ToMathML(e.input, e.display).Replace("'", "\"") != expected)
                    failed.Add(e.input);
            }
            await Assert.That(failed).IsEquivalentTo(different);
        }

        [Test]
        public async Task CorpusConvertsToOmml()
        {
            var invalid = new[] { "a+{(1]/4⟩ 📌+1 Jⁱ⁼⁵ |_a", "|_〖|_a〗^b", "𝐸=𝑚𝑐^2#(1)\n𝐸=𝑚𝑐^2\n𝐸=𝑚𝑐^2#(3)" };
            foreach (var e in Examples().Where(e => !e.input.StartsWith("$") && !e.input.StartsWith("\\[") && !e.input.StartsWith("\\(") && !invalid.Contains(e.input)))
            {
                var omml = SharpEquations.Internal.Omml.OmmlSerializer.Serialize(UnicodeMathParser.Parse(e.input, e.display));
                await Assert.That(omml).IsNotNull().Because(e.input);
            }
        }

        internal sealed class TestMml
        {
            public string mathml { get; set; }
            public string unicodemath { get; set; }
            public string tex { get; set; }
        }

        internal static List<TestMml> TestMmls()
        {
            var path = TestData.Path("unicodemath", "unicodemathml-testmml.json");
            return JsonSerializer.Deserialize<List<TestMml>>(File.ReadAllText(path));
        }

        // MathML to UnicodeMath, with the selection codes (Ⓐ, Ⓕ) as UnicodeMathML's tests keep them
        [Test]
        public async Task MathMLToUnicodeMath()
        {
            var failed = new List<string>();
            var cases = TestMmls().Where(t => t.unicodemath != null && !t.unicodemath.StartsWith("ⓘ")).ToList();
            await Assert.That(cases.Count).IsGreaterThan(70);
            // SharpEquations writes a slash that is not a fraction as the division slash ∕, which reads back as itself, where
            // UnicodeMathML escapes it (\/)
            var different = new Dictionary<string, string>
            {
                ["ⅆ/ⅆ𝑧 ⁡arcsin⁡𝑧=(1−𝑧²)^(−1\\/2) ,"] = "ⅆ/ⅆ𝑧 ⁡arcsin⁡𝑧=(1−𝑧²)^(−1∕2) ,",
            };
            foreach (var t in cases)
            {
                var actual = UnicodeMathSerializer.Serialize(UnicodeMathSerializer.ParseMathML(t.mathml), true);
                if (actual != (different.TryGetValue(t.unicodemath, out var ours) ? ours : t.unicodemath))
                    failed.Add("expected " + t.unicodemath + "\n  actual " + actual);
            }
            Console.WriteLine(failed.Count + " differ:\n" + string.Join("\n", failed));
            await Assert.That(failed).IsEmpty();
        }

        // MathML to the LaTeX of Word's LaTeX mode, as UnicodeMathML's tests expect it
        [Test]
        public async Task MathMLToWordLaTeX()
        {
            var failed = new List<string>();
            var cases = TestMmls().Where(t => t.tex != null).ToList();
            await Assert.That(cases.Count).IsGreaterThan(70);
            // SharpEquations writes prescripts on an empty group ({}_𝑛), as LaTeX has them, and braces a script with scripts
            // (𝑞^{(…)^2}), which UnicodeMathML leaves to read as two superscripts
            var different = new Dictionary<string, string>
            {
                ["_𝑛 𝐶_𝑘=\\binom{𝑛}{𝑘}=\\frac{𝑛!}{𝑘!(𝑛−𝑘)!}"] = "{}_𝑛𝐶_𝑘=\\binom{𝑛}{𝑘}=\\frac{𝑛!}{𝑘!(𝑛−𝑘)!}",
                ["𝜃_1(𝑧│𝜏)=𝜃_1(𝑧,𝑞)=2⁢∑_{𝑛=0}^∞(−1)^𝑛⁢𝑞^(𝑛+\\frac{1}{2})^2⁢\\sin ((2⁢𝑛+1)⁢𝑧),"] =
                    "𝜃_1(𝑧│𝜏)=𝜃_1(𝑧,𝑞)=2⁢∑_{𝑛=0}^∞(−1)^𝑛⁢𝑞^{(𝑛+\\frac{1}{2})^2}⁢\\sin ((2⁢𝑛+1)⁢𝑧),",
            };
            foreach (var t in cases)
            {
                var actual = LaTeXSerializer.Serialize(UnicodeMathSerializer.ParseMathML(t.mathml));
                if (actual != (different.TryGetValue(t.tex, out var ours) ? ours : t.tex))
                    failed.Add("expected " + t.tex + "\n  actual " + actual);
            }
            Console.WriteLine(failed.Count + " differ:\n" + string.Join("\n", failed));
            await Assert.That(failed).IsEmpty();
        }

        [Test]
        [Arguments("a^2+b^2=c^2", "a^2+b^2=c^2")]
        [Arguments("x=(-b±√(b^2-4ac))/2a", "x=\\frac{-b\\pm\\sqrt{b^2-4ac}}{2a}")]
        [Arguments("∑_(k=0)^n (n¦k)", "\\sum_{k=0}^n\\binom{n}{k}")]
        [Arguments("a¦b", "\\genfrac{}{}{0pt}{}{a}{b}")]
        [Arguments("n⒞k", "\\binom{n}{k}")]
        [Arguments("𝐅=m𝐚", "\\mathbf{F}=m\\mathbf{a}")]
        [Arguments("∀x∈ℝ:x^2≥0", "\\forall x\\in\\mathbb{R}:x^2\\ge0")]
        [Arguments("|x|", "|x|")]
        [Arguments("f(x)={■(x&x≥0@-x&x<0)┤", "f(x)=\\begin{cases}x&x\\ge0\\\\-x&x<0\\end{cases}")]
        [Arguments("E=mc^2#(1)", "E=mc^2\\tag{1}")]
        public async Task StandardLaTeX(string unicodemath, string latex)
        {
            await Assert.That(LaTeXSerializer.Serialize(UnicodeMathParser.Parse(unicodemath, true), true)).IsEqualTo(latex);
        }

        // the LaTeX examples of the corpus ($...$, $$...$$, \[...\], \(...\)) give the MathML of UnicodeMathML
        [Test]
        public async Task CorpusLaTeXConvertsToMathML()
        {
            var failed = new List<string>();
            var examples = Examples().Where(e => e.input.StartsWith("$") || e.input.StartsWith("\\[") || e.input.StartsWith("\\(")).ToList();
            await Assert.That(examples.Count).IsGreaterThan(20);
            // the examples define macros that later ones use
            var macros = new Dictionary<string, string>();
            foreach (var e in examples)
            {
                string actual;
                try
                {
                    actual = UnicodeMathTranslator.FromLaTeX(e.input, e.display, macros: macros);
                }
                catch (System.Exception ex)
                {
                    actual = "ERROR " + ex.Message;
                }
                var expected = System.Text.RegularExpressions.Regex.Replace(e.mathml, " xmlns=\"[^\"]*\"", "").Replace("'", "\"");
                // a slash of LaTeX is a slash for SharpEquations, where UnicodeMathML (as Word) builds a fraction
                if (e.input.Contains("q/r+2"))
                    continue;
                // UnicodeMathML reads the × of \mathbf{b\times c} as an identifier, SharpEquations as the operator it is
                if (e.input.Contains("\\mathbf{b\\times c}"))
                    expected = expected.Replace("<mi>×</mi>", "<mo>×</mo>");
                if (actual.Replace("'", "\"") != expected)
                    failed.Add(e.input + "\n  expected " + expected + "\n  actual   " + actual);
            }
            Console.WriteLine(failed.Count + " of " + examples.Count + " differ:\n" + string.Join("\n", failed));
            await Assert.That(failed).IsEmpty();
        }

        [Test]
        public async Task LaTeXToMathML()
        {
            var m = SharpEquations.MathMLNamespaces.MathML;
            var math = LaTeXParser.Parse("\\frac{a+b}{\\sqrt{2}}");
            await Assert.That(math.Attribute("display")).IsNull();
            await Assert.That(math.Element(m + "mfrac")).IsNotNull();
            await Assert.That((string)LaTeXParser.Parse("$$x^2$$").Attribute("display")).IsEqualTo("block");
            var macros = new Dictionary<string, string>();
            LaTeXParser.Parse("\\newcommand{\\R}{\\mathbb{R}} x", macros: macros);
            await Assert.That(LaTeXParser.Parse("x\\in\\R", macros: macros).Value).Contains("ℝ");
            // standard LaTeX back
            await Assert.That(LaTeXSerializer.Serialize(LaTeXParser.Parse("\\frac{a+b}{\\sqrt{2}}"), true)).IsEqualTo("\\frac{a+b}{\\sqrt{2}}");
        }

        [Test, Explicit]
        public void DumpStandardLaTeX()
        {
            foreach (var t in TestMmls())
                Console.WriteLine(LaTeXSerializer.Serialize(UnicodeMathSerializer.ParseMathML(t.mathml), true));
        }

        // UnicodeMath to MathML to UnicodeMath gives UnicodeMath with the same MathML
        [Test, Explicit]
        public void CorpusRoundTripsThroughUnicodeMath()
        {
            var invalid = new[] { "a+{(1]/4⟩ 📌+1 Jⁱ⁼⁵ |_a", "|_〖|_a〗^b", "𝐸=𝑚𝑐^2#(1)\n𝐸=𝑚𝑐^2\n𝐸=𝑚𝑐^2#(3)" };
            var lines = new List<string>();
            int same = 0, total = 0;
            foreach (var e in Examples().Where(e => !e.input.StartsWith("$") && !e.input.StartsWith("\\[") && !e.input.StartsWith("\\(") && !invalid.Contains(e.input)))
            {
                total++;
                var mathml = UnicodeMathTranslator.ToMathML(e.input, e.display);
                string back = null, mathml2 = null;
                try
                {
                    back = UnicodeMathSerializer.Serialize(UnicodeMathSerializer.ParseMathML(mathml));
                    mathml2 = UnicodeMathTranslator.ToMathML(back, e.display);
                }
                catch (System.Exception ex)
                {
                    mathml2 = "ERROR " + ex.Message;
                }
                if (mathml2 == mathml)
                    same++;
                else
                    lines.Add(JsonSerializer.Serialize(new { e.input, back, mathml, mathml2 }));
            }
            File.WriteAllLines(System.Environment.GetEnvironmentVariable("UMML_RT_DUMP") ?? Path.Combine(Path.GetTempPath(), "umml-rt.jsonl"), lines);
            Console.WriteLine(same + " of " + total + " round trip");
        }

        [Test]
        public async Task ConvertsToMathML()
        {
            await Assert.That(UnicodeMathParser.Parse("a^2+b^2=c^2").ToString(System.Xml.Linq.SaveOptions.DisableFormatting)).IsEqualTo("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msup><mi>𝑎</mi><mn>2</mn></msup><mo>+</mo><msup><mi>𝑏</mi><mn>2</mn></msup><mo>=</mo><msup><mi>𝑐</mi><mn>2</mn></msup></math>");
            var math = UnicodeMathParser.Parse("dy/dx", true);
            await Assert.That((string)math.Attribute("display")).IsEqualTo("block");
            await Assert.That((string)math.Element(SharpEquations.MathMLNamespaces.MathML + "mfrac").Attribute("intent")).IsEqualTo("derivative(1,y,x)");
            await Assert.That(() => UnicodeMathParser.Parse("|_〖|_a〗^b")).ThrowsExactly<System.FormatException>();
        }

        // compares the OMML of the equations of a Word document (UMML_WORD_DOCX, one equation per paragraph) with
        // the OMML converted from the UnicodeMath they were typed from (UMML_WORD_INPUT, one per line); writes UMML_WORD_DUMP
        [Test, Explicit]
        public void DumpWordComparison()
        {
            var m = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/officeDocument/2006/math";
            System.Xml.Linq.XDocument document;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(System.Environment.GetEnvironmentVariable("UMML_WORD_DOCX")))
            using (var stream = zip.GetEntry("word/document.xml").Open())
                document = System.Xml.Linq.XDocument.Load(stream);
            var words = document.Descendants(m + "oMath").ToList();
            var inputs = File.ReadAllLines(System.Environment.GetEnvironmentVariable("UMML_WORD_INPUT"));
            var lines = new List<string>();
            for (int i = 0; i < inputs.Length; i++)
            {
                string ours = null, error = null;
                try
                {
                    ours = SharpEquations.Internal.Omml.OmmlSerializer.Serialize(UnicodeMathParser.Parse(inputs[i], true)).ToString();
                }
                catch (System.Exception ex)
                {
                    error = ex.Message;
                }
                lines.Add(JsonSerializer.Serialize(new { input = inputs[i], word = i < words.Count ? words[i].ToString() : null, ours, error,
                    oursMathML = error == null ? UnicodeMathTranslator.ToMathML(inputs[i], true) : null }));
            }
            File.WriteAllLines(System.Environment.GetEnvironmentVariable("UMML_WORD_DUMP"), lines);
        }

        // writes the MathML of each corpus example, with the expected one, to the file named by UMML_MML_DUMP
        [Test, Explicit]
        public void DumpMathML()
        {
            var lines = new List<string>();
            foreach (var e in Examples().Where(e => !e.input.StartsWith("$") && !e.input.StartsWith("\\[") && !e.input.StartsWith("\\(")))
            {
                string actual, error = null;
                try
                {
                    actual = UnicodeMathTranslator.ToMathML(e.input, e.display);
                }
                catch (System.Exception ex)
                {
                    actual = null;
                    error = ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
                }
                lines.Add(JsonSerializer.Serialize(new { e.input, e.display, expected = e.mathml, actual, error }));
            }
            File.WriteAllLines(System.Environment.GetEnvironmentVariable("UMML_MML_DUMP"), lines);
        }

        // writes the syntax tree of each corpus example (or TIMEOUT, FAIL) to the file named by UMML_AST_DUMP
        [Test, Explicit]
        public void DumpSyntaxTrees()
        {
            var lines = new List<string>();
            foreach (var e in Examples())
            {
                var input = UnicodeMathInput.MapToPrivate(UnicodeMathInput.ResolveControlWords(e.input.Trim()));
                string result;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                using (var cts = new System.Threading.CancellationTokenSource(2000))
                {
                    try
                    {
                        result = UnicodeMathGrammar.TryParse(input, cts.Token, out var ast) ? Js.Stringify(UnicodeMathInput.AstMapFromPrivate(ast)) : "FAIL";
                    }
                    catch (System.OperationCanceledException)
                    {
                        result = "TIMEOUT";
                    }
                }
                lines.Add(JsonSerializer.Serialize(new { e.input, ms = watch.ElapsedMilliseconds, result }));
            }
            File.WriteAllLines(System.Environment.GetEnvironmentVariable("UMML_AST_DUMP"), lines);
        }

        [Test]
        public async Task CorpusParses()
        {
            // LaTeX examples go through a LaTeX to UnicodeMath translation first; these three are not valid UnicodeMath
            var invalid = new[] { "a+{(1]/4⟩ 📌+1 Jⁱ⁼⁵ |_a", "|_〖|_a〗^b", "𝐸=𝑚𝑐^2#(1)\n𝐸=𝑚𝑐^2\n𝐸=𝑚𝑐^2#(3)" };
            var examples = Examples().Where(e => !e.input.StartsWith("$") && !e.input.StartsWith("\\[") && !e.input.StartsWith("\\(")).ToList();
            var failed = examples.Where(e => Parse(e.input) == null).Select(e => e.input).ToList();
            await Assert.That(failed).IsEquivalentTo(invalid);
        }
    }
}
