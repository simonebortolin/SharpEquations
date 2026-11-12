namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AngouriMath;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>What an equation means, read from its presentation MathML, and what the engine of the calculator computes from it with AngouriMath.</summary>
    public class AngouriMathEngineTests
    {
        private static Entity Read(string unicodeMath) => MathReader.Read(Equation.Parse(unicodeMath, EquationFormat.UnicodeMath));

        /// <summary>The value of an expression read from UnicodeMath, as a decimal number.</summary>
        [Test]
        [Arguments("2+3×4", "14")]
        [Arguments("2(3+4)", "14")]
        [Arguments("(1+2)(3+4)", "21")]
        [Arguments("-2^2", "-4")]
        [Arguments("2^10", "1024")]
        [Arguments("1/2+1/3", "0.8333333333")]
        [Arguments("√2", "1.414213562")]
        [Arguments("∛27", "3")]
        [Arguments("√(9+16)", "5")]
        [Arguments("5!", "120")]
        [Arguments("|-3|+|2-5|", "6")]
        [Arguments("⌊2.7⌋", "2")]
        [Arguments("sin(π/2)", "1")]
        [Arguments("sin^2 x+cos^2 x", "1")]
        [Arguments("2 sin(π/6)", "1")]
        [Arguments("sin π/6", "0")]
        [Arguments("cos π", "-1")]
        [Arguments("ln e^2", "2")]
        [Arguments("log 1000", "3")]
        [Arguments("log_2 8", "3")]
        [Arguments("e^(iπ)", "-1")]
        [Arguments("i^2", "-1")]
        [Arguments("∑_(k=1)^10 k^2", "385")]
        [Arguments("∏_(k=1)^5 k", "120")]
        [Arguments("∫_0^1 x^2 dx", "0.3333333333")]
        [Arguments("∫_0^π sin x dx", "2")]
        [Arguments("1.5×4", "6")]
        [Arguments("2+3=", "5")]
        public async Task Evaluates(string unicodeMath, string expected)
        {
            var value = Read(unicodeMath);
            if (value.Vars.Any())
                value = value.Simplify();
            await Assert.That(AngouriMathEngine.Decimal(value.EvalNumerical())).IsEqualTo(expected);
        }

        [Test]
        [Arguments("x^2-5x+6=0", "x", new[] { "2", "3" })]
        [Arguments("2x+1=7", "x", new[] { "3" })]
        [Arguments("x^2=2", "x", new[] { "1.414213562", "-1.414213562" })]
        [Arguments("y^3-8", "y", new[] { "2" })]
        public async Task Solves(string unicodeMath, string variable, string[] real)
        {
            var expression = Read(unicodeMath);
            var equation = expression is Entity.Equalsf ? expression : expression.Equalizes(0);
            var solutions = ((Entity.Set.FiniteSet)equation.Solve(variable).Simplify())
                .Select(s => AngouriMathEngine.Decimal(s.EvalNumerical())).ToList();
            foreach (var solution in real)
                await Assert.That(solutions).Contains(solution);
        }

        /// <summary>The result of each action, drawn: it reads back from the LaTeX of AngouriMath without errors.</summary>
        [Test]
        [Arguments(CasAction.Evaluate, "1/3+1/6", "1/2 ≈ 0.5")]
        [Arguments(CasAction.Evaluate, "√8", "2 * sqrt(2) ≈ 2.828427125")]
        [Arguments(CasAction.Solve, "x^2-5x+6=0", null)]
        [Arguments(CasAction.Simplify, "(x^2-1)/(x-1)", null)]
        [Arguments(CasAction.Expand, "(x+1)^3", null)]
        [Arguments(CasAction.Factor, "x^2-1", null)]
        [Arguments(CasAction.Differentiate, "x^3 sin x", null)]
        [Arguments(CasAction.Integrate, "x e^x", null)]
        [Arguments(CasAction.Evaluate, "d/dx x^3", null)]
        public async Task ResultIsDrawn(CasAction action, string unicodeMath, string? text)
        {
            var result = new AngouriMathEngine().Run(action, Equation.Parse(unicodeMath, EquationFormat.UnicodeMath));
            await Assert.That(result.Math).IsNotNull().Because(result.Text);
            if (text != null)
                await Assert.That(result.Text).IsEqualTo(text);
        }

        [Test]
        public async Task SolvesForTheLetterAsked()
        {
            var engine = new AngouriMathEngine();
            var equation = Equation.Parse("a x+b=0", EquationFormat.UnicodeMath);
            await Assert.That(engine.Variables(equation)).IsEquivalentTo(new[] { "x", "a", "b" });
            var result = engine.Run(CasAction.Solve, equation, "x");
            await Assert.That(result.Text).Contains("b").And.Contains("a");
        }

        [Test]
        [Arguments("x+")]
        [Arguments("∫ x")]
        [Arguments("\"text\"")]
        [Arguments("a=b=c")]
        public async Task SaysWhatItCannotRead(string unicodeMath)
        {
            var exception = Assert.Throws<NotSupportedException>(() => Read(unicodeMath));
            await Assert.That(exception.Message).IsNotEmpty();
        }
    }
}
