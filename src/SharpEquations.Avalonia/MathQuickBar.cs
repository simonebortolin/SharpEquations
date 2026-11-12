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

    /// <summary>What <see cref="MathQuickBar"/> shows.</summary>
    public enum MathQuickBarMode
    {
        /// <summary>Nothing: the bar is closed.</summary>
        Hidden,

        /// <summary>The commands that complete the one typed before the caret (<c>\al</c>: <c>\alpha</c>, <c>\aleph</c>...).</summary>
        Completions,

        /// <summary>The structures that take the selection (a fraction of it, its root, brackets around it...).</summary>
        Selection,

        /// <summary>The commands of the table of the caret: rows and columns.</summary>
        Table,

        /// <summary>The quick symbols, in an empty slot.</summary>
        Symbols,
    }

    /// <summary>
    /// The floating bar of <see cref="MathEditBox"/>, at the caret: the completions of a command being typed (Up and
    /// Down choose, Tab or Enter take, Esc closes), the structures for the selection, the commands of a table, and quick
    /// symbols in an empty slot. Its buttons do not take the focus, so typing goes on in the editor.
    /// </summary>
    public class MathQuickBar : Border
    {
        private readonly MathEditBox owner;
        private readonly WrapPanel panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 2, LineSpacing = 2, MaxWidth = 420 };
        private IReadOnlyList<EquationCommand> completions = Array.Empty<EquationCommand>();
        private int selectedIndex;

        internal MathQuickBar(MathEditBox owner)
        {
            this.owner = owner;
            Child = panel;
            Padding = new Thickness(4);
            CornerRadius = new CornerRadius(6);
            BorderThickness = new Thickness(1);
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.FromArgb(0x40, 0, 0, 0) });
            ActualThemeVariantChanged += (_, _) => ApplyTheme();
            ApplyTheme();
        }

        /// <summary>What the bar shows.</summary>
        public MathQuickBarMode Mode { get; private set; }

        /// <summary>The completions shown, in <see cref="MathQuickBarMode.Completions"/>.</summary>
        public IReadOnlyList<EquationCommand> Completions => completions;

        /// <summary>The completion that Tab or Enter takes.</summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (completions.Count == 0)
                    return;
                selectedIndex = (value % completions.Count + completions.Count) % completions.Count;
                Highlight();
            }
        }

        /// <summary>Takes the selected completion; returns false when there is none.</summary>
        public bool AcceptCompletion()
        {
            if (Mode != MathQuickBarMode.Completions || completions.Count == 0)
                return false;
            owner.Editor.Complete(completions[selectedIndex]);
            return true;
        }

        internal void Show(MathQuickBarMode mode, IReadOnlyList<EquationCommand> commands)
        {
            if (mode == Mode && mode == MathQuickBarMode.Completions && commands.Select(c => c.Name).SequenceEqual(completions.Select(c => c.Name)))
                return;
            Mode = mode;
            completions = commands;
            selectedIndex = 0;
            panel.Children.Clear();
            switch (mode)
            {
                case MathQuickBarMode.Completions:
                    for (var i = 0; i < commands.Count; i++)
                    {
                        var command = commands[i];
                        var button = Button(command.Symbol.Length > 0 ? command.Symbol + "  \\" + command.Name : command.Text, "\\" + command.Name, () => owner.Editor.Complete(command));
                        button.HorizontalContentAlignment = HorizontalAlignment.Left;
                        button.MinWidth = 130;
                        panel.Children.Add(button);
                    }
                    Highlight();
                    break;
                case MathQuickBarMode.Selection:
                    var e = owner.Editor;
                    panel.Children.Add(Button("a/b", "Fraction of the selection", () => e.InsertStructure("mfrac")));
                    panel.Children.Add(Button("√x", "Root of the selection", () => e.InsertStructure("msqrt")));
                    panel.Children.Add(Button("xⁿ", "Superscript", () => e.InsertStructure("msup")));
                    panel.Children.Add(Button("xₙ", "Subscript", () => e.InsertStructure("msub")));
                    panel.Children.Add(Button("(x)", "Parentheses", () => e.InsertFences()));
                    panel.Children.Add(Button("[x]", "Brackets", () => e.InsertFences("[", "]")));
                    panel.Children.Add(Button("|x|", "Absolute value", () => e.InsertFences("|", "|")));
                    panel.Children.Add(Button("x̄", "Overline", () => e.InsertStructure("menclose", Notation("top"))));
                    panel.Children.Add(Button("▭", "Box", () => e.InsertStructure("menclose", Notation("box"))));
                    panel.Children.Add(Button("Linear", "Build down the selection", () => e.BuildDown()));
                    break;
                case MathQuickBarMode.Table:
                    var t = owner.Editor;
                    panel.Children.Add(Button("+ row", "Insert a row below", () => t.InsertTableRow()));
                    panel.Children.Add(Button("+ row ↑", "Insert a row above", () => t.InsertTableRow(above: true)));
                    panel.Children.Add(Button("+ col", "Insert a column on the right", () => t.InsertTableColumn()));
                    panel.Children.Add(Button("+ col ←", "Insert a column on the left", () => t.InsertTableColumn(before: true)));
                    panel.Children.Add(Button("− row", "Delete the row", () => t.DeleteTableRow()));
                    panel.Children.Add(Button("− col", "Delete the column", () => t.DeleteTableColumn()));
                    break;
                case MathQuickBarMode.Symbols:
                    foreach (var symbol in owner.QuickSymbols)
                        panel.Children.Add(Button(symbol, symbol, () => owner.Editor.Insert(symbol), 30));
                    break;
            }
        }

        private static KeyValuePair<string, string>[] Notation(string notation) => new[] { new KeyValuePair<string, string>("notation", notation) };

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
                    button.Background = i == selectedIndex && Mode == MathQuickBarMode.Completions ? highlight : Brushes.Transparent;
            }
        }
    }
}
