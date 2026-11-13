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
    using SharpEquations.Rendering;

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
            var at = View.PrepareContextMenu(point?.X, point?.Y);

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
            if (at is { } rect)
                menu.PlacementRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
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
            if (View.GetCopyAsText(format) is not { } text)
                return;
            var item = new DataTransferItem();
            item.SetText(text);
            if (format == EquationFormat.MathML)
                item.Set(MathMLClipboardFormat, MathClipboard.Encode(text));
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
            var menu = new ContextMenu();
            foreach (var item in View.GetContextMenu())
                menu.Items.Add(Item(item));
            return menu;
        }

        private Control Item(MathMenuItem item)
        {
            if (item.IsSeparator)
                return new Separator();
            var command = item.Command!;
            var menuItem = new MenuItem { Header = item.Header, Tag = command, IsEnabled = item.IsEnabled, InputGesture = Gesture(command) };
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

        private KeyGesture? Gesture(string command) => command switch
        {
            "undo" => Gesture(h => h.Undo, Key.Z),
            "redo" => Gesture(h => h.Redo, Key.Y),
            "cut" => Gesture(h => h.Cut, Key.X),
            "copy" => Gesture(h => h.Copy, Key.C),
            "paste" => Gesture(h => h.Paste, Key.V),
            "select-all" => Gesture(h => h.SelectAll, Key.A),
            "view" => ToggleViewModeGesture,
            "new-line" => new KeyGesture(Key.Enter, KeyModifiers.Shift),
            _ => null,
        };

        private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (e.Handled || !IsContextMenuEnabled)
                return;
            e.Handled = ShowContextMenu(e.TryGetPosition(this, out var point) ? point : null);
        }

        private static KeyGesture Gesture(Func<PlatformHotkeyConfiguration, List<KeyGesture>> gestures, Key key) =>
            Application.Current?.PlatformSettings?.HotkeyConfiguration is { } hotkeys && gestures(hotkeys) is { Count: > 0 } list
                ? list[0]
                : new KeyGesture(key, KeyModifiers.Control);
    }
}
