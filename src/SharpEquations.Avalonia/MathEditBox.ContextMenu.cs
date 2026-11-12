namespace SharpEquations.Avalonia
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives.PopupPositioning;
    using global::Avalonia.Input;
    using global::Avalonia.Input.Platform;

    /// <summary>The arguments of <see cref="MathEditBox.ContextMenuOpening"/>.</summary>
    public sealed class MathContextMenuEventArgs : EventArgs
    {
        /// <summary>Arguments with the menu about to open.</summary>
        public MathContextMenuEventArgs(ContextMenu menu) => Menu = menu;

        /// <summary>
        /// The menu, which a handler can change: its items have a tag that names their command (<c>cut</c>, <c>copy</c>,
        /// <c>paste</c>, <c>copy-as</c>...; see <see cref="MathEditBox.CreateContextMenu"/>).
        /// </summary>
        public ContextMenu Menu { get; }

        /// <summary>Set to true to open no menu.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>The context menu of the editor: right click, long press on touch, the Menu key or Shift+F10.</summary>
    public partial class MathEditBox
    {
        /// <summary>Whether the editor opens its context menu.</summary>
        public static readonly StyledProperty<bool> IsContextMenuEnabledProperty =
            AvaloniaProperty.Register<MathEditBox, bool>(nameof(IsContextMenuEnabled), true);

        private ContextMenu? openMenu;

        /// <summary>
        /// Raised before the context menu opens, with the menu of <see cref="CreateContextMenu"/>: a handler can add,
        /// remove or change items, or cancel it.
        /// </summary>
        public event EventHandler<MathContextMenuEventArgs>? ContextMenuOpening;

        /// <summary>Whether the editor opens its context menu.</summary>
        public bool IsContextMenuEnabled
        {
            get => GetValue(IsContextMenuEnabledProperty);
            set => SetValue(IsContextMenuEnabledProperty, value);
        }

        /// <summary>The context menu while it is open, or null.</summary>
        public ContextMenu? OpenContextMenu => openMenu is { IsOpen: true } ? openMenu : null;

        /// <summary>
        /// Opens the context menu at a point of the editor, or at the caret: a point outside the selection moves the caret
        /// there first, as text editors do. Returns whether a menu opened.
        /// </summary>
        public bool ShowContextMenu(Point? point = null)
        {
            if (!IsContextMenuEnabled)
                return false;
            Focus();
            if (point is { } p && !IsInSelection(p) && HitTest(p) is { } position)
                Editor.SetCaret(position);

            openMenu?.Close();
            var menu = CreateContextMenu();
            var args = new MathContextMenuEventArgs(menu);
            ContextMenuOpening?.Invoke(this, args);
            if (args.Cancel || menu.Items.Count == 0)
                return false;

            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.AnchorAndGravity;
            menu.PlacementAnchor = PopupAnchor.BottomLeft;
            menu.PlacementGravity = PopupGravity.BottomRight;
            if (point is { } at)
            {
                menu.PlacementRect = new Rect(at, new Size(1, 1));
            }
            else if (Layout?.GetCaretRect(Editor.Caret) is { } caret)
            {
                var origin = ContentOrigin();
                menu.PlacementRect = new Rect(caret.X + origin.X, caret.Y + origin.Y, 1, caret.Height);
            }
            openMenu = menu;
            menu.Open(this);
            return true;
        }

        /// <summary>
        /// Copies the selection, or the whole equation when nothing is selected, to the clipboard as text in a format
        /// (LaTeX as Word writes it); MathML goes also as the MathML of the clipboard (<see cref="MathMLClipboardFormat"/>).
        /// </summary>
        public async Task CopyAsAsync(EquationFormat format)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            var text = Editor.GetSelectedText(format) ?? (Editor.IsEmpty ? null : Editor.GetText(format));
            if (string.IsNullOrEmpty(text))
                return;
            var item = new DataTransferItem();
            item.SetText(text!);
            if (format == EquationFormat.MathML)
                item.Set(MathMLClipboardFormat, MathClipboard.Encode(text!));
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data).ConfigureAwait(true);
        }

        /// <summary>
        /// The context menu of the editor, built anew each time it opens, for the state of the editor. The tag of each
        /// item names its command: <c>undo</c>, <c>redo</c>, <c>cut</c>, <c>copy</c>, <c>paste</c>, <c>copy-as</c> (with
        /// <c>copy-as-mathml</c>, <c>copy-as-unicodemath</c>, <c>copy-as-latex</c>, <c>copy-as-omml</c>),
        /// <c>select-all</c>, <c>view</c> (linear or professional), <c>build-up</c>, <c>build-down</c>, <c>new-line</c>,
        /// <c>number</c>, <c>table</c> (with <c>row-above</c>, <c>row-below</c>, <c>column-left</c>,
        /// <c>column-right</c>, <c>delete-row</c>, <c>delete-column</c>). A read-only editor has the copy commands and
        /// select all.
        /// </summary>
        protected virtual ContextMenu CreateContextMenu()
        {
            var e = Editor;
            var editable = !IsReadOnly;
            var hasSelection = e.Selection != null;
            var items = new List<Control>();

            if (editable)
            {
                items.Add(Item("Undo", "undo", () => e.Undo(), e.CanUndo, Gesture(h => h.Undo, Key.Z)));
                items.Add(Item("Redo", "redo", () => e.Redo(), e.CanRedo, Gesture(h => h.Redo, Key.Y)));
                items.Add(new Separator());
                items.Add(Item("Cut", "cut", () => _ = CutAsync(), hasSelection, Gesture(h => h.Cut, Key.X)));
            }
            items.Add(Item("Copy", "copy", () => _ = CopyAsync(), hasSelection, Gesture(h => h.Copy, Key.C)));
            if (editable)
                items.Add(Item("Paste", "paste", () => _ = PasteAsync(), true, Gesture(h => h.Paste, Key.V)));

            var copyAs = new MenuItem { Header = hasSelection ? "Copy selection as" : "Copy equation as", Tag = "copy-as", IsEnabled = !e.IsEmpty };
            copyAs.Items.Add(Item("MathML", "copy-as-mathml", () => _ = CopyAsAsync(EquationFormat.MathML)));
            copyAs.Items.Add(Item("UnicodeMath", "copy-as-unicodemath", () => _ = CopyAsAsync(EquationFormat.UnicodeMath)));
            copyAs.Items.Add(Item("LaTeX", "copy-as-latex", () => _ = CopyAsAsync(EquationFormat.LaTeX)));
            copyAs.Items.Add(Item("OMML (Word)", "copy-as-omml", () => _ = CopyAsAsync(EquationFormat.Omml)));
            items.Add(copyAs);
            items.Add(Item("Select all", "select-all", () => e.SelectAll(), !e.IsEmpty, Gesture(h => h.SelectAll, Key.A)));

            if (editable)
            {
                items.Add(new Separator());
                items.Add(Item(ViewMode == MathViewMode.Linear ? "Professional" : "Linear", "view",
                    () => ViewMode = ViewMode == MathViewMode.Linear ? MathViewMode.Professional : MathViewMode.Linear, true, ToggleViewModeGesture));
                items.Add(Item("Build up", "build-up", () => e.BuildUpAll(), !e.IsEmpty));
                items.Add(Item("Build down", "build-down", () => e.BuildDown(), !e.IsEmpty && !e.IsLinear));
                if (!e.IsLinear)
                {
                    items.Add(Item("New line", "new-line", () => e.InsertLineBreak(), true, new KeyGesture(Key.Enter, KeyModifiers.Shift)));
                    items.Add(e.EquationNumber == null
                        ? Item("Number the equation", "number", () => e.SetEquationNumber("(1)"), !e.IsEmpty)
                        : Item("Remove the number", "number", () => e.SetEquationNumber(null)));
                }
                if (e.IsInTable)
                {
                    var table = new MenuItem { Header = "Table", Tag = "table" };
                    table.Items.Add(Item("Insert row above", "row-above", () => e.InsertTableRow(above: true)));
                    table.Items.Add(Item("Insert row below", "row-below", () => e.InsertTableRow()));
                    table.Items.Add(Item("Insert column left", "column-left", () => e.InsertTableColumn(before: true)));
                    table.Items.Add(Item("Insert column right", "column-right", () => e.InsertTableColumn()));
                    table.Items.Add(new Separator());
                    table.Items.Add(Item("Delete row", "delete-row", () => e.DeleteTableRow()));
                    table.Items.Add(Item("Delete column", "delete-column", () => e.DeleteTableColumn()));
                    items.Add(table);
                }
            }

            var menu = new ContextMenu();
            foreach (var item in items)
                menu.Items.Add(item);
            return menu;
        }

        private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (e.Handled || !IsContextMenuEnabled)
                return;
            e.Handled = ShowContextMenu(e.TryGetPosition(this, out var point) ? point : null);
        }

        private bool IsInSelection(Point point)
        {
            if (Editor.Selection is not { } selection || Layout is not { } layout)
                return false;
            var origin = ContentOrigin();
            foreach (var rect in layout.GetSelectionRects(selection))
            {
                if (point.X >= rect.X + origin.X && point.X <= rect.Right + origin.X && point.Y >= rect.Y + origin.Y && point.Y <= rect.Bottom + origin.Y)
                    return true;
            }
            return false;
        }

        private static KeyGesture Gesture(Func<PlatformHotkeyConfiguration, List<KeyGesture>> gestures, Key key) =>
            Application.Current?.PlatformSettings?.HotkeyConfiguration is { } hotkeys && gestures(hotkeys) is { Count: > 0 } list
                ? list[0]
                : new KeyGesture(key, KeyModifiers.Control);

        private MenuItem Item(string header, string tag, Action action, bool enabled = true, KeyGesture? gesture = null)
        {
            var item = new MenuItem { Header = header, Tag = tag, IsEnabled = enabled, InputGesture = gesture };
            item.Click += (_, args) =>
            {
                args.Handled = true;
                action();
                Focus();
            };
            return item;
        }
    }
}
