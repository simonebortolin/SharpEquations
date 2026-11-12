namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The library of formulas: each one drawn by a <see cref="MathView"/>, filtered by subject and by name, and opened in
    /// the calculator with a click; the ones with a graph drawn on the page of the graphs with their button.
    /// </summary>
    public sealed class FormulasPage : UserControl
    {
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        private readonly Action<Equation> open;
        private readonly Action<GraphPreset>? graph;
        private readonly List<(Formula Formula, Control Card)> cards = new List<(Formula, Control)>();
        private readonly List<ToggleButton> subjects = new List<ToggleButton>();
        private string? subject;

        /// <param name="open">Opens a formula in the calculator.</param>
        /// <param name="graph">Draws the graph of a formula; without it, the formulas have no button for their graph.</param>
        public FormulasPage(Action<Equation> open, Action<GraphPreset>? graph = null)
        {
            this.open = open;
            this.graph = graph;

            Search = new TextBox { PlaceholderText = "Search a formula", Width = 260 };
            // the property, not TextChanged, which Avalonia raises later, on the dispatcher
            Search.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                    Filter();
            };

            var chips = Ui.Row(Search);
            foreach (var name in new string?[] { null }.Concat(Formulas.Subjects))
            {
                var chip = new ToggleButton
                {
                    Content = name ?? "All",
                    IsChecked = name == null,
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 5),
                };
                chip.Click += (_, _) => ShowSubject(name);
                subjects.Add(chip);
                chips.Children.Add(chip);
            }

            var list = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 12 };
            foreach (var formula in Formulas.All)
            {
                var card = Card(formula);
                cards.Add((formula, card));
                list.Children.Add(card);
            }

            Content = Ui.Card("Formulas", "A library of formulas of algebra, geometry, trigonometry, calculus, statistics and physics: click one to open it in the calculator, or draw its graph", new StackPanel
            {
                Spacing = 16,
                Children = { chips, list },
            });
        }

        /// <summary>The search box: the formulas whose name or subject contain its text.</summary>
        public TextBox Search { get; }

        /// <summary>The formulas shown, after the subject and the search.</summary>
        public IEnumerable<Formula> Shown => cards.Where(c => c.Card.IsVisible).Select(c => c.Formula);

        /// <summary>Shows the formulas of a subject, or all of them for null.</summary>
        public void ShowSubject(string? name)
        {
            subject = name;
            for (var i = 0; i < subjects.Count; i++)
                subjects[i].IsChecked = (i == 0 ? null : Formulas.Subjects[i - 1]) == name;
            Filter();
        }

        /// <summary>Opens a formula in the calculator, as a click on it does.</summary>
        public void Open(Formula formula) => open(Equation.Parse(formula.UnicodeMath, EquationFormat.UnicodeMath, Display));

        /// <summary>Draws the graph of a formula, as its button does; false if it has none or the page draws no graphs.</summary>
        public bool Graph(Formula formula)
        {
            if (formula.Graph == null || graph == null)
                return false;
            graph(formula.Graph);
            return true;
        }

        private void Filter()
        {
            var shown = new HashSet<Formula>(Formulas.Find(subject, Search.Text));
            foreach (var (formula, card) in cards)
                card.IsVisible = shown.Contains(formula);
        }

        private Control Card(Formula formula)
        {
            var caption = Ui.Muted(formula.Subject.ToUpperInvariant() + "  ·  " + formula.Name, 11.5);
            var view = Ui.Math(formula.UnicodeMath, 19, display: true);
            view.HorizontalAlignment = HorizontalAlignment.Left;
            var button = new Button
            {
                Width = 352,
                MinHeight = 108,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                BorderThickness = new Thickness(1),
                Focusable = false,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = new StackPanel { Spacing = 8, Children = { caption, view } },
            };
            button.Bind(Button.BackgroundProperty, button.GetResourceObservable("CodeBrush"));
            button.Bind(Button.BorderBrushProperty, button.GetResourceObservable("CardBorderBrush"));
            ToolTip.SetTip(button, "Open in the calculator");
            button.Click += (_, _) => Open(formula);
            if (formula.Graph == null || graph == null)
                return button;

            var draw = new Button
            {
                Content = "Graph",
                FontSize = 12,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 2),
                Margin = new Thickness(0, 8, 10, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            ToolTip.SetTip(draw, "Draw on the page of the graphs, with a slider for each parameter");
            draw.Click += (_, _) => Graph(formula);
            return new Panel { Children = { button, draw } };
        }
    }
}
