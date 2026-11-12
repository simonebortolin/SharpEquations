namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using global::Avalonia.Layout;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The worksheet: what is computed, one entry after the other as on paper, each with what was typed, what was done
    /// to it and the result, ready to go back to the editor or to be drawn. It is kept between sessions, with the
    /// definitions it made.
    /// </summary>
    public sealed class WorksheetPage : UserControl
    {
        /// <summary>The key of the settings the worksheet is kept under.</summary>
        public const string SettingsKey = "worksheet";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        private static readonly (CasAction Action, string Face)[] Actions =
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
        private readonly ISettingsStore settings;
        private readonly StackPanel list = new StackPanel { Spacing = 10 };
        private readonly TextBlock message = Ui.Muted("", 12.5);
        private readonly TextBlock empty = Ui.Muted("Nothing computed yet: type below and press Enter.", 13);

        public WorksheetPage(ICasEngine engine, ISettingsStore? settings = null)
        {
            this.engine = engine;
            this.settings = settings ?? new MemorySettingsStore();
            Sheet = Worksheet.Deserialize(this.settings.Get(SettingsKey));
            Sheet.Replay(engine);
            Sheet.Changed += (_, _) => this.settings.Set(SettingsKey, Sheet.Entries.Count == 0 ? null : Sheet.Serialize());

            Editor = new MathEditBox
            {
                FontSize = 26,
                MinHeight = 70,
                Padding = new Thickness(16, 10),
                BorderThickness = 0,
                Equation = Equation.Parse("x^2-5x+6=0", EquationFormat.UnicodeMath, Display),
            };
            Editor.Bind(MathEditBox.BackgroundProperty, Editor.GetResourceObservable("EditorBrush"));
            var frame = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1.5), ClipToBounds = true, Child = Editor };
            frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("EditorBorderBrush"));

            Action = new ComboBox { ItemsSource = Actions.Select(a => a.Face).ToArray(), SelectedIndex = 1, MinWidth = 150 };
            ToolTip.SetTip(Action, "What is done to the equation");
            Letter = new ComboBox { MinWidth = 70 };
            ToolTip.SetTip(Letter, "The letter to solve for, and to differentiate and integrate by");
            Editor.EquationChanged += (_, _) => UpdateLetters();
            UpdateLetters();

            var enter = new Button { Content = "Enter", CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 6) };
            ToolTip.SetTip(enter, "Computes and adds the entry to the worksheet (Ctrl+Enter)");
            enter.Click += async (_, _) => await EnterAsync().ConfigureAwait(true);
            Editor.KeyDown += async (_, e) =>
            {
                if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    e.Handled = true;
                    await EnterAsync().ConfigureAwait(true);
                }
            };
            var clear = new Button { Content = "Clear worksheet", CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 6) };
            ToolTip.SetTip(clear, "Removes every entry (the letters defined stay)");
            clear.Click += (_, _) => Sheet.Clear();

            Sheet.Changed += (_, _) => Show();
            Show();

            Content = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    Ui.Card("Worksheet", "Each entry what was typed, what was done and its result, kept from one session to the next", new StackPanel { Spacing = 10, Children = { empty, list } }),
                    Ui.Card("Input", "Type an equation or an expression, choose what to do, and press Enter", new StackPanel
                    {
                        Spacing = 12,
                        Children = { frame, Ui.Row(Action, Ui.Muted("by", 12.5), Letter, enter, clear), message },
                    }),
                },
            };
        }

        /// <summary>Raised by the Graph button of an entry, with the equation to draw.</summary>
        public event EventHandler<Equation>? GraphRequested;

        /// <summary>The entries.</summary>
        public Worksheet Sheet { get; }

        /// <summary>The editor of the next entry.</summary>
        public MathEditBox Editor { get; }

        /// <summary>What is done to the equation of the editor.</summary>
        public ComboBox Action { get; }

        /// <summary>The letter to solve for, and to differentiate and integrate by.</summary>
        public ComboBox Letter { get; }

        /// <summary>Why the last entry was not added, or empty.</summary>
        public string Message => message.Text ?? "";

        /// <summary>The action chosen.</summary>
        public CasAction SelectedAction
        {
            get => Actions[Math.Max(0, Action.SelectedIndex)].Action;
            set => Action.SelectedIndex = Array.FindIndex(Actions, a => a.Action == value);
        }

        /// <summary>Computes the equation of the editor and adds the entry, as the Enter button does; null when nothing was added.</summary>
        public async Task<WorksheetEntry?> EnterAsync()
        {
            var equation = Editor.Equation;
            if (equation == null)
                return Fail("Type something first.");
            if (equation.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror"))
                return Fail("Not built up yet: finish the argument, or press Build up (Enter).");
            var action = SelectedAction;
            var letter = Letter.SelectedItem as string;
            // on another thread, since the engine can take long (it cannot be stopped: a result too late is dropped)
            var work = Task.Run(() => Worksheet.Compute(engine, action, equation, letter));
            if (await Task.WhenAny(work, Task.Delay(Timeout)).ConfigureAwait(true) != work)
                return Fail("It takes too long: " + engine.Name + " found no result in " + Timeout.TotalSeconds + " seconds.");
            message.Text = "";
            var entry = Sheet.Add(await work.ConfigureAwait(true));
            UpdateLetters();
            return entry;
        }

        private WorksheetEntry? Fail(string text)
        {
            message.Text = text;
            return null;
        }

        private void UpdateLetters()
        {
            IReadOnlyList<string> letters;
            try
            {
                letters = Editor.Equation is { } equation ? engine.Variables(equation) : Array.Empty<string>();
            }
#pragma warning disable CA1031 // what cannot be read has no letters
            catch (Exception)
#pragma warning restore CA1031
            {
                letters = Array.Empty<string>();
            }
            var selected = Letter.SelectedItem as string;
            if (Letter.ItemsSource is IReadOnlyList<string> old && old.SequenceEqual(letters))
                return;
            Letter.ItemsSource = letters;
            Letter.SelectedItem = selected != null && letters.Contains(selected) ? selected : letters.Count > 0 ? letters[0] : null;
        }

        private void Show()
        {
            list.Children.Clear();
            var number = 0;
            foreach (var entry in Sheet.Entries)
                list.Children.Add(Card(entry, ++number));
            empty.IsVisible = Sheet.Entries.Count == 0;
        }

        private Border Card(WorksheetEntry entry, int number)
        {
            var input = new MathView { FontSize = 20, Equation = entry.InputEquation, HorizontalAlignment = HorizontalAlignment.Left };
            var outputEquation = entry.OutputEquation;
            Control output = outputEquation != null
                ? new MathView { FontSize = 22, Equation = outputEquation, HorizontalAlignment = HorizontalAlignment.Left }
                : new SelectableTextBlock { Text = entry.Text, FontSize = 14, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Opacity = entry.Failed ? 0.75 : 1 };

            var use = new Button { Content = "To the editor", CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 3), IsEnabled = outputEquation != null };
            ToolTip.SetTip(use, "Puts the result in the editor");
            use.Click += (_, _) =>
            {
                Editor.Equation = outputEquation;
                Editor.Focus();
            };
            var graph = new Button { Content = "Graph", CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 3) };
            ToolTip.SetTip(graph, "Draws what was typed on the page of the graphs");
            graph.Click += (_, _) => GraphRequested?.Invoke(this, entry.InputEquation);
            var remove = new Button { Content = "✕", CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2) };
            ToolTip.SetTip(remove, "Removes this entry");
            remove.Click += (_, _) => Sheet.Remove(entry);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { use, graph, remove } };
            var top = new DockPanel { Children = { Ui.Docked(buttons, Dock.Right), Ui.Small(number + ". " + entry.Label) } };
            var arrow = Ui.Muted("→", 18);
            arrow.VerticalAlignment = VerticalAlignment.Center;
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 10),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        top,
                        input,
                        new DockPanel { Children = { Ui.Docked(arrow, Dock.Left), Ui.Docked(new Border { Width = 10 }, Dock.Left), output } },
                    },
                },
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CodeBrush"));
            return card;
        }
    }
}
