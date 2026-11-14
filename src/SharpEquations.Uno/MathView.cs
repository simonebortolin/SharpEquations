namespace SharpEquations.Uno
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>Shows an equation, laid out after MathML Core with a math font, its glyphs drawn as paths.</summary>
    public partial class MathView : UserControl
    {
        /// <summary>The equation shown.</summary>
        public static readonly DependencyProperty EquationProperty =
            DependencyProperty.Register(nameof(Equation), typeof(Equation), typeof(MathView), new PropertyMetadata(null, (d, _) => ((MathView)d).InvalidateMeasure()));

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        public static readonly DependencyProperty IsDisplayProperty =
            DependencyProperty.Register(nameof(IsDisplay), typeof(bool), typeof(MathView), new PropertyMetadata(false, (d, _) => ((MathView)d).InvalidateMeasure()));

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public static readonly DependencyProperty MathFontProperty =
            DependencyProperty.Register(nameof(MathFont), typeof(OpenTypeMathFont), typeof(MathView), new PropertyMetadata(null, (d, _) => ((MathView)d).InvalidateMeasure()));

        private static readonly Brush ErrorBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xE4, 0xE1));
        private readonly Canvas canvas = new Canvas { IsHitTestVisible = false };
        private readonly MathLayoutCache layoutCache = new MathLayoutCache();
        private (Equation Equation, MathNode Root)? tree;
        private MathLayout? drawn;

        /// <summary>A view with no equation.</summary>
        public MathView()
        {
            Content = canvas;
            IsTabStop = false;
            RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Render());
            RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => InvalidateMeasure());
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

        /// <summary>The layout of the equation, after a measure; null without an equation.</summary>
        public MathLayout? Layout { get; private set; }

        /// <inheritdoc/>
        protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
        {
            base.MeasureOverride(availableSize);
            if (Equation is not { } equation)
            {
                Layout = null;
                Render();
                return default;
            }
            if (tree is not { } current || !ReferenceEquals(current.Equation, equation))
                tree = current = (equation, MathNode.FromMathML(equation.ToMathML()));
            var options = new MathLayoutOptions { FontSize = FontSize, Display = IsDisplay, LineWidth = double.IsInfinity(availableSize.Width) ? null : availableSize.Width };
            Layout = MathLayoutEngine.Layout(current.Root, MathFont ?? MathFonts.Default, options, layoutCache);
            if (!ReferenceEquals(drawn, Layout))
                Render();
            return new Windows.Foundation.Size(Layout.Width, Layout.Height);
        }

        private void Render()
        {
            drawn = Layout;
            var target = new MathCanvas(canvas, new MathBrushes(Foreground, error: ErrorBrush), 0, 0);
            Layout?.Draw(target, 0, 0);
        }
    }
}
