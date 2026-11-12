namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using SharpEquations;

    /// <summary>
    /// An entry of a worksheet: what was typed (in UnicodeMath), what was done to it and by which letter, and the result,
    /// as an equation in UnicodeMath (null when it cannot be drawn) and as text. An entry that failed has no result,
    /// and its <see cref="Text"/> says why.
    /// </summary>
    public sealed record WorksheetEntry(string Input, CasAction Action, string? Letter, string? Output, string Text, bool Failed = false)
    {
        /// <summary>What was typed, as an equation.</summary>
        [JsonIgnore]
        public Equation InputEquation => Equation.Parse(Input, EquationFormat.UnicodeMath, Worksheet.Display);

        /// <summary>The result, as an equation, or null.</summary>
        [JsonIgnore]
        public Equation? OutputEquation =>
            Output != null && Equation.TryParse(Output, EquationFormat.UnicodeMath, Worksheet.Display, out var equation) ? equation : null;

        /// <summary>What was done, as the worksheet shows it: Solve for x, Derivative by t.</summary>
        [JsonIgnore]
        public string Label => Action switch
        {
            CasAction.Solve => "Solve for " + (Letter ?? "x"),
            CasAction.Differentiate => "Derivative by " + (Letter ?? "x"),
            CasAction.Integrate => "Antiderivative by " + (Letter ?? "x"),
            CasAction.Define => "Define",
            _ => Action.ToString(),
        };
    }

    /// <summary>
    /// A worksheet, the history of what was computed one entry after the other, as on paper: each entry an input, an
    /// action of a computer algebra system and its output. It is kept as JSON between sessions; the definitions it made
    /// are made again when it comes back (<see cref="Replay"/>).
    /// </summary>
    public sealed class Worksheet
    {
        /// <summary>The most entries kept: the oldest go first.</summary>
        public const int Capacity = 200;

        internal static readonly EquationOptions Display = new EquationOptions { Display = true };

        private readonly List<WorksheetEntry> entries = new List<WorksheetEntry>();

        /// <summary>The entries, the oldest first.</summary>
        public IReadOnlyList<WorksheetEntry> Entries => entries;

        /// <summary>Raised when an entry is added or removed.</summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Does an action to an equation with <paramref name="engine"/>, and adds the entry: with the result, or with why
        /// there is none (what the engine cannot read or do). It can take long, as <see cref="ICasEngine.Run"/>.
        /// </summary>
        public WorksheetEntry Run(ICasEngine engine, CasAction action, Equation equation, string? letter = null) =>
            Add(Compute(engine, action, equation, letter));

        /// <summary>The entry of an action, without adding it (for what computes on another thread and adds on its own).</summary>
        public static WorksheetEntry Compute(ICasEngine engine, CasAction action, Equation equation, string? letter = null)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(equation);
            var input = equation.ToString(EquationFormat.UnicodeMath);
            try
            {
                if (letter == null && action is CasAction.Solve or CasAction.Differentiate or CasAction.Integrate)
                {
                    var letters = engine.Variables(equation);
                    letter = letters.Count > 0 ? letters[0] : null;
                }
                var result = engine.Run(action, equation, letter);
                return new WorksheetEntry(input, action, letter, result.Math?.ToString(EquationFormat.UnicodeMath), result.Text);
            }
            catch (NotSupportedException e)
            {
                return new WorksheetEntry(input, action, letter, null, e.Message, true);
            }
#pragma warning disable CA1031 // what the engine cannot do, it says with exceptions of many kinds
            catch (Exception e)
#pragma warning restore CA1031
            {
                return new WorksheetEntry(input, action, letter, null, engine.Name + " cannot do it: " + e.Message, true);
            }
        }

        /// <summary>Adds an entry at the end, dropping the oldest beyond <see cref="Capacity"/>.</summary>
        public WorksheetEntry Add(WorksheetEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            entries.Add(entry);
            if (entries.Count > Capacity)
                entries.RemoveRange(0, entries.Count - Capacity);
            Changed?.Invoke(this, EventArgs.Empty);
            return entry;
        }

        /// <summary>Removes an entry; false if it is not in the worksheet.</summary>
        public bool Remove(WorksheetEntry entry)
        {
            var index = entries.IndexOf(entry);
            if (index < 0)
                return false;
            entries.RemoveAt(index);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>Removes every entry.</summary>
        public void Clear()
        {
            entries.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The worksheet as JSON, to keep it.</summary>
        public string Serialize() => JsonSerializer.Serialize(entries, WorksheetJson.Default.ListWorksheetEntry);

        /// <summary>A worksheet kept with <see cref="Serialize"/>; an empty one when the text is null or cannot be read.</summary>
        public static Worksheet Deserialize(string? json)
        {
            var worksheet = new Worksheet();
            if (string.IsNullOrEmpty(json))
                return worksheet;
            try
            {
                foreach (var entry in JsonSerializer.Deserialize(json, WorksheetJson.Default.ListWorksheetEntry) ?? new List<WorksheetEntry>())
                {
                    if (entry?.Input != null && Equation.TryParse(entry.Input, EquationFormat.UnicodeMath, Display, out _))
                        worksheet.entries.Add(entry with { Text = entry.Text ?? "" });
                }
            }
            catch (JsonException)
            {
                // a worksheet that cannot be read is an empty one
            }
            return worksheet;
        }

        /// <summary>Makes again, in order, the definitions of the entries that made one, so that the letters mean what they meant.</summary>
        public void Replay(ICasEngine engine)
        {
            ArgumentNullException.ThrowIfNull(engine);
            foreach (var entry in entries.Where(e => e.Action == CasAction.Define && !e.Failed))
            {
                try
                {
                    engine.Run(CasAction.Define, entry.InputEquation, null);
                }
#pragma warning disable CA1031 // a definition that cannot be made again is left out
                catch (Exception)
#pragma warning restore CA1031
                {
                }
            }
        }
    }

    /// <summary>The JSON of a worksheet, written at compile time so that it survives trimming (the browser).</summary>
    [JsonSerializable(typeof(List<WorksheetEntry>))]
    internal sealed partial class WorksheetJson : JsonSerializerContext
    {
    }
}
