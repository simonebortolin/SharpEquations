#if NET10_0_OR_GREATER
namespace SharpEquations.Avalonia.Tests.MathTools
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Avalonia.MathTools;

    /// <summary>The conversions of the unit converter, and the values written as UnicodeMath.</summary>
    public class UnitsTests
    {
        [Test]
        [Arguments("Length", 1, "mi", "km", 1.609344)]
        [Arguments("Length", 1, "ft", "in", 12)]
        [Arguments("Area", 1, "ha", "m²", 10000)]
        [Arguments("Area", 1, "mi²", "ac", 640)]
        [Arguments("Volume", 1, "gal", "L", 3.785411784)]
        [Arguments("Mass", 1, "lb", "oz", 16)]
        [Arguments("Mass", 1, "kg", "lb", 2.20462262185)]
        [Arguments("Time", 2, "h", "min", 120)]
        [Arguments("Speed", 100, "km/h", "m/s", 27.7777777778)]
        [Arguments("Temperature", 100, "°C", "°F", 212)]
        [Arguments("Temperature", -40, "°F", "°C", -40)]
        [Arguments("Temperature", 0, "K", "°C", -273.15)]
        [Arguments("Temperature", 491.67, "°R", "°F", 32)]
        [Arguments("Energy", 1, "kWh", "kcal", 860.420650096)]
        [Arguments("Pressure", 1, "atm", "psi", 14.6959487755)]
        [Arguments("Angle", 180, "°", "rad", Math.PI)]
        [Arguments("Data", 1, "GiB", "MB", 1073.741824)]
        public async Task Converts(string quantity, double value, string from, string to, double expected)
        {
            var q = Units.Get(quantity);
            await Assert.That(Units.Convert(value, q[from], q[to])).IsEqualTo(expected).Within(Math.Abs(expected) * 1e-9 + 1e-9);
            await Assert.That(Units.Convert(Units.Convert(value, q[from], q[to]), q[to], q[from])).IsEqualTo(value).Within(Math.Abs(value) * 1e-9 + 1e-9);
        }

        [Test]
        [Arguments(1.0, "1")]
        [Arguments(0.621371192237, "0.621371")]
        [Arguments(-273.15, "-273.15")]
        [Arguments(123456.7, "123457")]
        [Arguments(9460730472580800.0, "9.46073×10^15")]
        [Arguments(1.602176634e-19, "1.60218×10^(-19)")]
        [Arguments(9.999996e9, "1×10^10")]
        [Arguments(0.0, "0")]
        public async Task FormatsAsUnicodeMath(double value, string expected)
        {
            await Assert.That(Units.Format(value)).IsEqualTo(expected);
            await Assert.That(Equation.TryParse(expected, EquationFormat.UnicodeMath, out _)).IsTrue();
        }

        /// <summary>The unit after a value in scientific notation is not in its exponent.</summary>
        [Test]
        public async Task UnitIsNotInTheExponent()
        {
            var math = Equation.Parse(Units.Format(4.2195e6) + " \" cm\"", EquationFormat.UnicodeMath).ToMathML();
            var power = math.Descendants().Single(e => e.Name.LocalName == "msup");
            await Assert.That(power.Descendants().Any(e => e.Name.LocalName == "mtext")).IsFalse();
        }

        [Test]
        [Arguments("12.5", 12.5)]
        [Arguments(" 12,5 ", 12.5)]
        [Arguments("−3", -3.0)]
        [Arguments("1.5e3", 1500.0)]
        public async Task ParsesWhatIsTyped(string text, double expected)
        {
            await Assert.That(Units.TryParse(text, out var value)).IsTrue();
            await Assert.That(value).IsEqualTo(expected);
        }

        [Test]
        public async Task RejectsWhatIsNotANumber()
        {
            foreach (var text in new[] { "", "  ", "abc", "1e999" })
                await Assert.That(Units.TryParse(text, out _)).IsFalse().Because(text);
        }

        [Test]
        public async Task EverySymbolIsUniqueAndParses()
        {
            foreach (var quantity in Units.All)
            {
                await Assert.That(quantity.Units.Select(u => u.Symbol).Distinct().Count()).IsEqualTo(quantity.Units.Count).Because(quantity.Name);
                foreach (var unit in quantity.Units)
                {
                    var equation = Equation.Parse("1 \"" + unit.Symbol + "\"", EquationFormat.UnicodeMath);
                    await Assert.That(equation.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror")).IsFalse().Because(unit.Symbol);
                }
            }
        }
    }
}
#endif
