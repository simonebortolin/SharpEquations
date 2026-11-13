namespace SharpEquations.Rendering
{
    using System;
    using System.Collections.Generic;
    using SharpEquations.Editing;

    /// <summary>A size.</summary>
    public readonly struct MathSize
    {
        /// <summary>A size.</summary>
        public MathSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>The width.</summary>
        public double Width { get; }

        /// <summary>The height.</summary>
        public double Height { get; }
    }

    /// <summary>The space inside the border of a control, on each side.</summary>
    public readonly struct MathThickness
    {
        /// <summary>The same space on each side.</summary>
        public MathThickness(double uniform)
            : this(uniform, uniform, uniform, uniform)
        {
        }

        /// <summary>A space on each side.</summary>
        public MathThickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>The left.</summary>
        public double Left { get; }

        /// <summary>The top.</summary>
        public double Top { get; }

        /// <summary>The right.</summary>
        public double Right { get; }

        /// <summary>The bottom.</summary>
        public double Bottom { get; }
    }

    /// <summary>What a change of the editor asks the control to do again (<see cref="MathEditorView.Changed"/>).</summary>
    [Flags]
    public enum MathEditorViewChanges
    {
        /// <summary>Nothing.</summary>
        None = 0,

        /// <summary>Measure again: the equation changed, and is laid out again.</summary>
        Measure = 1,

        /// <summary>Draw again: the selection changed.</summary>
        Render = 2,

        /// <summary>Draw the caret again, and tell the input method where it is.</summary>
        Caret = 4,

        /// <summary>Update the floating bar (<see cref="MathEditorView.UpdateQuickBar"/>), once the equation is laid out again.</summary>
        QuickBar = 8,
    }

    /// <summary>The arguments of <see cref="MathEditorView.Changed"/>.</summary>
    public sealed class MathEditorViewChangedEventArgs : EventArgs
    {
        /// <summary>Arguments with what changed.</summary>
        public MathEditorViewChangedEventArgs(MathEditorViewChanges changes) => Changes = changes;

        /// <summary>What the control does again.</summary>
        public MathEditorViewChanges Changes { get; }
    }

    /// <summary>An item of the context menu of an editor (<see cref="MathEditorView.GetContextMenu"/>), or a separator.</summary>
    public sealed class MathMenuItem
    {
        private MathMenuItem(string? header, string? command, bool isEnabled, IReadOnlyList<MathMenuItem> items)
        {
            Header = header;
            Command = command;
            IsEnabled = isEnabled;
            Items = items;
        }

        /// <summary>A separator.</summary>
        public static MathMenuItem Separator { get; } = new MathMenuItem(null, null, false, Array.Empty<MathMenuItem>());

        /// <summary>The text of the item; null for a separator.</summary>
        public string? Header { get; }

        /// <summary>The command of the item (<c>undo</c>, <c>copy</c>, <c>copy-as-latex</c>...); null for a separator.</summary>
        public string? Command { get; }

        /// <summary>Whether the item can be chosen.</summary>
        public bool IsEnabled { get; }

        /// <summary>The items of its submenu; empty for an item that has none.</summary>
        public IReadOnlyList<MathMenuItem> Items { get; }

        /// <summary>Whether the item is a separator.</summary>
        public bool IsSeparator => Command == null;

        internal static MathMenuItem Item(string header, string command, bool isEnabled = true) => new MathMenuItem(header, command, isEnabled, Array.Empty<MathMenuItem>());

        internal static MathMenuItem Submenu(string header, string command, bool isEnabled, params MathMenuItem[] items) => new MathMenuItem(header, command, isEnabled, items);
    }

    /// <summary>
    /// The view of an editor without a toolkit: what an equation editor control does besides talking to its toolkit. It
    /// lays out the equation of the <see cref="Editor"/> and knows where it is in the control (inside the border and the
    /// padding, in the middle of the height), turns the pointer into positions of the caret, draws the selection, the
    /// equation and the blinking caret on an <see cref="IMathCanvas"/>, places the floating bar, makes the context menu
    /// and reads and writes what the clipboard holds. The control gives it its keys, pointer, focus and size, raises
    /// what it asks on <see cref="Changed"/>, and keeps for itself the clipboard, the popups, the timer of the caret and
    /// the input method.
    /// </summary>
    public sealed class MathEditorView
    {
        private readonly MathLayoutCache layoutCache = new MathLayoutCache();
        private (MathNode Root, IMathFont Font, double FontSize, double? LineWidth)? laidOut;
        private MathSelection? drawnSelection;
        private bool selecting;
        private bool isFocused;

        /// <summary>The view of an editor.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="editor"/> is null.</exception>
        public MathEditorView(MathEditor editor)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            QuickBar = new MathQuickBarState(editor);
            editor.Changed += OnEditorChanged;
        }

        /// <summary>The time the caret is shown, then hidden, as it blinks (<see cref="BlinkCaret"/>).</summary>
        public static TimeSpan CaretBlinkInterval { get; } = TimeSpan.FromMilliseconds(530);

        /// <summary>Raised when a change of the editor asks the control to measure or draw again.</summary>
        public event EventHandler<MathEditorViewChangedEventArgs>? Changed;

        /// <summary>The editor.</summary>
        public MathEditor Editor { get; }

        /// <summary>What the floating bar shows (<see cref="UpdateQuickBar"/>).</summary>
        public MathQuickBarState QuickBar { get; }

        /// <summary>The math font; needed to <see cref="Measure"/>.</summary>
        public IMathFont? Font { get; set; }

        /// <summary>The size of the font.</summary>
        public double FontSize { get; set; } = 16;

        /// <summary>The space between the border and the equation.</summary>
        public MathThickness Padding { get; set; }

        /// <summary>The thickness of the border.</summary>
        public double BorderThickness { get; set; }

        /// <summary>Whether the user cannot change the equation: no caret, no floating bar, no command that edits.</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>Whether the floating bar shows at the caret.</summary>
        public bool IsQuickBarEnabled { get; set; } = true;

        /// <summary>Whether the control has the focus: it shows the caret, shown again at once when the focus comes.</summary>
        public bool IsFocused
        {
            get => isFocused;
            set
            {
                if (value && !isFocused)
                    IsCaretVisible = true;
                isFocused = value;
            }
        }

        /// <summary>Whether the blinking caret is shown now.</summary>
        public bool IsCaretVisible { get; private set; } = true;

        /// <summary>The size of the control, from <see cref="Arrange"/>.</summary>
        public MathSize Size { get; private set; }

        /// <summary>The layout of the equation, after <see cref="Measure"/>.</summary>
        public MathLayout? Layout { get; private set; }

        /// <summary>
        /// The rectangle the floating bar is placed under, in the coordinates of the control, after
        /// <see cref="UpdateQuickBar"/>: under the whole equation, at the caret or the selection, so that the bar covers
        /// nothing of it; null while the bar is closed.
        /// </summary>
        public MathRect? QuickBarAnchor { get; private set; }

        /// <summary>The x of the origin of the layout in the control: inside the border and the padding.</summary>
        public double ContentX => Padding.Left + BorderThickness;

        /// <summary>The y of the origin of the layout in the control: inside the border and the padding, in the middle of the height.</summary>
        public double ContentY =>
            Padding.Top + BorderThickness + Math.Max(0, (Size.Height - Padding.Top - Padding.Bottom - BorderThickness * 2 - (Layout?.Height ?? 0)) / 2);

        /// <summary>
        /// Lays out the equation for the width the control can take (infinite for no limit), if it changed since, and
        /// gives the size the control wants: the layout, at least a little box for an empty equation, with the padding
        /// and the border.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Font"/> is not set.</exception>
        public MathSize Measure(double availableWidth)
        {
            var font = Font ?? throw new InvalidOperationException("The font of the view is not set.");
            var line = availableWidth - Padding.Left - Padding.Right - BorderThickness * 2;
            var lineWidth = double.IsInfinity(line) ? (double?)null : line;
            var key = (Editor.Root, font, FontSize, lineWidth);
            if (Layout == null || laidOut != key)
            {
                var options = new MathLayoutOptions { FontSize = FontSize, ShowPlaceholders = true, LineWidth = lineWidth };
                Layout = MathLayoutEngine.Layout(Editor.Root, font, options, layoutCache);
                laidOut = key;
            }
            var b = BorderThickness * 2;
            var width = Math.Max(Layout.Width, FontSize * 0.6);
            var height = Math.Max(Layout.Height, FontSize * 1.2);
            return new MathSize(width + Padding.Left + Padding.Right + b, height + Padding.Top + Padding.Bottom + b);
        }

        /// <summary>Takes the size the control was given.</summary>
        public void Arrange(double width, double height) => Size = new MathSize(width, height);

        /// <summary>Forgets the layout and all it keeps, when the control leaves the screen.</summary>
        public void Reset()
        {
            Layout = null;
            laidOut = null;
            drawnSelection = null;
            layoutCache.Clear();
        }

        /// <summary>Draws the selection and the equation (not the background, nor the border, nor the caret).</summary>
        public void Render(IMathCanvas canvas)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            drawnSelection = Editor.Selection;
            if (Layout is not { } layout)
                return;
            var x = ContentX;
            var y = ContentY;
            if (Editor.Selection is { } selection)
            {
                foreach (var rect in layout.GetSelectionRects(selection))
                    canvas.FillRectangle(new MathRect(rect.X + x, rect.Y + y, rect.Width, rect.Height), MathPaint.Selection, null);
            }
            layout.Draw(canvas, x, y);
        }

        /// <summary>
        /// Draws the caret, when the control has the focus, is not read only, and the caret is shown as it blinks: a layer
        /// of its own over the equation is best, so that the blinking draws nothing else again.
        /// </summary>
        public void RenderCaret(IMathCanvas canvas)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            if (IsFocused && IsCaretVisible && !IsReadOnly && Layout?.GetCaretRect(Editor.Caret) is { } caret)
                canvas.FillRectangle(new MathRect(caret.X + ContentX - 0.5, caret.Y + ContentY, 1.2, caret.Height), MathPaint.Caret, null);
        }

        /// <summary>Shows the caret if it was hidden, hides it if it was shown: each <see cref="CaretBlinkInterval"/>, while the control has the focus.</summary>
        public void BlinkCaret() => IsCaretVisible = !IsCaretVisible;

        /// <summary>The caret, in the coordinates of the control, one wide: for the input method and the context menu.</summary>
        public MathRect? GetCaretRect() =>
            Layout?.GetCaretRect(Editor.Caret) is { } caret ? new MathRect(caret.X + ContentX, caret.Y + ContentY, 1, caret.Height) : null;

        /// <summary>The position of the caret nearest a point of the control, or null before the layout.</summary>
        public MathPosition? HitTest(double x, double y) => Layout?.HitTest(x - ContentX, y - ContentY);

        /// <summary>Whether a point of the control is in the selection.</summary>
        public bool IsInSelection(double x, double y)
        {
            if (Editor.Selection is not { } selection || Layout is not { } layout)
                return false;
            var ox = ContentX;
            var oy = ContentY;
            foreach (var rect in layout.GetSelectionRects(selection))
            {
                if (x >= rect.X + ox && x <= rect.Right + ox && y >= rect.Y + oy && y <= rect.Bottom + oy)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The main button pressed at a point: the caret goes there (the selection extends to it, with
        /// <paramref name="extend"/>), and moving the pointer selects until it is released. The control takes the focus
        /// and captures the pointer.
        /// </summary>
        public void PointerPressed(double x, double y, bool extend)
        {
            if (HitTest(x, y) is { } position)
                Editor.SetCaret(position, extend);
            selecting = true;
        }

        /// <summary>The pointer moved to a point: while the button is held, the selection extends to it.</summary>
        public void PointerMoved(double x, double y)
        {
            if (selecting && HitTest(x, y) is { } position)
                Editor.SetCaret(position, true);
        }

        /// <summary>The button released; returns whether it was selecting, and the control releases the pointer.</summary>
        public bool PointerReleased()
        {
            var was = selecting;
            selecting = false;
            return was;
        }

        /// <summary>
        /// Acts on a key: first the floating bar, while it is open (<see cref="MathQuickBarState.ProcessKey"/>: the
        /// control then updates it), then the editor (<see cref="MathEditor.ProcessKey"/>). The control gives first the
        /// gestures of its platform (copy, paste, undo...) to its own commands.
        /// </summary>
        /// <returns>Whether the key was used.</returns>
        public bool ProcessKey(MathKey key, MathKeyModifiers modifiers) =>
            ProcessQuickBarKey(key, modifiers) || Editor.ProcessKey(key, modifiers, Layout, IsReadOnly);

        /// <summary>Acts on a key with the floating bar, while it is open: Up and Down choose a completion, Tab or Enter take it, Esc closes it.</summary>
        /// <returns>Whether the key was used: the control then updates the bar.</returns>
        public bool ProcessQuickBarKey(MathKey key, MathKeyModifiers modifiers) =>
            QuickBar.Mode != MathQuickBarMode.Hidden && QuickBar.ProcessKey(key, modifiers);

        /// <summary>
        /// Reads again what the floating bar shows, closed unless it is enabled, the control has the focus, is not read
        /// only and the equation is laid out, and where it goes (<see cref="QuickBarAnchor"/>).
        /// </summary>
        /// <returns>Whether what the bar shows changed (<see cref="MathQuickBarState.Update"/>).</returns>
        public bool UpdateQuickBar()
        {
            var changed = QuickBar.Update(IsQuickBarEnabled && IsFocused && !IsReadOnly && Layout != null);
            QuickBarAnchor = null;
            if (QuickBar.Mode == MathQuickBarMode.Hidden || Layout is not { } layout)
                return changed;
            var rects = Editor.Selection is { } selection ? layout.GetSelectionRects(selection) : Array.Empty<MathRect>();
            var anchor = rects.Count > 0 && rects[0].Width > 0 ? rects[0] : layout.GetCaretRect(Editor.Caret) ?? default;
            QuickBarAnchor = new MathRect(anchor.X + ContentX, ContentY, Math.Max(1, anchor.Width), Math.Max(layout.Height, anchor.Y + anchor.Height) + Padding.Bottom);
            return changed;
        }

        /// <summary>
        /// Before the context menu opens at a point of the control, or at the caret: a point outside the selection moves
        /// the caret there first, as text editors do.
        /// </summary>
        /// <returns>The rectangle the menu opens at, in the coordinates of the control: the point, or the caret; null if there is none.</returns>
        public MathRect? PrepareContextMenu(double? x, double? y)
        {
            if (x is { } px && y is { } py)
            {
                if (!IsInSelection(px, py) && HitTest(px, py) is { } position)
                    Editor.SetCaret(position);
                return new MathRect(px, py, 1, 1);
            }
            return GetCaretRect();
        }

        /// <summary>
        /// The context menu for the state of the editor. The command of each item: <c>undo</c>, <c>redo</c>,
        /// <c>cut</c>, <c>copy</c>, <c>paste</c>, <c>copy-as</c> (with <c>copy-as-mathml</c>, <c>copy-as-unicodemath</c>,
        /// <c>copy-as-latex</c>, <c>copy-as-omml</c>), <c>select-all</c>, <c>view</c> (linear or professional),
        /// <c>build-up</c>, <c>build-down</c>, <c>new-line</c>, <c>number</c>, <c>table</c> (with <c>row-above</c>,
        /// <c>row-below</c>, <c>column-left</c>, <c>column-right</c>, <c>delete-row</c>, <c>delete-column</c>). Read only,
        /// it has the copy commands and select all.
        /// </summary>
        public IReadOnlyList<MathMenuItem> GetContextMenu()
        {
            var e = Editor;
            var editable = !IsReadOnly;
            var hasSelection = e.Selection != null;
            var items = new List<MathMenuItem>();

            if (editable)
            {
                items.Add(MathMenuItem.Item("Undo", "undo", e.CanUndo));
                items.Add(MathMenuItem.Item("Redo", "redo", e.CanRedo));
                items.Add(MathMenuItem.Separator);
                items.Add(MathMenuItem.Item("Cut", "cut", hasSelection));
            }
            items.Add(MathMenuItem.Item("Copy", "copy", hasSelection));
            if (editable)
                items.Add(MathMenuItem.Item("Paste", "paste"));

            items.Add(MathMenuItem.Submenu(hasSelection ? "Copy selection as" : "Copy equation as", "copy-as", !e.IsEmpty,
                MathMenuItem.Item("MathML", "copy-as-mathml"),
                MathMenuItem.Item("UnicodeMath", "copy-as-unicodemath"),
                MathMenuItem.Item("LaTeX", "copy-as-latex"),
                MathMenuItem.Item("OMML (Word)", "copy-as-omml")));
            items.Add(MathMenuItem.Item("Select all", "select-all", !e.IsEmpty));

            if (editable)
            {
                items.Add(MathMenuItem.Separator);
                items.Add(MathMenuItem.Item(e.IsLinear ? "Professional" : "Linear", "view"));
                items.Add(MathMenuItem.Item("Build up", "build-up", !e.IsEmpty));
                items.Add(MathMenuItem.Item("Build down", "build-down", !e.IsEmpty && !e.IsLinear));
                if (!e.IsLinear)
                {
                    items.Add(MathMenuItem.Item("New line", "new-line"));
                    items.Add(e.EquationNumber == null
                        ? MathMenuItem.Item("Number the equation", "number", !e.IsEmpty)
                        : MathMenuItem.Item("Remove the number", "number"));
                }
                if (e.IsInTable)
                {
                    items.Add(MathMenuItem.Submenu("Table", "table", true,
                        MathMenuItem.Item("Insert row above", "row-above"),
                        MathMenuItem.Item("Insert row below", "row-below"),
                        MathMenuItem.Item("Insert column left", "column-left"),
                        MathMenuItem.Item("Insert column right", "column-right"),
                        MathMenuItem.Separator,
                        MathMenuItem.Item("Delete row", "delete-row"),
                        MathMenuItem.Item("Delete column", "delete-column")));
                }
            }
            return items;
        }

        /// <summary>
        /// Does a command of the context menu on the editor. The commands that need the clipboard (<c>cut</c>,
        /// <c>copy</c>, <c>paste</c>, <c>copy-as-...</c>) and <c>view</c>, which the control holds as a property of its
        /// own, are the control's: for these, and for an unknown command, it returns false.
        /// </summary>
        public bool ExecuteCommand(string command)
        {
            var e = Editor;
            switch (command)
            {
                case "undo":
                    e.Undo();
                    return true;
                case "redo":
                    e.Redo();
                    return true;
                case "select-all":
                    e.SelectAll();
                    return true;
                case "build-up":
                    e.BuildUpAll();
                    return true;
                case "build-down":
                    e.BuildDown();
                    return true;
                case "new-line":
                    e.InsertLineBreak();
                    return true;
                case "number":
                    e.SetEquationNumber(e.EquationNumber == null ? "(1)" : null);
                    return true;
                case "row-above":
                    e.InsertTableRow(above: true);
                    return true;
                case "row-below":
                    e.InsertTableRow();
                    return true;
                case "column-left":
                    e.InsertTableColumn(before: true);
                    return true;
                case "column-right":
                    e.InsertTableColumn();
                    return true;
                case "delete-row":
                    e.DeleteTableRow();
                    return true;
                case "delete-column":
                    e.DeleteTableColumn();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The format a <c>copy-as-...</c> command copies in, or null for another command.</summary>
        public static EquationFormat? CopyAsFormat(string command) => command switch
        {
            "copy-as-mathml" => EquationFormat.MathML,
            "copy-as-unicodemath" => EquationFormat.UnicodeMath,
            "copy-as-latex" => EquationFormat.LaTeX,
            "copy-as-omml" => EquationFormat.Omml,
            _ => null,
        };

        /// <summary>
        /// What copying the selection puts on the clipboard: the equation, the text in a format (UnicodeMath and LaTeX
        /// as they are typed, the others written from the equation; null for the input format), and its MathML.
        /// </summary>
        /// <returns>Whether something is selected.</returns>
        public bool TryGetCopy(EquationFormat? textFormat, out Equation equation, out string text, out string mathML)
        {
            if (Editor.GetSelectedEquation() is not { } selected)
            {
                equation = null!;
                text = mathML = string.Empty;
                return false;
            }
            var format = textFormat ?? Editor.InputFormat;
            equation = selected;
            text = format is EquationFormat.UnicodeMath or EquationFormat.LaTeX ? Editor.GetSelectedText(format)! : selected.ToString(format);
            mathML = selected.ToString(EquationFormat.MathML);
            return true;
        }

        /// <summary>The text that copying as a format puts on the clipboard: the selection, or the whole equation when nothing is selected; null when empty.</summary>
        public string? GetCopyAsText(EquationFormat format)
        {
            var text = Editor.GetSelectedText(format) ?? (Editor.IsEmpty ? null : Editor.GetText(format));
            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>Pastes the MathML of the clipboard; returns false when it does not read, or read only, and the control pastes its text instead.</summary>
        public bool PasteMathML(string mathML) => !IsReadOnly && !string.IsNullOrEmpty(mathML) && Editor.Paste(mathML, EquationFormat.MathML);

        /// <summary>Pastes the text of the clipboard: as MathML when it is MathML, otherwise read in the input format, or as plain text.</summary>
        public void PasteText(string text)
        {
            if (IsReadOnly || string.IsNullOrEmpty(text))
                return;
            if (text.TrimStart() is { Length: > 0 } trimmed && trimmed[0] == '<' && Equation.TryParse(text, EquationFormat.MathML, out _))
                Editor.Paste(text, EquationFormat.MathML);
            else
                Editor.Paste(text, Editor.InputFormat);
        }

        private void OnEditorChanged(object? sender, EventArgs e)
        {
            IsCaretVisible = true;
            var changes = MathEditorViewChanges.Caret | MathEditorViewChanges.QuickBar;
            if (laidOut is not { } done || !ReferenceEquals(done.Root, Editor.Root))
                changes |= MathEditorViewChanges.Measure;
            else if (!SameSelection(drawnSelection, Editor.Selection))
                changes |= MathEditorViewChanges.Render;
            Changed?.Invoke(this, new MathEditorViewChangedEventArgs(changes));
        }

        private static bool SameSelection(MathSelection? a, MathSelection? b) =>
            a == null ? b == null : b != null && a.StartPosition == b.StartPosition && a.EndPosition == b.EndPosition;
    }
}
