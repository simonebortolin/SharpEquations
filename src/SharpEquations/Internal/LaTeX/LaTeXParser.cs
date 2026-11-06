namespace SharpEquations.Internal.LaTeX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Xml.Linq;
    using SharpEquations.Internal.UnicodeMath;

    /// <summary>
    /// Reads LaTeX math into MathML through UnicodeMath, as Word's LaTeX math mode does: <see cref="LaTeXReader"/>
    /// rewrites it as UnicodeMath, which <see cref="UnicodeMathTranslator"/> converts.
    /// </summary>
    internal static class LaTeXParser
    {
        /// <param name="macros">The known macros, to which those the LaTeX defines are added; null for none.</param>
        public static XElement Parse(string latex, bool display = false, IDictionary<string, string> macros = null, bool strict = false,
            CancellationToken cancellationToken = default)
        {
            return UnicodeMathParser.FromTranslator(UnicodeMathTranslator.FromLaTeX(latex, display, true, macros, strict, cancellationToken), latex);
        }

        /// <summary>
        /// The macros given as LaTeX (by name without the backslash) in the form <see cref="Parse"/> takes, as if the
        /// LaTeX defined them with \def; null for none.
        /// </summary>
        public static Dictionary<string, string> Macros(IEnumerable<KeyValuePair<string, string>> latexMacros)
        {
            if (latexMacros is null)
                return null;
            var macros = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var macro in latexMacros)
            {
                if (string.IsNullOrEmpty(macro.Key) || !macro.Key.All(c => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z'))
                    throw new ArgumentException("A LaTeX macro name is made of ASCII letters: " + macro.Key + ".", nameof(latexMacros));
                LaTeXReader.ToUnicodeMath("\\def\\" + macro.Key + "{" + macro.Value + "}", macros);
            }
            return macros;
        }
    }
}
