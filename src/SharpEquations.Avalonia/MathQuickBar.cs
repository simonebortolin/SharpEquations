namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Documents;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;
    using SharpEquations.Editing;

    /// <summary>
    /// The floating bar of <see cref="MathEditBox"/>, at the caret: the completions of a command being typed (Up and
    /// Down choose, Tab or Enter take, Esc closes), the structures for the selection, the commands of a table, and quick
    /// symbols in an empty slot. Its buttons do not take the focus, so typing goes on in the editor.
    /// </summary>
    public class MathQuickBar : Border
    {
        private readonly MathEditBox owner;
        private readonly WrapPanel panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 2, LineSpacing = 2, MaxWidth = 420 };
        private readonly MathQuickBarState state;

        internal MathQuickBar(MathEditBox owner)
        {
            this.owner = owner;
            state = owner.View.QuickBar;
            Child = panel;
            Padding = new Thickness(4);
            CornerRadius = new CornerRadius(6);
            BorderThickness = new Thickness(1);
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.FromArgb(0x40, 0, 0, 0) });
            ActualThemeVariantChanged += (_, _) => ApplyTheme();
            ApplyTheme();
        }

        /// <summary>What the bar shows.</summary>
        public MathQuickBarMode Mode => state.Mode;

        /// <summary>The completions shown, in <see cref="MathQuickBarMode.Completions"/>.</summary>
        public IReadOnlyList<EquationCommand> Completions => state.Completions;

        /// <summary>The completion that Tab or Enter takes.</summary>
        public int SelectedIndex
        {
            get => state.SelectedIndex;
            set
            {
                state.SelectedIndex = value;
                Highlight();
            }
        }

        /// <summary>Takes the selected completion; returns false when there is none.</summary>
        public bool AcceptCompletion() => state.AcceptCompletion();

        /// <summary>Makes the buttons of what the bar shows again, if it <paramref name="changed"/>; highlights the chosen completion.</summary>
        internal void Refresh(bool changed)
        {
            if (!changed && Mode == MathQuickBarMode.Completions)
            {
                Highlight();
                return;
            }
            panel.Children.Clear();
            var completions = Mode == MathQuickBarMode.Completions;
            foreach (var item in state.GetItems(owner.QuickSymbols))
            {
                var button = Button(item.Text, item.ToolTip, item.Invoke, Mode == MathQuickBarMode.Symbols ? 30 : 40);
                if (completions)
                {
                    button.HorizontalContentAlignment = HorizontalAlignment.Left;
                    button.MinWidth = 130;
                }
                panel.Children.Add(button);
            }
            Highlight();
        }

        private static Button Button(string content, string tip, Action action, double minWidth = 40)
        {
            var button = new Button
            {
                Content = content,
                MinWidth = minWidth,
                Padding = new Thickness(6, 2),
                Focusable = false,
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            ToolTip.SetTip(button, tip);
            button.Click += (_, _) => action();
            return button;
        }

        private IBrush highlight = new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x99, 0xFF));

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            ApplyTheme();
        }

        /// <summary>
        /// The colours of the theme: those of the flyouts of the theme of the application (Fluent, Simple...), or a light or
        /// a dark set by the variant of the theme.
        /// </summary>
        private void ApplyTheme()
        {
            var dark = ActualThemeVariant == ThemeVariant.Dark;
            Background = Resource(dark ? Color.FromArgb(0xF8, 0x2B, 0x2B, 0x2B) : Color.FromArgb(0xF8, 0xFB, 0xFB, 0xFD),
                "FlyoutPresenterBackground", "SystemControlBackgroundChromeMediumLowBrush", "ThemeBackgroundBrush");
            BorderBrush = Resource(dark ? Color.FromArgb(0x80, 0x70, 0x70, 0x70) : Color.FromArgb(0x60, 0x80, 0x80, 0x80),
                "FlyoutBorderThemeBrush", "SystemControlTransientBorderBrush", "ThemeBorderLowBrush");
            TextElement.SetForeground(this, Resource(dark ? Colors.White : Colors.Black,
                "SystemControlForegroundBaseHighBrush", "ThemeForegroundBrush"));
            highlight = Resource(dark ? Color.FromArgb(0x60, 0x4C, 0xA6, 0xFF) : Color.FromArgb(0x40, 0x33, 0x99, 0xFF),
                "SystemControlHighlightListAccentLowBrush");
            Highlight();
        }

        private IBrush Resource(Color fallback, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (this.TryFindResource(key, ActualThemeVariant, out var value))
                {
                    switch (value)
                    {
                        case IBrush brush:
                            return brush;
                        case Color color:
                            return new SolidColorBrush(color);
                    }
                }
            }
            return new SolidColorBrush(fallback);
        }

        private void Highlight()
        {
            for (var i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is Button button)
                    button.Background = i == state.SelectedIndex && Mode == MathQuickBarMode.Completions ? highlight : Brushes.Transparent;
            }
        }
    }
}
