namespace SharpEquations.Wpf
{
    using System.Windows.Automation.Peers;
    using System.Windows.Automation.Provider;

    /// <summary>
    /// The automation peer of <see cref="MathEditBox"/>: an edit control whose value is the equation in UnicodeMath, as
    /// screen readers read the equations of Word; setting the value reads it in the input format.
    /// </summary>
    public class MathEditBoxAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
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
        public void SetValue(string value)
        {
            if (IsReadOnly)
                return;
            Owner.Editor.SelectAll();
            if (string.IsNullOrEmpty(value))
                Owner.Editor.DeleteSelection();
            else
                Owner.Editor.Paste(value, Owner.InputFormat);
        }

        /// <inheritdoc/>
        public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

        /// <inheritdoc/>
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

        /// <inheritdoc/>
        protected override string GetClassNameCore() => nameof(MathEditBox);

        /// <inheritdoc/>
        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : "Equation";
    }
}
