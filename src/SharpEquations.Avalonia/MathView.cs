namespace SharpEquations.Avalonia
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Documents;
    using global::Avalonia.Media;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>Shows an equation, laid out after MathML Core with a math font.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "The drawing is released when the equation is laid out again and when the control leaves the visual tree.")]
    public partial class MathView : Control
    {
        private readonly MathLayoutCache layoutCache = new MathLayoutCache();
        private MathDrawing? drawing;
        private (Equation Equation, MathNode Root)? tree;

        static MathView()
        {
            AffectsMeasure<MathView>(EquationProperty, FontSizeProperty, MathFontFamilyProperty, IsDisplayProperty);
            AffectsRender<MathView>(ForegroundProperty);
        }

        /// <summary>The equation shown.</summary>
        [GeneratedStyledProperty]
        public partial Equation? Equation { get; set; }

        /// <summary>The MathML of the equation shown; setting it reads the MathML.</summary>
        /// <exception cref="FormatException">The MathML is not valid.</exception>
        public string? MathML
        {
            get => Equation?.ToString(EquationFormat.MathML);
            set => Equation = string.IsNullOrEmpty(value) ? null : Equation.Parse(value!, EquationFormat.MathML);
        }

        /// <summary>The size of the font.</summary>
        [GeneratedStyledProperty(AddOwnerFrom = typeof(TextElement))]
        public partial double FontSize { get; set; }

        /// <summary>The brush of the equation.</summary>
        [GeneratedStyledProperty(AddOwnerFrom = typeof(TextElement))]
        public partial IBrush? Foreground { get; set; }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        [GeneratedStyledProperty]
        public partial FontFamily? MathFontFamily { get; set; }

        /// <summary>Whether the equation is laid out in display style even if it is inline.</summary>
        [GeneratedStyledProperty]
        public partial bool IsDisplay { get; set; }

        /// <summary>The layout of the equation, after a measure; null without an equation.</summary>
        public MathLayout? Layout => drawing?.Layout;

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            drawing?.Dispose();
            drawing = null;
            if (Equation is not { } equation)
                return default;
            if (tree is not { } current || !ReferenceEquals(current.Equation, equation))
                tree = current = (equation, MathNode.FromMathML(equation.ToMathML()));
            var options = new MathLayoutOptions { FontSize = FontSize, Display = IsDisplay, LineWidth = double.IsInfinity(availableSize.Width) ? null : availableSize.Width };
            var layout = MathLayoutEngine.Layout(current.Root, MathFonts.Get(MathFontFamily), options, layoutCache);
            drawing = new MathDrawing(layout);
            return new Size(layout.Width, layout.Height);
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            drawing?.Dispose();
            drawing = null;
            tree = null;
            layoutCache.Clear();
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            drawing?.Draw(context, default, new MathBrushes(Foreground, null, MathDrawing.ErrorBrush(ActualThemeVariant)));
        }
    }
}
