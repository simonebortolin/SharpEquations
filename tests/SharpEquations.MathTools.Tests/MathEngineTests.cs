namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using AngouriMath;
    using SharpEquations;
    using SharpEquations.MathTools;
    using SharpEquations.MathTools.MathEngine;

    /// <summary>
    /// The adapter of the engine of Microsoft Mathematics: what it writes and reads (everywhere), and the reference cases
    /// of the computer algebra done by the engine itself, only on Windows with Microsoft Mathematics installed (or
    /// <c>MATHENGINEN_PATH</c> set), skipped elsewhere.
    /// </summary>
    public class MathEngineTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        [Test]
        [Arguments("x^2+3*x-4", "x^2+3*x-4")]
        [Arguments("sin(x)^2", "sin(x)^2")]
        [Arguments("(x+1)/(x-1)", "(x+1)/(x-1)")]
        [Arguments("x-(y-1)", "x-(y-1)")]
        [Arguments("(-2)^x", "(-2)^x")]
        [Arguments("2^(1/2)", "2^(1/2)")]
        [Arguments("ln(x)", "ln(x)")]
        [Arguments("log(2, x)", "ln(x)/ln(2)")]
        [Arguments("abs(x-pi)", "abs(x-pi)")]
        [Arguments("x!", "x!")]
        public async Task WritesLinearInput(string angouri, string expected)
        {
            await Assert.That(MathEngineText.Linear(MathS.FromString(angouri, useCache: false))).IsEqualTo(expected);
        }

        [Test]
        public async Task WritesRationalsInBrackets()
        {
            await Assert.That(MathEngineText.Linear(Entity.Number.Rational.Create(1, 2) * "x")).IsEqualTo("(1/2)*x");
        }

        [Test]
        public async Task WritesCommands()
        {
            Entity f = "x^2";
            await Assert.That(MathEngineText.Command(CasAction.Evaluate, new[] { f }, null)).IsEqualTo("x^2");
            await Assert.That(MathEngineText.Command(CasAction.Factor, new[] { f }, null)).IsEqualTo("factor(x^2)");
            await Assert.That(MathEngineText.Command(CasAction.Differentiate, new[] { f }, "x")).IsEqualTo("deriv(x^2,x)");
            await Assert.That(MathEngineText.Command(CasAction.Integrate, new[] { f }, "x")).IsEqualTo("integral(x^2,x)");
            await Assert.That(MathEngineText.Command(CasAction.Solve, new[] { f }, "x")).IsEqualTo("x^2=0");
            Entity a = MathS.Equality("x+y", 3);
            Entity b = MathS.Equality("x-y", 1);
            await Assert.That(MathEngineText.Command(CasAction.Solve, new[] { a, b }, null)).IsEqualTo("x+y=3, x-y=1");
            await Assert.That(() => MathEngineText.Command(CasAction.Simplify, new[] { a, b }, null)).Throws<NotSupportedException>();
        }

        [Test]
        public async Task DecodesErrors()
        {
            await Assert.That(MathEngineText.Error(0)).IsNull();
            await Assert.That(MathEngineText.Error(2 << 16)).IsEqualTo("The computation was stopped.");
            await Assert.That(MathEngineText.Error((1 << 16) | 7)!).Contains("cannot read this (error -3)");
            await Assert.That(MathEngineText.Error(8)!).Contains("cannot compute this (error 4)");
        }

        [Test]
        public async Task SplitsValues()
        {
            const string result = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>x</mi><mo>=</mo><mn>1</mn>"
                + "<mo>,</mo><mi>x</mi><mo>=</mo><mo>-</mo><mn>4</mn><mtext>or</mtext><mi>x</mi><mo>=</mo><msqrt><mn>2</mn></msqrt>"
                + "<mo>≈</mo><mn>1.414</mn></mrow></math>";
            var values = MathEngineText.Values(result);
            await Assert.That(values.Count).IsEqualTo(3);
            await Assert.That(values[0].Value).IsEqualTo("1");
            await Assert.That(values[1].Value).IsEqualTo("-4");
            await Assert.That(values[2].Descendants().Any(e => e.Name.LocalName == "msqrt")).IsTrue();
            await Assert.That(values.All(v => v.Name.LocalName == "math")).IsTrue();
            foreach (var value in values)
                await Assert.That(Equation.TryParse(value.ToString(), EquationFormat.MathML, null, out _)).IsTrue();
        }

        [Test]
        public async Task ReadsTheMachine()
        {
            var image = new byte[0x100];
            image[0] = (byte)'M';
            image[1] = (byte)'Z';
            BitConverter.GetBytes(0x80).CopyTo(image, 0x3C);
            image[0x80] = (byte)'P';
            image[0x81] = (byte)'E';
            BitConverter.GetBytes((ushort)0x8664).CopyTo(image, 0x84);
            using (var stream = new MemoryStream(image))
                await Assert.That(MathEngineText.Machine(stream)).IsEqualTo((ushort)0x8664);
            using (var text = new MemoryStream(new byte[0x100]))
                await Assert.That(MathEngineText.Machine(text)).IsNull();
        }

        [Test]
        public async Task IsNotAvailableWithoutWindows()
        {
            if (!OperatingSystem.IsWindows())
                await Assert.That(MathEngineN.IsAvailable).IsFalse();
        }

        [Test]
        [MethodDataSource(typeof(CasCase), nameof(CasCase.Names))]
        public async Task ComputesLikeTheReference(string name)
        {
            if (!MathEngineN.IsAvailable)
                Skip.Test("The engine of Microsoft Mathematics is not installed (Windows, or MATHENGINEN_PATH).");
            var test = CasCase.Named(name);
            if (test.Pending != null)
                Skip.Test(test.Pending);
            var input = Equation.Parse(test.Input, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            using var engine = new MathEngineN { Angles = test.Angles };
            foreach (var definition in test.Definitions)
                engine.Run(CasAction.Define, Equation.Parse(definition, EquationFormat.UnicodeMath));
            var work = Task.Run(() => engine.Run(test.Action, input, test.Variable));
            if (await Task.WhenAny(work, Task.Delay(Timeout)) != work)
                Assert.Fail("No result in " + Timeout.TotalSeconds + " seconds.");
            var result = await work;
            await Assert.That(Reference.Check(test, result)).IsNull();
        }
    }
}
