#nullable enable

namespace SharpEquations
{
    using System.Collections.Generic;

    /// <summary>
    /// How <see cref="Equation"/> and <see cref="EquationConverter"/> read and write equations. Each option applies to
    /// the formats it names; <see cref="Default"/> suits most uses.
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new EquationOptions { Display = true, LaTeXFlavor = LaTeXFlavor.Word };
    /// </code>
    /// </example>
    public sealed record EquationOptions
    {
        /// <summary>The default options: inline, lenient, without formatting, standard LaTeX, no macros.</summary>
        public static EquationOptions Default { get; } = new EquationOptions();

        /// <summary>
        /// Whether an equation read from UnicodeMath or LaTeX without delimiters is a display (block) equation rather
        /// than an inline one. LaTeX delimiters decide by themselves: <c>$...$</c> and <c>\(...\)</c> are inline,
        /// <c>$$...$$</c> and <c>\[...\]</c> display. MathML and OMML carry this in the equation itself.
        /// </summary>
        public bool Display { get; init; }

        /// <summary>
        /// Whether what the reader does not know is an error (a <see cref="System.FormatException"/>) instead of being
        /// kept as text or dropped: a LaTeX or UnicodeMath control word that is not defined, an element that is not
        /// presentation MathML, an OMML element that is not in the schema.
        /// </summary>
        public bool Strict { get; init; }

        /// <summary>
        /// Whether the formatting of the source (the MathML attributes <c>mathcolor</c>, <c>mathbackground</c>,
        /// <c>mathsize</c>, <c>style</c> and the deprecated font attributes) is kept. By default it is dropped, so that
        /// the same mathematics reads to the same <see cref="Equation"/>.
        /// </summary>
        public bool PreserveFormatting { get; init; }

        /// <summary>The LaTeX written when the target format is <see cref="EquationFormat.LaTeX"/>.</summary>
        public LaTeXFlavor LaTeXFlavor { get; init; }

        /// <summary>
        /// LaTeX macros known when LaTeX is read, by name without the backslash, such as <c>["R"] = @"\mathbb{R}"</c>.
        /// The macros the LaTeX defines itself with \def or \newcommand hold for that LaTeX only. Null for none.
        /// </summary>
        public IReadOnlyDictionary<string, string>? LaTeXMacros { get; init; }
    }
}
