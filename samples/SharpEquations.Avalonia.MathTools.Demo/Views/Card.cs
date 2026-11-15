namespace SharpEquations.Avalonia.MathTools
{
    using global::Avalonia;
    using global::Avalonia.Controls;

    /// <summary>A card of a page: a title, a caption (text, or a control with text and formulas) and its content.</summary>
    public class Card : ContentControl
    {
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<Card, string?>(nameof(Title));

        public static readonly StyledProperty<object?> CaptionProperty =
            AvaloniaProperty.Register<Card, object?>(nameof(Caption));

        public string? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object? Caption
        {
            get => GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }
    }
}
