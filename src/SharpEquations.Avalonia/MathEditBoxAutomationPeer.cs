namespace SharpEquations.Avalonia
{
    using global::Avalonia.Automation.Peers;
    using global::Avalonia.Automation.Provider;

    /// <summary>
    /// The automation peer of <see cref="MathEditBox"/>: an edit control whose value is the equation in UnicodeMath, as
    /// screen readers read the equations of Word; setting the value reads it in the input format.
    /// </summary>
    public class MathEditBoxAutomationPeer : ControlAutomationPeer, IValueProvider
    {
        /// <summary>The peer of an editor.</summary>
        public MathEditBoxAutomationPeer(MathEditBox owner)
            : base(owner)
        {
        }

        /// <summary>The editor.</summary>
        public new MathEditBox Owner => (MathEditBox)base.Owner;

        /// <inheritdoc/>
        public bool IsReadOnly => Owner.IsReadOnly;

        /// <inheritdoc/>
        public string Value => Owner.GetText(EquationFormat.UnicodeMath);

        /// <inheritdoc/>
        public void SetValue(string? value)
        {
            if (IsReadOnly)
                return;
            Owner.Editor.SelectAll();
            if (string.IsNullOrEmpty(value))
                Owner.Editor.DeleteSelection();
            else
                Owner.Editor.Paste(value!, Owner.InputFormat);
        }

        /// <inheritdoc/>
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

        /// <inheritdoc/>
        protected override string GetClassNameCore() => nameof(MathEditBox);

        /// <inheritdoc/>
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : "Equation";
    }
}
