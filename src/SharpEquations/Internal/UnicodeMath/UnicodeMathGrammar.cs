namespace SharpEquations.Internal.UnicodeMath
{
    /// <summary>
    /// The UnicodeMath parser: the grammar of UnicodeMathML (src/unicodemathml-parser.pegjs), written with Parlot
    /// combinators in UnicodeMathGrammar.parlot.cs, which Parlot.SourceGenerator compiles at build time into this
    /// class (no Parlot assembly at run time).
    /// </summary>
    internal static partial class UnicodeMathGrammar
    {
        /// <summary>
        /// Parses UnicodeMath (with control words resolved and math alphanumerics mapped to the private use area)
        /// into the syntax tree of UnicodeMathML; false if it does not match the grammar.
        /// </summary>
        public static partial bool TryParse(string input, System.Threading.CancellationToken cancellationToken, out object value);

        public static bool TryParse(string input, out object value) => TryParse(input, System.Threading.CancellationToken.None, out value);
    }
}
