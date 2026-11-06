namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using SharpEquations.Internal.LaTeX;

    /// <summary>
    /// UnicodeMath to MathML: a port of unicodemathml() of UnicodeMathML (src/unicodemathml.js), with its default
    /// configuration (default intents, "us-tech" double-struck letters, T for transposes). The steps are those of
    /// UnicodeMathML: resolve control words, parse (<see cref="UnicodeMathGrammar"/>), preprocess, transform to a
    /// MathML syntax tree and print it. Equation numbers give an mlabeledtr.
    /// </summary>
    internal sealed partial class UnicodeMathTranslator
    {
        private const bool EmitDefaultIntents = true;

        // A control word left by ResolveControlWords is one it does not know.

        private bool alignmentMarks;
        private bool FTeX;
        private string selanchor;
        private string selfocus;

        /// <summary>
        /// The MathML text of the UnicodeMath; a FormatException if it does not parse. Equation arrays are aligned
        /// with maligngroup and malignmark elements when alignmentMarks is set (Word reads them), else by splitting
        /// their cells as UnicodeMathML does for browsers, which do not implement them.
        /// </summary>
        public static string ToMathML(string unicodemath, bool display, bool alignmentMarks = false, bool strict = false, CancellationToken cancellationToken = default)
        {
            return ToMathML(unicodemath, display, alignmentMarks, false, strict, cancellationToken);
        }

        /// <summary>
        /// The MathML text of LaTeX math, through UnicodeMath (<see cref="LaTeXReader"/>). The delimiters $...$, \(...\)
        /// make an inline equation, $$...$$ and \[...\] a display one; without them, display decides.
        /// </summary>
        public static string FromLaTeX(string tex, bool display, bool alignmentMarks = false, IDictionary<string, string> macros = null,
            bool strict = false, CancellationToken cancellationToken = default)
        {
            var chars = UnicodeMathText.Characters(tex.Trim());
            int start = 0, end = chars.Count;
            if (chars.Count > 0 && chars[0] == "$")
            {
                start = 1;
                display = false;
                if (chars.Count > 1 && chars[1] == "$")
                {
                    start = 2;
                    display = true;
                }
                if (end > start && chars[end - 1] == "$")
                    end--;
                if (end > start && chars[end - 1] == "$")
                    end--;
            }
            else if (chars.Count > 1 && chars[0] == "\\" && (chars[1] == "[" || chars[1] == "("))
            {
                start = 2;
                display = chars[1] == "[";
                if (end > start + 1 && chars[end - 2] == "\\" && (chars[end - 1] == "]" || chars[end - 1] == ")"))
                    end -= 2;
            }
            var unicodemath = LaTeXReader.ToUnicodeMath(string.Concat(chars.Skip(start).Take(end - start)), macros);
            var umChars = UnicodeMathText.Characters(unicodemath);
            if (umChars.Count > 0 && umChars[0] == "ⓓ" && umChars.Skip(1).FirstOrDefault(c => c != " ") != "{")
            {
                display = true;
                unicodemath = string.Concat(umChars.Skip(1));
            }
            return ToMathML(unicodemath, display, alignmentMarks, true, strict, cancellationToken);
        }

        private static string ToMathML(string unicodemath, bool display, bool alignmentMarks, bool tex, bool strict, CancellationToken cancellationToken)
        {
            unicodemath = unicodemath.Trim();
            var resolved = UnicodeMathInput.ResolveControlWords(unicodemath, strict
                ? unknown => throw new FormatException("Unknown control word " + unknown + (tex ? " in the LaTeX." : "."))
                : null);
            // macros (\def, \newcommand: ⓜ) apply in the equation that defines them
            if (!tex && LaTeXReader.HasMacros(resolved))
                resolved = LaTeXReader.ExpandMacros(resolved);
            var input = UnicodeMathInput.MapToPrivate(UnicodeMathInput.SetBuilderBars(resolved));
            // a closing bracket that LaTeX leaves without its opening one (𝑣⌋𝐵, 𝑎)) is a character, which UnicodeMath escapes
            if (!UnicodeMathGrammar.TryParse(input, cancellationToken, out var uast) &&
                !(tex && UnicodeMathGrammar.TryParse(UnicodeMathInput.MapToPrivate(UnicodeMathInput.SetBuilderBars(UnicodeMathInput.EscapeUnpairedClosers(resolved))), cancellationToken, out uast)))
                throw new FormatException("Not valid UnicodeMath: " + unicodemath);
            uast = UnicodeMathInput.AstMapFromPrivate(uast);

            var translator = new UnicodeMathTranslator { alignmentMarks = alignmentMarks, FTeX = tex };
            var puast = translator.Preprocess(new PreprocessStyle { Display = display }, uast);
            int dsty = display ? 1 : 0;
            if (UnicodeMathText.Characters(unicodemath).Any(c => UnicodeMathText.CodePointAt(c, 0) >= 0x1EE00 && UnicodeMathText.CodePointAt(c, 0) <= 0x1EEFF))
                dsty |= 2;
            var mast = translator.Transform(dsty, puast);
            return translator.Pretty(mast).Replace("<mtd></mtd>", "");
        }
    }
}
