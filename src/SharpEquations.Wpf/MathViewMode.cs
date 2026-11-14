namespace SharpEquations.Wpf
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>Whether an equation is shown built up or as linear text, as the math zones of Word.</summary>
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

    /// <summary>The arguments of <see cref="MathEditBox.Copying"/>.</summary>
    public sealed class MathCopyingEventArgs : EventArgs
    {
        /// <summary>Arguments with the equation copied and the data of the clipboard.</summary>
        public MathCopyingEventArgs(Equation equation, DataObject data)
        {
            Equation = equation;
            Data = data;
        }

        /// <summary>The selection copied.</summary>
        public Equation Equation { get; }

        /// <summary>The data that goes to the clipboard, with the text and the MathML; a handler can add formats.</summary>
        public DataObject Data { get; }

        /// <summary>Set to true to copy nothing.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>The arguments of <see cref="MathEditBox.MathContextMenuOpening"/>.</summary>
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
}
