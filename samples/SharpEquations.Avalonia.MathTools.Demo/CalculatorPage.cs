namespace SharpEquations.Avalonia.MathTools
{
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using SharpEquations;

    /// <summary>
    /// The scientific keypad: the keys type into the equation editor, which the keyboard types into too, and the equation
    /// is shown as UnicodeMath and LaTeX as it is typed.
    /// </summary>
    public sealed class CalculatorPage : UserControl
    {
        private readonly TextBox unicodeMath = Code();
        private readonly TextBox latex = Code();

        public CalculatorPage()
        {
            Editor = new MathEditBox
            {
                FontSize = 34,
                MinHeight = 110,
                Padding = new Thickness(22, 18),
                BorderThickness = 0,
            };
            Editor.Bind(MathEditBox.BackgroundProperty, Editor.GetResourceObservable("EditorBrush"));
            Editor.EquationChanged += (_, _) => Refresh();

            var frame = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1.5),
                ClipToBounds = true,
                Child = Editor,
            };
            frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("EditorBorderBrush"));

            var keypad = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                ItemSpacing = 18,
                LineSpacing = 12,
                Children =
                {
                    Keys(CalculatorKeys.Functions, 6, "KeyBrush"),
                    Keys(CalculatorKeys.Numbers, 5, "DigitBrush"),
                },
            };
            var editing = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
            foreach (var key in CalculatorKeys.Editing)
                editing.Children.Add(Key(key, "KeyBrush"));

            var formats = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,16,*"),
                Children = { Labelled("UnicodeMath", unicodeMath, 0), Labelled("LaTeX", latex, 2) },
            };

            Content = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    Ui.Card("Calculator", "The keys type into the editor as the keyboard does: sin( waits for its argument, x² squares what is before the caret, a/b and √ take the selection", new StackPanel
                    {
                        Spacing = 16,
                        Children = { frame, keypad, editing },
                    }),
                    Ui.Card("As text", "The equation in the linear formats, as it is typed", formats),
                },
            };
        }

        /// <summary>The equation editor the keys type into.</summary>
        public MathEditBox Editor { get; }

        /// <summary>Presses a key, as a click on it does.</summary>
        public void Press(CalculatorKey key)
        {
            key.Press(Editor.Editor);
            Editor.Focus();
        }

        private UniformGrid Keys(IReadOnlyList<CalculatorKey> keys, int columns, string brush)
        {
            var grid = new UniformGrid { Columns = columns, ColumnSpacing = 6, RowSpacing = 6 };
            foreach (var key in keys)
                grid.Children.Add(Key(key, brush));
            return grid;
        }

        private Button Key(CalculatorKey key, string brush)
        {
            var button = new Button
            {
                MinWidth = 58,
                MinHeight = 46,
                Padding = new Thickness(8, 4),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = key.IsMath
                    ? Ui.Math(key.Face, 18)
                    : new TextBlock { Text = key.Face, FontSize = 15 },
            };
            button.Bind(Button.BackgroundProperty, button.GetResourceObservable(brush));
            button.Bind(Button.BorderBrushProperty, button.GetResourceObservable("CardBorderBrush"));
            ToolTip.SetTip(button, key.Tip);
            button.Click += (_, _) => Press(key);
            return button;
        }

        private void Refresh()
        {
            // text that is not built up yet (sin( waiting for its argument) is an merror of the equation
            var equation = Editor.Equation;
            var complete = equation != null && !equation.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror");
            unicodeMath.Text = complete ? equation!.ToString(EquationFormat.UnicodeMath) : null;
            latex.Text = complete ? equation!.ToString(EquationFormat.LaTeX) : null;
        }

        private static StackPanel Labelled(string label, Control control, int column)
        {
            var panel = new StackPanel { Spacing = 6, Children = { Ui.Muted(label), control } };
            Grid.SetColumn(panel, column);
            return panel;
        }

        private static TextBox Code() => new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,monospace"),
            FontSize = 13,
            MinHeight = 44,
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(10),
            PlaceholderText = "Not built up yet: finish the argument, or press Build up (Enter)",
        };
    }
}
