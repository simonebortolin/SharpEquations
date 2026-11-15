namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>An entry of the worksheet as the page shows it: what was typed, what was done to it and the result.</summary>
    public sealed class WorksheetItem
    {
        internal WorksheetItem(WorksheetEntry entry, int number, ICommand useCommand, ICommand graphCommand, ICommand removeCommand)
        {
            Entry = entry;
            Title = number + ". " + entry.Label;
            Input = entry.InputEquation;
            Output = entry.OutputEquation;
            UseCommand = useCommand;
            GraphCommand = graphCommand;
            RemoveCommand = removeCommand;
        }

        public WorksheetEntry Entry { get; }

        /// <summary>The number of the entry and what was done: 2. Solve for x.</summary>
        public string Title { get; }

        /// <summary>What was typed, to draw.</summary>
        public Equation Input { get; }

        /// <summary>The result, to draw; null when it is only text.</summary>
        public Equation? Output { get; }

        public bool HasOutput => Output != null;

        /// <summary>The result as text, when it cannot be drawn.</summary>
        public string Text => Entry.Text;

        /// <summary>How strong the text is written: paler for an entry that failed.</summary>
        public double TextOpacity => Entry.Failed ? 0.75 : 1;

        /// <summary>Puts the result in the editor.</summary>
        public ICommand UseCommand { get; }

        /// <summary>Draws what was typed on the page of the graphs.</summary>
        public ICommand GraphCommand { get; }

        /// <summary>Removes the entry.</summary>
        public ICommand RemoveCommand { get; }
    }

    /// <summary>
    /// The worksheet: what is computed, one entry after the other as on paper, each with what was typed, what was done
    /// to it and the result, ready to go back to the editor or to be drawn. It is kept between sessions, with the
    /// definitions it made.
    /// </summary>
    public sealed class WorksheetViewModel : ObservableObject
    {
        /// <summary>The key of the settings the worksheet is kept under.</summary>
        public const string SettingsKey = "worksheet";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        private static readonly (CasAction Action, string Face)[] Faces =
        {
            (CasAction.Evaluate, "Evaluate"),
            (CasAction.Solve, "Solve"),
            (CasAction.Simplify, "Simplify"),
            (CasAction.Expand, "Expand"),
            (CasAction.Factor, "Factor"),
            (CasAction.Differentiate, "Derivative"),
            (CasAction.Integrate, "Antiderivative"),
            (CasAction.Define, "Define"),
        };

        private readonly ICasEngine engine;
        private Equation? equation = Equation.Parse("x^2-5x+6=0", EquationFormat.UnicodeMath, Display);
        private int actionIndex = 1;
        private IReadOnlyList<string> letters = Array.Empty<string>();
        private string? letter;
        private string message = "";
        private IReadOnlyList<WorksheetItem> items = Array.Empty<WorksheetItem>();

        public WorksheetViewModel(ICasEngine engine, ISettingsStore? settings = null)
        {
            this.engine = engine;
            var store = settings ?? new MemorySettingsStore();
            Sheet = Worksheet.Deserialize(store.Get(SettingsKey));
            Sheet.Replay(engine);
            Sheet.Changed += (_, _) =>
            {
                store.Set(SettingsKey, Sheet.Entries.Count == 0 ? null : Sheet.Serialize());
                Show();
            };
            EnterCommand = new RelayCommand(async () => await EnterAsync().ConfigureAwait(true));
            ClearCommand = new RelayCommand(Sheet.Clear);
            UpdateLetters();
            Show();
        }

        /// <summary>Raised by the Graph button of an entry, with the equation to draw.</summary>
        public event EventHandler<Equation>? GraphRequested;

        /// <summary>Raised when the editor should take the focus: a result was put in it.</summary>
        public event EventHandler? FocusRequested;

        /// <summary>The entries.</summary>
        public Worksheet Sheet { get; }

        /// <summary>The equation of the editor, the next entry.</summary>
        public Equation? Equation
        {
            get => equation;
            set
            {
                if (Set(ref equation, value))
                    UpdateLetters();
            }
        }

        /// <summary>What can be done to the equation of the editor.</summary>
        public IReadOnlyList<string> Actions { get; } = Faces.Select(f => f.Face).ToArray();

        /// <summary>What is done to the equation of the editor, in <see cref="Actions"/>.</summary>
        public int ActionIndex
        {
            get => actionIndex;
            set
            {
                if (value >= 0 && Set(ref actionIndex, value))
                    OnPropertyChanged(nameof(SelectedAction));
            }
        }

        /// <summary>The action chosen.</summary>
        public CasAction SelectedAction
        {
            get => Faces[actionIndex].Action;
            set => ActionIndex = Array.FindIndex(Faces, f => f.Action == value);
        }

        /// <summary>The letters of the equation.</summary>
        public IReadOnlyList<string> Letters
        {
            get => letters;
            private set => Set(ref letters, value);
        }

        /// <summary>The letter to solve for, and to differentiate and integrate by.</summary>
        public string? Letter
        {
            get => letter;
            set => Set(ref letter, value);
        }

        /// <summary>Why the last entry was not added, or empty.</summary>
        public string Message
        {
            get => message;
            private set => Set(ref message, value);
        }

        /// <summary>The entries, as the page shows them.</summary>
        public IReadOnlyList<WorksheetItem> Items
        {
            get => items;
            private set
            {
                if (Set(ref items, value))
                    OnPropertyChanged(nameof(IsEmpty));
            }
        }

        /// <summary>Whether nothing was computed yet.</summary>
        public bool IsEmpty => items.Count == 0;

        /// <summary>Computes the equation of the editor and adds the entry.</summary>
        public ICommand EnterCommand { get; }

        /// <summary>Removes every entry (the letters defined stay).</summary>
        public ICommand ClearCommand { get; }

        /// <summary>Computes the equation of the editor and adds the entry, as the Enter button does; null when nothing was added.</summary>
        public async Task<WorksheetEntry?> EnterAsync()
        {
            var typed = equation;
            if (typed == null)
                return Fail("Type something first.");
            if (typed.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror"))
                return Fail("Not built up yet: finish the argument, or press Build up (Enter).");
            var action = SelectedAction;
            var by = letter;
            // on another thread, since the engine can take long (it cannot be stopped: a result too late is dropped)
            var work = Task.Run(() => Worksheet.Compute(engine, action, typed, by));
            if (await Task.WhenAny(work, Task.Delay(Timeout)).ConfigureAwait(true) != work)
                return Fail("It takes too long: " + engine.Name + " found no result in " + Timeout.TotalSeconds + " seconds.");
            Message = "";
            var entry = Sheet.Add(await work.ConfigureAwait(true));
            UpdateLetters();
            return entry;
        }

        private WorksheetEntry? Fail(string text)
        {
            Message = text;
            return null;
        }

        private void UpdateLetters()
        {
            IReadOnlyList<string> found;
            try
            {
                found = equation is { } typed ? engine.Variables(typed) : Array.Empty<string>();
            }
#pragma warning disable CA1031 // what cannot be read has no letters
            catch (Exception)
#pragma warning restore CA1031
            {
                found = Array.Empty<string>();
            }
            if (letters.SequenceEqual(found))
                return;
            // the list of the view forgets its letter when its letters change: the letter is given again after them
            var selected = letter != null && found.Contains(letter) ? letter : found.Count > 0 ? found[0] : null;
            Letters = found;
            letter = selected;
            OnPropertyChanged(nameof(Letter));
        }

        private void Show() => Items = Sheet.Entries.Select((entry, index) => new WorksheetItem(
            entry,
            index + 1,
            new RelayCommand(() =>
            {
                Equation = entry.OutputEquation;
                FocusRequested?.Invoke(this, EventArgs.Empty);
            }),
            new RelayCommand(() => GraphRequested?.Invoke(this, entry.InputEquation)),
            new RelayCommand(() => Sheet.Remove(entry)))).ToList();
    }
}
