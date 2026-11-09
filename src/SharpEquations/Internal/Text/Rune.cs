#if !NETCOREAPP3_0_OR_GREATER
namespace System.Text
{
    using System.Collections.Generic;

    /// <summary>
    /// The System.Text.Rune of .NET Core 3.0 and later, which .NET Standard 2.0 lacks: a Unicode scalar value, with the
    /// members of Rune that SharpEquations uses and their behavior. It is internal, so it meets no Rune of the
    /// consumer's framework.
    /// </summary>
    internal readonly struct Rune : IEquatable<Rune>
    {
        private readonly int value;

        /// <exception cref="ArgumentOutOfRangeException">The value is a surrogate or beyond U+10FFFF.</exception>
        public Rune(int value)
        {
            if ((uint)value > 0x10FFFF || value >= 0xD800 && value <= 0xDFFF)
                throw new ArgumentOutOfRangeException(nameof(value));
            this.value = value;
        }

        public static Rune ReplacementChar => new Rune(0xFFFD);

        public int Value => value;

        public bool IsBmp => value <= 0xFFFF;

        public static bool IsLetter(Rune value) => char.IsLetter(value.ToString(), 0);

        public override string ToString() => char.ConvertFromUtf32(value);

        public bool Equals(Rune other) => value == other.value;

        public override bool Equals(object obj) => obj is Rune other && Equals(other);

        public override int GetHashCode() => value;

        public static bool operator ==(Rune left, Rune right) => left.value == right.value;

        public static bool operator !=(Rune left, Rune right) => left.value != right.value;
    }

    internal static class RuneExtensions
    {
        /// <summary>The runes of the text, as string.EnumerateRunes: a lone surrogate is <see cref="Rune.ReplacementChar"/>.</summary>
        public static IEnumerable<Rune> EnumerateRunes(this string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    yield return new Rune(char.ConvertToUtf32(s[i], s[++i]));
                else
                    yield return char.IsSurrogate(s[i]) ? Rune.ReplacementChar : new Rune(s[i]);
            }
        }
    }
}
#endif
