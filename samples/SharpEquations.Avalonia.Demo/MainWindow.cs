namespace SharpEquations.Avalonia.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>
    /// The equation editor with the tools of the equations of Word: structures and symbols to insert, the input format
    /// (UnicodeMath or LaTeX), the linear and built up views, the number of the equation, copy in every format, and the
    /// equation as MathML, UnicodeMath, LaTeX and OMML as it is typed.
    /// </summary>
    public sealed class MainWindow : Window
    {
        private static readonly (string Name, string UnicodeMath)[] Examples =
        {
            ("Quadratic formula", "x=(-b±√(b^2-4ac))/2a"),
            ("Euler", "e^(iπ)+1=0"),
            ("Gauss integral", "∫_-∞^∞ e^(-x^2) ⅆx=√π"),
            ("Sum of squares", "∑_(k=1)^n k^2=(n(n+1)(2n+1))/6"),
            ("Binomial theorem", "(a+b)^n=∑_(k=0)^n (n¦k) a^k b^(n-k)"),
            ("Matrix inverse", "(■(a&b@c&d))^(-1)=1/(ad-bc) (■(d&-b@-c&a))"),
            ("Piecewise", "|x|={■(x&x≥0@-x&x<0)┤"),
            ("Limit", "lim┬(x→0) sin⁡x/x=1"),
            ("Maxwell", "∇×𝐁=μ_0 (𝐉+ε_0 ∂𝐄/∂t)"),
            ("Numbered", "E=mc^2#(1)"),
        };

        private readonly MathEditBox editor;
        private readonly MathView preview;
        private readonly TextBox mathML = Output();
        private readonly TextBox unicodeMath = Output();
        private readonly TextBox wordLaTeX = Output();
        private readonly TextBox standardLaTeX = Output();
        private readonly TextBox omml = Output();
        private readonly TextBlock status = new TextBlock { Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        private readonly CheckBox linear = new CheckBox { Content = "Linear (Ctrl+Shift+L)" };
        private readonly TextBox number = new TextBox { PlaceholderText = "(1)", Width = 70 };

        public MainWindow()
        {
            Title = "SharpEquations — equation editor";
            Width = 1100;
            Height = 820;

            editor = new MathEditBox
            {
                FontSize = 32,
                MinHeight = 100,
                Padding = new Thickness(16, 12),
                BorderBrush = Brushes.SteelBlue,
                BorderThickness = 1.5,
            };
            preview = new MathView { FontSize = 20 };
            editor.EquationChanged += (_, _) => Refresh();
            editor.Editor.Changed += (_, _) => status.Text = Status();

            var rows = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    Settings(),
                    Structures(),
                    Symbols(),
                    editor,
                    Actions(),
                    new TextBlock
                    {
                        Text = "Type UnicodeMath (a/b, x^2, \\sqrt(x), \\alpha, \\sum_(i=1)^n, (■(a&b@c&d))) or LaTeX (\\frac{a}{b}, \\sqrt{x}) and a space or an operator "
                            + "builds it up. Arrows move through the slots, Tab through the cells of a matrix; Backspace selects a structure, then deletes it; "
                            + "Ctrl+Z undoes. #(1) at the end, or \\tag{1}, numbers the equation.",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7,
                    },
                    Outputs(),
                },
            };
            Content = new DockPanel
            {
                Children =
                {
                    new Border { [DockPanel.DockProperty] = Dock.Bottom, Padding = new Thickness(16, 4), Child = status },
                    new ScrollViewer { Content = rows },
                },
            };
            Opened += (_, _) =>
            {
                editor.Equation = Equation.Parse(Examples[0].UnicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
                editor.Focus();
            };
        }

        private static TextBox Output() => new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,monospace"),
            FontSize = 13,
            MinHeight = 60,
            MaxHeight = 220,
        };

        private StackPanel Settings()
        {
            var format = new ComboBox { ItemsSource = new[] { "UnicodeMath", "LaTeX" }, SelectedIndex = 0 };
            format.SelectionChanged += (_, _) => editor.InputFormat = format.SelectedIndex == 1 ? EquationFormat.LaTeX : EquationFormat.UnicodeMath;
            linear.IsCheckedChanged += (_, _) => editor.ViewMode = linear.IsChecked == true ? MathViewMode.Linear : MathViewMode.Professional;
            var display = new CheckBox { Content = "Display", IsChecked = true };
            display.IsCheckedChanged += (_, _) => editor.IsDisplay = display.IsChecked == true;

            var font = new ComboBox { ItemsSource = new[] { "Default font", "Cambria Math", "Libertinus Math" }, SelectedIndex = 0 };
            font.SelectionChanged += (_, _) => editor.MathFontFamily = preview.MathFontFamily = font.SelectedIndex switch
            {
                1 => new FontFamily("Cambria Math"),
                2 => MathFonts.LibertinusMath,
                _ => null,
            };
            var size = new Slider { Minimum = 14, Maximum = 72, Value = 32, Width = 140 };
            size.PropertyChanged += (_, e) =>
            {
                if (e.Property == RangeBase.ValueProperty)
                    editor.FontSize = size.Value;
            };

            var examples = new ComboBox { PlaceholderText = "Examples", ItemsSource = Examples.Select(e => e.Name).ToList() };
            examples.SelectionChanged += (_, _) =>
            {
                if (examples.SelectedIndex >= 0)
                {
                    editor.Equation = Equation.Parse(Examples[examples.SelectedIndex].UnicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
                    editor.Focus();
                }
            };

            return Row(Label("Input"), format, linear, display, Label("Font"), font, size, Label("Load"), examples);
        }

        private StackPanel Structures()
        {
            var empty = MathNode.Row(Array.Empty<MathNode>());
            MathNode Op(string text) => MathNode.Row(new[] { MathNode.Token("mo", text) });
            MathNode Accent(string mark, bool under = false) => MathNode.Element(
                under ? "munder" : "mover",
                new[] { empty, Op(mark) },
                new[] { new KeyValuePair<string, string>(under ? "accentunder" : "accent", "true") });

            return Row(
                Tool("a/b", "Fraction", () => editor.Editor.InsertStructure("mfrac")),
                Tool("xⁿ", "Superscript", () => editor.Editor.InsertStructure("msup")),
                Tool("xₙ", "Subscript", () => editor.Editor.InsertStructure("msub")),
                Tool("xₙⁿ", "Subscript and superscript", () => editor.Editor.InsertStructure("msubsup")),
                Tool("√x", "Square root", () => editor.Editor.InsertStructure("msqrt")),
                Tool("ⁿ√x", "Root", () => editor.Editor.InsertStructure("mroot")),
                Tool("(x)", "Parentheses", () => editor.Editor.InsertFences()),
                Tool("[x]", "Brackets", () => editor.Editor.InsertFences("[", "]")),
                Tool("|x|", "Absolute value", () => editor.Editor.InsertFences("|", "|")),
                Tool("∑", "Sum with limits", () => editor.Editor.InsertTemplate(MathNode.Element("munderover", new[] { Op("∑"), empty, empty }))),
                Tool("∫", "Integral with limits", () => editor.Editor.InsertTemplate(MathNode.Element("msubsup", new[] { Op("∫"), empty, empty }))),
                Tool("lim", "Limit", () => editor.Editor.InsertTemplate(MathNode.Element("munder", new[] { MathNode.Row(new[] { MathNode.Token("mi", "lim") }), empty }))),
                Tool("x̂", "Hat", () => editor.Editor.InsertTemplate(Accent("^"))),
                Tool("x̄", "Overline", () => editor.Editor.InsertTemplate(Accent("¯"))),
                Tool("x⃗", "Vector", () => editor.Editor.InsertTemplate(Accent("→"))),
                Tool("(⋮)", "2×2 matrix", () => editor.Editor.InsertMatrix(2, 2, "(", ")")),
                Tool("[⋮]", "3×3 matrix", () => editor.Editor.InsertMatrix(3, 3, "[", "]")),
                Tool("{", "Cases", () => editor.Editor.InsertTemplate(MathNode.Row(new[]
                {
                    MathNode.Token("mo", "{"),
                    MathNode.Element("mtable", new[]
                    {
                        MathNode.Element("mtr", new[] { MathNode.Element("mtd", new[] { empty }), MathNode.Element("mtd", new[] { empty }) }),
                        MathNode.Element("mtr", new[] { MathNode.Element("mtd", new[] { empty }), MathNode.Element("mtd", new[] { empty }) }),
                    }, new[] { new KeyValuePair<string, string>("columnalign", "left") }),
                    MathNode.Token("mo", string.Empty),
                }))),
                Tool("▭", "Box", () => editor.Editor.InsertStructure("menclose", new[] { new KeyValuePair<string, string>("notation", "box") })));
        }

        private StackPanel Symbols()
        {
            var symbols = "α β γ δ θ λ μ π σ φ ω Γ Δ Σ Ω ∞ ± ∓ × · ÷ ≤ ≥ ≠ ≈ ≡ ∝ → ⇒ ⇔ ∈ ∉ ⊂ ⊆ ∪ ∩ ∀ ∃ ∂ ∇ ℝ ℕ ℤ ℂ".Split(' ');
            return Row(symbols.Select(s => (Control)Tool(s, s, () => editor.Editor.Insert(s), 34)).ToArray());
        }

        private StackPanel Actions()
        {
            var copyFormat = new ComboBox { ItemsSource = new[] { "Copy as input format", "Copy as MathML (Word)", "Copy as UnicodeMath", "Copy as LaTeX" }, SelectedIndex = 0 };
            copyFormat.SelectionChanged += (_, _) => editor.CopyTextFormat = copyFormat.SelectedIndex switch
            {
                1 => EquationFormat.MathML,
                2 => EquationFormat.UnicodeMath,
                3 => EquationFormat.LaTeX,
                _ => null,
            };

            var apply = Tool("Number", "Number the equation (empty: remove the number)", () =>
                editor.EquationNumber = string.IsNullOrWhiteSpace(number.Text) ? null : number.Text!.Trim(), 70);

            return Row(
                Tool("↶ Undo", "Undo (Ctrl+Z)", () => editor.Editor.Undo(), 70),
                Tool("↷ Redo", "Redo (Ctrl+Y)", () => editor.Editor.Redo(), 70),
                Tool("Build up", "Build up what is typed (Enter)", () => editor.BuildUp(), 80),
                Tool("Build down", "The selection, or the equation, as linear text", () => editor.BuildDown(), 90),
                Tool("Select all", "Ctrl+A", () => editor.Editor.SelectAll(), 80),
                Tool("Copy", "Copy the selection (Ctrl+C)", () => _ = editor.CopyAsync(), 60),
                Tool("Paste", "Paste MathML, UnicodeMath or LaTeX (Ctrl+V)", () => _ = editor.PasteAsync(), 60),
                copyFormat,
                Label("Number"),
                number,
                apply,
                Tool("Clear", "An empty equation", () => editor.Equation = null, 60));
        }

        private StackPanel Outputs()
        {
            var tabs = new TabControl
            {
                ItemsSource = new[]
                {
                    new TabItem { Header = "MathML", Content = mathML },
                    new TabItem { Header = "UnicodeMath", Content = unicodeMath },
                    new TabItem { Header = "LaTeX (Word)", Content = wordLaTeX },
                    new TabItem { Header = "LaTeX", Content = standardLaTeX },
                    new TabItem { Header = "OMML", Content = omml },
                },
            };
            return new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Label("MathView (read only)"),
                    new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = preview },
                    tabs,
                },
            };
        }

        private void Refresh()
        {
            var equation = editor.Equation;
            linear.IsChecked = editor.ViewMode == MathViewMode.Linear;
            if (editor.EquationNumber is { } n && number.Text != n)
                number.Text = n;
            preview.Equation = equation;
            mathML.Text = equation?.ToString(EquationFormat.MathML);
            unicodeMath.Text = equation?.ToString(EquationFormat.UnicodeMath);
            wordLaTeX.Text = equation?.ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word });
            standardLaTeX.Text = equation?.ToString(EquationFormat.LaTeX);
            omml.Text = equation?.ToOmml().ToString();
            status.Text = Status();
        }

        private string Status()
        {
            var e = editor.Editor;
            var selection = e.Selection is { } s ? $"  ·  selection {s.Start}–{s.End} in /{string.Join("/", s.Path)}" : string.Empty;
            return $"{e.InputFormat}  ·  {(e.IsLinear ? "linear" : "built up")}  ·  caret {e.Caret}{selection}  ·  undo {(e.CanUndo ? "yes" : "no")}";
        }

        private static StackPanel Row(params Control[] children)
        {
            var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 6 };
            foreach (var child in children)
                row.Children.Add(child);
            return new StackPanel { Children = { row } };
        }

        private static TextBlock Label(string text) => new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };

        private Button Tool(string content, string tip, Action action, double minWidth = 44)
        {
            var button = new Button
            {
                Content = content,
                MinWidth = minWidth,
                Focusable = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            ToolTip.SetTip(button, tip);
            button.Click += (_, _) =>
            {
                action();
                editor.Focus();
            };
            return button;
        }
    }
}
