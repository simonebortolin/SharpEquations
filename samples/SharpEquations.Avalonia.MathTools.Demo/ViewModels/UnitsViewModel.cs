namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>A line of the table of the converter: a unit, the value in it, and whether it is one of the two chosen.</summary>
    public sealed record UnitLine(string Name, Equation Value, bool IsHighlighted);

    /// <summary>
    /// The unit converter: a value in a unit of a quantity, in another unit and in all of them, the results written in
    /// UnicodeMath for the view to draw.
    /// </summary>
    public sealed class UnitsViewModel : ObservableObject
    {
        private static readonly EquationOptions Display = new EquationOptions { Display = true };

        private Quantity quantity = Units.All[0];
        private Unit? from;
        private Unit? to;
        private string? value = "1";
        private string? result;
        private Equation? resultEquation;
        private IReadOnlyList<UnitLine> lines = Array.Empty<UnitLine>();
        private string? message;
        private bool updating;

        public UnitsViewModel()
        {
            SwapCommand = new RelayCommand(Swap);
            ShowQuantity();
        }

        /// <summary>The quantities: length, area, volume...</summary>
        public IReadOnlyList<Quantity> Quantities { get; } = Units.All;

        /// <summary>The quantity converted.</summary>
        public Quantity Quantity
        {
            get => quantity;
            set
            {
                // a list that changes its items gives null for a moment
                if (value != null && Set(ref quantity, value))
                    ShowQuantity();
            }
        }

        /// <summary>The units of the quantity.</summary>
        public IReadOnlyList<Unit> UnitList => quantity.Units;

        /// <summary>The value, as it is typed.</summary>
        public string? Value
        {
            get => value;
            set
            {
                if (Set(ref this.value, value))
                    Refresh();
            }
        }

        /// <summary>The unit of the value.</summary>
        public Unit? From
        {
            get => from;
            set
            {
                if (Set(ref from, value))
                    Refresh();
            }
        }

        /// <summary>The unit the value is converted to.</summary>
        public Unit? To
        {
            get => to;
            set
            {
                if (Set(ref to, value))
                    Refresh();
            }
        }

        /// <summary>The result, as UnicodeMath (<c>1 " km"=0.621371 " mi"</c>, with no-break spaces in the text), or null when the value is not a number.</summary>
        public string? Result
        {
            get => result;
            private set => Set(ref result, value);
        }

        /// <summary>The result, to draw.</summary>
        public Equation? ResultEquation
        {
            get => resultEquation;
            private set => Set(ref resultEquation, value);
        }

        /// <summary>The value in every unit of the quantity.</summary>
        public IReadOnlyList<UnitLine> Lines
        {
            get => lines;
            private set => Set(ref lines, value);
        }

        /// <summary>What to type, when the value is not a number.</summary>
        public string? Message
        {
            get => message;
            private set => Set(ref message, value);
        }

        /// <summary>Swaps the units.</summary>
        public ICommand SwapCommand { get; }

        /// <summary>Swaps the units, as the ⇄ button does.</summary>
        public void Swap()
        {
            var first = from;
            updating = true;
            From = to;
            updating = false;
            To = first;
        }

        private void ShowQuantity()
        {
            updating = true;
            OnPropertyChanged(nameof(UnitList));
            From = quantity.Units[0];
            To = quantity.Units[1];
            updating = false;
            Refresh();
        }

        private void Refresh()
        {
            if (updating || from == null || to == null)
                return;

            if (!Units.TryParse(value, out var number))
            {
                Result = null;
                ResultEquation = null;
                Lines = Array.Empty<UnitLine>();
                Message = "Type a number, such as 12.5 or 1.5e3.";
                return;
            }

            Message = null;
            Result = Units.Format(number) + " " + Text(from.Symbol) + "=" + Units.Format(Units.Convert(number, from, to)) + " " + Text(to.Symbol);
            ResultEquation = Equation.Parse(Result, EquationFormat.UnicodeMath, Display);
            Lines = quantity.Units
                .Select(unit => new UnitLine(unit.Name, Equation.Parse(Units.Format(Units.Convert(number, from, unit)) + " " + Text(unit.Symbol), EquationFormat.UnicodeMath), unit == from || unit == to))
                .ToList();
        }

        // a symbol as text of UnicodeMath, upright and kept as it is (a quote in it is not in any symbol), after a
        // no-break space that separates it from the number (the space of UnicodeMath before it ends an exponent, and is
        // not drawn)
        private static string Text(string symbol) => "\" " + symbol + "\"";
    }
}
