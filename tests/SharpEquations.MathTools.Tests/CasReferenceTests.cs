namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The reference tests of the computer algebra: each case of <c>tests/data/mathtools/cas.json</c> done by
    /// <see cref="AngouriMathEngine"/> (a case still pending is skipped with its reason) and the conversions of
    /// <c>tests/data/mathtools/units.json</c>.
    /// </summary>
    public class CasReferenceTests
    {
        private static readonly AngouriMathEngine Engine = new AngouriMathEngine();
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        [Test]
        [MethodDataSource(typeof(CasCase), nameof(CasCase.Names))]
        public async Task Computes(string name)
        {
            var test = CasCase.Named(name);
            if (test.Pending != null)
                Skip.Test(test.Pending);
            var input = Equation.Parse(test.Input, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            var work = Task.Run(() => Engine.Run(test.Action, input, test.Variable));
            if (await Task.WhenAny(work, Task.Delay(Timeout)) != work)
                Assert.Fail("No result in " + Timeout.TotalSeconds + " seconds.");
            var result = await work;
            await Assert.That(Reference.Check(test, result)).IsNull();
        }

        [Test]
        [MethodDataSource(typeof(UnitCase), nameof(UnitCase.Names))]
        public async Task Converts(string name)
        {
            var test = UnitCase.Named(name);
            var quantity = Units.Get(test.Quantity);
            var value = Units.Convert(test.Value, quantity[test.From], quantity[test.To]);
            await Assert.That(value).IsEqualTo(test.Expected).Within(1e-9 * Math.Max(1, Math.Abs(test.Expected)));
        }
    }
}
