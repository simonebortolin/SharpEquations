namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using global::Avalonia.Markup.Xaml;
    using SharpEquations;

    /// <summary>An equation written in the markup as UnicodeMath: <c>Equation="{local:UnicodeMath 'x^2+y^2=25'}"</c>.</summary>
    public sealed class UnicodeMathExtension : MarkupExtension
    {
        public UnicodeMathExtension()
        {
        }

        public UnicodeMathExtension(string text) => Text = text;

        /// <summary>The equation, as UnicodeMath.</summary>
        public string Text { get; set; } = "";

        /// <summary>Whether the equation is on a line of its own.</summary>
        public bool Display { get; set; }

        /// <inheritdoc/>
        public override object ProvideValue(IServiceProvider serviceProvider) =>
            Equation.Parse(Text, EquationFormat.UnicodeMath, new EquationOptions { Display = Display });
    }
}
