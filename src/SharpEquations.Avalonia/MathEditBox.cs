namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Automation.Peers;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Controls.Documents;
    using global::Avalonia.Data;
    using global::Avalonia.Input;
    using global::Avalonia.Input.Platform;
    using global::Avalonia.Input.TextInput;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Immutable;
    using global::Avalonia.Threading;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>
    /// A real-time equation editor, in the manner of the RichEditBox of WinUI in math mode and of the math zones of Word:
    /// UnicodeMath or LaTeX typed at the caret builds up into the equation when a space or an operator ends an argument,
    /// and the content is read and written as MathML (<see cref="GetMath"/>, <see cref="SetMath"/>) or as an
    /// <see cref="SharpEquations.Equation"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "The drawing is released when the equation is laid out again and when the control leaves the visual tree.")]
    public partial class MathEditBox : Control
    {
        /// <summary>The equation edited; null when the editor is empty. Two-way by default.</summary>
        public static readonly StyledProperty<Equation?> EquationProperty =
            AvaloniaProperty.Register<MathEditBox, Equation?>(nameof(Equation), defaultBindingMode: BindingMode.TwoWay);

        /// <summary>The format of what is typed: UnicodeMath (the default) or LaTeX.</summary>
        public static readonly StyledProperty<EquationFormat> InputFormatProperty =
            AvaloniaProperty.Register<MathEditBox, EquationFormat>(nameof(InputFormat), EquationFormat.UnicodeMath, validate: f => f is EquationFormat.UnicodeMath or EquationFormat.LaTeX);

        /// <summary>Whether the equation is shown built up or as linear text.</summary>
        public static readonly StyledProperty<MathViewMode> ViewModeProperty =
            AvaloniaProperty.Register<MathEditBox, MathViewMode>(nameof(ViewMode));

        /// <summary>Whether the equation is a display equation rather than inline.</summary>
        public static readonly StyledProperty<bool> IsDisplayProperty =
            AvaloniaProperty.Register<MathEditBox, bool>(nameof(IsDisplay), true);

        /// <summary>Whether the user cannot change the equation.</summary>
        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<MathEditBox, bool>(nameof(IsReadOnly));

        /// <summary>The size of the font.</summary>
        public static readonly StyledProperty<double> FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner<MathEditBox>();

        /// <summary>The brush of the equation.</summary>
        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner<MathEditBox>();

        /// <summary>The brush of the background.</summary>
        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<MathEditBox, IBrush?>(nameof(Background), Brushes.Transparent);

        /// <summary>The brush of the border.</summary>
        public static readonly StyledProperty<IBrush?> BorderBrushProperty =
            AvaloniaProperty.Register<MathEditBox, IBrush?>(nameof(BorderBrush));

        /// <summary>The thickness of the border.</summary>
        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<MathEditBox, double>(nameof(BorderThickness), 1);

        /// <summary>The space between the border and the equation.</summary>
        public static readonly StyledProperty<Thickness> PaddingProperty =
            AvaloniaProperty.Register<MathEditBox, Thickness>(nameof(Padding), new Thickness(6, 4));

        /// <summary>The brush of the selection.</summary>
        public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
            AvaloniaProperty.Register<MathEditBox, IBrush?>(nameof(SelectionBrush), new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0x33, 0x99, 0xFF)));

        /// <summary>The brush of the caret; null for the foreground.</summary>
        public static readonly StyledProperty<IBrush?> CaretBrushProperty =
            AvaloniaProperty.Register<MathEditBox, IBrush?>(nameof(CaretBrush));

        /// <summary>The brush of the boxes of the empty slots.</summary>
        public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
            AvaloniaProperty.Register<MathEditBox, IBrush?>(nameof(PlaceholderBrush), Brushes.Gray);

        /// <summary>The math font, a family with an OpenType MATH table; null for <see cref="MathFonts.Default"/>.</summary>
        public static readonly StyledProperty<FontFamily?> MathFontFamilyProperty =
            AvaloniaProperty.Register<MathEditBox, FontFamily?>(nameof(MathFontFamily));

        /// <summary>
        /// The format of the text that <see cref="CopyAsync"/> puts on the clipboard; null (the default) for the input
        /// format. MathML pastes as an equation into Word.
        /// </summary>
        public static readonly StyledProperty<EquationFormat?> CopyTextFormatProperty =
            AvaloniaProperty.Register<MathEditBox, EquationFormat?>(nameof(CopyTextFormat), validate: f => f != EquationFormat.Mtef);

        /// <summary>Whether the floating bar shows completions, structures, table commands and symbols at the caret.</summary>
        public static readonly StyledProperty<bool> IsQuickBarEnabledProperty =
            AvaloniaProperty.Register<MathEditBox, bool>(nameof(IsQuickBarEnabled), true);

        /// <summary>Whether the math autocorrect (<see cref="AutoCorrect"/>) replaces sequences by their symbols as they are typed.</summary>
        public static readonly StyledProperty<bool> IsAutoCorrectEnabledProperty =
            AvaloniaProperty.Register<MathEditBox, bool>(nameof(IsAutoCorrectEnabled), true);

        /// <summary>The gesture that switches between the built up and the linear view.</summary>
        public static readonly StyledProperty<KeyGesture?> ToggleViewModeGestureProperty =
            AvaloniaProperty.Register<MathEditBox, KeyGesture?>(nameof(ToggleViewModeGesture), new KeyGesture(Key.L, KeyModifiers.Control | KeyModifiers.Shift));

        private readonly DispatcherTimer blink;
        private readonly Popup popup;
        private readonly InputClient inputClient;
        private readonly CaretLayer caretLayer;
        private MathDrawing? drawing;
        private bool updatingEquation;
        private MathAutoCorrect autoCorrect;

        static MathEditBox()
        {
            FocusableProperty.OverrideDefaultValue<MathEditBox>(true);
            CursorProperty.OverrideDefaultValue<MathEditBox>(new Cursor(StandardCursorType.Ibeam));
            AffectsMeasure<MathEditBox>(FontSizeProperty, MathFontFamilyProperty, PaddingProperty, BorderThicknessProperty);
            AffectsRender<MathEditBox>(ForegroundProperty, BackgroundProperty, BorderBrushProperty, SelectionBrushProperty, CaretBrushProperty, PlaceholderBrushProperty);
        }

        /// <summary>An empty editor.</summary>
        public MathEditBox()
        {
            Editor = new MathEditor { IsDisplay = true };
            Editor.ContentChanged += OnEditorContentChanged;
            View = new MathEditorView(Editor);
            View.Changed += OnViewChanged;
            autoCorrect = Editor.AutoCorrect ?? new MathAutoCorrect();
            inputClient = new InputClient(this);
            TextInputMethodClientRequested += (_, e) => e.Client = inputClient;
            QuickBar = new MathQuickBar(this);
            popup = new Popup
            {
                Child = QuickBar,
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                IsLightDismissEnabled = false,
                Focusable = false,
            };
            LogicalChildren.Add(popup);
            VisualChildren.Add(popup);
            caretLayer = new CaretLayer(this);
            VisualChildren.Add(caretLayer);
            AddHandler(ContextRequestedEvent, OnContextRequested);
            blink = new DispatcherTimer { Interval = MathEditorView.CaretBlinkInterval };
            blink.Tick += (_, _) =>
            {
                View.BlinkCaret();
                caretLayer.InvalidateVisual();
            };
        }

        /// <summary>
        /// Raised when the selection is copied, before it goes to the clipboard: a handler can add formats to the item
        /// (OMML, a picture...) or cancel the copy.
        /// </summary>
        public event EventHandler<MathCopyingEventArgs>? Copying;

        /// <summary>Raised when the equation changes, by an edit or by <see cref="Equation"/>.</summary>
        public event EventHandler<EquationChangedEventArgs>? EquationChanged;

        /// <summary>The editing model: the tree of the equation, the caret and the selection, and the commands.</summary>
        public MathEditor Editor { get; }

        /// <summary>
        /// The view of the editor without a toolkit, which the control is built on: the layout and where it is drawn, the
        /// pointer, the floating bar, the context menu and the clipboard.
        /// </summary>
        public MathEditorView View { get; }

        /// <summary>The equation edited, with what is not built up yet built up where it parses; null when empty.</summary>
        public Equation? Equation
        {
            get => GetValue(EquationProperty);
            set => SetValue(EquationProperty, value);
        }

        /// <summary>The format of what is typed.</summary>
        public EquationFormat InputFormat
        {
            get => GetValue(InputFormatProperty);
            set => SetValue(InputFormatProperty, value);
        }

        /// <summary>Whether the equation is shown built up or as linear text.</summary>
        public MathViewMode ViewMode
        {
            get => GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        /// <summary>Whether the equation is a display equation rather than inline.</summary>
        public bool IsDisplay
        {
            get => GetValue(IsDisplayProperty);
            set => SetValue(IsDisplayProperty, value);
        }

        /// <summary>Whether the user cannot change the equation.</summary>
        public bool IsReadOnly
        {
            get => GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
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

        /// <summary>The brush of the background.</summary>
        public IBrush? Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        /// <summary>The brush of the border.</summary>
        public IBrush? BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        /// <summary>The thickness of the border.</summary>
        public double BorderThickness
        {
            get => GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        /// <summary>The space between the border and the equation.</summary>
        public Thickness Padding
        {
            get => GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        /// <summary>The brush of the selection.</summary>
        public IBrush? SelectionBrush
        {
            get => GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        /// <summary>The brush of the caret; null for the foreground.</summary>
        public IBrush? CaretBrush
        {
            get => GetValue(CaretBrushProperty);
            set => SetValue(CaretBrushProperty, value);
        }

        /// <summary>The brush of the boxes of the empty slots.</summary>
        public IBrush? PlaceholderBrush
        {
            get => GetValue(PlaceholderBrushProperty);
            set => SetValue(PlaceholderBrushProperty, value);
        }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public FontFamily? MathFontFamily
        {
            get => GetValue(MathFontFamilyProperty);
            set => SetValue(MathFontFamilyProperty, value);
        }

        /// <summary>The gesture that switches between the built up and the linear view (Ctrl+Shift+L by default).</summary>
        public KeyGesture? ToggleViewModeGesture
        {
            get => GetValue(ToggleViewModeGestureProperty);
            set => SetValue(ToggleViewModeGestureProperty, value);
        }

        /// <summary>Whether the floating bar (<see cref="QuickBar"/>) shows at the caret.</summary>
        public bool IsQuickBarEnabled
        {
            get => GetValue(IsQuickBarEnabledProperty);
            set => SetValue(IsQuickBarEnabledProperty, value);
        }

        /// <summary>The floating bar at the caret: completions of commands, structures for the selection, table commands, symbols.</summary>
        public MathQuickBar QuickBar { get; }

        /// <summary>Whether the floating bar is open.</summary>
        public bool IsQuickBarOpen => popup.IsOpen;

        /// <summary>The symbols the floating bar offers in an empty slot.</summary>
        public IList<string> QuickSymbols { get; } = new List<string> { "α", "β", "θ", "π", "∞", "±", "×", "≤", "≥", "≠", "≈", "→", "∈", "∂", "∑", "∫" };

        /// <summary>The format of the text copied to the clipboard; null for the input format.</summary>
        public EquationFormat? CopyTextFormat
        {
            get => GetValue(CopyTextFormatProperty);
            set => SetValue(CopyTextFormatProperty, value);
        }

        /// <summary>The layout of the equation, after a measure.</summary>
        public MathLayout? Layout => View.Layout;

        /// <summary>The MathML of the equation, as <c>RichEditTextDocument.GetMath</c>.</summary>
        public string GetMath() => Editor.ToMathML().ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

        /// <summary>Replaces the equation with MathML, as <c>RichEditTextDocument.SetMath</c>.</summary>
        /// <exception cref="FormatException">The MathML is not valid.</exception>
        public void SetMath(string mathML) => Editor.Load(string.IsNullOrWhiteSpace(mathML) ? null : Equation.Parse(mathML, EquationFormat.MathML));

        /// <summary>The equation in a format; for LaTeX, the LaTeX of Word.</summary>
        public string GetText(EquationFormat format) => Editor.GetText(format);

        /// <summary>
        /// The rules of the math autocorrect (<see cref="MathEditor.AutoCorrect"/>): <c>-&gt;</c> becomes → and
        /// <c>\alpha</c> and a space α as they are typed, with the entries of Word by default and those the application
        /// adds or removes; <see cref="IsAutoCorrectEnabled"/> turns it off.
        /// </summary>
        public MathAutoCorrect AutoCorrect
        {
            get => autoCorrect;
            set
            {
                autoCorrect = value ?? throw new ArgumentNullException(nameof(value));
                Editor.AutoCorrect = IsAutoCorrectEnabled ? value : null;
            }
        }

        /// <summary>Whether the math autocorrect (<see cref="AutoCorrect"/>) replaces sequences as they are typed; true by default.</summary>
        public bool IsAutoCorrectEnabled
        {
            get => GetValue(IsAutoCorrectEnabledProperty);
            set => SetValue(IsAutoCorrectEnabledProperty, value);
        }

        /// <summary>The number of the equation, shown at the right of its line; null without one (<see cref="MathEditor.EquationNumber"/>).</summary>
        public string? EquationNumber
        {
            get => Editor.EquationNumber;
            set => Editor.SetEquationNumber(value);
        }

        /// <summary>Builds up all that is typed and not built up yet.</summary>
        public void BuildUp() => Editor.BuildUpAll();

        /// <summary>Builds down the selection, or the whole equation, into linear text.</summary>
        public void BuildDown() => Editor.BuildDown();

        /// <summary>
        /// The selection in a format, or null when nothing is selected: UnicodeMath, LaTeX (as Word writes it unless the
        /// options say otherwise), MathML or OMML.
        /// </summary>
        public string? GetSelectedText(EquationFormat format, EquationOptions? options = null) => Editor.GetSelectedText(format, options);

        /// <summary>
        /// Copies the selection to the clipboard: as text in <see cref="CopyTextFormat"/> (the input format by default)
        /// and as MathML (<see cref="MathMLClipboardFormat"/>), with the formats a handler of <see cref="Copying"/> adds.
        /// </summary>
        public async Task CopyAsync()
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard || !View.TryGetCopy(CopyTextFormat, out var equation, out var text, out var mathML))
                return;
            var item = new DataTransferItem();
            item.SetText(text);
            item.Set(MathMLClipboardFormat, MathClipboard.Encode(mathML));
            var args = new MathCopyingEventArgs(equation, item);
            Copying?.Invoke(this, args);
            if (args.Cancel)
                return;
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data).ConfigureAwait(true);
        }

        /// <summary>Copies the selection to the clipboard and deletes it.</summary>
        public async Task CutAsync()
        {
            if (IsReadOnly)
                return;
            await CopyAsync().ConfigureAwait(true);
            Editor.DeleteSelection();
        }

        /// <summary>Pastes the clipboard: MathML, or text read in the input format (or as MathML), or plain text.</summary>
        public async Task PasteAsync()
        {
            if (IsReadOnly || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            var formats = await clipboard.GetDataFormatsAsync().ConfigureAwait(true);
            if (formats.Contains(MathMLClipboardFormat) && await clipboard.TryGetValueAsync(MathMLClipboardFormat).ConfigureAwait(true) is { Length: > 0 } bytes
                && View.PasteMathML(MathClipboard.Decode(bytes)))
            {
                return;
            }
            if (await clipboard.TryGetTextAsync().ConfigureAwait(true) is { Length: > 0 } text)
                View.PasteText(text);
        }

        /// <summary>
        /// The format of MathML on the clipboard, as bytes (<see cref="MathClipboard"/>): <c>MathML</c> on Windows, which
        /// Word reads and writes as UTF-8, <c>application/mathml+xml</c> elsewhere.
        /// </summary>
        public static DataFormat<byte[]> MathMLClipboardFormat { get; } =
            DataFormat.CreateBytesPlatformFormat(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "MathML" : "application/mathml+xml");

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == EquationProperty && !updatingEquation)
            {
                var equation = change.GetNewValue<Equation?>();
                Editor.Load(equation);
                if (equation != null)
                    SetCurrentValue(IsDisplayProperty, equation.IsDisplay);
            }
            else if (change.Property == InputFormatProperty)
            {
                Editor.InputFormat = change.GetNewValue<EquationFormat>();
            }
            else if (change.Property == ViewModeProperty)
            {
                Editor.SetLinear(change.GetNewValue<MathViewMode>() == MathViewMode.Linear);
            }
            else if (change.Property == IsDisplayProperty)
            {
                Editor.IsDisplay = change.GetNewValue<bool>();
            }
            else if (change.Property == IsFocusedProperty)
            {
                View.IsFocused = IsFocused;
                if (IsFocused)
                {
                    blink.Start();
                }
                else
                {
                    blink.Stop();
                }
                caretLayer.InvalidateVisual();
                UpdateQuickBar();
            }
            else if (change.Property == IsReadOnlyProperty)
            {
                View.IsReadOnly = IsReadOnly;
                caretLayer.InvalidateVisual();
            }
            else if (change.Property == CaretBrushProperty || change.Property == ForegroundProperty)
            {
                caretLayer.InvalidateVisual();
            }
            else if (change.Property == IsQuickBarEnabledProperty)
            {
                View.IsQuickBarEnabled = IsQuickBarEnabled;
                UpdateQuickBar();
            }
            else if (change.Property == BoundsProperty)
            {
                View.Arrange(Bounds.Width, Bounds.Height);
            }
            else if (change.Property == IsAutoCorrectEnabledProperty)
            {
                Editor.AutoCorrect = IsAutoCorrectEnabled ? autoCorrect : null;
            }
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            caretLayer.Measure(availableSize);
            View.Font = MathFonts.Get(MathFontFamily);
            View.FontSize = FontSize;
            View.Padding = new MathThickness(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom);
            View.BorderThickness = BorderThickness;
            var size = View.Measure(availableSize.Width);
            if (!ReferenceEquals(drawing?.Layout, View.Layout))
            {
                drawing?.Dispose();
                drawing = View.Layout is { } layout ? new MathDrawing(layout) : null;
                InvalidateVisual();
                caretLayer.InvalidateVisual();
            }
            return new Size(size.Width, size.Height);
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            var half = BorderThickness / 2;
            context.DrawRectangle(Background, BorderBrush != null && BorderThickness > 0 ? new Pen(BorderBrush, BorderThickness) : null, bounds.Deflate(half));
            if (drawing == null)
                return;
            View.Render(new MathCanvas(context, new MathBrushes(Foreground, PlaceholderBrush, MathDrawing.ErrorBrush(ActualThemeVariant), SelectionBrush), drawing));
        }

        /// <summary>Draws the caret, over the equation: a layer of its own, so that its blinking draws nothing else again.</summary>
        private void RenderCaret(DrawingContext context)
        {
            if (drawing != null)
                View.RenderCaret(new MathCanvas(context, new MathBrushes(Foreground, null, null, caret: CaretBrush ?? Foreground ?? Brushes.Black), drawing));
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new MathEditBoxAutomationPeer(this);

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            blink.Stop();
            drawing?.Dispose();
            drawing = null;
            View.Reset();
        }

        /// <inheritdoc/>
        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (e.Handled || IsReadOnly || string.IsNullOrEmpty(e.Text))
                return;
            Editor.Insert(e.Text!);
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled)
                return;

            // Shift+F10 opens the context menu, as the Menu key does (which Avalonia turns into ContextRequested)
            if (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift)
            {
                e.Handled = ShowContextMenu();
                return;
            }

            var mathKey = ToMathKey(e.Key);
            var modifiers = ToMathKeyModifiers(e.KeyModifiers);
            if (popup.IsOpen && mathKey is { } quickBarKey && View.ProcessQuickBarKey(quickBarKey, modifiers))
            {
                UpdateQuickBar();
                e.Handled = true;
                return;
            }

            var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
            bool Matches(System.Collections.Generic.List<KeyGesture>? gestures, Key key) =>
                gestures != null ? gestures.Any(g => g.Matches(e)) : e.Key == key && (e.KeyModifiers & KeyModifiers.Control) != 0;

            e.Handled = true;
            if (ToggleViewModeGesture?.Matches(e) == true)
                ViewMode = ViewMode == MathViewMode.Linear ? MathViewMode.Professional : MathViewMode.Linear;
            else if (Matches(hotkeys?.Copy, Key.C))
                _ = CopyAsync();
            else if (Matches(hotkeys?.Cut, Key.X))
                _ = CutAsync();
            else if (Matches(hotkeys?.Paste, Key.V))
                _ = PasteAsync();
            else if (Matches(hotkeys?.Undo, Key.Z) && !IsReadOnly)
                Editor.Undo();
            else if (Matches(hotkeys?.Redo, Key.Y) && !IsReadOnly)
                Editor.Redo();
            else if (Matches(hotkeys?.SelectAll, Key.A))
                Editor.SelectAll();
            else
                e.Handled = mathKey is { } editorKey && Editor.ProcessKey(editorKey, modifiers, Layout, IsReadOnly);
        }

        private static MathKey? ToMathKey(Key key) => key switch
        {
            Key.Left => MathKey.Left,
            Key.Right => MathKey.Right,
            Key.Up => MathKey.Up,
            Key.Down => MathKey.Down,
            Key.Home => MathKey.Home,
            Key.End => MathKey.End,
            Key.Back => MathKey.Backspace,
            Key.Delete => MathKey.Delete,
            Key.Tab => MathKey.Tab,
            Key.Enter => MathKey.Enter,
            Key.Escape => MathKey.Escape,
            _ => null,
        };

        private static MathKeyModifiers ToMathKeyModifiers(KeyModifiers modifiers) =>
            ((modifiers & KeyModifiers.Shift) != 0 ? MathKeyModifiers.Shift : MathKeyModifiers.None)
            | ((modifiers & KeyModifiers.Control) != 0 ? MathKeyModifiers.Control : MathKeyModifiers.None)
            | ((modifiers & KeyModifiers.Alt) != 0 ? MathKeyModifiers.Alt : MathKeyModifiers.None)
            | ((modifiers & KeyModifiers.Meta) != 0 ? MathKeyModifiers.Meta : MathKeyModifiers.None);

        /// <inheritdoc/>
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            Focus();
            var point = e.GetPosition(this);
            View.PointerPressed(point.X, point.Y, (e.KeyModifiers & KeyModifiers.Shift) != 0);
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var point = e.GetPosition(this);
            View.PointerMoved(point.X, point.Y);
        }

        /// <inheritdoc/>
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (View.PointerReleased())
                e.Pointer.Capture(null);
        }

        private void OnViewChanged(object? sender, MathEditorViewChangedEventArgs e)
        {
            if ((e.Changes & MathEditorViewChanges.Measure) != 0)
                InvalidateMeasure();
            else if ((e.Changes & MathEditorViewChanges.Render) != 0)
                InvalidateVisual();
            if ((e.Changes & MathEditorViewChanges.Caret) != 0)
            {
                caretLayer.InvalidateVisual();
                inputClient.RaiseCursorChanged();
            }
            if ((e.Changes & MathEditorViewChanges.QuickBar) != 0)
                Dispatcher.UIThread.Post(UpdateQuickBar, DispatcherPriority.Background);
        }

        /// <summary>Opens the floating bar at the caret with what fits the place of the caret, or closes it.</summary>
        private void UpdateQuickBar()
        {
            QuickBar.Refresh(View.UpdateQuickBar());
            if (View.QuickBarAnchor is not { } anchor)
            {
                popup.IsOpen = false;
                return;
            }
            popup.PlacementRect = new Rect(anchor.X, anchor.Y, anchor.Width, anchor.Height);
            popup.IsOpen = true;
        }

        private void OnEditorContentChanged(object? sender, EventArgs e)
        {
            var equation = Editor.ToEquation();
            updatingEquation = true;
            try
            {
                SetCurrentValue(EquationProperty, equation);
            }
            finally
            {
                updatingEquation = false;
            }
            if (ViewMode == MathViewMode.Linear != Editor.IsLinear)
                SetCurrentValue(ViewModeProperty, Editor.IsLinear ? MathViewMode.Linear : MathViewMode.Professional);
            EquationChanged?.Invoke(this, new EquationChangedEventArgs(equation));
        }

        /// <summary>The layer of the caret, over the whole control.</summary>
        private sealed class CaretLayer : Control
        {
            private readonly MathEditBox owner;

            public CaretLayer(MathEditBox owner)
            {
                this.owner = owner;
                IsHitTestVisible = false;
                Focusable = false;
            }

            public override void Render(DrawingContext context) => owner.RenderCaret(context);
        }

        /// <summary>The client of the input method: where the caret is, so that the window of the candidates follows it.</summary>
        private sealed class InputClient : TextInputMethodClient
        {
            private readonly MathEditBox owner;

            public InputClient(MathEditBox owner) => this.owner = owner;

            public override Visual TextViewVisual => owner;

            public override bool SupportsPreedit => false;

            public override bool SupportsSurroundingText => false;

            public override string SurroundingText => string.Empty;

            public override Rect CursorRectangle
            {
                get
                {
                    return owner.View.GetCaretRect() is { } caret ? new Rect(caret.X, caret.Y, caret.Width, caret.Height) : default;
                }
            }

            public override TextSelection Selection
            {
                get => default;
                set { }
            }

            public void RaiseCursorChanged() => RaiseCursorRectangleChanged();
        }
    }
}
