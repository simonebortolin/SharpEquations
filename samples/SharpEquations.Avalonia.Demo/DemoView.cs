namespace SharpEquations.Avalonia.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Input;
    using global::Avalonia.Layout;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Immutable;
    using global::Avalonia.Styling;
    using SharpEquations;
    using SharpEquations.Editing;

    /// <summary>
    /// The equation editor with the tools of the equations of Word: structures and symbols to insert, the input format
    /// (UnicodeMath or LaTeX), the linear and built up views, the number of the equation, copy in every format, a gallery
    /// of examples, and the equation as MathML, UnicodeMath, LaTeX and OMML as it is typed. The desktop window and the
    /// WebAssembly page show the same view.
    /// </summary>
    public sealed class DemoView : UserControl
    {
        private const string RepositoryUrl = "https://github.com/simonebortolin/SharpEquations";

        private static readonly (string Name, string UnicodeMath)[] Examples =
        {
            ("Quadratic formula", "x=(-b±√(b^2-4ac))/2a"),
            ("Euler's identity", "e^(iπ)+1=0"),
            ("Gaussian integral", "∫_-∞^∞ e^(-x^2) ⅆx=√π"),
            ("Sum of squares", "∑_(k=1)^n k^2=(n(n+1)(2n+1))/6"),
            ("Binomial theorem", "(a+b)^n=∑_(k=0)^n (n¦k) a^k b^(n-k)"),
            ("Matrix inverse", "(■(a&b@c&d))^(-1)=1/(ad-bc) (■(d&-b@-c&a))"),
            ("Absolute value", "|x|={■(x&x≥0@-x&x<0)┤"),
            ("A limit", "lim┬(x→0) sin⁡x/x=1"),
            ("Ampère–Maxwell", "∇×𝐁=μ_0 (𝐉+ε_0 ∂𝐄/∂t)"),
            ("Fourier transform", "f̂(ξ)=∫_-∞^∞ f(x) e^(-2πixξ) ⅆx"),
            ("Numbered", "E=mc^2#(1)"),
            ("Aligned lines", "█(f(x)&=(x+1)^2@&=x^2+2x+1)"),
        };

        private readonly MathEditBox editor;
        private readonly MathView preview;
        private readonly List<MathView> gallery = new List<MathView>();
        private readonly (string Name, Func<Equation, string> Write, TextBox Box)[] outputs;
        private readonly TabControl outputTabs = new TabControl();
        private readonly TextBlock status = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly ToggleSwitch linear = new ToggleSwitch { OnContent = "Linear", OffContent = "Built up" };
        private readonly TextBox number = new TextBox { PlaceholderText = "(1)", Width = 64 };
        private readonly Grid columns = new Grid();
        private readonly Control outputCard;
        private readonly Control examplesCard;

        public DemoView()
        {
            Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
            {
                ["PageBrush"] = Solid(0xF3F4FA),
                ["CardBrush"] = Solid(0xFFFFFF),
                ["CardBorderBrush"] = Solid(0xE3E5F0),
                ["MutedBrush"] = Solid(0x6B7280),
                ["EditorBrush"] = Solid(0xFCFCFF),
                ["CodeBrush"] = Solid(0xF6F7FB),
                ["ChipBrush"] = Solid(0xEEF0FB),
                ["EditorBorderBrush"] = Solid(0x818CF8),
            };
            Resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
            {
                ["PageBrush"] = Solid(0x0D0F16),
                ["CardBrush"] = Solid(0x161923),
                ["CardBorderBrush"] = Solid(0x262A38),
                ["MutedBrush"] = Solid(0x9AA3B5),
                ["EditorBrush"] = Solid(0x10131B),
                ["CodeBrush"] = Solid(0x0F121A),
                ["ChipBrush"] = Solid(0x1E2230),
                ["EditorBorderBrush"] = Solid(0x6366F1),
            };
            Styles.Add(new Style(x => x.OfType<Button>().Class("tool"))
            {
                Setters =
                {
                    new Setter(Button.CornerRadiusProperty, new CornerRadius(8)),
                    new Setter(Button.PaddingProperty, new Thickness(8, 5)),
                    new Setter(Button.MinHeightProperty, 34d),
                    new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                },
            });
            Styles.Add(new Style(x => x.OfType<Button>().Class("symbol"))
            {
                Setters =
                {
                    new Setter(Button.FontFamilyProperty, MathFonts.LibertinusMath),
                    new Setter(Button.FontSizeProperty, 17d),
                    new Setter(Button.MinWidthProperty, 38d),
                    new Setter(Button.PaddingProperty, new Thickness(4, 2)),
                },
            });
            Styles.Add(new Style(x => x.OfType<Button>().Class("formula"))
            {
                Setters =
                {
                    new Setter(Button.MinHeightProperty, 44d),
                    new Setter(Button.PaddingProperty, new Thickness(8, 4)),
                },
            });
            Styles.Add(new Style(x => x.OfType<Button>().Class("example"))
            {
                Setters =
                {
                    new Setter(Button.CornerRadiusProperty, new CornerRadius(12)),
                    new Setter(Button.PaddingProperty, new Thickness(12, 10)),
                    new Setter(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                    new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                },
            });

            editor = new MathEditBox
            {
                FontSize = 34,
                MinHeight = 120,
                Padding = new Thickness(22, 18),
                BorderThickness = 0,
            };
            editor.Bind(MathEditBox.BackgroundProperty, editor.GetResourceObservable("EditorBrush"));
            preview = new MathView { FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center };
            outputs = new (string, Func<Equation, string>, TextBox)[]
            {
                ("MathML", e => e.ToString(EquationFormat.MathML), Code()),
                ("UnicodeMath", e => e.ToString(EquationFormat.UnicodeMath), Code()),
                ("LaTeX", e => e.ToString(EquationFormat.LaTeX), Code()),
                ("LaTeX (Word)", e => e.ToString(EquationFormat.LaTeX, new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word }), Code()),
                ("OMML", e => e.ToOmml().ToString(), Code()),
            };
            editor.EquationChanged += (_, _) => Refresh();
            editor.Editor.Changed += (_, _) => status.Text = Status();

            outputCard = OutputCard();
            examplesCard = ExamplesCard();
            columns.Children.Add(outputCard);
            columns.Children.Add(examplesCard);
            columns.SizeChanged += (_, e) => Arrange(e.NewSize.Width);

            var page = new StackPanel
            {
                Spacing = 20,
                Margin = new Thickness(20, 24, 20, 32),
                MaxWidth = 1240,
                Children = { EditorCard(), columns },
            };
            var statusBar = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 6),
                Child = status,
            };
            statusBar.Bind(Border.BorderBrushProperty, statusBar.GetResourceObservable("CardBorderBrush"));
            statusBar.Bind(Border.BackgroundProperty, statusBar.GetResourceObservable("CardBrush"));
            status.Bind(TextBlock.ForegroundProperty, status.GetResourceObservable("MutedBrush"));

            var root = new DockPanel
            {
                Children =
                {
                    Docked(Header(), Dock.Top),
                    Docked(statusBar, Dock.Bottom),
                    new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                },
            };
            root.Bind(Panel.BackgroundProperty, root.GetResourceObservable("PageBrush"));
            Content = root;

            AttachedToVisualTree += (_, _) =>
            {
                if (editor.Equation == null)
                    Load(Examples[0].UnicodeMath);
            };
        }

        private Border Header()
        {
            var title = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "SharpEquations", FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                    new TextBlock
                    {
                        Text = "A real-time equation editor in .NET: type UnicodeMath or LaTeX and watch it build up, as in Word.",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.White, 0.85),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            };

            var hero = new MathView
            {
                FontSize = 26,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24, 0),
                Equation = Equation.Parse("∮_∂Σ 𝐁⋅ⅆ𝐥=μ_0 ∬_Σ 𝐉⋅ⅆ𝐒", EquationFormat.UnicodeMath, new EquationOptions { Display = true }),
            };

            var theme = HeaderButton("☾  Theme", "Light or dark theme");
            theme.Click += (_, _) =>
            {
                if (Application.Current is { } app)
                    app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
            };
            var github = HeaderButton("GitHub ↗", RepositoryUrl);
            github.Click += async (_, _) =>
            {
                if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
                    await launcher.LaunchUriAsync(new Uri(RepositoryUrl)).ConfigureAwait(true);
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { theme, github } };
            var bar = new DockPanel { Children = { Docked(buttons, Dock.Right), Docked(hero, Dock.Right), title } };
            bar.SizeChanged += (_, e) =>
            {
                var narrow = e.NewSize.Width < 640;
                hero.IsVisible = e.NewSize.Width > 900;
                DockPanel.SetDock(buttons, narrow ? Dock.Bottom : Dock.Right);
                buttons.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                buttons.Margin = narrow ? new Thickness(0, 14, 0, 0) : default;
            };

            return new Border
            {
                Padding = new Thickness(28, 22),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(0x43, 0x38, 0xCA), 0),
                        new GradientStop(Color.FromRgb(0x7C, 0x3A, 0xED), 0.55),
                        new GradientStop(Color.FromRgb(0xC0, 0x26, 0xD3), 1),
                    },
                },
                Child = bar,
            };
        }

        private static Button HeaderButton(string text, string tip)
        {
            var button = new Button
            {
                Content = text,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Colors.White, 0.16),
                BorderBrush = new SolidColorBrush(Colors.White, 0.35),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14, 6),
            };
            ToolTip.SetTip(button, tip);
            return button;
        }

        private Border EditorCard()
        {
            var ribbon = new TabControl
            {
                Padding = new Thickness(0, 8, 0, 0),
                ItemsSource = new[]
                {
                    new TabItem { Header = Small("Structures"), Content = Structures() },
                    new TabItem { Header = Small("Symbols"), Content = Symbols("∞ ± ∓ × · ÷ ≤ ≥ ≠ ≈ ≡ ≅ ∝ ∼ ≪ ≫ ∂ ∇ ∀ ∃ ∄ ∅ ∈ ∉ ⊂ ⊆ ⊃ ∪ ∩ ∧ ∨ ¬ ⊕ ⊗ ∘ † ℏ ℓ ℝ ℕ ℤ ℚ ℂ") },
                    new TabItem { Header = Small("Greek"), Content = Symbols("α β γ δ ε ζ η θ ϑ ι κ λ μ ν ξ π ρ σ τ υ φ χ ψ ω Γ Δ Θ Λ Ξ Π Σ Φ Ψ Ω") },
                    new TabItem { Header = Small("Arrows"), Content = Symbols("→ ← ↑ ↓ ↔ ↦ ⇒ ⇐ ⇔ ⟶ ⟹ ⟺ ↗ ↘ ⇀ ⇌ ∴ ∵ ⋯ ⋮ ⋱ … ′ ″ ∠ ⊥ ∥ ° ∑ ∏ ∫ ∬ ∮ ⋃ ⋂") },
                    new TabItem { Header = Small("Tables"), Content = Tables() },
                },
            };

            var editorFrame = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1.5),
                ClipToBounds = true,
                Child = editor,
            };
            editorFrame.Bind(Border.BorderBrushProperty, editorFrame.GetResourceObservable("EditorBorderBrush"));

            var hint = new TextBlock
            {
                Text = "Type a/b, x^2, \\sqrt(x), \\alpha, \\sum_(i=1)^n or (■(a&b@c&d)) — or \\frac{a}{b} in LaTeX — and a space or an operator builds it up. "
                    + "Arrows move through the slots, Tab through the cells; Backspace selects a structure, then deletes it. #(1) or \\tag{1} numbers the equation, "
                    + "Shift+Enter starts a new line, Ctrl+Shift+L toggles the linear view. Type \\ and the floating bar completes the command.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                LineHeight = 19,
            };
            hint.Bind(TextBlock.ForegroundProperty, hint.GetResourceObservable("MutedBrush"));

            return Card("Editor", "MathEditBox: the equation of Word, in Avalonia", new StackPanel
            {
                Spacing = 14,
                Children = { Settings(), ribbon, editorFrame, Actions(), hint },
            });
        }

        private Border OutputCard()
        {
            outputTabs.ItemsSource = outputs.Select(o => new TabItem { Header = Small(o.Name), Content = o.Box }).ToList();
            var copy = Tool("Copy", "Copy this format", async () =>
            {
                if (outputTabs.SelectedIndex >= 0 && outputs[outputTabs.SelectedIndex].Box.Text is { Length: > 0 } text
                    && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                {
                    var item = new global::Avalonia.Input.DataTransferItem();
                    item.SetText(text);
                    var data = new global::Avalonia.Input.DataTransfer();
                    data.Add(item);
                    await clipboard.SetDataAsync(data).ConfigureAwait(true);
                }
            }, 64);
            copy.HorizontalAlignment = HorizontalAlignment.Right;
            copy.VerticalAlignment = VerticalAlignment.Top;

            var previewFrame = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                MinHeight = 80,
                Child = preview,
            };
            previewFrame.Bind(Border.BackgroundProperty, previewFrame.GetResourceObservable("CodeBrush"));

            return Card("Output", "MathView, read only, and the equation in every format", new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    previewFrame,
                    new Grid { Children = { outputTabs, copy } },
                },
            });
        }

        private Border ExamplesCard()
        {
            var list = new StackPanel { Spacing = 8 };
            foreach (var (name, unicodeMath) in Examples)
            {
                var caption = Small(name);
                caption.FontSize = 11.5;
                caption.Bind(TextBlock.ForegroundProperty, caption.GetResourceObservable("MutedBrush"));
                var button = new Button
                {
                    Classes = { "example" },
                    BorderThickness = new Thickness(1),
                    Focusable = false,
                    Content = new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            caption,
                            Gallery(unicodeMath),
                        },
                    },
                };
                button.Bind(Button.BackgroundProperty, button.GetResourceObservable("CodeBrush"));
                button.Bind(Button.BorderBrushProperty, button.GetResourceObservable("CardBorderBrush"));
                button.Click += (_, _) =>
                {
                    Load(unicodeMath);
                    editor.Focus();
                };
                list.Children.Add(button);
            }
            return Card("Examples", "Click one to edit it", list);
        }

        private MathView Gallery(string unicodeMath)
        {
            var view = new MathView
            {
                FontSize = 17,
                HorizontalAlignment = HorizontalAlignment.Left,
                Equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true }),
            };
            gallery.Add(view);
            return view;
        }

        private void Arrange(double width)
        {
            var wide = width >= 900;
            columns.ColumnDefinitions = wide ? new ColumnDefinitions("3*,20,2*") : new ColumnDefinitions("*");
            columns.RowDefinitions = wide ? new RowDefinitions("Auto") : new RowDefinitions("Auto,20,Auto");
            Grid.SetColumn(outputCard, 0);
            Grid.SetRow(outputCard, 0);
            Grid.SetColumn(examplesCard, wide ? 2 : 0);
            Grid.SetRow(examplesCard, wide ? 0 : 2);
        }

        private void Load(string unicodeMath)
            => editor.Equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath, new EquationOptions { Display = true });

        private WrapPanel Settings()
        {
            var format = new ComboBox { ItemsSource = new[] { "UnicodeMath", "LaTeX" }, SelectedIndex = 0, MinWidth = 130 };
            format.SelectionChanged += (_, _) => editor.InputFormat = format.SelectedIndex == 1 ? EquationFormat.LaTeX : EquationFormat.UnicodeMath;
            linear.IsCheckedChanged += (_, _) => editor.ViewMode = linear.IsChecked == true ? MathViewMode.Linear : MathViewMode.Professional;
            var display = new ToggleSwitch { OnContent = "Display", OffContent = "Inline", IsChecked = true };
            display.IsCheckedChanged += (_, _) => editor.IsDisplay = display.IsChecked == true;
            var quickBar = new ToggleSwitch { OnContent = "Quick bar", OffContent = "Quick bar", IsChecked = true };
            quickBar.IsCheckedChanged += (_, _) => editor.IsQuickBarEnabled = quickBar.IsChecked == true;

            // the default is Cambria Math on Windows and Libertinus Math elsewhere: STIX Two Math, embedded in the demo,
            // shows the change of font everywhere
            var fonts = new List<(string Name, FontFamily? Family)>
            {
                ($"Default ({MathFonts.Default.Typeface.FontFamily.FamilyNames[0].Split('#').Last()})", null),
                ("Libertinus Math", MathFonts.LibertinusMath),
                ("STIX Two Math", DemoFonts.StixTwoMath),
            };
            if (OperatingSystem.IsWindows() && MathFonts.TryGet(new FontFamily("Cambria Math"), out _))
                fonts.Add(("Cambria Math", new FontFamily("Cambria Math")));
            var font = new ComboBox { ItemsSource = fonts.Select(f => f.Name).ToList(), SelectedIndex = 0, MinWidth = 170 };
            font.SelectionChanged += (_, _) =>
            {
                var family = fonts[Math.Max(0, font.SelectedIndex)].Family;
                editor.MathFontFamily = preview.MathFontFamily = family;
                foreach (var view in gallery)
                    view.MathFontFamily = family;
            };
            var size = new Slider { Minimum = 16, Maximum = 72, Value = 34, Width = 130, VerticalAlignment = VerticalAlignment.Center };
            size.PropertyChanged += (_, e) =>
            {
                if (e.Property == RangeBase.ValueProperty)
                    editor.FontSize = size.Value;
            };

            return Row(Field("Input", format), Field("Font", font), Field("Size", size), linear, display, quickBar);
        }

        private WrapPanel Structures()
        {
            var empty = MathNode.Row(Array.Empty<MathNode>());
            MathNode Op(string text) => MathNode.Row(new[] { MathNode.Token("mo", text) });
            MathNode Accent(string mark, bool under = false) => MathNode.Element(
                under ? "munder" : "mover",
                new[] { empty, Op(mark) },
                new[] { new KeyValuePair<string, string>(under ? "accentunder" : "accent", "true") });

            return Row(
                Formula("a/b", "Fraction", () => editor.Editor.InsertStructure("mfrac")),
                Formula("x^n", "Superscript", () => editor.Editor.InsertStructure("msup")),
                Formula("x_n", "Subscript", () => editor.Editor.InsertStructure("msub")),
                Formula("x_n^n", "Subscript and superscript", () => editor.Editor.InsertStructure("msubsup")),
                Formula("√x", "Square root", () => editor.Editor.InsertStructure("msqrt")),
                Formula("√(n&x)", "Root", () => editor.Editor.InsertStructure("mroot")),
                Formula("(x)", "Parentheses", () => editor.Editor.InsertFences()),
                Formula("[x]", "Brackets", () => editor.Editor.InsertFences("[", "]")),
                Formula("\\{x\\}", "Braces", () => editor.Editor.InsertFences("{", "}")),
                Formula("|x|", "Absolute value", () => editor.Editor.InsertFences("|", "|")),
                Formula("∑_(k=1)^n", "Sum with limits", () => editor.Editor.InsertTemplate(MathNode.Element("munderover", new[] { Op("∑"), empty, empty }))),
                Formula("∏_(k=1)^n", "Product with limits", () => editor.Editor.InsertTemplate(MathNode.Element("munderover", new[] { Op("∏"), empty, empty }))),
                Formula("∫_a^b", "Integral with limits", () => editor.Editor.InsertTemplate(MathNode.Element("msubsup", new[] { Op("∫"), empty, empty }))),
                Formula("lim┬(x→∞)", "Limit", () => editor.Editor.InsertTemplate(MathNode.Element("munder", new[] { MathNode.Row(new[] { MathNode.Token("mi", "lim") }), empty }))),
                Formula("x̂", "Hat", () => editor.Editor.InsertTemplate(Accent("^"))),
                Formula("x̄", "Overline", () => editor.Editor.InsertTemplate(Accent("¯"))),
                Formula("x⃗", "Vector", () => editor.Editor.InsertTemplate(Accent("→"))),
                Formula("ẋ", "Dot", () => editor.Editor.InsertTemplate(Accent("˙"))),
                Formula("▭(x)", "Box", () => editor.Editor.InsertStructure("menclose", new[] { new KeyValuePair<string, string>("notation", "box") })),
                Symbol("↵", "New line (Shift+Enter)", () => editor.Editor.InsertLineBreak()));
        }

        private WrapPanel Tables()
        {
            var empty = MathNode.Row(Array.Empty<MathNode>());
            return Row(
                Formula("(■(a&b@c&d))", "2×2 matrix", () => editor.Editor.InsertMatrix(2, 2, "(", ")")),
                Formula("[■(a&b&c@d&e&f@g&h&i)]", "3×3 matrix", () => editor.Editor.InsertMatrix(3, 3, "[", "]")),
                Formula("|■(a&b@c&d)|", "Determinant", () => editor.Editor.InsertMatrix(2, 2, "|", "|")),
                Formula("{■(x&x≥0@-x&x<0)┤", "Cases", () => editor.Editor.InsertTemplate(MathNode.Row(new[]
                {
                    MathNode.Token("mo", "{"),
                    MathNode.Element("mtable", new[]
                    {
                        MathNode.Element("mtr", new[] { MathNode.Element("mtd", new[] { empty }), MathNode.Element("mtd", new[] { empty }) }),
                        MathNode.Element("mtr", new[] { MathNode.Element("mtd", new[] { empty }), MathNode.Element("mtd", new[] { empty }) }),
                    }, new[] { new KeyValuePair<string, string>("columnalign", "left") }),
                    MathNode.Token("mo", string.Empty),
                }))),
                Tool("+ Row", "Insert a row in the table", () => editor.Editor.InsertTableRow(), 70),
                Tool("+ Column", "Insert a column in the table", () => editor.Editor.InsertTableColumn(), 86),
                Tool("− Row", "Delete the row of the table", () => editor.Editor.DeleteTableRow(), 70),
                Tool("− Column", "Delete the column of the table", () => editor.Editor.DeleteTableColumn(), 86));
        }

        private WrapPanel Symbols(string symbols)
            => Row(symbols.Split(' ').Select(s => (Control)Symbol(s, s, () => editor.Editor.Insert(s))).ToArray());

        private WrapPanel Actions()
        {
            var copyFormat = new ComboBox { ItemsSource = new[] { "Copy as typed", "Copy as MathML", "Copy as UnicodeMath", "Copy as LaTeX" }, SelectedIndex = 0 };
            copyFormat.SelectionChanged += (_, _) => editor.CopyTextFormat = copyFormat.SelectedIndex switch
            {
                1 => EquationFormat.MathML,
                2 => EquationFormat.UnicodeMath,
                3 => EquationFormat.LaTeX,
                _ => null,
            };
            var apply = Tool("Number", "Number the equation (empty: remove the number)", () =>
                editor.EquationNumber = string.IsNullOrWhiteSpace(number.Text) ? null : number.Text!.Trim(), 72);

            return Row(
                Tool("↶ Undo", "Undo (Ctrl+Z)", () => editor.Editor.Undo(), 72),
                Tool("↷ Redo", "Redo (Ctrl+Y)", () => editor.Editor.Redo(), 72),
                Tool("Build up", "Build up what is typed (Enter)", () => editor.BuildUp(), 80),
                Tool("Build down", "The selection, or the equation, as linear text", () => editor.BuildDown(), 92),
                Tool("Select all", "Ctrl+A", () => editor.Editor.SelectAll(), 84),
                Tool("Copy", "Copy the selection (Ctrl+C)", () => _ = editor.CopyAsync(), 60),
                Tool("Paste", "Paste MathML, UnicodeMath or LaTeX (Ctrl+V)", () => _ = editor.PasteAsync(), 60),
                copyFormat,
                number,
                apply,
                Tool("Clear", "An empty equation", () => editor.Equation = null, 60));
        }

        private void Refresh()
        {
            var equation = editor.Equation;
            linear.IsChecked = editor.ViewMode == MathViewMode.Linear;
            if (editor.EquationNumber is { } n && number.Text != n)
                number.Text = n;
            preview.Equation = equation;
            foreach (var (_, write, box) in outputs)
                box.Text = equation == null ? null : write(equation);
            status.Text = Status();
        }

        private string Status()
        {
            var e = editor.Editor;
            var selection = e.Selection is { } s ? $"  ·  selection {s.Start}–{s.End} in /{string.Join("/", s.Path)}" : string.Empty;
            return $"{e.InputFormat}  ·  {(e.IsLinear ? "linear" : "built up")}  ·  caret {e.Caret}{selection}  ·  undo {(e.CanUndo ? "yes" : "no")}";
        }

        private static Border Card(string title, string subtitle, Control content)
        {
            var heading = new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold };
            var caption = new TextBlock { Text = subtitle, FontSize = 12.5 };
            caption.Bind(TextBlock.ForegroundProperty, caption.GetResourceObservable("MutedBrush"));
            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22, 18, 22, 22),
                BoxShadow = BoxShadows.Parse("0 6 28 0 #14000000"),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children = { new StackPanel { Spacing = 2, Children = { heading, caption } }, content },
                },
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CardBrush"));
            card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("CardBorderBrush"));
            return card;
        }

        private static TextBox Code()
        {
            var box = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,monospace"),
                FontSize = 12.5,
                MinHeight = 120,
                MaxHeight = 260,
                Padding = new Thickness(12, 10),
                CornerRadius = new CornerRadius(10),
            };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            return box;
        }

        private static StackPanel Field(string label, Control control)
        {
            var text = Small(label);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("MutedBrush"));
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { text, control } };
        }

        private static Control Docked(Control control, Dock dock)
        {
            DockPanel.SetDock(control, dock);
            return control;
        }

        private static TextBlock Small(string text) => new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeight.SemiBold };

        private static ImmutableSolidColorBrush Solid(uint rgb) => new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000 | rgb));

        private static WrapPanel Row(params Control[] children)
        {
            var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
            foreach (var child in children)
            {
                child.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(child);
            }
            return row;
        }

        private Button Formula(string unicodeMath, string tip, Action action)
        {
            var button = Tool(unicodeMath, tip, action, 44);
            button.Classes.Add("formula");
            if (Equation.TryParse(unicodeMath, EquationFormat.UnicodeMath, out var equation))
                button.Content = new MathView { Equation = equation, FontSize = 17, IsHitTestVisible = false };
            return button;
        }

        private Button Symbol(string content, string tip, Action action)
        {
            var button = Tool(content, tip, action, 38);
            button.Classes.Add("symbol");
            return button;
        }

        private Button Tool(string content, string tip, Action action, double minWidth = 44)
        {
            var button = new Button { Content = content, MinWidth = minWidth, Focusable = false, Classes = { "tool" } };
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
