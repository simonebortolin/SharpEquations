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
            "displaystyle", "scriptlevel", "largeop",
        };

        // the spacing accents of latex2mathml, with the combining marks SharpEquations writes for them
        private static readonly Dictionary<string, string> Accents = new Dictionary<string, string>
        {
            ["˙"] = "\u0307", ["¨"] = "\u0308", ["˜"] = "\u0303", ["~"] = "\u0303", ["^"] = "\u0302", ["ˇ"] = "\u030C",
            ["´"] = "\u0301", ["`"] = "\u0300", ["˘"] = "\u0306", ["˚"] = "\u030A", ["→"] = "\u20D7",
        };

        // the bars over and under a base, a mark of mover or munder for latex2mathml, menclose for SharpEquations
        private static readonly HashSet<string> Bars = new HashSet<string> { "¯", "‾", "―", "_", "\u0305", "\u0332" };

        // a length of 0 in any unit: 0, 0pt, 0.0em
        private static bool IsZero(string length) => length.Trim().TrimEnd("abcdefghijklmnopqrstuvwxyz%".ToCharArray()).Trim('0', '.').Length == 0;

        private static bool IsSpace(string text) => text.Length > 0 &&
            text.All(c => c == ' ' || c == '\u00A0' || c is >= '\u2000' and <= '\u200B' || c == '\u205F');

        private static readonly HashSet<string> InferredRow = new HashSet<string> { "math", "mrow", "mtd", "msqrt", "mstyle", "mpadded", "mphantom", "menclose", "mtext" };

        /// <summary>
        /// The equation as MathML, with what latex2mathml and SharpEquations write differently for the same equation
        /// made the same: attributes that say the default or set the style, spaces (formatting, which an equation does not
        /// keep), invisible function application and times, the middle dot of \cdot (· for latex2mathml, ⋅ for
        /// SharpEquations), names of operators and primes (mo for latex2mathml, mi and mo for SharpEquations), spacing
        /// accents (˙ for latex2mathml, the combining dot above for SharpEquations), bars (mover with ¯ for latex2mathml,
        /// menclose for SharpEquations), capital Greek letters (upright in TeX, which latex2mathml writes as mi without
        /// mathvariant), symbols that are neither letters nor digits (mi for latex2mathml, %, °, mo for SharpEquations,
        /// which marks a fence or a structure character taken literally with intent=":text"), rows that group nothing.
        /// </summary>
        private static string Canonical(Equation equation)
        {
            var math = new XElement(equation.ToMathML());
            foreach (var e in math.DescendantsAndSelf().ToList())
            {
                e.Attributes().Where(a => Defaulted.Contains(a.Name.LocalName) || a.Name.LocalName == "intent" && a.Value == ":text").Remove();
                var name = e.Name.LocalName;
                // a style as mathvariant, which MathML Core keeps for normal identifiers only: the math alphanumeric characters
                if (!e.HasElements && (string?)e.Attribute("mathvariant") is { } variant && !(variant == "normal" && name == "mi") &&
                    SharpEquations.Internal.UnicodeMath.UnicodeMathSerializer.MathVariants.TryGetValue(variant, out var style))
                {
                    e.Value = string.Concat(SharpEquations.Internal.Text.CodePoints.Of(e.Value)
                        .Select(c => SharpEquations.Internal.UnicodeMath.UnicodeMathText.GetMathAlphanumeric(c.ToString(), style)));
                    e.Attribute("mathvariant")!.Remove();
                }
                var text = e.HasElements ? null : e.Value;
                switch (name)
                {
                    case "mi" when text is { Length: 1 } && !char.IsLetterOrDigit(text[0]) && text != "⬚":
                        e.Name = M + "mo";
                        break;
                    // a digit of a math alphabet (𝟏), one identifier for SharpEquations, as OMML and UnicodeMath read it
                    case "mn" when text is { Length: 2 } && char.IsSurrogatePair(text, 0) && char.ConvertToUtf32(text, 0) is >= 0x1D7CE and <= 0x1D7FF:
                        e.Name = M + "mi";
                        break;
                    case "mspace":
                    case "mo" or "mtext" when text != null && IsSpace(text):
                    case "mo" when text is "\u2061" or "\u2062":
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
            foreach (var e in math.Descendants().Where(e => e.Name.LocalName is "mover" or "munder" && Bars.Contains(e.Elements().Skip(1).FirstOrDefault()?.Value ?? "")).ToList())
                e.ReplaceWith(new XElement(M + "menclose", new XAttribute("notation", e.Name.LocalName == "mover" ? "top" : "bottom"), e.Elements().First()));
            // the empty operators that hold the place of a fence or a cell, and the rows of empty cells (an empty matrix
            // is a table without rows for latex2mathml, one empty cell for SharpEquations)
            math.Descendants(M + "mo").Where(e => !e.HasElements && e.Value.Length == 0).Remove();
            math.Descendants(M + "mtr").Where(r => r.Elements().All(d => !d.HasElements && d.Value.Length == 0)).Remove();
            foreach (var e in math.Descendants(M + "mstyle").Where(e => !e.HasAttributes))
                e.Name = M + "mrow";
            // the thickness of a fraction bar that is not 0, and the moves of mpadded relative to the content (the labels
            // of \xrightarrow), formatting; an operator that holds an element (\mathop{x}), its element; the empty
            // placeholders (an empty mrow or mi for latex2mathml, ⬚ for SharpEquations), which hold a place only as the
            // argument of a script or a fraction; a length of 0 in any unit (0px), 0
            math.Descendants(M + "mfrac").Attributes("linethickness").Where(a => !IsZero(a.Value)).Remove();
            foreach (var e in math.Descendants(M + "mpadded").Where(e => e.HasAttributes && e.Attributes().All(a => a.Value.StartsWith("+") || a.Value.StartsWith("-"))).ToList())
                e.ReplaceWith(new XElement(M + "mrow", e.Nodes()));
            foreach (var a in math.Descendants(M + "mpadded").Attributes().Where(a => IsZero(a.Value)))
                a.Value = "0";
            foreach (var e in math.Descendants(M + "mo").Where(e => e.HasElements).ToList())
                e.ReplaceWith(e.Nodes());
            foreach (var e in math.Descendants().Where(e => e.Name.LocalName is "mi" or "mrow" && !e.HasElements && e.Value is "" or "⬚").ToList())
            {
                if (InferredRow.Contains(e.Parent!.Name.LocalName))
                    e.Remove();
                else
                    e.ReplaceWith(new XElement(M + "mi"));
            }
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

        // what the reader does not read as latex2mathml does, in four groups; a case that starts to pass must leave its
        // group, and the union of the groups is what LaTeXReadsAsLatex2MathMLReadsIt allows

        // commands of MathJax, KaTeX, plain TeX or packages that Word's LaTeX, which the reader follows, does not have:
        // links, classes, colors, boxes and moves, sizes, \hbox and \verb, \LaTeX, \skew, the primitives of plain TeX
        // (\matrix{}, \cr, \eqalign, \displaylines, \above, \atop, \brace) and its font switches (\rm, \cal), the classes
        // of \mathord, \mathbin and \mathrel, the bra-kets of the physics package, \mathsfit, \argmax, \widecheck
        private static readonly HashSet<string> NotInWordLaTeX = new HashSet<string>
        {
            "href", "class", "style", "textcolor", "colorbox", "fcolorbox", "raise", "lower", "moveleft", "moveright",
            "shoveleft", "vcenter", "smash-bottom", "smash-top", "llap", "mathllap", "rule", "mathchoice-block",
            "mathchoice-inline", "unicode", "verb", "huge", "large", "normalsize-scriptsize", "small-tiny", "footnotesize",
            "LaTeX", "LaTeX-with-style", "TeX", "TeX-with-style", "hbox", "hbox-with-math-mode", "hbox-with-backslash-in-text",
            "fbox", "leftroot-uproot", "skew", "skew-with-braces", "displaylines", "eqalign", "matrix-without-begin-and-end",
            "hfil", "above", "abovewithdelims", "atop-and-atopwithdelims", "brace", "brace-1", "oldstyle", "scr", "tt",
            "calligraphic-mode", "fraktur", "global-fonts", "greatest-common-divisor",
            "math-commands-that-currently-does-nothing", "mathbin", "mathord", "mathrel", "bra", "ket", "braket", "mathsfit",
            "argmax", "widecheck",
        };

        // LaTeX and amsmath that the reader does not read yet: an operator name with nothing after it (\lg, \liminf,
        // \injlim, \operatorname*{argmax}_x), which UnicodeMath reads as letters, and the names of amsmath that are two
        // words (lim inf); \limits after \mathop; both scripts of \overbrace and \underbrace; \middle between empty
        // delimiters and bars with nothing between them (|\,|); a font over \text and escaped braces (\mathrm{\{b\}},
        // \mathsf{\text{}}, \boldsymbol{1}); \newenvironment and ## in a macro
        private static readonly HashSet<string> NotReadYet = new HashSet<string>
        {
            "lg", "liminf", "limsup", "injlim", "projlim", "varinjlim", "operatornamewithlimits", "operatorname-star",
            "issue-125-4-limits", "overbrace-c", "underbrace-c", "middle", "spaces", "mathrm", "mathsf", "boldsymbol",
            "newenvironment-multi-node", "newcommand-escaped-hash",
        };

        // the same equation, which SharpEquations holds otherwise: an equation number as the label of its row (\tag),
        // and none for aligned equations, whose numbers latex2mathml writes as cells; the alignment of align, split and
        // cases as marks in one cell; ... as an ellipsis and * as the operator ∗, as Word reads them; a letter of \text
        // as an upright identifier; \Box as □, as KaTeX and MathJax write it (◻ for latex2mathml); \not as the long
        // solidus over the symbol, where latex2mathml puts a slash before it; the space of \ and the font of the text
        // (\textbf), formatting an equation does not keep
        private static readonly HashSet<string> HeldOtherwise = new HashSet<string>
        {
            "tag", "tag-star", "align1", "align-nonumber", "align-notag", "split", "align0", "issue-106", "issue-60-1",
            "issue-60-2", "numbers-identifiers-and-operators", "hphantom", "boxed-box", "not", "issue-118", "textbf", "textit",
            "textsf", "texttt", "emph",
        };

        // what is no LaTeX: \e, which no package defines, and ntext, a mistyped \text
        private static readonly HashSet<string> NotLaTeX = new HashSet<string> { "backslash-e", "issue-125-1-overset" };

        private static readonly HashSet<string> ReadOtherwise = new HashSet<string>(NotInWordLaTeX.Concat(NotReadYet).Concat(HeldOtherwise).Concat(NotLaTeX));

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
            // a case is in one group of what the reader reads otherwise
            await Assert.That(ReadOtherwise.Count).IsEqualTo(NotInWordLaTeX.Count + NotReadYet.Count + HeldOtherwise.Count + NotLaTeX.Count);
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
