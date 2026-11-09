namespace SharpEquations.Avalonia
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Documents;
    using global::Avalonia.Media;
    using SharpEquations.Rendering;

    /// <summary>Shows an equation, laid out after MathML Core with a math font.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "The drawing is released when the equation is laid out again and when the control leaves the visual tree.")]
    public class MathView : Control
    {
        /// <summary>The equation shown.</summary>
        public static readonly StyledProperty<Equation?> EquationProperty =
            AvaloniaProperty.Register<MathView, Equation?>(nameof(Equation));

        /// <summary>The size of the font.</summary>
        public static readonly StyledProperty<double> FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner<MathView>();

        /// <summary>The brush of the equation.</summary>
        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner<MathView>();

        /// <summary>The math font, a family with an OpenType MATH table; null for <see cref="MathFonts.Default"/>.</summary>
        public static readonly StyledProperty<FontFamily?> MathFontFamilyProperty =
            AvaloniaProperty.Register<MathView, FontFamily?>(nameof(MathFontFamily));

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        public static readonly StyledProperty<bool> IsDisplayProperty =
            AvaloniaProperty.Register<MathView, bool>(nameof(IsDisplay));

        private MathDrawing? drawing;

        static MathView()
        {
            AffectsMeasure<MathView>(EquationProperty, FontSizeProperty, MathFontFamilyProperty, IsDisplayProperty);
            AffectsRender<MathView>(ForegroundProperty);
        }

        /// <summary>The equation shown.</summary>
        public Equation? Equation
        {
            get => GetValue(EquationProperty);
            set => SetValue(EquationProperty, value);
        }

        /// <summary>The MathML of the equation shown; setting it reads the MathML.</summary>
        /// <exception cref="FormatException">The MathML is not valid.</exception>
        public string? MathML
        {
            get => Equation?.ToString(EquationFormat.MathML);
            set => Equation = string.IsNullOrEmpty(value) ? null : Equation.Parse(value!, EquationFormat.MathML);
        }

        /// <summary>The size of the font.</summary>
        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        /// <summary>The brush of the equation.</summary>
        public IBrush? Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public FontFamily? MathFontFamily
        {
            get => GetValue(MathFontFamilyProperty);
            set => SetValue(MathFontFamilyProperty, value);
        }

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        public bool IsDisplay
        {
            get => GetValue(IsDisplayProperty);
            set => SetValue(IsDisplayProperty, value);
        }

        /// <summary>The layout of the equation, after a measure; null without an equation.</summary>
        public MathLayout? Layout => drawing?.Layout;

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            drawing?.Dispose();
            drawing = null;
            if (Equation is not { } equation)
                return default;
            var layout = MathLayoutEngine.Layout(equation, MathFonts.Get(MathFontFamily), new MathLayoutOptions { FontSize = FontSize, Display = IsDisplay, LineWidth = double.IsInfinity(availableSize.Width) ? null : availableSize.Width });
            drawing = new MathDrawing(layout);
            return new Size(layout.Width, layout.Height);
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            drawing?.Dispose();
            drawing = null;
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            drawing?.Draw(context, default, new MathBrushes(Foreground, null, MathDrawing.ErrorBrush(ActualThemeVariant)));
        }
    }
}
