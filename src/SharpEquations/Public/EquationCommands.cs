#nullable enable

namespace SharpEquations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpEquations.Internal.LaTeX;
    using SharpEquations.Internal.Text;
    using SharpEquations.Internal.UnicodeMath;

    /// <summary>
    /// A command of UnicodeMath (a control word, such as <c>\alpha</c>, which Word's math autocorrect replaces) or of
    /// LaTeX (<c>\alpha</c>, <c>\frac</c>), as an editor offers it to complete what is typed.
    /// </summary>
    public sealed class EquationCommand
    {
        internal EquationCommand(string name, string symbol, int arguments)
        {
            Name = name;
            Symbol = symbol;
            Arguments = arguments;
        }

        /// <summary>The name of the command, without the backslash.</summary>
        public string Name { get; }

        /// <summary>
        /// The character the command stands for (<c>α</c>, <c>∑</c>, the <c>√</c> of <c>\sqrt</c> in UnicodeMath), or empty
        /// for a command that has none (a structure or a style of LaTeX, <c>\frac</c>, <c>\mathbf</c>).
        /// </summary>
        public string Symbol { get; }

        /// <summary>The braced arguments the command takes in LaTeX (2 for <c>\frac</c>); 0 in UnicodeMath.</summary>
        public int Arguments { get; }

        /// <summary>The text of the command as it is typed: the backslash, the name and its empty arguments (<c>\frac{}{}</c>).</summary>
        public string Text => "\\" + Name + string.Concat(Enumerable.Repeat("{}", Arguments));

        /// <inheritdoc/>
        public override string ToString() => Symbol.Length > 0 ? Text + " " + Symbol : Text;
    }

    /// <summary>
    /// The commands an equation is typed with: the control words of UnicodeMath, and the commands of LaTeX that
    /// <see cref="Equation.Parse(string, EquationFormat, EquationOptions?, System.Threading.CancellationToken)"/> reads.
    /// </summary>
    public static class EquationCommands
    {
        // the commands of LaTeX with arguments, and the styles, that the LaTeX reader reads
        private static readonly (string Name, int Arguments)[] LaTeXStructures =
        {
            ("frac", 2), ("dfrac", 2), ("tfrac", 2), ("cfrac", 2), ("binom", 2), ("dbinom", 2), ("tbinom", 2), ("sqrt", 1),
            ("overset", 2), ("underset", 2), ("stackrel", 2), ("hat", 1), ("widehat", 1), ("check", 1), ("tilde", 1),
            ("widetilde", 1), ("acute", 1), ("grave", 1), ("dot", 1), ("ddot", 1), ("breve", 1), ("bar", 1), ("vec", 1),
            ("overline", 1), ("underline", 1), ("overbrace", 1), ("underbrace", 1), ("overrightarrow", 1), ("overleftarrow", 1),
            ("mathring", 1), ("boxed", 1), ("text", 1), ("textrm", 1), ("textbf", 1), ("textit", 1), ("mathrm", 1), ("mathbf", 1),
            ("mathit", 1), ("mathcal", 1), ("mathbb", 1), ("mathfrak", 1), ("mathsf", 1), ("mathtt", 1),
            ("boldsymbol", 1), ("operatorname", 1), ("tag", 1), ("phantom", 1), ("left", 0), ("right", 0), ("middle", 0),
            ("lim", 0), ("max", 0), ("min", 0), ("sup", 0), ("inf", 0), ("det", 0), ("gcd", 0),
            ("sin", 0), ("cos", 0), ("tan", 0), ("cot", 0), ("sec", 0), ("csc", 0), ("arcsin", 0), ("arccos", 0), ("arctan", 0),
            ("sinh", 0), ("cosh", 0), ("tanh", 0), ("coth", 0), ("log", 0), ("ln", 0), ("exp", 0), ("arg", 0),
            ("deg", 0), ("dim", 0), ("ker", 0), ("hom", 0), ("Pr", 0), ("quad", 0), ("qquad", 0),
        };

        private static readonly Lazy<IReadOnlyList<EquationCommand>> UnicodeMath = new Lazy<IReadOnlyList<EquationCommand>>(() =>
            ControlWords.Table
                .Where(w => w.Key.All(char.IsLetterOrDigit))
                .Select(w => new EquationCommand(w.Key, w.Value, 0))
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .ToList());

        private static readonly Lazy<IReadOnlyList<EquationCommand>> LaTeX = new Lazy<IReadOnlyList<EquationCommand>>(() =>
        {
            var commands = new Dictionary<string, EquationCommand>(StringComparer.Ordinal);
            // the symbols LaTeX and UnicodeMath name alike, then those of the writer, then the structures
            foreach (var word in ControlWords.Table.Where(w => w.Key.All(char.IsLetter) && IsLaTeXSymbol(w.Value)))
                commands[word.Key] = new EquationCommand(word.Key, word.Value, 0);
            foreach (var symbol in LaTeXSymbols.SymbolCommands.Where(s => s.Value.All(char.IsLetter)))
            {
                if (!commands.ContainsKey(symbol.Value))
                    commands[symbol.Value] = new EquationCommand(symbol.Value, symbol.Key, 0);
            }
            foreach (var (name, arguments) in LaTeXStructures)
                commands[name] = new EquationCommand(name, string.Empty, arguments);
            return commands.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        });

        /// <summary>
        /// A symbol that a control word of UnicodeMath stands for in LaTeX too: not an operator of UnicodeMath alone (the
        /// boxes, enclosed numbers and brackets that build its structures), a combining accent or a lone bracket.
        /// </summary>
        private static bool IsLaTeXSymbol(string symbol)
        {
            if (!CodePoints.IsSingle(symbol, out var rune))
                return false;
            return rune.Value switch
            {
                >= 0x0300 and <= 0x036F or >= 0x20D0 and <= 0x20FF => false,
                >= 0x2460 and <= 0x24FF or >= 0x2500 and <= 0x25FF or >= 0x2600 and <= 0x27BF => false,
                '(' or ')' or '[' or ']' or '{' or '}' or '|' or 0x2016 or 0x2223 or 0x27E8 or 0x27E9 or 0x2308 or 0x2309 or 0x230A or 0x230B
                    or 0x3016 or 0x3017 => false,
                _ => true,
            };
        }

        /// <summary>The commands of a format, by name: UnicodeMath or LaTeX.</summary>
        /// <exception cref="ArgumentException">Another format, which has no commands.</exception>
        public static IReadOnlyList<EquationCommand> Get(EquationFormat format) => format switch
        {
            EquationFormat.UnicodeMath => UnicodeMath.Value,
            EquationFormat.LaTeX => LaTeX.Value,
            _ => throw new ArgumentException("Only UnicodeMath and LaTeX have commands.", nameof(format)),
        };

        /// <summary>
        /// The commands whose name starts with a prefix (without the backslash; ordinal, then case-insensitive), the
        /// exact name first and then the shortest: the completions of <c>\al</c> are <c>\aleph</c>, <c>\alpha</c>...
        /// </summary>
        /// <exception cref="ArgumentException">A format other than UnicodeMath or LaTeX.</exception>
        public static IReadOnlyList<EquationCommand> Complete(string prefix, EquationFormat format, int max = 20)
        {
            if (prefix == null)
                throw new ArgumentNullException(nameof(prefix));
            prefix = prefix.TrimStart('\\');
            return Get(format)
                .Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name == prefix ? 0 : c.Name.StartsWith(prefix, StringComparison.Ordinal) ? 1 : 2)
                .ThenBy(c => c.Name.Length)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .Take(max)
                .ToList();
        }
    }
}
