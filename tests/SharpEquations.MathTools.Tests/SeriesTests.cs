namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AngouriMath;
    using SharpEquations.MathTools;

    /// <summary>The sums to infinity of a known form, each by its value.</summary>
    public class SeriesTests
    {
        private static Entity? Sum(string body, long from) =>
            Series.Value((Entity.Summationf)MathS.Sum(MathS.FromString(body), MathS.Var("k"), from, MathS.oo));

        /// <summary>A sum with a known value is that value exactly: no sum is left, and it is the decimal expected.</summary>
        [Test]
        [Arguments("1/k^2", 1, 1.6449340668482264)]
        [Arguments("k^(-2)", 1, 1.6449340668482264)]
        [Arguments("3/k^2", 1, 4.934802200544679)]
        [Arguments("1/k^2", 3, 0.3949340668482264)]
        [Arguments("1/k^4", 1, 1.0823232337111381)]
        [Arguments("1/k^6", 1, 1.0173430619844491)]
        [Arguments("1/k^12", 1, 1.000246086553308)]
        [Arguments("(-1)^(k+1)/k", 1, 0.6931471805599453)]
        [Arguments("(-1)^k/k^2", 1, -0.8224670334241132)]
        [Arguments("(-1)^k/(2k+1)", 0, 0.7853981633974483)]
        [Arguments("(-1)^k/(2k+1)^3", 0, 0.9689461462593693)]
        [Arguments("1/(k(k+1))", 1, 1.0)]
        [Arguments("1/(k^2+3k+2)", 0, 1.0)]
        [Arguments("1/(k(k+2))", 1, 0.75)]
        [Arguments("1/(4k^2-1)", 1, 0.5)]
        [Arguments("1/k^2+1/2^k", 1, 2.6449340668482264)]
        [Arguments("1/(k(k+1)(k+2))", 1, 0.25)]
        [Arguments("1/(k^2(k+1))", 1, 0.6449340668482264)]
        [Arguments("1/(2k+1)^2", 0, 1.2337005501361697)]
        [Arguments("1/((3k+1)(3k+2))", 0, 0.6045997880780726)]
        [Arguments("1/((5k+1)(5k+2))", 0, 0.5455310704014045)]
        [Arguments("(-1)^k/(3k+1)", 0, 0.8356488482647211)]
        [Arguments("1/k^2+(-1)^k/k^2", 1, 0.8224670334241132)]
        [Arguments("1/(k^2+1)", 1, 1.076674047468581)]
        [Arguments("1/(k^2+1)", 0, 2.076674047468581)]
        [Arguments("1/(k^2-2)", 1, -0.056810407700620236)]
        [Arguments("1/(k^2+k+1)", 0, 1.7981472805626901)]
        [Arguments("1/(k^2+2k+3)", 2, 0.3610281005734779)]
        [Arguments("(-1)^k/(k^2+1)", 0, 0.6360145274910666)]
        [Arguments("(-1)^k/(k^2+4)", 3, -0.04706661758669953)]
        [Arguments("2/(pi*k^2)", 1, 1.0471975511965976)]
        [Arguments("1/k^2-1/k^4", 1, 0.5626108331370883)]
        [Arguments("1/(k^2-1/4)", 1, 2.0)]
        [Arguments("(-1)^k/(2k+1)^5", 0, 0.9961578280770881)]
        public async Task SumsAKnownSeries(string body, long from, double expected)
        {
            var value = Sum(body, from);
            await Assert.That(value).IsNotNull();
            await Assert.That(value!.Nodes.Any(n => n is Entity.Summationf)).IsFalse();
            var number = ((Entity.Number.Complex)value.EvalNumerical()).RealPart.EDecimal.ToDouble();
            await Assert.That(Math.Abs(number - expected)).IsLessThan(1e-9 * Math.Max(1, Math.Abs(expected)));
        }

        /// <summary>A sum whose terms do not go to zero fast enough has no end.</summary>
        [Test]
        public async Task TheHarmonicSeriesDiverges()
        {
            await Assert.That(Sum("1/k", 1)).IsEqualTo(MathS.oo);
            await Assert.That(Sum("-2/k", 5)).IsEqualTo(-MathS.oo);
            await Assert.That(Sum("k/(k^2+1)", 0)).IsEqualTo(MathS.oo);
            await Assert.That(Sum("-1/(2k+1)", 0)).IsEqualTo(-MathS.oo);
            await Assert.That(Sum("k^2", 1)).IsEqualTo(MathS.oo);
        }

        /// <summary>
        /// A sum with no known closed form is left as it is: ζ(3), a term of another form, one that is not a number at an
        /// index, and one that goes up and down without end.
        /// </summary>
        [Test]
        [Arguments("1/k^3", 1)]
        [Arguments("(-1)^k/k^3", 1)]
        [Arguments("sin(k)/k^2", 1)]
        [Arguments("1/(k^3+1)", 1)]
        [Arguments("1/(k-3)^2", 1)]
        [Arguments("(-1)^k", 0)]
        public async Task LeavesAnUnknownSeries(string body, long from) => await Assert.That(Sum(body, from)).IsNull();

        /// <summary>The engine evaluates the sum read from UnicodeMath to π²/6.</summary>
        [Test]
        public async Task TheEngineSumsTheBaselSeries()
        {
            var result = AngouriMathEngine.Run(CasAction.Evaluate, MathReader.Read(Equation.Parse("∑_(k=1)^∞ 1/k^2", EquationFormat.UnicodeMath)), null);
            await Assert.That(result.Text).Contains("pi");
            await Assert.That(result.Text).Contains("1.644934");
        }
    }
}
