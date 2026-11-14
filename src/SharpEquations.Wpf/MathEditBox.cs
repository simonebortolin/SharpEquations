namespace SharpEquations.Wpf
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using System.Windows.Automation.Peers;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;

    /// <summary>
    /// A real-time equation editor for WPF, in the manner of the RichEditBox of WinUI in math mode and of the math zones of
    /// Word: UnicodeMath or LaTeX typed at the caret builds up into the equation when a space or an operator ends an
    /// argument, and the content is read and written as MathML (<see cref="GetMath"/>, <see cref="SetMath"/>) or as an
    /// <see cref="SharpEquations.Equation"/>. It adapts the view of Rendering (<see cref="View"/>), which does the rest.
    /// </summary>
    public class MathEditBox : Control
    {
        /// <summary>The equation edited; null when the editor is empty. Two-way by default.</summary>
        public static readonly DependencyProperty EquationProperty =
            DependencyProperty.Register(nameof(Equation), typeof(Equation), typeof(MathEditBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((MathEditBox)d).OnEquationChanged((Equation?)e.NewValue)));

        /// <summary>The format of what is typed: UnicodeMath (the default) or LaTeX.</summary>
        public static readonly DependencyProperty InputFormatProperty =
            DependencyProperty.Register(nameof(InputFormat), typeof(EquationFormat), typeof(MathEditBox), new PropertyMetadata(EquationFormat.UnicodeMath, (d, e) => ((MathEditBox)d).Editor.InputFormat = (EquationFormat)e.NewValue), f => f is EquationFormat.UnicodeMath or EquationFormat.LaTeX);

        /// <summary>Whether the equation is shown built up or as linear text.</summary>
        public static readonly DependencyProperty ViewModeProperty =
            DependencyProperty.Register(nameof(ViewMode), typeof(MathViewMode), typeof(MathEditBox), new PropertyMetadata(MathViewMode.Professional, (d, e) => ((MathEditBox)d).Editor.SetLinear((MathViewMode)e.NewValue == MathViewMode.Linear)));

        /// <summary>Whether the equation is a display equation rather than inline.</summary>
        public static readonly DependencyProperty IsDisplayProperty =
            DependencyProperty.Register(nameof(IsDisplay), typeof(bool), typeof(MathEditBox), new PropertyMetadata(true, (d, e) => ((MathEditBox)d).Editor.IsDisplay = (bool)e.NewValue));

        /// <summary>Whether the user cannot change the equation.</summary>
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(MathEditBox), new PropertyMetadata(false, (d, e) => ((MathEditBox)d).OnReadOnlyChanged()));

        /// <summary>The brush of the selection.</summary>
        public static readonly DependencyProperty SelectionBrushProperty =
            DependencyProperty.Register(nameof(SelectionBrush), typeof(Brush), typeof(MathEditBox), new FrameworkPropertyMetadata(Frozen(new SolidColorBrush(Color.FromArgb(0x60, 0x33, 0x99, 0xFF))), FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>The brush of the caret; null for the foreground.</summary>
        public static readonly DependencyProperty CaretBrushProperty =
            DependencyProperty.Register(nameof(CaretBrush), typeof(Brush), typeof(MathEditBox), new PropertyMetadata(null, (d, e) => ((MathEditBox)d).caretLayer.InvalidateVisual()));

        /// <summary>The brush of the boxes of the empty slots.</summary>
        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register(nameof(PlaceholderBrush), typeof(Brush), typeof(MathEditBox), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public static readonly DependencyProperty MathFontProperty =
            DependencyProperty.Register(nameof(MathFont), typeof(OpenTypeMathFont), typeof(MathEditBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>Whether the floating bar shows completions, structures, table commands and symbols at the caret.</summary>
        public static readonly DependencyProperty IsQuickBarEnabledProperty =
            DependencyProperty.Register(nameof(IsQuickBarEnabled), typeof(bool), typeof(MathEditBox), new PropertyMetadata(true, (d, e) => ((MathEditBox)d).OnQuickBarEnabledChanged()));

        /// <summary>Whether the math autocorrect (<see cref="AutoCorrect"/>) replaces sequences by their symbols as they are typed.</summary>
        public static readonly DependencyProperty IsAutoCorrectEnabledProperty =
            DependencyProperty.Register(nameof(IsAutoCorrectEnabled), typeof(bool), typeof(MathEditBox), new PropertyMetadata(true, (d, e) => ((MathEditBox)d).OnAutoCorrectEnabledChanged()));

        /// <summary>Whether the editor opens its context menu.</summary>
        public static readonly DependencyProperty IsContextMenuEnabledProperty =
            DependencyProperty.Register(nameof(IsContextMenuEnabled), typeof(bool), typeof(MathEditBox), new PropertyMetadata(true));

        /// <summary>
        /// The format of the text that <see cref="Copy"/> puts on the clipboard; null (the default) for the input format.
        /// MathML pastes as an equation into Word.
        /// </summary>
        public static readonly DependencyProperty CopyTextFormatProperty =
            DependencyProperty.Register(nameof(CopyTextFormat), typeof(EquationFormat?), typeof(MathEditBox), new PropertyMetadata(null), f => f is not EquationFormat.Mtef);

        /// <summary>The format of MathML on the clipboard of Windows, which Word reads and writes as UTF-8 (<see cref="MathClipboard"/>).</summary>
        public const string MathMLClipboardFormat = "MathML";

        private static readonly Brush ErrorBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xE1)));
        private static readonly Brush HighlightBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x99, 0xFF)));
        private readonly CaretLayer caretLayer;
        private readonly Popup popup;
        private readonly StackPanel quickBar = new StackPanel();
        private readonly DispatcherTimer blink;
        private MathAutoCorrect autoCorrect;
        private MathLayout? drawn;
        private ContextMenu? openMenu;
        private bool updatingEquation;
        private int selectedButton = -1;

        static MathEditBox()
        {
            FocusableProperty.OverrideMetadata(typeof(MathEditBox), new FrameworkPropertyMetadata(true));
            CursorProperty.OverrideMetadata(typeof(MathEditBox), new FrameworkPropertyMetadata(Cursors.IBeam));
            PaddingProperty.OverrideMetadata(typeof(MathEditBox), new FrameworkPropertyMetadata(new Thickness(6, 4, 6, 4)));
            BorderThicknessProperty.OverrideMetadata(typeof(MathEditBox), new FrameworkPropertyMetadata(new Thickness(1)));
            BorderBrushProperty.OverrideMetadata(typeof(MathEditBox), new FrameworkPropertyMetadata(Brushes.Gray));
            BackgroundProperty.OverrideMetadata(typeof(MathEditBox), new FrameworkPropertyMetadata(Brushes.Transparent));
        }

        /// <summary>An empty editor.</summary>
        public MathEditBox()
        {
            Editor = new MathEditor { IsDisplay = true };
            Editor.ContentChanged += OnEditorContentChanged;
            View = new MathEditorView(Editor);
            View.Changed += OnViewChanged;
            autoCorrect = Editor.AutoCorrect ?? new MathAutoCorrect();

            caretLayer = new CaretLayer(this);
            AddVisualChild(caretLayer);

            popup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Relative,
                StaysOpen = true,
                Focusable = false,
                AllowsTransparency = true,
                Child = new Border
                {
                    Child = quickBar,
                    Padding = new Thickness(4),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    Background = Frozen(new SolidColorBrush(Color.FromArgb(0xF8, 0xFB, 0xFB, 0xFD))),
                    BorderBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80))),
                },
            };

            blink = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = MathEditorView.CaretBlinkInterval };
            blink.Tick += (_, _) =>
            {
                View.BlinkCaret();
                caretLayer.InvalidateVisual();
            };
            Unloaded += (_, _) =>
            {
                blink.Stop();
                popup.IsOpen = false;
                drawn = null;
                View.Reset();
            };
        }

        /// <summary>
        /// Raised when the selection is copied, before it goes to the clipboard: a handler can add formats to the data
        /// (OMML, a picture...) or cancel the copy.
        /// </summary>
        public event EventHandler<MathCopyingEventArgs>? Copying;

        /// <summary>Raised when the equation changes, by an edit or by <see cref="Equation"/>.</summary>
        public event EventHandler<EquationChangedEventArgs>? EquationChanged;

        /// <summary>
        /// Raised before the context menu opens, with the menu of <see cref="CreateContextMenu"/>: a handler can add,
        /// remove or change items, or cancel it.
        /// </summary>
        public event EventHandler<MathContextMenuEventArgs>? MathContextMenuOpening;

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
            get => (Equation?)GetValue(EquationProperty);
            set => SetValue(EquationProperty, value);
        }

        /// <summary>The format of what is typed.</summary>
        public EquationFormat InputFormat
        {
            get => (EquationFormat)GetValue(InputFormatProperty);
            set => SetValue(InputFormatProperty, value);
        }

        /// <summary>Whether the equation is shown built up or as linear text.</summary>
        public MathViewMode ViewMode
        {
            get => (MathViewMode)GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        /// <summary>Whether the equation is a display equation rather than inline.</summary>
        public bool IsDisplay
        {
            get => (bool)GetValue(IsDisplayProperty);
            set => SetValue(IsDisplayProperty, value);
        }

        /// <summary>Whether the user cannot change the equation.</summary>
        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        /// <summary>The brush of the selection.</summary>
        public Brush? SelectionBrush
        {
            get => (Brush?)GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        /// <summary>The brush of the caret; null for the foreground.</summary>
        public Brush? CaretBrush
        {
            get => (Brush?)GetValue(CaretBrushProperty);
            set => SetValue(CaretBrushProperty, value);
        }

        /// <summary>The brush of the boxes of the empty slots.</summary>
        public Brush? PlaceholderBrush
        {
            get => (Brush?)GetValue(PlaceholderBrushProperty);
            set => SetValue(PlaceholderBrushProperty, value);
        }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public OpenTypeMathFont? MathFont
        {
            get => (OpenTypeMathFont?)GetValue(MathFontProperty);
            set => SetValue(MathFontProperty, value);
        }

        /// <summary>Whether the floating bar shows at the caret.</summary>
        public bool IsQuickBarEnabled
        {
            get => (bool)GetValue(IsQuickBarEnabledProperty);
            set => SetValue(IsQuickBarEnabledProperty, value);
        }

        /// <summary>Whether the floating bar is open.</summary>
        public bool IsQuickBarOpen => popup.IsOpen;

        /// <summary>The symbols the floating bar offers in an empty slot.</summary>
        public IList<string> QuickSymbols { get; } = new List<string> { "α", "β", "θ", "π", "∞", "±", "×", "≤", "≥", "≠", "≈", "→", "∈", "∂", "∑", "∫" };

        /// <summary>Whether the editor opens its context menu.</summary>
        public bool IsContextMenuEnabled
        {
            get => (bool)GetValue(IsContextMenuEnabledProperty);
            set => SetValue(IsContextMenuEnabledProperty, value);
        }

        /// <summary>The context menu while it is open, or null.</summary>
        public ContextMenu? OpenContextMenu => openMenu is { IsOpen: true } ? openMenu : null;

        /// <summary>The format of the text copied to the clipboard; null for the input format.</summary>
        public EquationFormat? CopyTextFormat
        {
            get => (EquationFormat?)GetValue(CopyTextFormatProperty);
            set => SetValue(CopyTextFormatProperty, value);
        }

        /// <summary>
        /// The rules of the math autocorrect (<see cref="MathEditor.AutoCorrect"/>): <c>-&gt;</c> becomes → and
        /// <c>\alpha</c> and a space α as they are typed; <see cref="IsAutoCorrectEnabled"/> turns it off.
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
            get => (bool)GetValue(IsAutoCorrectEnabledProperty);
            set => SetValue(IsAutoCorrectEnabledProperty, value);
        }

        /// <summary>The number of the equation, shown at the right of its line; null without one (<see cref="MathEditor.EquationNumber"/>).</summary>
        public string? EquationNumber
        {
            get => Editor.EquationNumber;
            set => Editor.SetEquationNumber(value);
        }

        /// <summary>The layout of the equation, after a measure.</summary>
        public MathLayout? Layout => View.Layout;

        internal StackPanel QuickBarPanel => quickBar;

        /// <inheritdoc/>
        protected override int VisualChildrenCount => 1;

        /// <summary>The MathML of the equation, as <c>RichEditTextDocument.GetMath</c>.</summary>
        public string GetMath() => Editor.ToMathML().ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

        /// <summary>Replaces the equation with MathML, as <c>RichEditTextDocument.SetMath</c>.</summary>
        /// <exception cref="FormatException">The MathML is not valid.</exception>
        public void SetMath(string mathML) => Editor.Load(string.IsNullOrWhiteSpace(mathML) ? null : Equation.Parse(mathML, EquationFormat.MathML));

        /// <summary>The equation in a format; for LaTeX, the LaTeX of Word.</summary>
        public string GetText(EquationFormat format) => Editor.GetText(format);

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
        public void Copy()
        {
            if (!View.TryGetCopy(CopyTextFormat, out var equation, out var text, out var mathML))
                return;
            var data = new DataObject();
            data.SetText(text);
            data.SetData(MathMLClipboardFormat, new MemoryStream(MathClipboard.Encode(mathML)));
            var args = new MathCopyingEventArgs(equation, data);
            Copying?.Invoke(this, args);
            if (!args.Cancel)
                Clipboard.SetDataObject(data, true);
        }

        /// <summary>Copies the selection to the clipboard and deletes it.</summary>
        public void Cut()
        {
            if (IsReadOnly)
                return;
            Copy();
            Editor.DeleteSelection();
        }

        /// <summary>Pastes the clipboard: MathML, or text read in the input format (or as MathML), or plain text.</summary>
        public void Paste()
        {
            if (IsReadOnly)
                return;
            var data = Clipboard.GetDataObject();
            if (data != null && data.GetDataPresent(MathMLClipboardFormat) && Bytes(data.GetData(MathMLClipboardFormat)) is { Length: > 0 } bytes
                && View.PasteMathML(MathClipboard.Decode(bytes)))
            {
                return;
            }
            if (Clipboard.ContainsText() && Clipboard.GetText() is { Length: > 0 } text)
                View.PasteText(text);
        }

        /// <summary>
        /// Copies the selection, or the whole equation when nothing is selected, to the clipboard as text in a format
        /// (LaTeX as Word writes it); MathML goes also as the MathML of the clipboard (<see cref="MathMLClipboardFormat"/>).
        /// </summary>
        public void CopyAs(EquationFormat format)
        {
            if (View.GetCopyAsText(format) is not { } text)
                return;
            var data = new DataObject();
            data.SetText(text);
            if (format == EquationFormat.MathML)
                data.SetData(MathMLClipboardFormat, new MemoryStream(MathClipboard.Encode(text)));
            Clipboard.SetDataObject(data, true);
        }

        /// <summary>
        /// Opens the context menu at a point of the editor, or at the caret: a point outside the selection moves the caret
        /// there first, as text editors do. Returns whether a menu opened.
        /// </summary>
        public bool ShowContextMenu(Point? point = null)
        {
            if (!IsContextMenuEnabled)
                return false;
            Focus();
            var at = View.PrepareContextMenu(point?.X, point?.Y);
            if (openMenu != null)
                openMenu.IsOpen = false;
            var menu = CreateContextMenu();
            var args = new MathContextMenuEventArgs(menu);
            MathContextMenuOpening?.Invoke(this, args);
            if (args.Cancel || menu.Items.Count == 0)
                return false;
            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.Relative;
            if (at is { } rect)
            {
                menu.HorizontalOffset = rect.X;
                menu.VerticalOffset = rect.Bottom;
            }
            openMenu = menu;
            menu.IsOpen = true;
            return true;
        }

        /// <summary>
        /// The context menu of the editor, built anew each time it opens, for the state of the editor
        /// (<see cref="MathEditorView.GetContextMenu"/>). The tag of each item names its command: <c>undo</c>,
        /// <c>redo</c>, <c>cut</c>, <c>copy</c>, <c>paste</c>, <c>copy-as</c> (with <c>copy-as-mathml</c>,
        /// <c>copy-as-unicodemath</c>, <c>copy-as-latex</c>, <c>copy-as-omml</c>), <c>select-all</c>, <c>view</c>,
        /// <c>build-up</c>, <c>build-down</c>, <c>new-line</c>, <c>number</c>, <c>table</c> (with <c>row-above</c>,
        /// <c>row-below</c>, <c>column-left</c>, <c>column-right</c>, <c>delete-row</c>, <c>delete-column</c>).
        /// </summary>
        protected virtual ContextMenu CreateContextMenu()
        {
            var menu = new ContextMenu();
            foreach (var item in View.GetContextMenu())
                menu.Items.Add(Item(item));
            return menu;
        }

        /// <inheritdoc/>
        protected override Visual GetVisualChild(int index) => index == 0 ? caretLayer : throw new ArgumentOutOfRangeException(nameof(index));

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            caretLayer.Measure(constraint);
            var thickness = BorderThickness;
            View.Font = MathFont ?? MathFonts.Default;
            View.FontSize = FontSize;
            View.Padding = new MathThickness(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom);
            View.BorderThickness = (thickness.Left + thickness.Top + thickness.Right + thickness.Bottom) / 4;
            var size = View.Measure(constraint.Width);
            if (!ReferenceEquals(drawn, View.Layout))
            {
                InvalidateVisual();
                caretLayer.InvalidateVisual();
            }
            return new Size(size.Width, size.Height);
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            caretLayer.Arrange(new Rect(arrangeBounds));
            View.Arrange(arrangeBounds.Width, arrangeBounds.Height);
            return arrangeBounds;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var thickness = View.BorderThickness;
            var size = RenderSize;
            var pen = BorderBrush != null && thickness > 0 ? new Pen(BorderBrush, thickness) : null;
            drawingContext.DrawRectangle(Background ?? Brushes.Transparent, pen, new Rect(thickness / 2, thickness / 2, Math.Max(0, size.Width - thickness), Math.Max(0, size.Height - thickness)));
            drawn = View.Layout;
            View.Render(new MathCanvas(drawingContext, new MathBrushes(Foreground, PlaceholderBrush, ErrorBrush, SelectionBrush)));
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == FontSizeProperty || e.Property == PaddingProperty || e.Property == BorderThicknessProperty)
            {
                InvalidateMeasure();
            }
            else if (e.Property == ForegroundProperty || e.Property == BackgroundProperty || e.Property == BorderBrushProperty)
            {
                InvalidateVisual();
                caretLayer.InvalidateVisual();
            }
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new MathEditBoxAutomationPeer(this);

        /// <inheritdoc/>
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            OnFocusChanged(true);
        }

        /// <inheritdoc/>
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            OnFocusChanged(false);
        }

        /// <inheritdoc/>
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);
            if (e.Handled)
                return;
            e.Handled = Type(e.Text);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled)
                e.Handled = HandleKey(e.Key == Key.System ? e.SystemKey : e.Key, Modifiers(Keyboard.Modifiers));
        }

        /// <inheritdoc/>
        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            base.OnContextMenuOpening(e);
            if (e.Handled)
                return;

            // the keyboard (the Menu key, Shift+F10) opens it with no point: at the caret
            ShowContextMenu(e.CursorLeft < 0 || e.CursorTop < 0 ? null : new Point(e.CursorLeft, e.CursorTop));
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var point = e.GetPosition(this);
            PressAt(point.X, point.Y, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            CaptureMouse();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var point = e.GetPosition(this);
            View.PointerMoved(point.X, point.Y);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (View.PointerReleased())
                ReleaseMouseCapture();
        }

        /// <summary>The main button pressed at a point of the control: it takes the focus and the caret goes there.</summary>
        internal void PressAt(double x, double y, bool extend)
        {
            Focus();
            View.PointerPressed(x, y, extend);
        }

        /// <summary>Text typed, as a keyboard or an input method types it: the control characters of the keys are left to them.</summary>
        /// <returns>Whether the text was used.</returns>
        internal bool Type(string text)
        {
            if (IsReadOnly || string.IsNullOrEmpty(text))
                return false;
            foreach (var ch in text)
            {
                if (ch < ' ' || ch == '\u007F')
                    return false;
            }
            Editor.Insert(text);
            return true;
        }

        /// <summary>Acts on a key pressed with modifiers: the context menu, the floating bar, the shortcuts, then the editor.</summary>
        /// <returns>Whether the key was used.</returns>
        internal bool HandleKey(Key key, MathKeyModifiers modifiers)
        {
            var control = (modifiers & MathKeyModifiers.Control) != 0;
            var shift = (modifiers & MathKeyModifiers.Shift) != 0;

            if (key == Key.Apps || key == Key.F10 && modifiers == MathKeyModifiers.Shift)
                return ShowContextMenu();

            var mathKey = ToMathKey(key);
            if (popup.IsOpen && mathKey is { } quickBarKey && View.ProcessQuickBarKey(quickBarKey, modifiers))
            {
                UpdateQuickBar();
                return true;
            }

            switch (key)
            {
                case Key.L when control && shift:
                    ToggleViewMode();
                    return true;
                case Key.C when control:
                case Key.Insert when control:
                    Copy();
                    return true;
                case Key.X when control:
                case Key.Delete when shift && !control:
                    Cut();
                    return true;
                case Key.V when control:
                case Key.Insert when shift && !control:
                    Paste();
                    return true;
                case Key.Z when control && shift && !IsReadOnly:
                case Key.Y when control && !IsReadOnly:
                    Editor.Redo();
                    return true;
                case Key.Z when control && !IsReadOnly:
                    Editor.Undo();
                    return true;
                case Key.A when control:
                    Editor.SelectAll();
                    return true;
                default:
                    return mathKey is { } editorKey && Editor.ProcessKey(editorKey, modifiers, Layout, IsReadOnly);
            }
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

        private static MathKeyModifiers Modifiers(ModifierKeys modifiers) =>
            ((modifiers & ModifierKeys.Shift) != 0 ? MathKeyModifiers.Shift : MathKeyModifiers.None)
            | ((modifiers & ModifierKeys.Control) != 0 ? MathKeyModifiers.Control : MathKeyModifiers.None)
            | ((modifiers & ModifierKeys.Alt) != 0 ? MathKeyModifiers.Alt : MathKeyModifiers.None)
            | ((modifiers & ModifierKeys.Windows) != 0 ? MathKeyModifiers.Meta : MathKeyModifiers.None);

        private static byte[]? Bytes(object? data) => data switch
        {
            byte[] bytes => bytes,
            MemoryStream stream => stream.ToArray(),
            Stream stream => ReadAll(stream),
            string text => MathClipboard.Encode(text),
            _ => null,
        };

        private static byte[] ReadAll(Stream stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }

        private static string Gesture(string command) => command switch
        {
            "undo" => "Ctrl+Z",
            "redo" => "Ctrl+Y",
            "cut" => "Ctrl+X",
            "copy" => "Ctrl+C",
            "paste" => "Ctrl+V",
            "select-all" => "Ctrl+A",
            "view" => "Ctrl+Shift+L",
            "new-line" => "Shift+Enter",
            _ => string.Empty,
        };

        private void ToggleViewMode() => ViewMode = ViewMode == MathViewMode.Linear ? MathViewMode.Professional : MathViewMode.Linear;

        private object Item(MathMenuItem item)
        {
            if (item.IsSeparator)
                return new Separator();
            var command = item.Command!;
            var menuItem = new MenuItem { Header = item.Header, Tag = command, IsEnabled = item.IsEnabled, InputGestureText = Gesture(command) };
            if (item.Items.Count > 0)
            {
                foreach (var child in item.Items)
                    menuItem.Items.Add(Item(child));
                return menuItem;
            }
            menuItem.Click += (_, args) =>
            {
                args.Handled = true;
                Execute(command);
                Focus();
            };
            return menuItem;
        }

        /// <summary>Does a command of the context menu: those of the clipboard and the view here, the others on the editor (<see cref="MathEditorView.ExecuteCommand"/>).</summary>
        private void Execute(string command)
        {
            switch (command)
            {
                case "cut":
                    Cut();
                    break;
                case "copy":
                    Copy();
                    break;
                case "paste":
                    Paste();
                    break;
                case "view":
                    ToggleViewMode();
                    break;
                case var _ when MathEditorView.CopyAsFormat(command) is { } format:
                    CopyAs(format);
                    break;
                default:
                    View.ExecuteCommand(command);
                    break;
            }
        }

        /// <summary>Draws the caret, over the equation: a layer of its own, so that its blinking draws nothing else again.</summary>
        private void RenderCaret(DrawingContext context) =>
            View.RenderCaret(new MathCanvas(context, new MathBrushes(Foreground, caret: CaretBrush ?? Foreground ?? Brushes.Black)));

        private void OnEquationChanged(Equation? equation)
        {
            if (updatingEquation)
                return;
            Editor.Load(equation);
            if (equation != null)
                SetCurrentValue(IsDisplayProperty, equation.IsDisplay);
        }

        private void OnReadOnlyChanged()
        {
            View.IsReadOnly = IsReadOnly;
            caretLayer.InvalidateVisual();
            UpdateQuickBar();
        }

        private void OnQuickBarEnabledChanged()
        {
            View.IsQuickBarEnabled = IsQuickBarEnabled;
            UpdateQuickBar();
        }

        private void OnAutoCorrectEnabledChanged() => Editor.AutoCorrect = IsAutoCorrectEnabled ? autoCorrect : null;

        private void OnFocusChanged(bool focused)
        {
            View.IsFocused = focused;
            if (focused)
                blink.Start();
            else
                blink.Stop();
            caretLayer.InvalidateVisual();
            UpdateQuickBar();
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

        private void OnViewChanged(object? sender, MathEditorViewChangedEventArgs e)
        {
            if ((e.Changes & MathEditorViewChanges.Measure) != 0)
                InvalidateMeasure();
            else if ((e.Changes & MathEditorViewChanges.Render) != 0)
                InvalidateVisual();
            if ((e.Changes & MathEditorViewChanges.Caret) != 0)
                caretLayer.InvalidateVisual();
            if ((e.Changes & MathEditorViewChanges.QuickBar) != 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateQuickBar));
        }

        /// <summary>Opens the floating bar under the equation, at the caret, with what fits the place of the caret, or closes it.</summary>
        private void UpdateQuickBar()
        {
            if (View.UpdateQuickBar() || View.QuickBar.Mode != MathQuickBarMode.Completions)
                FillQuickBar();
            Highlight();
            if (View.QuickBarAnchor is not { } anchor || !IsLoaded)
            {
                popup.IsOpen = false;
                return;
            }
            popup.HorizontalOffset = anchor.X;
            popup.VerticalOffset = anchor.Bottom;
            popup.IsOpen = true;
        }

        private void FillQuickBar()
        {
            quickBar.Children.Clear();
            var completions = View.QuickBar.Mode == MathQuickBarMode.Completions;
            quickBar.Orientation = completions ? Orientation.Vertical : Orientation.Horizontal;
            foreach (var item in View.QuickBar.GetItems(QuickSymbols))
            {
                var button = new Button
                {
                    Content = item.Text,
                    ToolTip = item.ToolTip,
                    MinWidth = completions ? 130 : View.QuickBar.Mode == MathQuickBarMode.Symbols ? 26 : 36,
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(1),
                    HorizontalContentAlignment = completions ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                    Focusable = false,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                };
                button.Click += (_, _) => item.Invoke();
                quickBar.Children.Add(button);
            }
            selectedButton = -1;
        }

        /// <summary>Highlights the completion that Tab or Enter takes.</summary>
        private void Highlight()
        {
            var selected = View.QuickBar.Mode == MathQuickBarMode.Completions ? View.QuickBar.SelectedIndex : -1;
            if (selected == selectedButton)
                return;
            selectedButton = selected;
            for (var i = 0; i < quickBar.Children.Count; i++)
            {
                if (quickBar.Children[i] is Button button)
                    button.Background = i == selected ? HighlightBrush : Brushes.Transparent;
            }
        }

        /// <summary>The layer of the caret, over the whole control.</summary>
        private sealed class CaretLayer : FrameworkElement
        {
            private readonly MathEditBox owner;

            public CaretLayer(MathEditBox owner)
            {
                this.owner = owner;
                IsHitTestVisible = false;
                Focusable = false;
            }

            protected override void OnRender(DrawingContext drawingContext) => owner.RenderCaret(drawingContext);
        }
    }
}
