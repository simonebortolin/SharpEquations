#nullable enable

namespace SharpEquations.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// The test cases of latex2mathml: LaTeX with the MathML latex2mathml writes for it, and macros with the LaTeX they
    /// expand to.
    /// </summary>
    public class Latex2MathMLCorpusTests
    {
        public sealed class Conversion
        {
            public string id { get; set; } = "";
            public string latex { get; set; } = "";
            public bool display { get; set; }
            public string mathml { get; set; } = "";
        }

        public sealed class Macro
        {
            public string id { get; set; } = "";
            public string latex { get; set; } = "";
            public string equivalent { get; set; } = "";
            public bool display { get; set; }
        }

        internal static List<Conversion> Conversions() => JsonSerializer.Deserialize<List<Conversion>>(
            File.ReadAllText(TestData.Path("latex", "latex2mathml-converter.json")))!;

        internal static List<Macro> Macros() => JsonSerializer.Deserialize<List<Macro>>(
            File.ReadAllText(TestData.Path("latex", "latex2mathml-macros.json")))!;

        private static EquationOptions Options(bool display) => new EquationOptions { Display = display };

        private static string Compact(Equation equation) => string.Concat(equation.ToMathML().Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)))
            .Replace(" xmlns=\"http://www.w3.org/1998/Math/MathML\"", "");

        private static readonly XNamespace M = "http://www.w3.org/1998/Math/MathML";

        // the attributes of latex2mathml that only repeat what the MathML operator dictionary or the default says
        private static readonly HashSet<string> Defaulted = new HashSet<string>
        {
            "fence", "form", "stretchy", "minsize", "maxsize", "symmetric", "lspace", "rspace", "columnalign", "rowalign",
            "columnspacing", "rowspacing", "columnlines", "rowlines", "accent", "accentunder", "movablelimits", "separator",
            "displaystyle", "scriptlevel",
        };

        // the spacing accents of latex2mathml, with the combining marks SharpEquations writes for them
        private static readonly Dictionary<string, string> Accents = new Dictionary<string, string>
        {
            ["˙"] = "̇", ["¨"] = "̈", ["˜"] = "̃", ["^"] = "̂", ["ˇ"] = "̌", ["´"] = "́",
            ["`"] = "̀", ["˘"] = "̆", ["˚"] = "̊", ["→"] = "⃗", ["⃛"] = "⃛", ["⃜"] = "⃜",
        };

        private static bool IsSpace(string text) => text.Length > 0 && text.All(c => c == ' ' || c == ' ' || c is >= ' ' and <= '​' || c == ' ');

        private static readonly HashSet<string> InferredRow = new HashSet<string> { "math", "mrow", "mtd", "msqrt", "mstyle", "mpadded", "mphantom", "menclose", "mtext" };

        /// <summary>
        /// The equation as MathML, with what latex2mathml and SharpEquations write differently for the same equation
        /// made the same: attributes that say the default or set the style, spaces (formatting, which an equation does not
        /// keep), invisible function application and times, the middle dot of \cdot (· for latex2mathml, ⋅ for
        /// SharpEquations), names of operators and primes (mo for latex2mathml, mi and mo for SharpEquations), spacing
        /// accents (˙ for latex2mathml, the combining ̇ for SharpEquations), bars (mover with ¯ for latex2mathml,
        /// menclose for SharpEquations), capital Greek letters (upright in TeX, which latex2mathml writes as mi without
        /// mathvariant), rows that group nothing.
        /// </summary>
        private static string Canonical(Equation equation)
        {
            var math = new XElement(equation.ToMathML());
            foreach (var e in math.DescendantsAndSelf().ToList())
            {
                e.Attributes().Where(a => Defaulted.Contains(a.Name.LocalName)).Remove();
                var name = e.Name.LocalName;
                var text = e.HasElements ? null : e.Value;
                switch (name)
                {
                    case "mspace":
                    case "mo" or "mtext" when text != null && IsSpace(text):
                    case "mo" when text is "⁡" or "⁢":
                        e.Remove();
                        break;
                    case "mo" when text == "·":
                        e.Value = "⋅";
                        break;
                    case "mo" when text != null && Accents.TryGetValue(text, out var mark) && e.Parent!.Name.LocalName is "mover" && e.ElementsBeforeSelf().Any():
                        e.Value = mark;
                        break;
                    case "mi" or "mo" when text != null && text.Length > 1 && text.All(char.IsLetter):
                    case "mi" when text != null && text.Length > 0 && text.All(c => c is '′' or '″' or '‴' or '⁗'):
                        e.Name = M + (text.All(char.IsLetter) ? "mi" : "mo");
                        break;
                    case "mi" when text is { Length: 1 } && text[0] is >= 'Α' and <= 'Ω' && e.Attribute("mathvariant") == null:
                        e.SetAttributeValue("mathvariant", "normal");
                        break;
                }
            }
            foreach (var e in math.Descendants(M + "mover").Where(e => e.Elements().Skip(1).FirstOrDefault()?.Value is "¯" or "‾" or "―" or "̅").ToList())
                e.ReplaceWith(new XElement(M + "menclose", new XAttribute("notation", "top"), e.Elements().First()));
            bool changed;
            do
            {
                changed = false;
                foreach (var e in math.Descendants().Where(e => e.Name.LocalName is "mrow" or "mstyle" && !e.HasAttributes).ToList())
                    if (e.Elements().Count() == 1 || InferredRow.Contains(e.Parent!.Name.LocalName))
                    {
                        e.ReplaceWith(e.Nodes());
                        changed = true;
                    }
            }
            while (changed);
            return string.Concat(math.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting))).Replace(" xmlns=\"" + M.NamespaceName + "\"", "");
        }

        // what does not read back as it was written, through the UnicodeMath that LaTeX is read as: a name of operator
        // alone (\operatorname{lg}, which UnicodeMath reads as the letters l g), bars with nothing between them (|\,|\:|,
        // \{\middle|\}), a parenthesis alone in a phantom (\mathstrut, \phantom{(})
        private static readonly HashSet<string> NotWrittenBack = new HashSet<string>
        {
            "spaces", "lg", "middle", "mathstrut",
        };

        // what the reader of LaTeX, which reads LaTeX as Word does, reads otherwise than latex2mathml: the commands Word
        // does not know (\href, \class, \style, \color, \raise, \smash, \mathchoice, \LaTeX, the sizes, \hbox, \verb,
        // the old font switches \rm, \cal, \Bbb...), the environments and arguments it does not take (matrix*, \matrix{},
        // the column specification of array, \hline, \substack, \sideset, \genfrac, \above, \atop, unbraced arguments as
        // \underset ab), \limits and \mathop, spaces before scripts (x ^ { 2 }), a script without base, and the symbols
        // it maps to other characters or does not have (\gt, \lt, \And, \S, \omicron, \dotsb, \shortmid...)
        private static readonly HashSet<string> ReadOtherwise = new HashSet<string>
        {
            "307-a", "307-b", "And", "Bmatrix", "LaTeX", "LaTeX-with-style", "S", "TeX", "TeX-with-style", "above",
            "abovewithdelims", "align-nonumber", "align-notag", "align0", "align1", "argmax",
            "array-with-horizontal-lines", "atop-and-atopwithdelims", "backslash-e", "bigcirc",
            "bigl-smallmatrix-bigr", "binomial-coefficients", "blackboard-bold", "boldsymbol", "boxed-box", "bra",
            "brace", "brace-1", "braket", "calligraphic-mode", "centerdot", "cfrac", "circledS", "class",
            "colorbox", "comments", "degree", "diagdown-diagup", "displaylines", "dotsb", "dotsc", "dotsi", "dotsm",
            "dotso", "emph", "empty-color-works", "empty-subscript", "eqalign", "escaped-characters",
            "exponent-without-base-works", "fbox", "fcolorbox", "footnotesize", "fraktur", "genfrac-displaystyle",
            "genfrac-scriptscriptstyle", "genfrac-scriptstyle", "genfrac-textstyle", "gggtr", "global-fonts",
            "greatest-common-divisor", "gt", "gvertneqq", "hbox", "hbox-with-backslash-in-text",
            "hbox-with-math-mode", "hfil", "hphantom", "href", "huge", "idotsint", "injlim", "intop", "issue-106",
            "issue-118", "issue-125-1-overset", "issue-125-3-underset", "issue-125-4-limits",
            "issue-125-4-mathop", "issue-301-a", "issue-301-b", "issue-301-c", "issue-33", "issue-42",
            "issue-498-prod", "issue-498-sum", "issue-60-1", "issue-60-2", "issue-75-1-row", "issue-75-2-rows",
            "issue-76", "ket", "lVert-double", "large", "ldotp", "leftroot-uproot", "lg", "liminf",
            "limit-at-plus-infinity", "limsup", "llap", "llless", "lower", "lt", "lvertneqq",
            "math-commands-that-currently-does-nothing", "mathbf", "mathbin", "mathcal", "mathchoice-block",
            "mathchoice-inline", "mathfrak", "mathit", "mathllap", "mathop", "mathord", "mathrel",
            "mathrm", "mathscr", "mathsf", "mathsfit", "mathstrut", "mathtt", "matrix-with-alignment",
            "matrix-without-begin-and-end", "middle", "mit", "moveleft", "moveright", "newcommand-escaped-hash",
            "newenvironment-multi-node", "ngeqq", "normalsize-scriptsize", "not", "nshortmid", "nshortparallel",
            "nsubseteqq", "null-delimiter", "numbers-identifiers-and-operators", "oldstyle", "omicron",
            "operatorname-star", "operatornamewithlimits", "overbrace-c",
            "pod", "prime-no-base", "projlim", "rVert-double", "raise", "rule", "rvert", "scr", "shortmid",
            "shoveleft", "sideset", "skew", "skew-with-braces", "small-tiny", "smallfrown", "smallint",
            "smallsmile", "smash", "smash-bottom", "smash-top", "spaces", "split", "style", "tag", "tag-star",
            "textbf", "textcolor", "textit", "textrm", "textsf", "texttt", "thickapprox", "thicksim", "thickspace",
            "tilde-1", "tilde-and-space-in-text", "tt", "underbrace-c", "underleftarrow", "underleftrightarrow",
            "underline", "underrightarrow", "unicode", "upper-Vmatrix", "varinjlim", "varsubsetneqq",
            "varsupsetneq", "varsupsetneqq", "vcenter", "verb", "vmatrix", "widecheck", "widetilde",
            "xLeftarrow-double", "xhookleftarrow", "xleftarrow", "xleftarrow-with-argument", "xleftrightarrow",
            "xrightarrow", "xrightarrow-with-argument",
        };

        // the macros the reader does not expand as LaTeX does: \DeclareMathOperator, the default of an optional argument,
        // an empty body (which leaves an empty group), \newenvironment
        private static readonly HashSet<string> NotExpanded = new HashSet<string>
        {
            "declare-math-operator", "newcommand-optional-default", "newcommand-empty-body", "newenvironment",
            "newenvironment-with-args",
        };

        private static string? Check(Func<string?> check)
        {
            try
            {
                return check();
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>
        /// The cases that fail and are not known to, and the known ones that no longer fail (to take out of the list).
        /// </summary>
        private static List<string> Unexpected<T>(IEnumerable<T> cases, Func<T, string> id, Func<T, string> input, HashSet<string> known, Func<T, string?> check)
        {
            var failures = cases.Select(c => (Id: id(c), Input: input(c), Failure: Check(() => check(c)))).ToList();
            var failed = new HashSet<string>(failures.Where(f => f.Failure != null).Select(f => f.Id));
            return failures.Where(f => f.Failure != null && !known.Contains(f.Id)).Select(f => f.Id + ": " + f.Input + "\n  " + f.Failure)
                .Concat(known.Where(k => !failed.Contains(k)).Select(k => k + ": passes, take it out of the known failures"))
                .ToList();
        }

        [Test]
        public async Task CorpusIsComplete()
        {
            await Assert.That(Conversions()).Count().IsEqualTo(327);
            await Assert.That(Macros()).Count().IsEqualTo(10);
        }

        // the MathML of latex2mathml writes LaTeX that, once read back, reads back to itself
        [Test]
        public async Task MathMLWritesLaTeXThatReadsBack()
        {
            var unexpected = Unexpected(Conversions(), c => c.id, c => c.latex, NotWrittenBack, c =>
            {
                var latex = Equation.Parse(c.mathml, EquationFormat.MathML).ToString(EquationFormat.LaTeX);
                var once = Equation.Parse(latex, EquationFormat.LaTeX, Options(c.display));
                var twice = Equation.Parse(once.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX, Options(c.display));
                return once == twice ? null : latex + "\n  " + once.ToString(EquationFormat.LaTeX);
            });
            await Assert.That(unexpected).IsEmpty();
        }

        // the LaTeX reads as the equation of the MathML latex2mathml writes for it
        [Test]
        public async Task LaTeXReadsAsLatex2MathMLReadsIt()
        {
            var unexpected = Unexpected(Conversions(), c => c.id, c => c.latex, ReadOtherwise, c =>
            {
                var expected = Equation.Parse(c.mathml, EquationFormat.MathML);
                var actual = Equation.Parse(c.latex, EquationFormat.LaTeX, Options(c.display)).WithDisplay(expected.IsDisplay);
                return actual == expected || Canonical(actual) == Canonical(expected) ? null : "expected " + Canonical(expected) + "\n  actual   " + Canonical(actual);
            });
            await Assert.That(unexpected).IsEmpty();
        }

        [Test]
        public async Task MacrosExpandToTheirEquivalent()
        {
            var unexpected = Unexpected(Macros(), m => m.id, m => m.latex, NotExpanded, m =>
            {
                var expected = Equation.Parse(m.equivalent, EquationFormat.LaTeX, Options(m.display));
                var actual = Equation.Parse(m.latex, EquationFormat.LaTeX, Options(m.display));
                return actual == expected ? null : expected.ToString(EquationFormat.LaTeX) + "\n  " + actual.ToString(EquationFormat.LaTeX);
            });
            await Assert.That(unexpected).IsEmpty();
        }
    }
}
