namespace SharpEquations.Editing.Text
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>Text indexed by code points, never by UTF-16 <see cref="char"/>s: the offsets of the caret in a run.</summary>
    internal static class CodePointText
    {
        public static int Length(string s) => s.EnumerateRunes().Count();

        public static List<Rune> Runes(string s) => s.EnumerateRunes().ToList();

        public static string Join(IEnumerable<Rune> runes)
        {
            var sb = new StringBuilder();
            foreach (var rune in runes)
                sb.Append(rune.ToString());
            return sb.ToString();
        }

        public static string Substring(string s, int start, int length) => Join(s.EnumerateRunes().Skip(start).Take(length));

        public static string Substring(string s, int start) => Join(s.EnumerateRunes().Skip(start));

        public static string Insert(string s, int index, string text)
        {
            var runes = Runes(s);
            return Join(runes.Take(index)) + text + Join(runes.Skip(index));
        }

        public static string Remove(string s, int index, int count)
        {
            var runes = Runes(s);
            return Join(runes.Take(index)) + Join(runes.Skip(index + count));
        }

        public static bool IsSingle(string s, out Rune rune)
        {
            rune = default;
            var count = 0;
            foreach (var r in s.EnumerateRunes())
            {
                if (++count > 1)
                    return false;
                rune = r;
            }
            return count == 1;
        }
    }
}
