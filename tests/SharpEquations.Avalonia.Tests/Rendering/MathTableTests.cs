namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using SharpEquations.Rendering.OpenType;

    /// <summary>The MATH table of the embedded Libertinus Math, read from the font file without a toolkit.</summary>
    public class MathTableTests
    {
        private static byte[] Table(string tag)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SharpEquations.Avalonia", "Assets", "Fonts", "LibertinusMath-Regular.otf");
            var font = File.ReadAllBytes(path);
            int U16(int at) => font[at] << 8 | font[at + 1];
            long U32(int at) => (long)U16(at) << 16 | (uint)U16(at + 2);
            var tables = U16(4);
            for (var i = 0; i < tables; i++)
            {
                var record = 12 + i * 16;
                if (Encoding.ASCII.GetString(font, record, 4) == tag)
                    return font.AsSpan((int)U32(record + 8), (int)U32(record + 12)).ToArray();
            }
            throw new InvalidDataException("No " + tag + " table.");
        }

        [Test]
        public async Task Constants()
        {
            var math = MathTable.Parse(Table("MATH"));
            await Assert.That(math[MathConstant.ScriptPercentScaleDown]).IsBetween(50, 90);
            await Assert.That(math[MathConstant.ScriptScriptPercentScaleDown]).IsBetween(30, 70);
            await Assert.That(math[MathConstant.AxisHeight]).IsBetween(200, 300);
            await Assert.That(math[MathConstant.FractionRuleThickness]).IsBetween(20, 100);
            await Assert.That(math[MathConstant.RadicalDegreeBottomRaisePercent]).IsBetween(30, 80);
            await Assert.That(math.MinConnectorOverlap).IsGreaterThan(0);
        }

        [Test]
        public async Task NotAMathTable()
        {
            await Assert.That(() => MathTable.Parse(new byte[] { 0, 2, 0, 0 })).Throws<FormatException>();
            await Assert.That(() => MathTable.Parse(new byte[] { 0, 1, 0, 0, 0, 100 })).Throws<FormatException>();
        }
    }
}
