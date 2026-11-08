namespace SharpEquations.Internal.LaTeX
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using SharpEquations.Internal.Text;
    using SharpEquations.Internal.UnicodeMath;
    using static SharpEquations.Internal.UnicodeMath.UnicodeMathText;

    /// <summary>
    /// Characters as standard LaTeX (amsmath, amssymb), which LaTeX, KaTeX and MathJax read: math alphanumerics as
    /// letters with their font command (𝐱 → \mathbf{x}, ℝ → \mathbb{R}), Greek letters and symbols as commands
    /// (α → \alpha, ≤ → \le). Other characters stay as they are, which XeLaTeX and LuaLaTeX with unicode-math read.
    /// </summary>
    internal static partial class LaTeXSymbols
    {
        [GeneratedRegex("[A-Za-z]$")]
        private static partial Regex EndsWithLetter();

        [GeneratedRegex(@"[\\{}#$%&_^~]")]
        private static partial Regex TextSpecialCharacters();

        [GeneratedRegex(@"(\\[A-Za-z]+) (?=[^A-Za-z ]|$)")]
        private static partial Regex SpaceAfterCommand();

        private static readonly Dictionary<string, string> Commands = new Dictionary<string, string>
        {
            // Greek
            ["α"] = "alpha", ["β"] = "beta", ["γ"] = "gamma", ["δ"] = "delta", ["ε"] = "varepsilon", ["ϵ"] = "epsilon", ["ζ"] = "zeta",
            ["η"] = "eta", ["θ"] = "theta", ["ϑ"] = "vartheta", ["ι"] = "iota", ["κ"] = "kappa", ["ϰ"] = "varkappa", ["λ"] = "lambda", ["μ"] = "mu",
            ["ν"] = "nu", ["ξ"] = "xi", ["π"] = "pi", ["ϖ"] = "varpi", ["ρ"] = "rho", ["ϱ"] = "varrho", ["σ"] = "sigma", ["ς"] = "varsigma",
            ["τ"] = "tau", ["υ"] = "upsilon", ["φ"] = "varphi", ["ϕ"] = "phi", ["χ"] = "chi", ["ψ"] = "psi", ["ω"] = "omega",
            ["Γ"] = "Gamma", ["Δ"] = "Delta", ["Θ"] = "Theta", ["Λ"] = "Lambda", ["Ξ"] = "Xi", ["Π"] = "Pi", ["Σ"] = "Sigma",
            ["Υ"] = "Upsilon", ["Φ"] = "Phi", ["Ψ"] = "Psi", ["Ω"] = "Omega", ["ϴ"] = "Theta",
            // letterlike and miscellaneous
            ["∞"] = "infty", ["∂"] = "partial", ["∇"] = "nabla", ["ℏ"] = "hbar", ["ℓ"] = "ell", ["℘"] = "wp", ["ℜ"] = "Re", ["ℑ"] = "Im",
            ["ℵ"] = "aleph", ["∅"] = "emptyset", ["∀"] = "forall", ["∃"] = "exists", ["∄"] = "nexists", ["¬"] = "neg",
            ["…"] = "ldots", ["⋯"] = "cdots", ["⋮"] = "vdots", ["⋱"] = "ddots", ["°"] = "degree", ["†"] = "dagger", ["‡"] = "ddagger",
            ["∠"] = "angle", ["△"] = "triangle", ["□"] = "square", ["◊"] = "lozenge", ["⊤"] = "top", ["⊥"] = "perp", ["⊺"] = "intercal",
            // large operators
            ["∑"] = "sum", ["∏"] = "prod", ["∐"] = "coprod", ["∫"] = "int", ["∬"] = "iint", ["∭"] = "iiint", ["∮"] = "oint",
            ["⋃"] = "bigcup", ["⋂"] = "bigcap", ["⋁"] = "bigvee", ["⋀"] = "bigwedge", ["⨁"] = "bigoplus", ["⨂"] = "bigotimes", ["⨀"] = "bigodot",
            ["⨄"] = "biguplus", ["⨆"] = "bigsqcup",
            // binary operators
            ["±"] = "pm", ["∓"] = "mp", ["×"] = "times", ["÷"] = "div", ["⋅"] = "cdot", ["∘"] = "circ", ["∙"] = "bullet", ["∗"] = "ast",
            ["⋆"] = "star", ["∪"] = "cup", ["∩"] = "cap", ["⊎"] = "uplus", ["⊔"] = "sqcup", ["⊓"] = "sqcap", ["∨"] = "vee", ["∧"] = "wedge",
            ["∖"] = "setminus", ["⊕"] = "oplus", ["⊖"] = "ominus", ["⊗"] = "otimes", ["⊘"] = "oslash", ["⊙"] = "odot", ["⨯"] = "times",
            ["≀"] = "wr", ["⋄"] = "diamond",
            // relations
            ["≤"] = "le", ["≥"] = "ge", ["≠"] = "ne", ["≡"] = "equiv", ["≈"] = "approx", ["≅"] = "cong", ["∼"] = "sim", ["≃"] = "simeq",
            ["∝"] = "propto", ["≪"] = "ll", ["≫"] = "gg", ["≺"] = "prec", ["≻"] = "succ", ["⪯"] = "preceq", ["⪰"] = "succeq",
            ["⊂"] = "subset", ["⊃"] = "supset", ["⊆"] = "subseteq", ["⊇"] = "supseteq", ["⊊"] = "subsetneq", ["⊋"] = "supsetneq",
            ["⊄"] = "not\\subset", ["⊈"] = "nsubseteq", ["⊑"] = "sqsubseteq", ["⊒"] = "sqsupseteq", ["∈"] = "in", ["∉"] = "notin",
            ["∋"] = "ni", ["∣"] = "mid", ["∤"] = "nmid", ["∥"] = "parallel", ["∦"] = "nparallel", ["⊢"] = "vdash", ["⊣"] = "dashv",
            ["⊨"] = "models", ["≍"] = "asymp", ["≐"] = "doteq", ["≔"] = "coloneqq", ["≮"] = "nless", ["≯"] = "ngtr", ["≰"] = "nleq",
            ["≱"] = "ngeq", ["≢"] = "not\\equiv", ["≁"] = "nsim", ["≉"] = "not\\approx", ["≇"] = "ncong", ["⩽"] = "leqslant", ["⩾"] = "geqslant",
            ["≲"] = "lesssim", ["≳"] = "gtrsim", ["∴"] = "therefore", ["∵"] = "because", ["∷"] = "dblcolon",
            // arrows
            ["←"] = "leftarrow", ["→"] = "to", ["↑"] = "uparrow", ["↓"] = "downarrow", ["↔"] = "leftrightarrow", ["↕"] = "updownarrow",
            ["⇐"] = "Leftarrow", ["⇒"] = "Rightarrow", ["⇑"] = "Uparrow", ["⇓"] = "Downarrow", ["⇔"] = "Leftrightarrow", ["↦"] = "mapsto",
            ["⟵"] = "longleftarrow", ["⟶"] = "longrightarrow", ["⟷"] = "longleftrightarrow", ["⟸"] = "Longleftarrow", ["⟹"] = "Longrightarrow",
            ["⟺"] = "Longleftrightarrow", ["⟼"] = "longmapsto", ["↗"] = "nearrow", ["↘"] = "searrow", ["↙"] = "swarrow", ["↖"] = "nwarrow",
            ["↩"] = "hookleftarrow", ["↪"] = "hookrightarrow", ["⇌"] = "rightleftharpoons", ["↼"] = "leftharpoonup", ["⇀"] = "rightharpoonup",
            // delimiters
            ["⟨"] = "langle", ["⟩"] = "rangle", ["⌈"] = "lceil", ["⌉"] = "rceil", ["⌊"] = "lfloor", ["⌋"] = "rfloor", ["‖"] = "|",
            ["{"] = "{", ["}"] = "}", ["〈"] = "langle", ["〉"] = "rangle",
        };

        private static readonly Dictionary<string, string> FontCommands = new Dictionary<string, string>
        {
            ["mup"] = "mathrm", ["mbf"] = "mathbf", ["mbfit"] = "boldsymbol", ["mscr"] = "mathcal", ["mbfscr"] = "mathcal", ["mfrak"] = "mathfrak",
            ["Bbb"] = "mathbb", ["mbffrak"] = "mathfrak", ["msans"] = "mathsf", ["mbfsans"] = "mathsf", ["mitsans"] = "mathsf",
            ["mbfitsans"] = "mathsf", ["mtt"] = "mathtt", ["mit"] = "",
        };

        // math alphanumeric character → (style, plain character), from the table of UnicodeMathText
        private static readonly Dictionary<string, (string Style, string Plain)> Alphanumerics = BuildAlphanumerics();

        private static Dictionary<string, (string Style, string Plain)> BuildAlphanumerics()
        {
            // the Greek letters that have math alphanumerics: Α-Ω, α-ω and the variants of the Mathematical
            // Alphanumeric Symbols block
            var plain = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10))
                .Concat(Enumerable.Range(0x391, 0x3A9 - 0x391 + 1).Where(c => c != 0x3A2)).Concat(Enumerable.Range(0x3B1, 0x3C9 - 0x3B1 + 1))
                .Concat(new[] { 0x3F4, 0x3F5, 0x3D1, 0x3F0, 0x3D5, 0x3F1, 0x3D6, '∂', '∇' })
                .Select(c => char.ConvertFromUtf32(c)).ToList();
            var map = new Dictionary<string, (string, string)>();
            foreach (var style in FontCommands.Keys)
            {
                foreach (var ch in plain)
                {
                    var styled = GetMathAlphanumeric(ch, style);
                    if (styled != ch && !map.ContainsKey(styled))
                        map[styled] = (style, ch);
                }
            }
            map["ℎ"] = ("mit", "h");
            return map;
        }

        /// <summary>The style (mbf, mit, Bbb...) and the plain character of a mathematical alphanumeric (𝐲: mbf, y).</summary>
        public static bool TryGetPlain(string ch, out string style, out string plain)
        {
            var found = Alphanumerics.TryGetValue(ch, out var styled);
            (style, plain) = found ? styled : (null, null);
            return found;
        }

        /// <summary>The LaTeX of a run of text in math: each character as a letter, a command or itself.</summary>
        public static string Math(string text)
        {
            var sb = new StringBuilder();
            foreach (var codePoint in CodePoints.Of(text))
                sb.Append(Character(codePoint.ToString()));
            return sb.ToString();
        }

        private static string Character(string ch)
        {
            switch (ch)
            {
                case "ⅆ":
                    return "\\mathrm{d}";
                case "ⅇ":
                    return "\\mathrm{e}";
                case "ⅈ":
                    return "\\mathrm{i}";
                case "ⅉ":
                    return "\\mathrm{j}";
                case "ⅅ":
                    return "\\mathrm{D}";
                case "\u2061":
                case "\u2062":
                case "\u2063":
                case "\u200B":
                case "\uFE00":
                case "\uFE01":
                    return "";
                case "−":
                    return "-";
                case "⬚":
                    return "{}";
                case "\u2009":
                case "\u200A":
                    return "\\,";
                case "\u205F":
                    return "\\:";
                case "\u2005":
                case "\u2004":
                    return "\\;";
                case "\u2003":
                    return "\\quad ";
                case "\u2002":
                    return "\\enspace ";
                case "\u00A0":
                    return "~";
                case "·":
                    return "\\cdot ";
                case "│":
                    // the separator of a set or a bra-ket, which the reader takes back from a bar between braces or angles
                    return "|";
                case "′":
                    return "'";
                case "″":
                    return "''";
                case "‴":
                    return "'''";
                case "⁗":
                    return "''''";
                case "#":
                case "$":
                case "%":
                case "&":
                case "_":
                    return "\\" + ch;
            }
            if (Alphanumerics.TryGetValue(ch, out var styled))
            {
                var letter = Commands.TryGetValue(styled.Plain, out var greek) ? "\\" + greek + " " : styled.Plain;
                var font = FontCommands[styled.Style];
                return font.Length == 0 ? letter : "\\" + font + "{" + letter.TrimEnd() + "}";
            }
            if (Commands.TryGetValue(ch, out var command))
                return "\\" + command + (EndsWithLetter().IsMatch(command) ? " " : "");
            return ch;
        }

        /// <summary>Upright text in math: \mathrm{x} for a letter, \text{...} for words.</summary>
        public static string Upright(string text)
        {
            // an upright Greek letter is text: \alpha is italic, and \mathrm does not change it; the capitals of LaTeX,
            // \Delta, are upright
            if (Js.Len(text) == 1 && Commands.ContainsKey(text))
                return char.IsLetter(text, 0) && !(text[0] >= 'Α' && text[0] <= 'Ω') ? Text(text) : Math(text);
            return Js.Len(text) == 1 ? "\\mathrm{" + Math(text) + "}" : Text(text);
        }

        /// <summary>\text{...}, with the characters that are special in LaTeX escaped.</summary>
        public static string Text(string text)
        {
            var escaped = TextSpecialCharacters().Replace(text, m => m.Value == "\\" ? "\\textbackslash{}" : m.Value == "^" || m.Value == "~" ? "\\" + m.Value + "{}" : "\\" + m.Value);
            return "\\text{" + escaped.Replace("\u00A0", " ") + "}";
        }

        /// <summary>The spaces that a command left before a character that cannot continue its name are removed.</summary>
        public static string Tidy(string latex)
        {
            latex = SpaceAfterCommand().Replace(latex, "$1");
            return latex.Trim();
        }
    }
}
