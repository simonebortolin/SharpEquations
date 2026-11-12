namespace SharpEquations.MathTools.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>The worksheet: its entries, how it is kept, and the definitions it makes again.</summary>
    public class WorksheetTests
    {
        private static Equation Parse(string unicodeMath) => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);

        [Test]
        public async Task EntriesAreComputed()
        {
            var sheet = new Worksheet();
            var engine = new AngouriMathEngine();
            var solved = sheet.Run(engine, CasAction.Solve, Parse("x^2-5x+6=0"));
            await Assert.That(solved.Letter).IsEqualTo("x");
            await Assert.That(solved.Label).IsEqualTo("Solve for x");
            await Assert.That(solved.Failed).IsFalse();
            await Assert.That(solved.Text).Contains("2");
            await Assert.That(solved.Text).Contains("3");
            var derivative = sheet.Run(engine, CasAction.Differentiate, Parse("t^3"), "t");
            await Assert.That(derivative.OutputEquation).IsNotNull();
            await Assert.That(derivative.Label).IsEqualTo("Derivative by t");
            await Assert.That(sheet.Entries.Count).IsEqualTo(2);
        }

        [Test]
        public async Task WhatCannotBeDoneIsAnEntryToo()
        {
            var entry = new Worksheet().Run(new AngouriMathEngine(), CasAction.Evaluate, Parse("\"text\""));
            await Assert.That(entry.Failed).IsTrue();
            await Assert.That(entry.Output).IsNull();
            await Assert.That(entry.Text).IsNotEmpty();
        }

        [Test]
        public async Task KeptAsJson()
        {
            var sheet = new Worksheet();
            var engine = new AngouriMathEngine();
            sheet.Run(engine, CasAction.Expand, Parse("(a+b)^2"));
            sheet.Run(engine, CasAction.Integrate, Parse("cos x"), "x");
            var back = Worksheet.Deserialize(sheet.Serialize());
            await Assert.That(back.Entries.SequenceEqual(sheet.Entries)).IsTrue();
        }

        [Test]
        [Arguments(null)]
        [Arguments("")]
        [Arguments("not json")]
        [Arguments("{\"a\":1}")]
        [Arguments("[{\"Action\":0}]")]
        public async Task WhatCannotBeReadIsEmpty(string? json) =>
            await Assert.That(Worksheet.Deserialize(json).Entries).IsEmpty();

        [Test]
        public async Task DefinitionsAreMadeAgain()
        {
            var sheet = new Worksheet();
            sheet.Run(new AngouriMathEngine(), CasAction.Define, Parse("a≔5"));
            var engine = new AngouriMathEngine();
            Worksheet.Deserialize(sheet.Serialize()).Replay(engine);
            await Assert.That(engine.Definitions.ContainsKey("a")).IsTrue();
            await Assert.That(engine.Run(CasAction.Evaluate, Parse("2a"), null).Text).IsEqualTo("10");
        }

        [Test]
        public async Task AddRemoveAndCapacity()
        {
            var sheet = new Worksheet();
            var changes = 0;
            sheet.Changed += (_, _) => changes++;
            for (var i = 0; i < Worksheet.Capacity + 5; i++)
                sheet.Add(new WorksheetEntry(i.ToString(System.Globalization.CultureInfo.InvariantCulture), CasAction.Evaluate, null, null, ""));
            await Assert.That(sheet.Entries.Count).IsEqualTo(Worksheet.Capacity);
            await Assert.That(sheet.Entries[0].Input).IsEqualTo("5");
            await Assert.That(sheet.Remove(sheet.Entries[0])).IsTrue();
            await Assert.That(sheet.Remove(new WorksheetEntry("x", CasAction.Evaluate, null, null, ""))).IsFalse();
            sheet.Clear();
            await Assert.That(sheet.Entries).IsEmpty();
            await Assert.That(changes).IsEqualTo(Worksheet.Capacity + 7);
        }
    }
}
