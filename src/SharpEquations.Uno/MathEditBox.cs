namespace SharpEquations.Uno
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.UI;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.System;
    using Windows.UI.Core;

    /// <summary>Whether an equation is shown built up or as linear text.</summary>
    public enum MathViewMode
    {
        /// <summary>Built up: fractions, scripts, radicals.</summary>
        Professional,

        /// <summary>As the linear text of the input format.</summary>
        Linear,
    }

    /// <summary>The arguments of <see cref="MathEditBox.EquationChanged"/>.</summary>
    public sealed class EquationChangedEventArgs : EventArgs
    {
        /// <summary>Arguments with the new equation.</summary>
        public EquationChangedEventArgs(Equation? equation) => Equation = equation;

        /// <summary>The equation; null when the editor is empty.</summary>
        public Equation? Equation { get; }
    }

    /// <summary>The arguments of <see cref="MathEditBox.ContextMenuOpening"/>.</summary>
    public sealed class MathContextMenuEventArgs : EventArgs
    {
        /// <summary>Arguments with the menu about to open.</summary>
        public MathContextMenuEventArgs(MenuFlyout menu) => Menu = menu;

        /// <summary>The menu, which a handler can change: the tag of each item names its command (<see cref="MathEditorView.GetContextMenu"/>).</summary>
        public MenuFlyout Menu { get; }

        /// <summary>Set to true to open no menu.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// A real-time equation editor for Uno Platform, in the manner of the RichEditBox of WinUI in math mode and of the
    /// math zones of Word: UnicodeMath or LaTeX typed at the caret builds up into the equation when a space or an
    /// operator ends an argument, and the content is read and written as MathML (<see cref="GetMath"/>,
    /// <see cref="SetMath"/>) or as an <see cref="SharpEquations.Equation"/>. It is built of the elements every renderer
    /// has: shapes and paths for the equation, a transparent <see cref="TextBox"/> for the input (so that the input
    /// methods and the keyboards of touch screens work), and the view of Rendering (<see cref="View"/>) for all the rest.
    /// </summary>
    public partial class MathEditBox : UserControl
    {
        /// <summary>The equation edited; null when the editor is empty.</summary>
        public static readonly DependencyProperty EquationProperty =
            DependencyProperty.Register(nameof(Equation), typeof(Equation), typeof(MathEditBox), new PropertyMetadata(null, (d, e) => ((MathEditBox)d).OnEquationChanged((Equation?)e.NewValue)));

        /// <summary>The format of what is typed: UnicodeMath (the default) or LaTeX.</summary>
        public static readonly DependencyProperty InputFormatProperty =
            DependencyProperty.Register(nameof(InputFormat), typeof(EquationFormat), typeof(MathEditBox), new PropertyMetadata(EquationFormat.UnicodeMath, (d, e) => ((MathEditBox)d).Editor.InputFormat = (EquationFormat)e.NewValue));

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
            DependencyProperty.Register(nameof(SelectionBrush), typeof(Brush), typeof(MathEditBox), new PropertyMetadata(new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x33, 0x99, 0xFF)), (d, e) => ((MathEditBox)d).RenderEquation()));

        /// <summary>The brush of the caret; null for the foreground.</summary>
        public static readonly DependencyProperty CaretBrushProperty =
            DependencyProperty.Register(nameof(CaretBrush), typeof(Brush), typeof(MathEditBox), new PropertyMetadata(null, (d, e) => ((MathEditBox)d).RenderCaret()));

        /// <summary>The brush of the boxes of the empty slots.</summary>
        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register(nameof(PlaceholderBrush), typeof(Brush), typeof(MathEditBox), new PropertyMetadata(new SolidColorBrush(Colors.Gray), (d, e) => ((MathEditBox)d).RenderEquation()));

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        public static readonly DependencyProperty MathFontProperty =
            DependencyProperty.Register(nameof(MathFont), typeof(OpenTypeMathFont), typeof(MathEditBox), new PropertyMetadata(null, (d, e) => ((MathEditBox)d).InvalidateMeasure()));

        /// <summary>Whether the floating bar shows completions, structures, table commands and symbols at the caret.</summary>
        public static readonly DependencyProperty IsQuickBarEnabledProperty =
            DependencyProperty.Register(nameof(IsQuickBarEnabled), typeof(bool), typeof(MathEditBox), new PropertyMetadata(true, (d, e) => ((MathEditBox)d).OnQuickBarEnabledChanged()));

        /// <summary>The format of the text that <see cref="CopyAsync"/> puts on the clipboard; null for the input format.</summary>
        public static readonly DependencyProperty CopyTextFormatProperty =
            DependencyProperty.Register(nameof(CopyTextFormat), typeof(EquationFormat?), typeof(MathEditBox), new PropertyMetadata(null));

        private static readonly Brush LightError = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xE4, 0xE1));
        private static readonly Brush DarkError = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x5C, 0x24, 0x24));
        private readonly Border border;
        private readonly Canvas equationLayer = new Canvas { IsHitTestVisible = false };
        private readonly Canvas caretLayer = new Canvas { IsHitTestVisible = false };
        private readonly Canvas inputLayer = new Canvas();
        private readonly TextBox input;
        private readonly Popup popup;
        private readonly StackPanel quickBar = new StackPanel { Spacing = 2 };
        private readonly DispatcherTimer blink;
        private MathLayout? drawn;
        private bool updatingEquation;
        private bool composing;
        private bool clearingInput;
        private int selectedButton = -1;

        /// <summary>An empty editor.</summary>
        public MathEditBox()
        {
            Editor = new MathEditor { IsDisplay = true };
            Editor.ContentChanged += OnEditorContentChanged;
            View = new MathEditorView(Editor);
            View.Changed += OnViewChanged;

            Padding = new Thickness(6, 4, 6, 4);
            BorderThickness = new Thickness(1);
            BorderBrush = new SolidColorBrush(Colors.Gray);
            IsTabStop = false;

            input = new TextBox
            {
                Opacity = 0,
                MinWidth = 0,
                MinHeight = 0,
                Width = 2,
                Height = 16,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                AcceptsReturn = false,
                IsSpellCheckEnabled = false,
            };
            input.PreviewKeyDown += OnInputKeyDown;
            input.TextChanged += OnInputTextChanged;
            input.TextCompositionStarted += (_, _) => composing = true;
            input.TextCompositionEnded += (_, _) =>
            {
                composing = false;
                TakeInput();
            };
            input.GotFocus += (_, _) => OnFocusChanged(true);
            input.LostFocus += (_, _) => OnFocusChanged(false);
            inputLayer.Children.Add(input);

            popup = new Popup
            {
                IsLightDismissEnabled = false,
                Child = new Border
                {
                    Child = quickBar,
                    Padding = new Thickness(4),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                },
            };

            border = new Border();
            var root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
            root.Children.Add(border);
            root.Children.Add(equationLayer);
            root.Children.Add(caretLayer);
            root.Children.Add(inputLayer);
            root.Children.Add(popup);
            Content = root;

            blink = new DispatcherTimer { Interval = MathEditorView.CaretBlinkInterval };
            blink.Tick += (_, _) =>
            {
                View.BlinkCaret();
                RenderCaret();
            };

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            RightTapped += (_, e) =>
            {
                var at = e.GetPosition(this);
                e.Handled = ShowContextMenu(at.X, at.Y);
            };
            Unloaded += (_, _) =>
            {
                blink.Stop();
                drawn = null;
                View.Reset();
            };
            ActualThemeChanged += (_, _) =>
            {
                ApplyQuickBarTheme();
                RenderEquation();
            };
            RegisterPropertyChangedCallback(ForegroundProperty, (_, _) =>
            {
                RenderEquation();
                RenderCaret();
            });
            RegisterPropertyChangedCallback(BackgroundProperty, (_, _) => ApplyBorder());
            RegisterPropertyChangedCallback(BorderBrushProperty, (_, _) => ApplyBorder());
            RegisterPropertyChangedCallback(BorderThicknessProperty, (_, _) => ApplyBorder());
            RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => InvalidateMeasure());
            RegisterPropertyChangedCallback(PaddingProperty, (_, _) => InvalidateMeasure());
            ApplyBorder();
            ApplyQuickBarTheme();
        }

        /// <summary>Raised when the equation changes, by an edit or by <see cref="Equation"/>.</summary>
        public event EventHandler<EquationChangedEventArgs>? EquationChanged;

        /// <summary>Raised before the context menu opens: a handler can add, remove or change items, or cancel it.</summary>
        public event EventHandler<MathContextMenuEventArgs>? ContextMenuOpening;

        /// <summary>The editing model: the tree of the equation, the caret and the selection, and the commands.</summary>
        public MathEditor Editor { get; }

        /// <summary>The view of the editor without a toolkit, which the control is built on.</summary>
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

        /// <summary>The format of the text copied to the clipboard; null for the input format.</summary>
        public EquationFormat? CopyTextFormat
        {
            get => (EquationFormat?)GetValue(CopyTextFormatProperty);
            set => SetValue(CopyTextFormatProperty, value);
        }

        /// <summary>The symbols the floating bar offers in an empty slot.</summary>
        public IList<string> QuickSymbols { get; } = new List<string> { "α", "β", "θ", "π", "∞", "±", "×", "≤", "≥", "≠", "≈", "→", "∈", "∂", "∑", "∫" };

        internal Canvas EquationLayer => equationLayer;

        internal Canvas CaretLayer => caretLayer;

        internal StackPanel QuickBarPanel => quickBar;

        internal TextBox Input => input;

        /// <summary>Whether the floating bar is open.</summary>
        public bool IsQuickBarOpen => popup.IsOpen;

        /// <summary>The layout of the equation, after a measure.</summary>
        public MathLayout? Layout => View.Layout;

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

        /// <summary>Gives the focus to the editor, so that what is typed goes to it.</summary>
        public bool FocusEditor(FocusState state = FocusState.Programmatic) => input.Focus(state);

        /// <summary>Copies the selection to the clipboard, as text in <see cref="CopyTextFormat"/> (the input format by default).</summary>
        public Task CopyAsync()
        {
            if (View.TryGetCopy(CopyTextFormat, out _, out var text, out _))
                SetClipboard(text);
            return Task.CompletedTask;
        }

        /// <summary>Copies the selection to the clipboard and deletes it.</summary>
        public async Task CutAsync()
        {
            if (IsReadOnly)
                return;
            await CopyAsync().ConfigureAwait(true);
            Editor.DeleteSelection();
        }

        /// <summary>Pastes the text of the clipboard: as MathML when it is MathML, otherwise read in the input format.</summary>
        public async Task PasteAsync()
        {
            if (IsReadOnly)
                return;
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text) && await content.GetTextAsync() is { Length: > 0 } text)
                View.PasteText(text);
        }

        /// <summary>Copies the selection, or the whole equation when nothing is selected, as text in a format (LaTeX as Word writes it).</summary>
        public Task CopyAsAsync(EquationFormat format)
        {
            if (View.GetCopyAsText(format) is { } text)
                SetClipboard(text);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Opens the context menu at a point of the editor, or at the caret: a point outside the selection moves the caret
        /// there first. Returns whether a menu opened.
        /// </summary>
        public bool ShowContextMenu(double? x = null, double? y = null)
        {
            FocusEditor();
            var at = View.PrepareContextMenu(x, y);
            var menu = CreateContextMenu();
            var args = new MathContextMenuEventArgs(menu);
            ContextMenuOpening?.Invoke(this, args);
            if (args.Cancel || menu.Items.Count == 0)
                return false;
            var options = new FlyoutShowOptions();
            if (at is { } rect)
                options.Position = new Windows.Foundation.Point(rect.X, rect.Bottom);
            menu.ShowAt(this, options);
            return true;
        }

        /// <summary>The context menu, built anew each time it opens from <see cref="MathEditorView.GetContextMenu"/>.</summary>
        protected virtual MenuFlyout CreateContextMenu()
        {
            var menu = new MenuFlyout();
            foreach (var item in View.GetContextMenu())
                menu.Items.Add(Item(item));
            return menu;
        }

        /// <inheritdoc/>
        protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
        {
            base.MeasureOverride(availableSize);
            var thickness = BorderThickness;
            View.Font = MathFont ?? MathFonts.Default;
            View.FontSize = FontSize;
            View.Padding = new MathThickness(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom);
            View.BorderThickness = (thickness.Left + thickness.Top + thickness.Right + thickness.Bottom) / 4;
            var size = View.Measure(availableSize.Width);
            if (!ReferenceEquals(drawn, View.Layout))
            {
                RenderEquation();
                RenderCaret();
            }
            return new Windows.Foundation.Size(size.Width, size.Height);
        }

        /// <inheritdoc/>
        protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            View.Arrange(size.Width, size.Height);
            Place(equationLayer);
            Place(caretLayer);
            PlaceInput();
            return size;
        }

        private void Place(Canvas layer) => layer.RenderTransform = new TranslateTransform { X = View.ContentX, Y = View.ContentY };

        private MathBrushes Brushes(bool caret = false) => caret
            ? new MathBrushes(Foreground, caret: CaretBrush ?? Foreground ?? new SolidColorBrush(Colors.Black))
            : new MathBrushes(Foreground, PlaceholderBrush, ActualTheme == ElementTheme.Dark ? DarkError : LightError, SelectionBrush);

        private void RenderEquation()
        {
            drawn = View.Layout;
            View.Render(new MathCanvas(equationLayer, Brushes(), View.ContentX, View.ContentY));
        }

        private void RenderCaret()
        {
            View.RenderCaret(new MathCanvas(caretLayer, Brushes(caret: true), View.ContentX, View.ContentY));
            PlaceInput();
        }

        /// <summary>Puts the transparent input box at the caret, so that the window of the input method follows it.</summary>
        private void PlaceInput()
        {
            if (View.GetCaretRect() is not { } caret)
                return;
            Canvas.SetLeft(input, caret.X);
            Canvas.SetTop(input, caret.Y);
            input.Height = Math.Max(1, caret.Height);
        }

        private void ApplyBorder()
        {
            border.Background = Background;
            border.BorderBrush = BorderBrush;
            border.BorderThickness = BorderThickness;
            InvalidateMeasure();
        }

        private void OnEquationChanged(Equation? equation)
        {
            if (updatingEquation)
                return;
            Editor.Load(equation);
            if (equation != null)
                IsDisplay = equation.IsDisplay;
        }

        private void OnReadOnlyChanged()
        {
            View.IsReadOnly = IsReadOnly;
            RenderCaret();
            UpdateQuickBar();
        }

        private void OnQuickBarEnabledChanged()
        {
            View.IsQuickBarEnabled = IsQuickBarEnabled;
            UpdateQuickBar();
        }

        private void OnFocusChanged(bool focused)
        {
            View.IsFocused = focused;
            if (focused)
                blink.Start();
            else
                blink.Stop();
            RenderCaret();
            UpdateQuickBar();
        }

        private void OnEditorContentChanged(object? sender, EventArgs e)
        {
            var equation = Editor.ToEquation();
            updatingEquation = true;
            try
            {
                Equation = equation;
            }
            finally
            {
                updatingEquation = false;
            }
            if (ViewMode == MathViewMode.Linear != Editor.IsLinear)
                ViewMode = Editor.IsLinear ? MathViewMode.Linear : MathViewMode.Professional;
            EquationChanged?.Invoke(this, new EquationChangedEventArgs(equation));
        }

        private void OnViewChanged(object? sender, MathEditorViewChangedEventArgs e)
        {
            if ((e.Changes & MathEditorViewChanges.Measure) != 0)
                InvalidateMeasure();
            else if ((e.Changes & MathEditorViewChanges.Render) != 0)
                RenderEquation();
            if ((e.Changes & MathEditorViewChanges.Caret) != 0)
                RenderCaret();
            if ((e.Changes & MathEditorViewChanges.QuickBar) != 0)
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdateQuickBar);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
                return;
            PressAt(point.Position.X, point.Position.Y, (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0);
            CapturePointer(e.Pointer);
            e.Handled = true;
        }

        /// <summary>The main button pressed at a point of the control: it takes the focus and the caret goes there.</summary>
        internal void PressAt(double x, double y, bool extend)
        {
            FocusEditor(FocusState.Pointer);
            View.PointerPressed(x, y, extend);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this).Position;
            View.PointerMoved(point.X, point.Y);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (View.PointerReleased())
                ReleasePointerCapture(e.Pointer);
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!composing && !clearingInput)
                TakeInput();
        }

        /// <summary>Text typed in the input box, as a keyboard or an input method types it.</summary>
        internal void Type(string text) => input.Text += text;

        /// <summary>Gives the editor what was typed in the input box, and empties it.</summary>
        private void TakeInput()
        {
            var text = input.Text;
            if (string.IsNullOrEmpty(text))
                return;
            clearingInput = true;
            try
            {
                input.Text = string.Empty;
            }
            finally
            {
                clearingInput = false;
            }
            if (!IsReadOnly)
                Editor.Insert(text);
        }

        private static MathKeyModifiers Modifiers()
        {
            static bool Down(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
            var modifiers = MathKeyModifiers.None;
            if (Down(VirtualKey.Shift))
                modifiers |= MathKeyModifiers.Shift;
            if (Down(VirtualKey.Control))
                modifiers |= MathKeyModifiers.Control;
            if (Down(VirtualKey.Menu))
                modifiers |= MathKeyModifiers.Alt;
            if (Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows))
                modifiers |= MathKeyModifiers.Meta;
            return modifiers;
        }

        private static MathKey? ToMathKey(VirtualKey key) => key switch
        {
            VirtualKey.Left => MathKey.Left,
            VirtualKey.Right => MathKey.Right,
            VirtualKey.Up => MathKey.Up,
            VirtualKey.Down => MathKey.Down,
            VirtualKey.Home => MathKey.Home,
            VirtualKey.End => MathKey.End,
            VirtualKey.Back => MathKey.Backspace,
            VirtualKey.Delete => MathKey.Delete,
            VirtualKey.Tab => MathKey.Tab,
            VirtualKey.Enter => MathKey.Enter,
            VirtualKey.Escape => MathKey.Escape,
            _ => null,
        };

        private void OnInputKeyDown(object sender, KeyRoutedEventArgs e) => e.Handled = HandleKey(e.Key, Modifiers());

        /// <summary>Acts on a key pressed with modifiers: the context menu, the floating bar, the shortcuts, then the editor.</summary>
        /// <returns>Whether the key was used.</returns>
        internal bool HandleKey(VirtualKey key, MathKeyModifiers modifiers)
        {
            // Command on macOS, Control elsewhere
            var command = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()
                ? (modifiers & MathKeyModifiers.Meta) != 0
                : (modifiers & MathKeyModifiers.Control) != 0;
            var shift = (modifiers & MathKeyModifiers.Shift) != 0;

            if (key == VirtualKey.Application || key == VirtualKey.F10 && modifiers == MathKeyModifiers.Shift)
                return ShowContextMenu();

            var mathKey = ToMathKey(key);
            if (popup.IsOpen && mathKey is { } quickBarKey && View.ProcessQuickBarKey(quickBarKey, modifiers))
            {
                UpdateQuickBar();
                return true;
            }

            switch (key)
            {
                case VirtualKey.L when command && shift:
                    ViewMode = ViewMode == MathViewMode.Linear ? MathViewMode.Professional : MathViewMode.Linear;
                    return true;
                case VirtualKey.C when command:
                    _ = CopyAsync();
                    return true;
                case VirtualKey.X when command:
                    _ = CutAsync();
                    return true;
                case VirtualKey.V when command:
                    _ = PasteAsync();
                    return true;
                case VirtualKey.Z when command && shift && !IsReadOnly:
                case VirtualKey.Y when command && !IsReadOnly:
                    Editor.Redo();
                    return true;
                case VirtualKey.Z when command && !IsReadOnly:
                    Editor.Undo();
                    return true;
                case VirtualKey.A when command:
                    Editor.SelectAll();
                    return true;
                default:
                    return mathKey is { } editorKey && Editor.ProcessKey(editorKey, modifiers, Layout, IsReadOnly);
            }
        }

        private MenuFlyoutItemBase Item(MathMenuItem item)
        {
            if (item.IsSeparator)
                return new MenuFlyoutSeparator();
            var command = item.Command!;
            if (item.Items.Count > 0)
            {
                var submenu = new MenuFlyoutSubItem { Text = item.Header, Tag = command, IsEnabled = item.IsEnabled };
                foreach (var child in item.Items)
                    submenu.Items.Add(Item(child));
                return submenu;
            }
            var menuItem = new MenuFlyoutItem { Text = item.Header, Tag = command, IsEnabled = item.IsEnabled, KeyboardAcceleratorTextOverride = Gesture(command) };
            menuItem.Click += (_, _) =>
            {
                Execute(command);
                FocusEditor();
            };
            return menuItem;
        }

        private static string Gesture(string command)
        {
            var key = OperatingSystem.IsMacOS() ? "⌘" : "Ctrl+";
            return command switch
            {
                "undo" => key + "Z",
                "redo" => key + "Y",
                "cut" => key + "X",
                "copy" => key + "C",
                "paste" => key + "V",
                "select-all" => key + "A",
                "view" => key + "Shift+L",
                "new-line" => "Shift+Enter",
                _ => string.Empty,
            };
        }

        private void Execute(string command)
        {
            switch (command)
            {
                case "cut":
                    _ = CutAsync();
                    break;
                case "copy":
                    _ = CopyAsync();
                    break;
                case "paste":
                    _ = PasteAsync();
                    break;
                case "view":
                    ViewMode = ViewMode == MathViewMode.Linear ? MathViewMode.Professional : MathViewMode.Linear;
                    break;
                case var _ when MathEditorView.CopyAsFormat(command) is { } format:
                    _ = CopyAsAsync(format);
                    break;
                default:
                    View.ExecuteCommand(command);
                    break;
            }
        }

        private static void SetClipboard(string text)
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }

        /// <summary>Opens the floating bar under the equation, at the caret, with what fits the place of the caret, or closes it.</summary>
        private void UpdateQuickBar()
        {
            if (View.UpdateQuickBar() || View.QuickBar.Mode != MathQuickBarMode.Completions)
                FillQuickBar();
            Highlight();
            if (View.QuickBarAnchor is not { } anchor)
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
                    MinWidth = completions ? 130 : View.QuickBar.Mode == MathQuickBarMode.Symbols ? 30 : 40,
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalContentAlignment = completions ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    AllowFocusOnInteraction = false,
                    IsTabStop = false,
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                };
                ToolTipService.SetToolTip(button, item.ToolTip);
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
            var dark = ActualTheme == ElementTheme.Dark;
            var highlight = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(0x60, 0x4C, 0xA6, 0xFF) : Windows.UI.Color.FromArgb(0x40, 0x33, 0x99, 0xFF));
            for (var i = 0; i < quickBar.Children.Count; i++)
            {
                if (quickBar.Children[i] is Button button)
                    button.Background = i == selected ? highlight : new SolidColorBrush(Colors.Transparent);
            }
        }

        private void ApplyQuickBarTheme()
        {
            var dark = ActualTheme == ElementTheme.Dark;
            if (popup.Child is Border frame)
            {
                frame.Background = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(0xF8, 0x2B, 0x2B, 0x2B) : Windows.UI.Color.FromArgb(0xF8, 0xFB, 0xFB, 0xFD));
                frame.BorderBrush = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(0x80, 0x70, 0x70, 0x70) : Windows.UI.Color.FromArgb(0x60, 0x80, 0x80, 0x80));
            }
            selectedButton = -2;
            Highlight();
        }
    }
}
