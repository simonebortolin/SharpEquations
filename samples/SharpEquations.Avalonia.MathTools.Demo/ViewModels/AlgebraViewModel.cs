namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>A letter defined, as a definition: a ≔ 3, drawn when it can be read and written otherwise.</summary>
    public sealed record AlgebraDefinition(Equation? Math, string Text);

    /// <summary>A line of the results of the calculator: the result of an action with its label, or why there is none.</summary>
    public sealed class AlgebraItem
    {
        internal AlgebraItem(string message) => Message = message;

        internal AlgebraItem(CasResult result, string label)
        {
            Result = result;
            Label = label;
        }

        /// <summary>Why there is no result, or null.</summary>
        public string? Message { get; }

        /// <summary>The result, or null for a message.</summary>
        public CasResult? Result { get; }

        /// <summary>What was done: Solve for x.</summary>
        public string? Label { get; }

        public bool IsResult => Result != null;

        /// <summary>The result, to draw; null when it is only text.</summary>
        public Equation? Math => Result?.Math;

        /// <summary>The result as text, when it cannot be drawn.</summary>
        public string? Text => Result is { Math: null } ? Result.Text : null;

        /// <summary>The decimal values of the result, to draw.</summary>
        public Equation? Approximate => Result?.Approximate;

        /// <summary>The decimal values as text, when they cannot be drawn.</summary>
        public string? ApproximateText => Result is { Approximate: null } ? Result.ApproximateText : null;
    }

    /// <summary>
    /// What the calculator computes from the equation of the editor, with a computer algebra system (<see cref="ICasEngine"/>):
    /// its value as it is typed, and on demand its solutions, its simplified, expanded or factored form, its derivative
    /// and its integral, each result ready to go back to the editor.
    /// </summary>
    public sealed class AlgebraViewModel : ObservableObject
    {
        private const int Kept = 8;

        private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly List<CasResult> history = new List<CasResult>();
        private Equation? equation;
        private IReadOnlyList<string> letters = Array.Empty<string>();
        private string? letter;
        private int angleIndex;
        private CasResult? answer;
        private string? answerNote;
        private IReadOnlyList<AlgebraDefinition> definitions = Array.Empty<AlgebraDefinition>();
        private int version;
        private int scheduled;

        public AlgebraViewModel(ICasEngine engine)
        {
            Engine = engine;
            angleIndex = engine.Angles == AngleUnit.Degrees ? 1 : 0;
            RunCommand = new RelayCommand(async action => await RunAsync((CasAction)action!).ConfigureAwait(true));
            GraphCommand = new RelayCommand(() => RequestGraph());
            ForgetCommand = new RelayCommand(async () =>
            {
                Engine.ClearDefinitions();
                ShowDefinitions();
                await RefreshAnswer().ConfigureAwait(true);
            });
            UseCommand = new RelayCommand(math => Use(math as Equation));
            ShowDefinitions();
            ShowAnswer(null, "The value of what is typed shows here.");
        }

        /// <summary>Raised by the Graph button, with the equation of the editor to draw.</summary>
        public event EventHandler<Equation>? GraphRequested;

        /// <summary>Raised with a result to put in the editor.</summary>
        public event EventHandler<Equation>? EditorRequested;

        /// <summary>The computer algebra system, with the letters defined.</summary>
        public ICasEngine Engine { get; }

        /// <summary>The equation of the editor: its value is computed a moment after it changes.</summary>
        public Equation? Equation
        {
            get => equation;
            set
            {
                if (Set(ref equation, value))
                    Schedule();
            }
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

        /// <summary>The units of the angles.</summary>
        public IReadOnlyList<string> AngleUnits { get; } = new[] { "Radians", "Degrees" };

        /// <summary>The unit of the angles of sin, cos, tan and their inverses: 0 for radians, 1 for degrees.</summary>
        public int AngleIndex
        {
            get => angleIndex;
            set
            {
                if (value < 0 || !Set(ref angleIndex, value))
                    return;
                Engine.Angles = value == 1 ? AngleUnit.Degrees : AngleUnit.Radians;
                _ = RefreshAnswer();
            }
        }

        /// <summary>The value of the equation of the editor, as it was last computed.</summary>
        public CasResult? Answer
        {
            get => answer;
            private set
            {
                if (Set(ref answer, value))
                    OnPropertyChanged(nameof(AnswerMath));
            }
        }

        /// <summary>The value, to draw.</summary>
        public Equation? AnswerMath => answer?.Math;

        /// <summary>The value as text when it cannot be drawn, or why there is none.</summary>
        public string? AnswerNote
        {
            get => answerNote;
            private set => Set(ref answerNote, value);
        }

        /// <summary>The letters defined.</summary>
        public IReadOnlyList<AlgebraDefinition> Definitions
        {
            get => definitions;
            private set
            {
                if (Set(ref definitions, value))
                    OnPropertyChanged(nameof(HasDefinitions));
            }
        }

        public bool HasDefinitions => definitions.Count > 0;

        /// <summary>The results of the actions and the messages, the last first.</summary>
        public ObservableCollection<AlgebraItem> Items { get; } = new ObservableCollection<AlgebraItem>();

        /// <summary>The results of the actions, the last first.</summary>
        public IReadOnlyList<CasResult> Results => history;

        /// <summary>Does an action (a <see cref="CasAction"/>) to the equation of the editor.</summary>
        public ICommand RunCommand { get; }

        /// <summary>Sends the equation of the editor to be drawn.</summary>
        public ICommand GraphCommand { get; }

        /// <summary>Forgets the letters defined.</summary>
        public ICommand ForgetCommand { get; }

        /// <summary>Puts a result (an <see cref="SharpEquations.Equation"/>) in the editor.</summary>
        public ICommand UseCommand { get; }

        /// <summary>Sends the equation of the editor to be drawn, as the Graph button does; false when it cannot be read.</summary>
        public bool RequestGraph()
        {
            if (!TryRead(out var read, out _, out var error))
            {
                AddMessage(error!);
                return false;
            }
            GraphRequested?.Invoke(this, read!);
            return true;
        }

        /// <summary>Puts a result in the editor.</summary>
        public void Use(Equation? math)
        {
            if (math != null)
                EditorRequested?.Invoke(this, math);
        }

        /// <summary>Computes the value of the equation of the editor now, as typing does after a pause.</summary>
        public async Task RefreshAnswer()
        {
            scheduled++;
            var current = ++version;
            if (!TryRead(out var read, out var found, out var error))
            {
                UpdateLetters(Array.Empty<string>());
                ShowAnswer(null, error);
                return;
            }
            UpdateLetters(found);
            var (result, message) = await Compute(CasAction.Evaluate, read!, null).ConfigureAwait(true);
            if (current == version)
                ShowAnswer(result, message);
        }

        /// <summary>Does an action to the equation of the editor, as its button does, and adds the result to the list.</summary>
        public async Task<CasResult?> RunAsync(CasAction action)
        {
            if (!TryRead(out var read, out _, out var error))
            {
                AddMessage(error!);
                return null;
            }
            var by = letter;
            var (result, message) = await Compute(action, read!, by).ConfigureAwait(true);
            if (result == null)
            {
                AddMessage(message!);
                return null;
            }
            if (action == CasAction.Define)
            {
                ShowDefinitions();
                await RefreshAnswer().ConfigureAwait(true);
            }
            history.Insert(0, result);
            Add(new AlgebraItem(result, Label(action, by ?? "x")));
            return result;
        }

        // the value, a moment after the last change of the equation
        private async void Schedule()
        {
            var current = ++scheduled;
            await Task.Delay(Delay).ConfigureAwait(true);
            if (current == scheduled)
                await RefreshAnswer().ConfigureAwait(true);
        }

        private bool TryRead(out Equation? read, out IReadOnlyList<string> found, out string? error)
        {
            found = Array.Empty<string>();
            error = null;
            read = equation;
            if (read == null)
            {
                error = "The value of what is typed shows here.";
                return false;
            }
            if (read.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror"))
            {
                error = "Not built up yet: finish the argument, or press Build up (Enter).";
                return false;
            }
            try
            {
                found = Engine.Variables(read);
                return true;
            }
            catch (NotSupportedException e)
            {
                error = e.Message;
                return false;
            }
        }

        // on another thread, since the engine can take long (it cannot be stopped: a result too late is dropped)
        private async Task<(CasResult? Result, string? Message)> Compute(CasAction action, Equation read, string? by)
        {
            var work = Task.Run(() => Engine.Run(action, read, by));
            if (await Task.WhenAny(work, Task.Delay(Timeout)).ConfigureAwait(true) != work)
                return (null, "It takes too long: " + Engine.Name + " found no result in " + Timeout.TotalSeconds + " seconds.");
            try
            {
                return (await work.ConfigureAwait(true), null);
            }
#pragma warning disable CA1031 // what the engine cannot do, it says with exceptions of many kinds
            catch (Exception e)
#pragma warning restore CA1031
            {
                return (null, Engine.Name + " cannot do it: " + e.Message);
            }
        }

        private void UpdateLetters(IReadOnlyList<string> found)
        {
            if (letters.SequenceEqual(found))
                return;
            // the list of the view forgets its letter when its letters change: the letter is given again after them
            var selected = letter != null && found.Contains(letter) ? letter : found.Count > 0 ? found[0] : null;
            Letters = found;
            letter = selected;
            OnPropertyChanged(nameof(Letter));
        }

        private void ShowAnswer(CasResult? result, string? message)
        {
            Answer = result;
            AnswerNote = result == null ? message : result.Math == null ? result.Text : null;
        }

        // the letters defined, as a definition each: a ≔ 3
        private void ShowDefinitions()
        {
            Definitions = Engine.Definitions.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d =>
            {
                var text = d.Key + "≔" + d.Value.ToString(EquationFormat.UnicodeMath);
                return new AlgebraDefinition(SharpEquations.Equation.TryParse(text, EquationFormat.UnicodeMath, out var math) ? math : null, text);
            }).ToList();
        }

        private void AddMessage(string message) => Add(new AlgebraItem(message));

        private void Add(AlgebraItem item)
        {
            Items.Insert(0, item);
            while (Items.Count > Kept)
                Items.RemoveAt(Items.Count - 1);
        }

        private static string Label(CasAction action, string by) => action switch
        {
            CasAction.Solve => "Solve for " + by,
            CasAction.Differentiate => "Derivative by " + by,
            CasAction.Integrate => "Antiderivative by " + by,
            CasAction.Define => "Defined",
            _ => action.ToString(),
        };
    }
}
