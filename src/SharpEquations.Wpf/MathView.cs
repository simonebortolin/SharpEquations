namespace SharpEquations.Wpf
{
    using System;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Media;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>Shows an equation, laid out after MathML Core with a math font, its glyphs drawn as geometries from their outlines.</summary>
    public class MathView : FrameworkElement
    {
        /// <summary>The equation shown.</summary>
        public static readonly DependencyProperty EquationProperty =
            DependencyProperty.Register(nameof(Equation), typeof(Equation), typeof(MathView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        public static readonly DependencyProperty IsDisplayProperty =
            DependencyProperty.Register(nameof(IsDisplay), typeof(bool), typeof(MathView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public static readonly DependencyProperty MathFontProperty =
            DependencyProperty.Register(nameof(MathFont), typeof(OpenTypeMathFont), typeof(MathView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>The size of the font.</summary>
        public static readonly DependencyProperty FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner(typeof(MathView), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>The brush of the equation.</summary>
        public static readonly DependencyProperty ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner(typeof(MathView), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly Brush ErrorBrush = CreateErrorBrush();
        private readonly MathLayoutCache layoutCache = new MathLayoutCache();
        private (Equation Equation, MathNode Root)? tree;

        /// <summary>A view with no equation.</summary>
        public MathView()
        {
            Unloaded += (_, _) =>
            {
                tree = null;
                layoutCache.Clear();
            };
        }

        /// <summary>The equation shown.</summary>
        public Equation? Equation
        {
            get => (Equation?)GetValue(EquationProperty);
            set => SetValue(EquationProperty, value);
        }

        /// <summary>The MathML of the equation shown; setting it reads the MathML.</summary>
        /// <exception cref="FormatException">The MathML is not valid.</exception>
        public string? MathML
        {
            get => Equation?.ToString(EquationFormat.MathML);
            set => Equation = string.IsNullOrEmpty(value) ? null : Equation.Parse(value!, EquationFormat.MathML);
        }

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        public bool IsDisplay
        {
            get => (bool)GetValue(IsDisplayProperty);
            set => SetValue(IsDisplayProperty, value);
        }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public OpenTypeMathFont? MathFont
        {
            get => (OpenTypeMathFont?)GetValue(MathFontProperty);
            set => SetValue(MathFontProperty, value);
        }

        /// <summary>The size of the font.</summary>
        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        /// <summary>The brush of the equation.</summary>
        public Brush? Foreground
        {
            get => (Brush?)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        /// <summary>The layout of the equation, after a measure; null without an equation.</summary>
        public MathLayout? Layout { get; private set; }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Equation is not { } equation)
            {
                Layout = null;
                return default;
            }
            if (tree is not { } current || !ReferenceEquals(current.Equation, equation))
                tree = current = (equation, MathNode.FromMathML(equation.ToMathML()));
            var options = new MathLayoutOptions { FontSize = FontSize, Display = IsDisplay, LineWidth = double.IsInfinity(availableSize.Width) ? null : availableSize.Width };
            Layout = MathLayoutEngine.Layout(current.Root, MathFont ?? MathFonts.Default, options, layoutCache);
            return new Size(Layout.Width, Layout.Height);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext) =>
            Layout?.Draw(new MathCanvas(drawingContext, new MathBrushes(Foreground, error: ErrorBrush)), 0, 0);

        private static SolidColorBrush CreateErrorBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xE1));
            brush.Freeze();
            return brush;
        }
    }
}
