namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SharpEquations.Editing.Text;

    /// <summary>
    /// The math autocorrect of the editor, as Word's: sequences replaced by their symbols as they are typed (<c>-&gt;</c>
    /// by →, <c>!=</c> by ≠), the commands of <see cref="EquationCommands"/> followed by a space (<c>\alpha</c> by α),
    /// and the shortcuts an application adds (<c>\R</c> by ℝ).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sequence is replaced when the character typed after it does not continue a longer one: <c>&lt;-</c> waits for a
    /// <c>&gt;</c> that makes it <c>&lt;-&gt;</c>, ↔, and an operator that starts a sequence (<c>+</c> of <c>+-</c>) waits
    /// for the next character before it builds up. A sequence that ends with a letter (a shortcut <c>\R</c>) is replaced
    /// only when a character that is not a letter follows, so that <c>\Re</c> stays a command. The space that replaces a
    /// command or a shortcut is not kept. Text between quotes is left as typed.
    /// </para>
    /// <para>
    /// In UnicodeMath every entry applies. In LaTeX, where <c>&lt;=</c> is a less-than sign and an equals sign, only the
    /// entries that start with a backslash do: the shortcuts of the application.
    /// </para>
    /// </remarks>
    public sealed class MathAutoCorrect
    {
        private static readonly KeyValuePair<string, string>[] DefaultEntries =
        {
            new KeyValuePair<string, string>("->", "→"),
            new KeyValuePair<string, string>("<-", "←"),
            new KeyValuePair<string, string>("<->", "↔"),
            new KeyValuePair<string, string>("=>", "⇒"),
            new KeyValuePair<string, string>("<=>", "⇔"),
            new KeyValuePair<string, string>("!=", "≠"),
            new KeyValuePair<string, string>("/=", "≠"),
            new KeyValuePair<string, string>("<=", "≤"),
            new KeyValuePair<string, string>(">=", "≥"),
            new KeyValuePair<string, string>("+-", "±"),
            new KeyValuePair<string, string>("-+", "∓"),
            new KeyValuePair<string, string>("<<", "≪"),
            new KeyValuePair<string, string>(">>", "≫"),
            new KeyValuePair<string, string>("...", "…"),
            new KeyValuePair<string, string>("~=", "≅"),
            new KeyValuePair<string, string>("~~", "≈"),
            new KeyValuePair<string, string>(":=", "≔"),
            new KeyValuePair<string, string>("::", "∷"),
            new KeyValuePair<string, string>("!!", "‼"),
            new KeyValuePair<string, string>("/<", "≮"),
            new KeyValuePair<string, string>("/>", "≯"),
        };

        private static readonly Lazy<Dictionary<string, string>> UnicodeMathSymbols = new Lazy<Dictionary<string, string>>(() => Symbols(EquationFormat.UnicodeMath));

        private static readonly Lazy<Dictionary<string, string>> LaTeXSymbols = new Lazy<Dictionary<string, string>>(() => Symbols(EquationFormat.LaTeX));

        /// <summary>An autocorrect with the entries of Word, or without any.</summary>
        public MathAutoCorrect(bool withDefaults = true)
        {
            if (withDefaults)
            {
                foreach (var entry in DefaultEntries)
                    Entries[entry.Key] = entry.Value;
            }
        }

        /// <summary>The entries of Word's math autocorrect that an autocorrect has by default, sequence → replacement.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Defaults => DefaultEntries;

        /// <summary>The entries, sequence → replacement; an application adds, changes and removes them.</summary>
        public IDictionary<string, string> Entries { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Whether a command of <see cref="EquationCommands"/> that stands for a symbol becomes the symbol when a space
        /// follows it (<c>\alpha</c> becomes α, <c>\in</c> ∈), as in Word; true by default.
        /// </summary>
        public bool ReplaceCommands { get; set; } = true;

        /// <summary>
        /// The longest entry that the text ends with and that may be replaced, for the input format; null without one.
        /// With the character typed after the text, an entry it does not continue (<c>&lt;-</c> before <c>b</c>, not before
        /// <c>&gt;</c>); without one (<paramref name="next"/> empty), an entry that nothing could continue (<c>!=</c>, not
        /// <c>&lt;-</c>, nor a shortcut that ends with a letter); at the end of what is typed (<paramref name="final"/>),
        /// any entry.
        /// </summary>
        internal KeyValuePair<string, string>? Match(string before, string next, EquationFormat format, bool final = false)
        {
            if (before.Length == 0 || IsQuoted(before))
                return null;

            KeyValuePair<string, string>? best = null;
            foreach (var entry in Applicable(format))
            {
                var key = entry.Key;
                if (!before.EndsWith(key, StringComparison.Ordinal) || StartsInWord(before, key))
                    continue;
                var endsWithLetter = IsLetter(Last(key));
                if (next.Length > 0)
                {
                    if (endsWithLetter && IsLetter(next.EnumerateRunes().First()) || Continues(key + next, format))
                        continue;
                }
                else if (!final && (endsWithLetter || Extends(key, format)))
                {
                    continue;
                }
                if (best == null || key.Length > best.Value.Key.Length)
                    best = entry;
            }
            return best;
        }

        /// <summary>
        /// The command that the text ends with (<c>\alpha</c>), with the symbol it stands for, when a space is typed after
        /// it; null without one.
        /// </summary>
        internal KeyValuePair<string, string>? MatchCommand(string before, EquationFormat format)
        {
            if (!ReplaceCommands || IsQuoted(before))
                return null;
            var symbols = format switch
            {
                EquationFormat.UnicodeMath => UnicodeMathSymbols.Value,
                EquationFormat.LaTeX => LaTeXSymbols.Value,
                _ => null,
            };
            var i = before.Length;
            while (i > 0 && before[i - 1] < 0x80 && char.IsLetterOrDigit(before[i - 1]))
                i--;
            if (symbols == null || i == before.Length || i == 0 || before[i - 1] != '\\' || i >= 2 && before[i - 2] == '\\')
                return null;
            var name = before.Substring(i);
            return symbols.TryGetValue(name, out var symbol) ? new KeyValuePair<string, string>("\\" + name, symbol) : null;
        }

        /// <summary>
        /// The length, in code points, of the end of the text that starts an entry or is an entry a longer one could
        /// continue (the <c>+</c> of <c>+-</c>, the <c>&lt;-</c> of <c>&lt;-&gt;</c>): the sequence is not finished, and it
        /// waits to build up; 0 without one.
        /// </summary>
        internal int Pending(string text, EquationFormat format)
        {
            if (text.Length == 0 || IsQuoted(text))
                return 0;
            var pending = 0;
            foreach (var entry in Applicable(format))
            {
                var runes = CodePointText.Runes(entry.Key);
                for (var length = Math.Min(runes.Count, CodePointText.Length(text)); length > pending; length--)
                {
                    if (length == runes.Count && !Extends(entry.Key, format))
                        continue;
                    if (text.EndsWith(CodePointText.Join(runes.Take(length)), StringComparison.Ordinal))
                    {
                        pending = length;
                        break;
                    }
                }
            }
            return pending;
        }

        /// <summary>The commands of a format that stand for a symbol that is not ASCII, by name.</summary>
        private static Dictionary<string, string> Symbols(EquationFormat format)
        {
            var symbols = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var command in EquationCommands.Get(format))
            {
                if (command.Arguments == 0 && command.Symbol.Length > 0 && command.Symbol.EnumerateRunes().All(r => r.Value >= 0x80))
                    symbols[command.Name] = command.Symbol;
            }
            return symbols;
        }

        /// <summary>The entries that apply to an input format: all in UnicodeMath, the backslash shortcuts in LaTeX.</summary>
        private IEnumerable<KeyValuePair<string, string>> Applicable(EquationFormat format) => format switch
        {
            EquationFormat.UnicodeMath => Entries.Where(e => e.Key.Length > 0),
            EquationFormat.LaTeX => Entries.Where(e => e.Key.Length > 0 && e.Key[0] == '\\'),
            _ => Enumerable.Empty<KeyValuePair<string, string>>(),
        };

        /// <summary>Whether the text is inside quotes (UnicodeMath text), which is left as typed.</summary>
        private static bool IsQuoted(string text) => text.Count(c => c == '"') % 2 == 1;

        /// <summary>Whether an entry that starts with a letter begins inside a word of the text: <c>pi</c> in <c>api</c>.</summary>
        private static bool StartsInWord(string text, string key)
        {
            if (!IsLetter(key.EnumerateRunes().First()) || text.Length == key.Length)
                return false;
            return IsLetter(Last(text.Substring(0, text.Length - key.Length)));
        }

        private static Rune Last(string text) => text.EnumerateRunes().Last();

        private static bool IsLetter(Rune rune) => Rune.IsLetter(rune);

        /// <summary>Whether an entry starts with the text (or is the text): the sequence typed so far is not finished.</summary>
        private bool Continues(string text, EquationFormat format) =>
            Applicable(format).Any(e => e.Key.Length >= text.Length && e.Key.StartsWith(text, StringComparison.Ordinal));

        /// <summary>Whether a longer entry starts with the entry.</summary>
        private bool Extends(string key, EquationFormat format) =>
            Applicable(format).Any(e => e.Key.Length > key.Length && e.Key.StartsWith(key, StringComparison.Ordinal));
    }
}
