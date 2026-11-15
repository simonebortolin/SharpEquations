namespace SharpEquations.Editing
{
    using System;

    /// <summary>
    /// The keys the editor acts on (<see cref="MathEditor.ProcessKey"/>, <see cref="MathQuickBarState.ProcessKey"/>),
    /// whatever toolkit they come from: a control maps the keys of its toolkit to these.
    /// </summary>
    public enum MathKey
    {
        /// <summary>The left arrow.</summary>
        Left,

        /// <summary>The right arrow.</summary>
        Right,

        /// <summary>The up arrow.</summary>
        Up,

        /// <summary>The down arrow.</summary>
        Down,

        /// <summary>Home.</summary>
        Home,

        /// <summary>End.</summary>
        End,

        /// <summary>Backspace.</summary>
        Backspace,

        /// <summary>Delete.</summary>
        Delete,

        /// <summary>Tab.</summary>
        Tab,

        /// <summary>Enter.</summary>
        Enter,

        /// <summary>Esc.</summary>
        Escape,
    }

    /// <summary>The modifier keys held with a <see cref="MathKey"/>.</summary>
    [Flags]
    public enum MathKeyModifiers
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>Shift.</summary>
        Shift = 1,

        /// <summary>Control.</summary>
        Control = 2,

        /// <summary>Alt (Option on macOS).</summary>
        Alt = 4,

        /// <summary>The Windows key, Command on macOS.</summary>
        Meta = 8,
    }
}
