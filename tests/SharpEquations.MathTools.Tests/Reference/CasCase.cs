namespace SharpEquations.MathTools.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using SharpEquations.MathTools;

    /// <summary>
    /// A case of the reference tests of the computer algebra (<c>tests/data/mathtools/cas.json</c>): an action, its input
    /// in UnicodeMath, the letter it is done by, and the expected values in UnicodeMath (the solutions, for
    /// <see cref="CasAction.Solve"/>), compared by <see cref="Reference.Check"/>; a case that is <c>pending</c> is a goal
    /// not reached yet, and says why.
    /// </summary>
    public sealed record CasCase(CasAction Action, string Input, string? Variable, IReadOnlyList<string> Expected, string? Pending)
    {
        internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        public static IReadOnlyList<CasCase> All { get; } =
            JsonSerializer.Deserialize<List<CasCase>>(File.ReadAllText(TestData.Path("mathtools", "cas.json")), Options)!;

        /// <summary>The name of a case in the test list: its action and its input, which are unique.</summary>
        public string Name => Action + " " + Input + (Variable != null ? " by " + Variable : "");

        public static IEnumerable<string> Names() => All.Select(c => c.Name);

        public static CasCase Named(string name) => All.Single(c => c.Name == name);
    }

    /// <summary>A case of the reference tests of the unit converter (<c>tests/data/mathtools/units.json</c>), with the factors of NIST SP 811.</summary>
    public sealed record UnitCase(string Quantity, double Value, string From, string To, double Expected)
    {
        public static IReadOnlyList<UnitCase> All { get; } =
            JsonSerializer.Deserialize<List<UnitCase>>(File.ReadAllText(TestData.Path("mathtools", "units.json")), CasCase.Options)!;

        public string Name => $"{Value} {From} in {To}";

        public static IEnumerable<string> Names() => All.Select(c => c.Name);

        public static UnitCase Named(string name) => All.Single(c => c.Name == name);
    }
}
