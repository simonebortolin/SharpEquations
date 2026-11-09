namespace SharpEquations.Internal.Text
{
    using System.Text.RegularExpressions;

    /// <summary>The ASCII letters and digits of control words, operator names and numbers.</summary>
    internal static partial class AsciiPatterns
    {
        /// <summary>A letter anywhere in the text.</summary>
        [GeneratedRegex("[A-Za-z]")]
        public static partial Regex Letter();

        /// <summary>Letters only, at least one (\z: a final line feed is not a letter, as $ would take it).</summary>
        [GeneratedRegex(@"^[A-Za-z]+\z")]
        public static partial Regex Letters();

        /// <summary>A name: a letter and letters or digits after it (sharet1).</summary>
        [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*\z")]
        public static partial Regex Name();

        /// <summary>Digits only, or nothing.</summary>
        [GeneratedRegex(@"^[0-9]*\z")]
        public static partial Regex Digits();
    }
}
