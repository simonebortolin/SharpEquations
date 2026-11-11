#if NET10_0_OR_GREATER
namespace SharpEquations.Avalonia.Tests.MathTools
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.Avalonia.MathTools;
    using SharpEquations.Editing;

    /// <summary>The keys of the keypad of the math tools type the equation they show.</summary>
    public class CalculatorKeysTests
    {
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        public static System.Collections.Generic.IEnumerable<(string Keys, string Expected)> Sequences() => new[]
        {
            ("sin x )", "sin(x)"),
            ("ln x )", "ln(x)"),
            ("sin^-1 x )", "sin^(-1)(x)"),
            ("7 x^2", "7^2"),
            ("( x + 1 ) x^2", "(x+1)^2"),
            ("x x^n 3 +", "x^3+"),
            ("a/b 1 ↓ 2", "1/2"),
            ("√x 2", "√2"),
            ("e^x x +", "e^x+"),
            ("|x| x", "|x|"),
            ("5 n! ×", "5!×"),
            ("(x) x", "(x)"),
            ("|x| x + 1", "|x+1|"),
            ("2 × π", "2×π"),
            ("1 . 5 ÷ 3 = x", "1.5÷3=x"),
            ("ⅆ/ⅆx x", "ⅆ/ⅆx x"),
        };

        [Test]
        [MethodDataSource(nameof(Sequences))]
        public async Task KeysTypeTheEquation(string keys, string expected)
        {
            var editor = new MathEditor { IsDisplay = true };
            foreach (var face in keys.Split(' '))
            {
                if (face == "↓")
                {
                    editor.MoveDown();
                    continue;
                }
                var key = CalculatorKeys.Functions.Concat(CalculatorKeys.Numbers).Concat(CalculatorKeys.Editing).FirstOrDefault(k => k.Face == face);
                if (key == null)
                    editor.Insert(face);
                else
                    key.Press(editor);
            }
            editor.BuildUpAll();
            await Assert.That(editor.ToEquation()).IsEqualTo(Equation.Parse(expected, EquationFormat.UnicodeMath, Display));
        }

        [Test]
        public async Task EveryKeyHasAFaceThatParses()
        {
            foreach (var key in CalculatorKeys.Functions.Concat(CalculatorKeys.Numbers).Where(k => k.IsMath))
                await Assert.That(Equation.TryParse(key.Face, EquationFormat.UnicodeMath, out _)).IsTrue().Because(key.Face);
        }

        [Test]
        public async Task EveryKeyTypesSomething()
        {
            foreach (var key in CalculatorKeys.Functions.Concat(CalculatorKeys.Numbers))
            {
                var editor = new MathEditor();
                key.Press(editor);
                await Assert.That(editor.IsEmpty).IsFalse().Because(key.Face);
            }
        }
    }
}
#endif
