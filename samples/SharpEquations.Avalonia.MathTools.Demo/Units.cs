namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>A unit of a quantity: a value in it is <c>value × Factor + Offset</c> in the base unit of the quantity.</summary>
    public sealed class Unit
    {
        public Unit(string name, string symbol, double factor, double offset = 0)
        {
            Name = name;
            Symbol = symbol;
            Factor = factor;
            Offset = offset;
        }

        public string Name { get; }

        public string Symbol { get; }

        public double Factor { get; }

        public double Offset { get; }

        public override string ToString() => $"{Name} ({Symbol})";
    }

    /// <summary>A quantity and its units, the first of which is the base unit.</summary>
    public sealed class Quantity
    {
        public Quantity(string name, params Unit[] units)
        {
            Name = name;
            Units = units;
        }

        public string Name { get; }

        public IReadOnlyList<Unit> Units { get; }

        public Unit this[string symbol] => Units.Single(u => u.Symbol == symbol);

        public override string ToString() => Name;
    }

    /// <summary>The quantities of the converter, and the conversion and the formatting of the values.</summary>
    public static class Units
    {
        private const double Inch = 0.0254;
        private const double Pound = 0.45359237;
        private const double Gallon = 3.785411784e-3;

        public static IReadOnlyList<Quantity> All { get; } = new[]
        {
            new Quantity("Length",
                new Unit("metre", "m", 1), new Unit("kilometre", "km", 1e3), new Unit("centimetre", "cm", 1e-2), new Unit("millimetre", "mm", 1e-3),
                new Unit("micrometre", "μm", 1e-6), new Unit("nanometre", "nm", 1e-9), new Unit("inch", "in", Inch), new Unit("foot", "ft", 12 * Inch),
                new Unit("yard", "yd", 36 * Inch), new Unit("mile", "mi", 63360 * Inch), new Unit("nautical mile", "nmi", 1852),
                new Unit("astronomical unit", "au", 149597870700), new Unit("light-year", "ly", 9460730472580800)),
            new Quantity("Area",
                new Unit("square metre", "m²", 1), new Unit("square kilometre", "km²", 1e6), new Unit("square centimetre", "cm²", 1e-4),
                new Unit("hectare", "ha", 1e4), new Unit("are", "a", 100), new Unit("square inch", "in²", Inch * Inch),
                new Unit("square foot", "ft²", 144 * Inch * Inch), new Unit("acre", "ac", 4046.8564224), new Unit("square mile", "mi²", 63360 * Inch * 63360 * Inch)),
            new Quantity("Volume",
                new Unit("cubic metre", "m³", 1), new Unit("litre", "L", 1e-3), new Unit("millilitre", "mL", 1e-6), new Unit("cubic centimetre", "cm³", 1e-6),
                new Unit("US gallon", "gal", Gallon), new Unit("US quart", "qt", Gallon / 4), new Unit("US pint", "pt", Gallon / 8),
                new Unit("US cup", "cup", Gallon / 16), new Unit("US fluid ounce", "fl oz", Gallon / 128), new Unit("imperial gallon", "imp gal", 4.54609e-3),
                new Unit("cubic foot", "ft³", 1728 * Inch * Inch * Inch), new Unit("cubic inch", "in³", Inch * Inch * Inch)),
            new Quantity("Mass",
                new Unit("kilogram", "kg", 1), new Unit("gram", "g", 1e-3), new Unit("milligram", "mg", 1e-6), new Unit("tonne", "t", 1e3),
                new Unit("pound", "lb", Pound), new Unit("ounce", "oz", Pound / 16), new Unit("stone", "st", 14 * Pound),
                new Unit("short ton", "ton", 2000 * Pound), new Unit("carat", "ct", 2e-4)),
            new Quantity("Time",
                new Unit("second", "s", 1), new Unit("millisecond", "ms", 1e-3), new Unit("microsecond", "μs", 1e-6), new Unit("minute", "min", 60),
                new Unit("hour", "h", 3600), new Unit("day", "d", 86400), new Unit("week", "wk", 604800), new Unit("year (365.25 days)", "yr", 31557600)),
            new Quantity("Speed",
                new Unit("metre per second", "m/s", 1), new Unit("kilometre per hour", "km/h", 1 / 3.6), new Unit("mile per hour", "mph", 63360 * Inch / 3600),
                new Unit("foot per second", "ft/s", 12 * Inch), new Unit("knot", "kn", 1852 / 3600.0), new Unit("speed of light", "c", 299792458)),
            new Quantity("Temperature",
                new Unit("kelvin", "K", 1), new Unit("degree Celsius", "°C", 1, 273.15), new Unit("degree Fahrenheit", "°F", 5 / 9.0, 459.67 * 5 / 9.0),
                new Unit("degree Rankine", "°R", 5 / 9.0)),
            new Quantity("Energy",
                new Unit("joule", "J", 1), new Unit("kilojoule", "kJ", 1e3), new Unit("calorie", "cal", 4.184), new Unit("kilocalorie", "kcal", 4184),
                new Unit("watt-hour", "Wh", 3600), new Unit("kilowatt-hour", "kWh", 3.6e6), new Unit("electronvolt", "eV", 1.602176634e-19),
                new Unit("British thermal unit", "BTU", 1055.05585262)),
            new Quantity("Pressure",
                new Unit("pascal", "Pa", 1), new Unit("kilopascal", "kPa", 1e3), new Unit("bar", "bar", 1e5), new Unit("atmosphere", "atm", 101325),
                new Unit("millimetre of mercury", "mmHg", 133.322387415), new Unit("pound per square inch", "psi", Pound * 9.80665 / (Inch * Inch))),
            new Quantity("Angle",
                new Unit("radian", "rad", 1), new Unit("degree", "°", Math.PI / 180), new Unit("gradian", "grad", Math.PI / 200),
                new Unit("arcminute", "′", Math.PI / 10800), new Unit("arcsecond", "″", Math.PI / 648000), new Unit("turn", "tr", 2 * Math.PI)),
            new Quantity("Data",
                new Unit("byte", "B", 1), new Unit("bit", "bit", 0.125), new Unit("kilobyte", "kB", 1e3), new Unit("megabyte", "MB", 1e6),
                new Unit("gigabyte", "GB", 1e9), new Unit("terabyte", "TB", 1e12), new Unit("kibibyte", "KiB", 1024), new Unit("mebibyte", "MiB", 1 << 20),
                new Unit("gibibyte", "GiB", 1 << 30), new Unit("tebibyte", "TiB", 1099511627776)),
        };

        public static Quantity Get(string name) => All.Single(q => q.Name == name);

        /// <summary>A value in a unit, in another unit of the same quantity.</summary>
        public static double Convert(double value, Unit from, Unit to) => (value * from.Factor + from.Offset - to.Offset) / to.Factor;

        /// <summary>A number typed by the user: a point or a comma before the decimals, and an exponent (<c>1.5e3</c>).</summary>
        public static bool TryParse(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var normalized = text!.Trim().Replace(" ", string.Empty).Replace('−', '-').Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// A value in UnicodeMath, with six significant digits: in scientific notation (<c>1.5×10^6</c>) when it is very
        /// large or very small. What follows it is separated by a space, which ends the exponent.
        /// </summary>
        public static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "\"—\"";
            var magnitude = Math.Abs(value);
            if (magnitude != 0 && (magnitude >= 1e6 || magnitude < 1e-4))
            {
                var exponent = (int)Math.Floor(Math.Log10(magnitude));
                var mantissa = Math.Round(value / Math.Pow(10, exponent), 5);
                if (Math.Abs(mantissa) >= 10)
                {
                    mantissa /= 10;
                    exponent++;
                }
                return Plain(mantissa) + "×10^" + (exponent < 0 ? "(-" + -exponent + ")" : exponent.ToString(CultureInfo.InvariantCulture));
            }
            return Plain(value);
        }

        private static string Plain(double value)
        {
            var text = value.ToString("G6", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }
    }
}
