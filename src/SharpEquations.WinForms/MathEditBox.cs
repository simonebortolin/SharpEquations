namespace SharpEquations.WinForms
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using SkiaSharp;

    /// <summary>
    /// A real-time equation editor for Windows Forms, in the manner of the RichEditBox of WinUI in math mode and of the
    /// math zones of Word: UnicodeMath or LaTeX typed at the caret builds up into the equation when a space or an operator
    /// ends an argument, and the content is read and written as MathML (<see cref="GetMath"/>, <see cref="SetMath"/>) or
    /// as an <see cref="SharpEquations.Equation"/>. It is drawn with Skia, and adapts the view of Rendering
    /// (<see cref="View"/>), which does the rest. With <see cref="AutoSize"/> (the default) its height follows the
    /// equation, as that of a <see cref="TextBox"/> follows its font.
    /// </summary>
    public class MathEditBox : Control
    {
        /// <summary>The format of MathML on the clipboard of Windows, which Word reads and writes as UTF-8 (<see cref="MathClipboard"/>).</summary>
        public const string MathMLClipboardFormat = "MathML";

        private const int WM_CONTEXTMENU = 0x007B;
        private static readonly SKColor ErrorColor = new SKColor(0xFF, 0xE4, 0xE1);
        private readonly MathSurface surface = new MathSurface();
        private readonly Timer blink;
        private QuickBar? quickBar;
        private Form? quickBarOwner;
        private MathAutoCorrect autoCorrect;
        private SKPicture? content;
        private MathLayout? recorded;
        private ContextMenuStrip? openMenu;
        private char pendingHighSurrogate;
        private bool updatingEquation;
        private Equation? equation;
        private EquationFormat inputFormat = EquationFormat.UnicodeMath;
        private MathViewMode viewMode;
        private bool isDisplay = true;
        private bool isReadOnly;
        private bool isQuickBarEnabled = true;
        private bool isAutoCorrectEnabled = true;
        private EquationFormat? copyTextFormat;
        private Color selectionColor = Color.FromArgb(0x60, 0x33, 0x99, 0xFF);
        private Color caretColor = Color.Empty;
        private Color placeholderColor = Color.Gray;
        private Color borderColor = Color.Gray;
        private int borderWidth = 1;
        private OpenTypeMathFont? mathFont;
        private double? fontSize;

        /// <summary>An empty editor.</summary>
        public MathEditBox()
        {
            // the model and the view first: setting the padding and the colours below lays out and draws again
            Editor = new MathEditor { IsDisplay = true };
            Editor.ContentChanged += OnEditorContentChanged;
            View = new MathEditorView(Editor);
            View.Changed += OnViewChanged;
            autoCorrect = Editor.AutoCorrect ?? new MathAutoCorrect();

            blink = new Timer { Interval = (int)MathEditorView.CaretBlinkInterval.TotalMilliseconds };
            blink.Tick += (_, _) =>
            {
                View.BlinkCaret();
                InvalidateCaret();
            };

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            SetStyle(ControlStyles.StandardDoubleClick, false);
            TabStop = true;
            Cursor = Cursors.IBeam;
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            Padding = new Padding(6, 4, 6, 4);
            base.AutoSize = true;
            Size = new Size(200, 40);
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
        [Browsable(false)]
        public MathEditor Editor { get; }

        /// <summary>
        /// The view of the editor without a toolkit, which the control is built on: the layout and where it is drawn, the
        /// pointer, the floating bar, the context menu and the clipboard. Its coordinates are device-independent pixels.
        /// </summary>
        [Browsable(false)]
        public MathEditorView View { get; }

        /// <summary>The equation edited, with what is not built up yet built up where it parses; null when empty.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Equation? Equation
        {
            get => equation;
            set
            {
                if (ReferenceEquals(equation, value))
                    return;
                equation = value;
                if (updatingEquation)
                    return;
                Editor.Load(value);
                if (value != null)
                    IsDisplay = value.IsDisplay;
            }
        }

        /// <summary>The format of what is typed: UnicodeMath (the default) or LaTeX.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The format is neither UnicodeMath nor LaTeX.</exception>
        [DefaultValue(EquationFormat.UnicodeMath)]
        public EquationFormat InputFormat
        {
            get => inputFormat;
            set
            {
                if (value is not (EquationFormat.UnicodeMath or EquationFormat.LaTeX))
                    throw new ArgumentOutOfRangeException(nameof(value), "What is typed is UnicodeMath or LaTeX.");
                inputFormat = value;
                Editor.InputFormat = value;
            }
        }

        /// <summary>Whether the equation is shown built up or as linear text.</summary>
        [DefaultValue(MathViewMode.Professional)]
        public MathViewMode ViewMode
        {
            get => viewMode;
            set
            {
                if (viewMode == value)
                    return;
                viewMode = value;
                Editor.SetLinear(value == MathViewMode.Linear);
            }
        }

        /// <summary>Whether the equation is a display equation rather than inline.</summary>
        [DefaultValue(true)]
        public bool IsDisplay
        {
            get => isDisplay;
            set
            {
                isDisplay = value;
                Editor.IsDisplay = value;
            }
        }

        /// <summary>Whether the user cannot change the equation.</summary>
        [DefaultValue(false)]
        public bool IsReadOnly
        {
            get => isReadOnly;
            set
            {
                isReadOnly = value;
                View.IsReadOnly = value;
                InvalidateCaret();
                UpdateQuickBar();
            }
        }

        /// <summary>The colour of the selection.</summary>
        public Color SelectionColor
        {
            get => selectionColor;
            set
            {
                selectionColor = value;
                InvalidateContent();
            }
        }

        /// <summary>The colour of the caret; <see cref="Color.Empty"/> for the foreground.</summary>
        public Color CaretColor
        {
            get => caretColor;
            set
            {
                caretColor = value;
                InvalidateCaret();
            }
        }

        /// <summary>The colour of the boxes of the empty slots.</summary>
        public Color PlaceholderColor
        {
            get => placeholderColor;
            set
            {
                placeholderColor = value;
                InvalidateContent();
            }
        }

        /// <summary>The colour of the border; <see cref="Color.Empty"/> for none.</summary>
        public Color BorderColor
        {
            get => borderColor;
            set
            {
                borderColor = value;
                Invalidate();
            }
        }

        /// <summary>The width of the border, in device-independent pixels; 1 by default.</summary>
        [DefaultValue(1)]
        public int BorderWidth
        {
            get => borderWidth;
            set
            {
                borderWidth = Math.Max(0, value);
                Relayout();
            }
        }

        /// <summary>The math font; null for <see cref="MathFonts.Default"/>.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OpenTypeMathFont? MathFont
        {
            get => mathFont;
            set
            {
                mathFont = value;
                Relayout();
            }
        }

        /// <summary>The size of the font, in device-independent pixels; by default that of <see cref="Control.Font"/>.</summary>
        public double FontSize
        {
            get => fontSize ?? Font.SizeInPoints * 96 / 72;
            set
            {
                if (!(value > 0))
                    throw new ArgumentOutOfRangeException(nameof(value), "The size of the font is positive.");
                fontSize = value;
                Relayout();
            }
        }

        /// <summary>Whether the height of the control follows the equation; true by default. The width is the one it is given.</summary>
        [DefaultValue(true)]
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override bool AutoSize
        {
            get => base.AutoSize;
            set
            {
                base.AutoSize = value;
                Relayout();
            }
        }

        /// <summary>Whether the floating bar shows completions, structures, table commands and symbols at the caret.</summary>
        [DefaultValue(true)]
        public bool IsQuickBarEnabled
        {
            get => isQuickBarEnabled;
            set
            {
                isQuickBarEnabled = value;
                View.IsQuickBarEnabled = value;
                UpdateQuickBar();
            }
        }

        /// <summary>Whether the floating bar is open.</summary>
        [Browsable(false)]
        public bool IsQuickBarOpen => quickBar is { Visible: true };

        /// <summary>The symbols the floating bar offers in an empty slot.</summary>
        [Browsable(false)]
        public IList<string> QuickSymbols { get; } = new List<string> { "α", "β", "θ", "π", "∞", "±", "×", "≤", "≥", "≠", "≈", "→", "∈", "∂", "∑", "∫" };

        /// <summary>Whether the editor opens its context menu.</summary>
        [DefaultValue(true)]
        public bool IsContextMenuEnabled { get; set; } = true;

        /// <summary>The context menu while it is open, or null.</summary>
        [Browsable(false)]
        public ContextMenuStrip? OpenContextMenu => openMenu is { Visible: true } ? openMenu : null;

        /// <summary>
        /// The format of the text that <see cref="Copy"/> puts on the clipboard; null (the default) for the input format.
        /// MathML pastes as an equation into Word.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The format is MTEF, which is read only.</exception>
        [DefaultValue(null)]
        public EquationFormat? CopyTextFormat
        {
            get => copyTextFormat;
            set => copyTextFormat = value is EquationFormat.Mtef ? throw new ArgumentOutOfRangeException(nameof(value), "MTEF is read only.") : value;
        }

        /// <summary>
        /// The rules of the math autocorrect (<see cref="MathEditor.AutoCorrect"/>): <c>-&gt;</c> becomes → and
        /// <c>\alpha</c> and a space α as they are typed; <see cref="IsAutoCorrectEnabled"/> turns it off.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
        [DefaultValue(true)]
        public bool IsAutoCorrectEnabled
        {
            get => isAutoCorrectEnabled;
            set
            {
                isAutoCorrectEnabled = value;
                Editor.AutoCorrect = value ? autoCorrect : null;
            }
        }

        /// <summary>The number of the equation, shown at the right of its line; null without one (<see cref="MathEditor.EquationNumber"/>).</summary>
        [DefaultValue(null)]
        public string? EquationNumber
        {
            get => Editor.EquationNumber;
            set => Editor.SetEquationNumber(value);
        }

        /// <summary>The layout of the equation, after a measure, in device-independent pixels.</summary>
        [Browsable(false)]
        public MathLayout? EquationLayout => View.Layout;

        /// <summary>The scale of the device-independent pixels of the view to the pixels of the screen.</summary>
        internal float DpiScale => DeviceDpi / 96f;

        internal MathSurface Surface => surface;

        internal QuickBar? QuickBarWindow => quickBar;

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
            if (!View.TryGetCopy(CopyTextFormat, out var copied, out var text, out var mathML))
                return;
            var data = new DataObject();
            data.SetText(text);
            data.SetData(MathMLClipboardFormat, new MemoryStream(MathClipboard.Encode(mathML)));
            var args = new MathCopyingEventArgs(copied, data);
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
        /// Opens the context menu at a point of the editor, in pixels, or at the caret: a point outside the selection
        /// moves the caret there first, as text editors do. Returns whether a menu opened.
        /// </summary>
        public bool ShowContextMenu(Point? point = null)
        {
            if (!IsContextMenuEnabled)
                return false;
            Focus();
            var scale = DpiScale;
            var at = View.PrepareContextMenu(point?.X / (double)scale, point?.Y / (double)scale);
            openMenu?.Close();
            var menu = CreateContextMenu();
            var args = new MathContextMenuEventArgs(menu);
            MathContextMenuOpening?.Invoke(this, args);
            if (args.Cancel || menu.Items.Count == 0)
            {
                menu.Dispose();
                return false;
            }
            menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
            openMenu = menu;
            var location = at is { } rect ? new Point((int)(rect.X * scale), (int)Math.Ceiling(rect.Bottom * scale)) : point ?? Point.Empty;
            menu.Show(this, location);
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
        protected virtual ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            foreach (var item in View.GetContextMenu())
                menu.Items.Add(Item(item));
            return menu;
        }

        /// <inheritdoc/>
        public override Size GetPreferredSize(Size proposedSize)
        {
            var scale = DpiScale;
            var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
            var size = Measure(width);
            return new Size(width, (int)Math.Ceiling(size.Height * scale));
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (AutoSize && View.Font != null)
                height = (int)Math.Ceiling(Measure(width).Height * DpiScale);
            base.SetBoundsCore(x, y, width, height, specified);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                blink.Dispose();
                quickBar?.Dispose();
                openMenu?.Dispose();
                content?.Dispose();
                surface.Dispose();
                SetQuickBarOwner(null);
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override AccessibleObject CreateAccessibilityInstance() => new MathEditBoxAccessibleObject(this);

        /// <inheritdoc/>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Relayout();
        }

        /// <inheritdoc/>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            blink.Stop();
            quickBar?.Hide();
            InvalidateContent();
            View.Reset();
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            var scale = DpiScale;
            if (View.Layout == null)
                Measure(Width);
            View.Arrange(ClientSize.Width / scale, ClientSize.Height / scale);
            using (var canvas = surface.Begin(ClientSize, MathCanvas.ToSkia(BackColor), scale))
            {
                var thickness = View.BorderThickness;
                if (thickness > 0 && !borderColor.IsEmpty)
                {
                    using var pen = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)thickness, Color = MathCanvas.ToSkia(borderColor) };
                    var half = (float)thickness / 2;
                    canvas.DrawRect(half, half, Math.Max(0, ClientSize.Width / scale - (float)thickness), Math.Max(0, ClientSize.Height / scale - (float)thickness), pen);
                }
                // the layout can change without a change of the view (a measure of the parent's layout): draw what is laid out
                if (content == null || !ReferenceEquals(recorded, View.Layout))
                {
                    content?.Dispose();
                    content = RecordContent();
                    recorded = View.Layout;
                }
                canvas.DrawPicture(content);
                using var caret = new MathCanvas(canvas, new MathColors(Foreground, caret: caretColor.IsEmpty ? Foreground : MathCanvas.ToSkia(caretColor)));
                View.RenderCaret(caret);
            }
            surface.Present(e.Graphics);
        }

        /// <inheritdoc/>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            InvalidateContent();
            UpdateQuickBar();
        }

        /// <inheritdoc/>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Relayout();
        }

        /// <inheritdoc/>
        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            Relayout();
        }

        /// <inheritdoc/>
        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            InvalidateContent();
        }

        /// <inheritdoc/>
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            Relayout();
        }

        /// <inheritdoc/>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            OnFocusChanged(true);
        }

        /// <inheritdoc/>
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            OnFocusChanged(false);
        }

        /// <inheritdoc/>
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
        {
            Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End => true,
            _ => base.IsInputKey(keyData),
        };

        /// <inheritdoc/>
        protected override bool ProcessDialogKey(Keys keyData)
        {
            // Tab, Enter and Escape go to the editor first (the next slot, a new row, the completions), then to the form
            if ((keyData & Keys.KeyCode) is Keys.Tab or Keys.Enter or Keys.Escape && HandleKey(keyData & Keys.KeyCode, Modifiers(keyData)))
                return true;
            return base.ProcessDialogKey(keyData);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && HandleKey(e.KeyCode, Modifiers(e.KeyData)))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.Handled)
                return;
            var ch = e.KeyChar;
            if (char.IsHighSurrogate(ch))
            {
                pendingHighSurrogate = ch;
                e.Handled = true;
                return;
            }
            var text = char.IsLowSurrogate(ch) && pendingHighSurrogate != '\0' ? new string(new[] { pendingHighSurrogate, ch }) : ch.ToString();
            pendingHighSurrogate = '\0';
            e.Handled = Type(text);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            PressAt(e.X / DpiScale, e.Y / DpiScale, (ModifierKeys & Keys.Shift) != 0);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            View.PointerMoved(e.X / DpiScale, e.Y / DpiScale);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && View.PointerReleased())
                Capture = false;
        }

        /// <inheritdoc/>
        protected override void WndProc(ref Message m)
        {
            // a right click, the Menu key or Shift+F10: the keyboard sends no point (-1, -1), and the menu opens at the caret
            if (m.Msg == WM_CONTEXTMENU)
            {
                var lParam = unchecked((int)(long)m.LParam);
                Point? point = lParam == -1 ? null : PointToClient(new Point(unchecked((short)(lParam & 0xFFFF)), unchecked((short)((lParam >> 16) & 0xFFFF))));
                ShowContextMenu(point);
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>The main button pressed at a point of the control, in device-independent pixels: it takes the focus and the caret goes there.</summary>
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

        /// <summary>Acts on a key pressed with modifiers: the floating bar, the shortcuts, then the editor.</summary>
        /// <returns>Whether the key was used.</returns>
        internal bool HandleKey(Keys key, MathKeyModifiers modifiers)
        {
            var control = (modifiers & MathKeyModifiers.Control) != 0;
            var shift = (modifiers & MathKeyModifiers.Shift) != 0;

            var mathKey = ToMathKey(key);
            if (IsQuickBarOpen && mathKey is { } quickBarKey && View.ProcessQuickBarKey(quickBarKey, modifiers))
            {
                UpdateQuickBar();
                return true;
            }

            switch (key)
            {
                case Keys.L when control && shift:
                    ToggleViewMode();
                    return true;
                case Keys.C when control:
                case Keys.Insert when control:
                    Copy();
                    return true;
                case Keys.X when control:
                case Keys.Delete when shift && !control:
                    Cut();
                    return true;
                case Keys.V when control:
                case Keys.Insert when shift && !control:
                    Paste();
                    return true;
                case Keys.Z when control && shift && !IsReadOnly:
                case Keys.Y when control && !IsReadOnly:
                    Editor.Redo();
                    return true;
                case Keys.Z when control && !IsReadOnly:
                    Editor.Undo();
                    return true;
                case Keys.A when control:
                    Editor.SelectAll();
                    return true;
                default:
                    return mathKey is { } editorKey && Editor.ProcessKey(editorKey, modifiers, EquationLayout, IsReadOnly);
            }
        }

        /// <summary>Draws the selection and the equation into a picture, which each paint draws again under the caret.</summary>
        private SKPicture RecordContent()
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(SKRect.Create(0, 0, (float)View.Size.Width, (float)View.Size.Height));
            using (var math = new MathCanvas(canvas, new MathColors(Foreground, MathCanvas.ToSkia(placeholderColor), ErrorColor, MathCanvas.ToSkia(selectionColor))))
                View.Render(math);
            return recorder.EndRecording();
        }

        private SKColor Foreground => MathCanvas.ToSkia(ForeColor);

        private static MathKey? ToMathKey(Keys key) => key switch
        {
            Keys.Left => MathKey.Left,
            Keys.Right => MathKey.Right,
            Keys.Up => MathKey.Up,
            Keys.Down => MathKey.Down,
            Keys.Home => MathKey.Home,
            Keys.End => MathKey.End,
            Keys.Back => MathKey.Backspace,
            Keys.Delete => MathKey.Delete,
            Keys.Tab => MathKey.Tab,
            Keys.Enter => MathKey.Enter,
            Keys.Escape => MathKey.Escape,
            _ => null,
        };

        private static MathKeyModifiers Modifiers(Keys keyData) =>
            ((keyData & Keys.Shift) != 0 ? MathKeyModifiers.Shift : MathKeyModifiers.None)
            | ((keyData & Keys.Control) != 0 ? MathKeyModifiers.Control : MathKeyModifiers.None)
            | ((keyData & Keys.Alt) != 0 ? MathKeyModifiers.Alt : MathKeyModifiers.None);

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

        /// <summary>Gives the view its font, size, padding and border, and lays out the equation for a width in pixels.</summary>
        private MathSize Measure(int width)
        {
            var scale = DpiScale;
            View.Font = MathFont ?? MathFonts.Default;
            View.FontSize = FontSize;
            View.Padding = new MathThickness(Padding.Left / scale, Padding.Top / scale, Padding.Right / scale, Padding.Bottom / scale);
            View.BorderThickness = borderWidth;
            var size = View.Measure(width > 0 ? width / (double)scale : double.PositiveInfinity);
            if (!ReferenceEquals(recorded, View.Layout))
                Invalidate();
            return size;
        }

        /// <summary>Lays out the equation again, and gives the control the height it takes.</summary>
        private void Relayout()
        {
            var size = Measure(Width);
            if (AutoSize)
            {
                var height = (int)Math.Ceiling(size.Height * DpiScale);
                if (height != Height)
                    Height = height;
            }
            InvalidateContent();
            UpdateQuickBar();
        }

        private void InvalidateContent()
        {
            content?.Dispose();
            content = null;
            Invalidate();
        }

        private void InvalidateCaret()
        {
            if (View.GetCaretRect() is not { } caret)
            {
                Invalidate();
                return;
            }
            var scale = DpiScale;
            Invalidate(new Rectangle((int)Math.Floor((caret.X - 2) * scale), (int)Math.Floor((caret.Y - 1) * scale), (int)Math.Ceiling(5 * scale), (int)Math.Ceiling((caret.Height + 2) * scale)));
        }

        private bool ShouldSerializeSelectionColor() => selectionColor != Color.FromArgb(0x60, 0x33, 0x99, 0xFF);

        private void ResetSelectionColor() => SelectionColor = Color.FromArgb(0x60, 0x33, 0x99, 0xFF);

        private bool ShouldSerializeCaretColor() => !caretColor.IsEmpty;

        private void ResetCaretColor() => CaretColor = Color.Empty;

        private bool ShouldSerializePlaceholderColor() => placeholderColor != Color.Gray;

        private void ResetPlaceholderColor() => PlaceholderColor = Color.Gray;

        private bool ShouldSerializeBorderColor() => borderColor != Color.Gray;

        private void ResetBorderColor() => BorderColor = Color.Gray;

        private bool ShouldSerializeFontSize() => fontSize.HasValue;

        private void ResetFontSize()
        {
            fontSize = null;
            Relayout();
        }

        private void ToggleViewMode() => ViewMode = ViewMode == MathViewMode.Linear ? MathViewMode.Professional : MathViewMode.Linear;

        private ToolStripItem Item(MathMenuItem item)
        {
            if (item.IsSeparator)
                return new ToolStripSeparator();
            var command = item.Command!;
            var menuItem = new ToolStripMenuItem(item.Header) { Tag = command, Enabled = item.IsEnabled, ShortcutKeyDisplayString = Gesture(command) };
            if (item.Items.Count > 0)
            {
                foreach (var child in item.Items)
                    menuItem.DropDownItems.Add(Item(child));
                return menuItem;
            }
            menuItem.Click += (_, _) =>
            {
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

        private void OnFocusChanged(bool focused)
        {
            View.IsFocused = focused;
            if (focused)
                blink.Start();
            else
                blink.Stop();
            InvalidateCaret();
            UpdateQuickBar();
        }

        private void OnEditorContentChanged(object? sender, EventArgs e)
        {
            var edited = Editor.ToEquation();
            updatingEquation = true;
            try
            {
                Equation = edited;
            }
            finally
            {
                updatingEquation = false;
            }
            if (ViewMode == MathViewMode.Linear != Editor.IsLinear)
                viewMode = Editor.IsLinear ? MathViewMode.Linear : MathViewMode.Professional;
            EquationChanged?.Invoke(this, new EquationChangedEventArgs(edited));
        }

        private void OnViewChanged(object? sender, MathEditorViewChangedEventArgs e)
        {
            if ((e.Changes & MathEditorViewChanges.Measure) != 0)
                Relayout();
            else if ((e.Changes & MathEditorViewChanges.Render) != 0)
                InvalidateContent();
            if ((e.Changes & MathEditorViewChanges.Caret) != 0)
                Invalidate();
            if ((e.Changes & MathEditorViewChanges.QuickBar) != 0 && IsHandleCreated)
                BeginInvoke(new Action(UpdateQuickBar));
        }

        /// <summary>Opens the floating bar under the equation, at the caret, with what fits the place of the caret, or closes it.</summary>
        private void UpdateQuickBar()
        {
            if (!IsHandleCreated || IsDisposed)
                return;
            var refill = View.UpdateQuickBar() || View.QuickBar.Mode != MathQuickBarMode.Completions;
            if (View.QuickBarAnchor is not { } anchor || FindForm() is not { Visible: true } form)
            {
                quickBar?.Hide();
                return;
            }
            var bar = quickBar ??= new QuickBar();
            SetQuickBarOwner(form);
            var completions = View.QuickBar.Mode == MathQuickBarMode.Completions;
            if (refill || bar.Count == 0)
                bar.Fill(View.QuickBar.GetItems(QuickSymbols), completions, completions ? 130 : View.QuickBar.Mode == MathQuickBarMode.Symbols ? 26 : 36);
            bar.Highlight(completions ? View.QuickBar.SelectedIndex : -1);
            var scale = DpiScale;
            bar.Location = PointToScreen(new Point((int)(anchor.X * scale), (int)Math.Ceiling(anchor.Bottom * scale)));
            if (!bar.Visible)
                bar.Show(form);
        }

        /// <summary>Follows the form the floating bar belongs to: the bar moves with it.</summary>
        private void SetQuickBarOwner(Form? form)
        {
            if (ReferenceEquals(quickBarOwner, form))
                return;
            if (quickBarOwner != null)
                quickBarOwner.Move -= OnOwnerMoved;
            quickBarOwner = form;
            if (form != null)
                form.Move += OnOwnerMoved;
        }

        private void OnOwnerMoved(object? sender, EventArgs e) => UpdateQuickBar();
    }
}
