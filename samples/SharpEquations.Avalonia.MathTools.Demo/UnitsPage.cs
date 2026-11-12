namespace SharpEquations.Avalonia.MathTools
{
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The unit converter: a value in a unit of a quantity, in another unit and in all of them, the results written in
    /// UnicodeMath and drawn by a <see cref="MathView"/>.
    /// </summary>
    public sealed class UnitsPage : UserControl
    {
        private readonly MathView result = new MathView { FontSize = 30, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly StackPanel table = new StackPanel { Spacing = 2 };
        private bool updating;

        public UnitsPage()
        {
            Quantity = new ComboBox { ItemsSource = Units.All, SelectedIndex = 0, MinWidth = 160 };
            Value = new TextBox { Text = "1", Width = 150, PlaceholderText = "A number" };
            From = new ComboBox { MinWidth = 220 };
            To = new ComboBox { MinWidth = 220 };
            var swap = new Button { Content = "⇄", FontSize = 17, Padding = new Thickness(12, 2), CornerRadius = new CornerRadius(8) };
            ToolTip.SetTip(swap, "Swap the units");

            Quantity.SelectionChanged += (_, _) => ShowQuantity();
            Value.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                    Refresh();
            };
            From.SelectionChanged += (_, _) => Refresh();
            To.SelectionChanged += (_, _) => Refresh();
            swap.Click += (_, _) => Swap();

            var resultFrame = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 14), MinHeight = 76, Child = result };
            resultFrame.Bind(Border.BackgroundProperty, resultFrame.GetResourceObservable("CodeBrush"));

            Content = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    Ui.Card("Units", "A value in a unit, in another: length, area, volume, mass, time, speed, temperature, energy, pressure, angle and data", new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            Ui.Row(Field("Quantity", Quantity), Field("Value", Value), Field("From", From), swap, Field("To", To)),
                            resultFrame,
                        },
                    }),
                    Ui.Card("In every unit", "The value in all the units of the quantity", table),
                },
            };
            ShowQuantity();
        }

        public ComboBox Quantity { get; }

        public TextBox Value { get; }

        public ComboBox From { get; }

        public ComboBox To { get; }

        /// <summary>The result, as UnicodeMath (<c>1 " km"=0.621371 " mi"</c>, with no-break spaces in the text), or null when the value is not a number.</summary>
        public string? Result { get; private set; }

        /// <summary>Swaps the units, as the ⇄ button does.</summary>
        public void Swap()
        {
            var from = From.SelectedIndex;
            updating = true;
            From.SelectedIndex = To.SelectedIndex;
            updating = false;
            To.SelectedIndex = from;
        }

        private Quantity Selected => (Quantity)Quantity.SelectedItem!;

        private void ShowQuantity()
        {
            updating = true;
            From.ItemsSource = Selected.Units;
            To.ItemsSource = Selected.Units;
            From.SelectedIndex = 0;
            To.SelectedIndex = 1;
            updating = false;
            Refresh();
        }

        private void Refresh()
        {
            if (updating || From.SelectedItem is not Unit from || To.SelectedItem is not Unit to)
                return;

            table.Children.Clear();
            if (!Units.TryParse(Value.Text, out var value))
            {
                Result = null;
                result.Equation = null;
                table.Children.Add(Ui.Muted("Type a number, such as 12.5 or 1.5e3."));
                return;
            }

            Result = Units.Format(value) + " " + Text(from.Symbol) + "=" + Units.Format(Units.Convert(value, from, to)) + " " + Text(to.Symbol);
            result.Equation = Equation.Parse(Result, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            foreach (var unit in Selected.Units)
                table.Children.Add(Line(unit, Units.Convert(value, from, unit), unit == from || unit == to));
        }

        private static Grid Line(Unit unit, double value, bool highlighted)
        {
            var name = new TextBlock { Text = unit.Name, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center };
            if (highlighted)
                name.FontWeight = FontWeight.SemiBold;
            var math = Ui.Math(Units.Format(value) + " " + Text(unit.Symbol), 17);
            math.HorizontalAlignment = HorizontalAlignment.Left;
            math.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(math, 1);
            return new Grid { ColumnDefinitions = new ColumnDefinitions("220,*"), MinHeight = 32, Children = { name, math } };
        }

        // a symbol as text of UnicodeMath, upright and kept as it is (a quote in it is not in any symbol), after a
        // no-break space that separates it from the number (the space of UnicodeMath before it ends an exponent, and is
        // not drawn)
        private static string Text(string symbol) => "\"\u00A0" + symbol + "\"";

        private static StackPanel Field(string label, Control control)
        {
            var text = Ui.Muted(label, 12.5);
            text.VerticalAlignment = VerticalAlignment.Center;
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { text, control } };
        }
    }
}
