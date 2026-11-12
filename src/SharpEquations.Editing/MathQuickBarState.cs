namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// What the floating bar at the caret of an editor shows, and the completion chosen in it, with no user interface:
    /// a control draws the bar after <see cref="Mode"/> and <see cref="Completions"/>, and gives it the keys first
    /// (<see cref="ProcessKey"/>: Up and Down choose a completion, Tab or Enter take it, Esc closes the completions).
    /// </summary>
    public sealed class MathQuickBarState
    {
        private int selectedIndex;

        /// <summary>The state of the bar of an editor; closed until <see cref="Update"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="editor"/> is null.</exception>
        public MathQuickBarState(MathEditor editor)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        /// <summary>The editor.</summary>
        public MathEditor Editor { get; }

        /// <summary>The most completions the bar shows.</summary>
        public int MaxCompletions { get; set; } = 8;

        /// <summary>What the bar shows.</summary>
        public MathQuickBarMode Mode { get; private set; }

        /// <summary>The completions shown, in <see cref="MathQuickBarMode.Completions"/>; empty otherwise.</summary>
        public IReadOnlyList<EquationCommand> Completions { get; private set; } = Array.Empty<EquationCommand>();

        /// <summary>The completion that Tab or Enter takes; it wraps around the completions.</summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (Completions.Count == 0)
                    return;
                selectedIndex = (value % Completions.Count + Completions.Count) % Completions.Count;
            }
        }

        /// <summary>
        /// Reads again from the editor what the bar shows (<see cref="MathEditor.GetQuickBarMode"/>), or closes it when
        /// it is not <paramref name="enabled"/> (the control has no focus, is read only...). The chosen completion is
        /// kept while the completions are the same.
        /// </summary>
        /// <returns>Whether <see cref="Mode"/> or <see cref="Completions"/> changed.</returns>
        public bool Update(bool enabled)
        {
            var mode = MathQuickBarMode.Hidden;
            IReadOnlyList<EquationCommand> completions = Array.Empty<EquationCommand>();
            if (enabled)
                mode = Editor.GetQuickBarMode(MaxCompletions, out completions);
            var same = mode == Mode && Completions.Select(c => c.Name).SequenceEqual(completions.Select(c => c.Name));
            Mode = mode;
            Completions = completions;
            if (!same)
                selectedIndex = 0;
            return !same;
        }

        /// <summary>Takes the selected completion; returns false when there is none.</summary>
        public bool AcceptCompletion()
        {
            if (Mode != MathQuickBarMode.Completions || Completions.Count == 0)
                return false;
            Editor.Complete(Completions[selectedIndex]);
            return true;
        }

        /// <summary>
        /// Acts on a key while the completions are shown, before the editor does (<see cref="MathEditor.ProcessKey"/>):
        /// Up and Down choose a completion, Tab or Enter take it, Esc closes the completions.
        /// </summary>
        /// <returns>Whether the key was used: false when the completions are not shown, or with a modifier.</returns>
        public bool ProcessKey(MathKey key, MathKeyModifiers modifiers)
        {
            if (Mode != MathQuickBarMode.Completions || modifiers != MathKeyModifiers.None)
                return false;
            switch (key)
            {
                case MathKey.Down:
                    SelectedIndex++;
                    return true;
                case MathKey.Up:
                    SelectedIndex--;
                    return true;
                case MathKey.Tab or MathKey.Enter:
                    return AcceptCompletion();
                case MathKey.Escape:
                    Editor.DismissCompletions();
                    Update(true);
                    return true;
                default:
                    return false;
            }
        }
    }
}
