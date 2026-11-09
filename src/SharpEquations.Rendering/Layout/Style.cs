namespace SharpEquations.Rendering.Layout
{
    using System;
    using System.Globalization;

    /// <summary>The style an element is laid out in: its size, script level, display style and whether it is cramped.</summary>
    internal readonly struct Style
    {
        public Style(double size, int level, bool display, bool cramped, string? color)
        {
            Size = size;
            Level = level;
            Display = display;
            Cramped = cramped;
            Color = color;
        }

        /// <summary>The size of the em, in the units of the layout.</summary>
        public double Size { get; }

        public int Level { get; }

        public bool Display { get; }

        public bool Cramped { get; }

        public string? Color { get; }

        public Style WithLevel(int level, double scriptPercent, double scriptScriptPercent) =>
            new Style(Size * Scale(level, scriptPercent, scriptScriptPercent) / Scale(Level, scriptPercent, scriptScriptPercent), level, Display, Cramped, Color);

        public Style WithDisplay(bool display) => new Style(Size, Level, display, Cramped, Color);

        public Style WithCramped(bool cramped) => new Style(Size, Level, Display, cramped, Color);

        public Style WithColor(string? color) => new Style(Size, Level, Display, Cramped, color);

        public Style WithSize(double size) => new Style(size, Level, Display, Cramped, Color);

        private static double Scale(int level, double script, double scriptScript)
        {
            if (level <= 0)
                return Math.Pow(1 / 0.71, -level);
            if (level == 1)
                return script;
            return scriptScript * Math.Pow(0.71, level - 2);
        }
    }

    /// <summary>The lengths of MathML: numbers with units, percentages and the named spaces.</summary>
    internal static class Lengths
    {
        /// <summary>
        /// Parses a length; a percentage, or a number without unit, is of <paramref name="reference"/>. Returns false
        /// if the value is not a length.
        /// </summary>
        public static bool TryParse(string? value, double em, double ex, double reference, out double length)
        {
            length = 0;
            if (value == null)
                return false;
            var v = value.Trim();
            var named = NamedSpace(v);
            if (named != null)
            {
                length = named.Value * em / 18;
                return true;
            }

            var i = 0;
            while (i < v.Length && (char.IsDigit(v[i]) || v[i] is '.' or '-' or '+'))
                i++;
            if (!double.TryParse(v.Substring(0, i), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return false;
            var unit = v.Substring(i).Trim();
            switch (unit)
            {
                case "":
                    length = number * reference;
                    return true;
                case "%":
                    length = number / 100 * reference;
                    return true;
                case "em":
                    length = number * em;
                    return true;
                case "ex":
                    length = number * ex;
                    return true;
                case "px":
                    length = number;
                    return true;
                case "pt":
                    length = number * 96 / 72;
                    return true;
                case "pc":
                    length = number * 16;
                    return true;
                case "in":
                    length = number * 96;
                    return true;
                case "cm":
                    length = number * 96 / 2.54;
                    return true;
                case "mm":
                    length = number * 9.6 / 2.54;
                    return true;
                case "mu":
                    length = number * em / 18;
                    return true;
                default:
                    return false;
            }
        }

        private static int? NamedSpace(string v) => v switch
        {
            "veryverythinmathspace" => 1,
            "verythinmathspace" => 2,
            "thinmathspace" => 3,
            "mediummathspace" => 4,
            "thickmathspace" => 5,
            "verythickmathspace" => 6,
            "veryverythickmathspace" => 7,
            "negativeveryverythinmathspace" => -1,
            "negativeverythinmathspace" => -2,
            "negativethinmathspace" => -3,
            "negativemediummathspace" => -4,
            "negativethickmathspace" => -5,
            "negativeverythickmathspace" => -6,
            "negativeveryverythickmathspace" => -7,
            _ => null,
        };
    }
}
