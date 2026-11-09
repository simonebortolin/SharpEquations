namespace SharpEquations.Rendering.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal enum OperatorForm
    {
        Prefix,
        Infix,
        Postfix,
    }

    [Flags]
    internal enum OperatorFlags
    {
        None = 0,
        Stretchy = 1,
        Symmetric = 2,
        LargeOp = 4,
        MovableLimits = 8,
        Fence = 16,
        Accent = 32,
        Horizontal = 64,
    }

    /// <summary>The properties of an operator: its spaces, in eighteenths of an em, and its flags.</summary>
    internal readonly struct OperatorEntry
    {
        public OperatorEntry(int lspace, int rspace, OperatorFlags flags)
        {
            LSpace = lspace;
            RSpace = rspace;
            Flags = flags;
        }

        public int LSpace { get; }

        public int RSpace { get; }

        public OperatorFlags Flags { get; }

        public bool Has(OperatorFlags flag) => (Flags & flag) == flag;
    }

    /// <summary>
    /// The operator dictionary of MathML Core, for the operators equations use: the spaces around each form of an
    /// operator, and whether it stretches, is a large operator, takes limits that move in inline style, is an accent.
    /// </summary>
    internal static class OperatorDictionary
    {
        private const OperatorFlags FenceV = OperatorFlags.Stretchy | OperatorFlags.Symmetric | OperatorFlags.Fence;
        private const OperatorFlags ArrowH = OperatorFlags.Stretchy | OperatorFlags.Horizontal;
        private const OperatorFlags AccentH = OperatorFlags.Accent | OperatorFlags.Stretchy | OperatorFlags.Horizontal;

        private static readonly Dictionary<(string, OperatorForm), OperatorEntry> Entries = Build();

        public static OperatorEntry Lookup(string text, OperatorForm form)
        {
            if (Entries.TryGetValue((text, form), out var entry))
                return entry;
            foreach (var other in new[] { OperatorForm.Infix, OperatorForm.Postfix, OperatorForm.Prefix })
            {
                if (Entries.TryGetValue((text, other), out entry))
                    return entry;
            }
            return form == OperatorForm.Infix ? new OperatorEntry(5, 5, OperatorFlags.None) : new OperatorEntry(0, 0, OperatorFlags.None);
        }

        public static bool TryLookup(string text, out OperatorEntry entry)
        {
            foreach (var form in new[] { OperatorForm.Infix, OperatorForm.Postfix, OperatorForm.Prefix })
            {
                if (Entries.TryGetValue((text, form), out entry))
                    return true;
            }
            entry = default;
            return false;
        }

        private static Dictionary<(string, OperatorForm), OperatorEntry> Build()
        {
            var d = new Dictionary<(string, OperatorForm), OperatorEntry>();

            void Add(string chars, OperatorForm form, int l, int r, OperatorFlags flags = OperatorFlags.None)
            {
                foreach (var rune in chars.EnumerateRunes())
                    d[(rune.ToString(), form)] = new OperatorEntry(l, r, flags);
            }

            Add("([{|‖⌈⌊⟨⟦⟪〈⟮⦃⦅⦇⦉❲〖", OperatorForm.Prefix, 0, 0, FenceV);
            Add(")]}|‖⌉⌋⟩⟧⟫〉⟯⦄⦆⦈⦊❳〗", OperatorForm.Postfix, 0, 0, FenceV);
            Add("|‖∣∥│", OperatorForm.Infix, 5, 5, OperatorFlags.Stretchy | OperatorFlags.Symmetric);
            Add("/∕⁄", OperatorForm.Infix, 1, 1, OperatorFlags.Stretchy | OperatorFlags.Symmetric);
            Add("\\∖", OperatorForm.Infix, 4, 4);

            Add("=≠<>≤≥≦≧≪≫≈≉≡≢≅≃≄∼≍∝≺≻⪯⪰∈∉∋∌⊂⊃⊆⊇⊊⊋⊄⊅⊏⊐⊑⊒⊢⊣⊤⊨⊩⊥∥∦≔≕≝≜≐≑≗≙≟≲≳⋘⋙⩽⩾∷∶:", OperatorForm.Infix, 5, 5);
            Add("→←↔⇒⇐⇔↦⟶⟵⟷⟹⟸⟺⟼↪↩⇀↼⇁↽⇌⇋↠↞⇄⇆⤳↣↢⇉⇇⇶", OperatorForm.Infix, 5, 5, ArrowH);
            Add("↑↓↕⇑⇓⇕↗↘↙↖", OperatorForm.Infix, 5, 5, OperatorFlags.Stretchy);

            Add("+-−±∓∪∩⊎∨∧⊕⊖⊔⊓∔⊻⊼⊽⋎⋏", OperatorForm.Infix, 4, 4);
            Add("+-−±∓¬", OperatorForm.Prefix, 0, 0);
            Add("×·⋅∗∘∙⊗⊙⊘÷⋆⨯⋄⋇⊛⊚⊡⊠⊟⊞⋉⋊⋋⋌≀", OperatorForm.Infix, 4, 4);

            Add(",;", OperatorForm.Infix, 0, 3);
            Add("…⋯⋮⋰⋱", OperatorForm.Infix, 0, 0);
            Add("!′″‴⁗°%", OperatorForm.Postfix, 0, 0);
            Add("∂∇∀∃∄∠∡∢√∛∜", OperatorForm.Prefix, 0, 0);

            Add("∑∏∐⋀⋁⋂⋃⨀⨁⨂⨄⨆⨉", OperatorForm.Prefix, 1, 2, OperatorFlags.LargeOp | OperatorFlags.MovableLimits | OperatorFlags.Symmetric);
            Add("∫∬∭⨌∮∯∰∱∲∳⨍⨎⨏⨐⨑⨒⨓⨔⨕⨖⨗⨘⨙⨚⨛⨜", OperatorForm.Prefix, 1, 2, OperatorFlags.LargeOp | OperatorFlags.Symmetric);

            Add("⁡⁢⁣⁤", OperatorForm.Infix, 0, 0);

            Add("‾¯_^ˆˇ˜~̲̂̃̌̄̅⏞⏟⏜⏝⎴⎵⏠⏡⃗⃖⃡⃐⃑=", OperatorForm.Postfix, 0, 0, AccentH);
            Add("˙¨´`˘˚̇̈́̀̆̊⃛⃜", OperatorForm.Postfix, 0, 0, OperatorFlags.Accent);
            return d;
        }
    }
}
