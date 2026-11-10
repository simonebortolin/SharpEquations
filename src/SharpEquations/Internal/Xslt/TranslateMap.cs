#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>The map of an XPath <c>translate()</c>, by code points: the characters replaced and those removed.</summary>
    internal sealed class TranslateMap
    {
        private const int Keep = -1;
        private const int Remove = -2;

        private readonly string from;
        private readonly string to;
        private readonly int[] ascii = new int[128];
        private readonly Dictionary<int, int>? others;

        public TranslateMap(string from, string to)
        {
            this.from = from;
            this.to = to;
            for (var i = 0; i < ascii.Length; i++)
                ascii[i] = Keep;
            var target = new List<int>();
            foreach (var rune in to.EnumerateRunes())
                target.Add(rune.Value);
            var index = 0;
            foreach (var rune in from.EnumerateRunes())
            {
                var replacement = index < target.Count ? target[index] : Remove;
                index++;
                // the first occurrence of a character in the map is the one that counts
                if (rune.Value < ascii.Length)
                {
                    if (ascii[rune.Value] == Keep)
                        ascii[rune.Value] = replacement;
                }
                else
                {
                    others ??= new Dictionary<int, int>();
                    if (!others.TryGetValue(rune.Value, out _))
                        others[rune.Value] = replacement;
                }
            }
        }

        public bool Maps(string from, string to) => this.from == from && this.to == to;

        private int Find(int codePoint)
        {
            if (codePoint < ascii.Length)
                return ascii[codePoint];
            return others != null && others.TryGetValue(codePoint, out var replacement) ? replacement : Keep;
        }

        /// <summary>The text translated: the text itself when nothing in it is replaced or removed.</summary>
        public string Apply(string s)
        {
            StringBuilder? result = null;
            var index = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                var replacement = Find(rune.Value);
                // a lone surrogate, which reads as U+FFFD, is written as U+FFFD
                if (replacement == Keep && rune.Value == 0xFFFD && s[index] != '�')
                    replacement = 0xFFFD;
                if (replacement != Keep && result == null)
                    result = new StringBuilder(s, 0, index, s.Length);
                if (result != null)
                {
                    switch (replacement)
                    {
                        case Keep when rune.IsBmp:
                            result.Append((char)rune.Value);
                            break;
                        case Keep:
                            result.Append(rune.ToString());
                            break;
                        case Remove:
                            break;
                        case <= 0xFFFF:
                            result.Append((char)replacement);
                            break;
                        default:
                            result.Append(char.ConvertFromUtf32(replacement));
                            break;
                    }
                }
                index += rune.IsBmp ? 1 : 2;
            }
            return result == null ? s : result.ToString();
        }
    }
}
