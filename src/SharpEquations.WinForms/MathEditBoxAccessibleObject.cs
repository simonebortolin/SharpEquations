namespace SharpEquations.WinForms
{
    using System.Windows.Forms;

    /// <summary>
    /// The accessible object of <see cref="MathEditBox"/>: an editable text whose value is the equation in UnicodeMath,
    /// as screen readers read the equations of Word; setting the value reads it in the input format.
    /// </summary>
    public class MathEditBoxAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly MathEditBox editor;

        /// <summary>The accessible object of an editor.</summary>
        public MathEditBoxAccessibleObject(MathEditBox owner)
            : base(owner) => editor = owner;

        /// <inheritdoc/>
        public override AccessibleRole Role => editor.AccessibleRole != AccessibleRole.Default ? editor.AccessibleRole : AccessibleRole.Text;

        /// <inheritdoc/>
        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name : "Equation";
            set => base.Name = value;
        }

        /// <inheritdoc/>
        public override AccessibleStates State => base.State | (editor.IsReadOnly ? AccessibleStates.ReadOnly : AccessibleStates.None);

        /// <inheritdoc/>
#if NET
        [System.Diagnostics.CodeAnalysis.AllowNull]
#endif
        public override string Value
        {
            get => editor.GetText(EquationFormat.UnicodeMath);
            set
            {
                if (editor.IsReadOnly)
                    return;
                editor.Editor.SelectAll();
                if (string.IsNullOrEmpty(value))
                    editor.Editor.DeleteSelection();
                else
                    editor.Editor.Paste(value, editor.InputFormat);
            }
        }
    }
}
