namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using SharpEquations.Editing.Text;

    /// <summary>
    /// An equation being edited, as in the math zones of Word and the math mode of the RichEditBox of WinUI: the user
    /// types UnicodeMath or LaTeX at the caret, and the text builds up into structures when a character that ends an
    /// argument is typed (a space, an operator).
    /// </summary>
    /// <remarks>
    /// The equation is a tree of <see cref="MathNode"/>s whose only text not built up is in runs. The editor has no
    /// user interface: a control gives it the keys and the text typed, and draws <see cref="Root"/>, the
    /// <see cref="Caret"/> and the <see cref="Selection"/>.
    /// </remarks>
    public sealed class MathEditor
    {
        private readonly Stack<State> undo = new Stack<State>();
        private readonly Stack<State> redo = new Stack<State>();
        private EquationFormat inputFormat = EquationFormat.UnicodeMath;
        private MathPosition? typingAt;

        /// <summary>An editor of the equation, or of an empty equation.</summary>
        public MathEditor(Equation? equation = null)
        {
            Root = Empty(false);
            Caret = Anchor = new MathPosition(MathTree.RootRow, 0);
            Load(equation);
            undo.Clear();
        }

        /// <summary>Raised when the equation, the caret or the selection change.</summary>
        public event EventHandler? Changed;

        /// <summary>Raised when the equation changes.</summary>
        public event EventHandler? ContentChanged;

        /// <summary>The <c>math</c> node of the equation; its only child is the row of the equation.</summary>
        public MathNode Root { get; private set; }

        /// <summary>The position of the caret.</summary>
        public MathPosition Caret { get; private set; }

        /// <summary>The other end of the selection; the caret when nothing is selected.</summary>
        public MathPosition Anchor { get; private set; }

        /// <summary>The format of what is typed: <see cref="EquationFormat.UnicodeMath"/> or <see cref="EquationFormat.LaTeX"/>.</summary>
        /// <exception cref="ArgumentException">Another format.</exception>
        public EquationFormat InputFormat
        {
            get => inputFormat;
            set
            {
                if (value is not (EquationFormat.UnicodeMath or EquationFormat.LaTeX))
                    throw new ArgumentException("The input format is UnicodeMath or LaTeX.", nameof(value));
                inputFormat = value;
            }
        }

        /// <summary>The equation is shown as linear text (one run) rather than built up.</summary>
        public bool IsLinear { get; private set; }

        /// <summary>The equation is a display equation rather than inline.</summary>
        public bool IsDisplay
        {
            get => Root.GetAttribute("display") == "block";
            set
            {
                if (value != IsDisplay)
                {
                    Root = Root.WithAttribute("display", value ? "block" : null);
                    OnChanged(true);
                }
            }
        }

        /// <summary>The selection, or null when nothing is selected.</summary>
        public MathSelection? Selection => ComputeSelection(Anchor, Caret);

        /// <summary>Whether <see cref="Undo"/> has something to undo.</summary>
        public bool CanUndo => undo.Count > 0;

        /// <summary>Whether <see cref="Redo"/> has something to redo.</summary>
        public bool CanRedo => redo.Count > 0;

        /// <summary>The equation is empty.</summary>
        public bool IsEmpty => RootRow.Children.Count == 0;

        private MathNode RootRow => Root.Children[0];

        /// <summary>Replaces the equation, with the caret at its end.</summary>
        public void Load(Equation? equation)
        {
            PushUndo();
            Root = equation == null ? Empty(IsDisplay) : FromEquation(equation);
            if (IsLinear)
                Root = Linearize(Root);
            SetCaretAtEnd();
            OnChanged(true);
        }

        /// <summary>
        /// The equation, with the runs built up where they parse and written as <c>merror</c> where they do not; null if
        /// it is empty.
        /// </summary>
        public Equation? ToEquation() => IsEmpty ? null : BuildUp.ToEquation(Built().Children[0].Children, Root.Attributes);

        /// <summary>The MathML of the equation, as <see cref="ToEquation"/>; an empty <c>math</c> element if it is empty.</summary>
        public XElement ToMathML() => ToEquation()?.ToMathML() ?? new XElement(MathMLNamespaces.MathML + "math");

        /// <summary>The equation in a format; for LaTeX, the LaTeX of Word.</summary>
        public string GetText(EquationFormat format)
        {
            if (IsLinear && format == InputFormat)
                return RootRow.Children.Count == 1 && RootRow.Children[0].IsRun ? RootRow.Children[0].Text : string.Empty;
            var equation = ToEquation();
            if (equation == null)
                return string.Empty;
            return format is EquationFormat.UnicodeMath or EquationFormat.LaTeX ? BuildUp.ToLinear(equation, format) : equation.ToString(format);
        }

        /// <summary>Shows the equation as linear text, or builds it up again.</summary>
        public void SetLinear(bool linear)
        {
            if (linear == IsLinear)
                return;
            PushUndo();
            Root = linear ? Linearize(Root) : Built();
            IsLinear = linear;
            SetCaretAtEnd();
            OnChanged(true);
        }

        /// <summary>Builds up every run of the equation that parses.</summary>
        public void BuildUpAll()
        {
            if (IsLinear)
            {
                SetLinear(false);
                return;
            }
            var built = Built();
            if (ReferenceEquals(built, Root))
                return;
            PushUndo();
            Root = built;
            SetCaretAtEnd();
            OnChanged(true);
        }

        /// <summary>Builds down the selection, or the whole equation, into a run of linear text.</summary>
        public void BuildDown()
        {
            var selection = Selection;
            if (selection == null || selection.IsInRun)
            {
                SetLinear(true);
                return;
            }

            var row = MathTree.Get(Root, selection.Path);
            var nodes = row.Children.Skip(selection.Start).Take(selection.End - selection.Start).ToList();
            var equation = BuildUp.ToEquation(nodes.Select(n => BuildUp.BuildAll(n, InputFormat, IsDisplay)), IsDisplay);
            if (equation == null)
                return;
            PushUndo();
            var children = row.Children.ToList();
            children.RemoveRange(selection.Start, selection.End - selection.Start);
            var run = MathNode.Run(BuildUp.ToLinear(equation, InputFormat));
            children.Insert(selection.Start, run);
            ReplaceChildren(selection.Path.ToArray(), children, selection.Start, CodePointText.Length(run.Text));
            OnChanged(true);
        }

        /// <summary>Types text at the caret, replacing the selection, building up what the text ends.</summary>
        public void Insert(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var started = false;
            var built = false;
            foreach (var rune in text.EnumerateRunes())
            {
                if (rune.Value is '\r' or '\n' or '\t')
                {
                    if (!IsLinear)
                        BuildUpAll();
                    continue;
                }
                if (!started)
                {
                    if (typingAt == null || typingAt != Caret || Selection != null)
                        PushUndo();
                    started = true;
                }
                if (Selection != null)
                    RemoveSelection();
                InsertRune(rune);
                if (!IsLinear && BuildUp.IsTrigger(rune, InputFormat))
                    built |= TryBuildUpAtCaret(rune.Value == ' ');
            }
            typingAt = built ? null : Caret;
            OnChanged(true);
        }

        /// <summary>Inserts nodes at the caret, replacing the selection.</summary>
        public void InsertNodes(IEnumerable<MathNode> nodes)
        {
            var list = nodes.ToList();
            if (list.Count == 0)
                return;
            PushUndo();
            if (Selection != null)
                RemoveSelection();

            var (rowPath, index) = SplitRunAtCaret();
            var row = MathTree.Get(Root, rowPath);
            var children = row.Children.ToList();
            children.InsertRange(index, list);
            Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
            SetCaret(rowPath, index + list.Count);
            OnChanged(true);
        }

        /// <summary>
        /// Pastes text: an equation in the format inserted as nodes, or the text as a run if it does not parse. Returns
        /// whether the text was read as an equation.
        /// </summary>
        public bool Paste(string text, EquationFormat format)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            if (Equation.TryParse(text, format, new EquationOptions { Display = IsDisplay }, out var equation) && equation != null)
            {
                InsertNodes(FromEquation(equation).Children[0].Children);
                return true;
            }

            PushUndo();
            if (Selection != null)
                RemoveSelection();
            foreach (var rune in text.EnumerateRunes())
                InsertRune(rune);
            OnChanged(true);
            return false;
        }

        /// <summary>The selected nodes, with a selection in a run as a run of the selected text.</summary>
        public IReadOnlyList<MathNode> GetSelectedNodes()
        {
            var selection = Selection;
            if (selection == null)
                return Array.Empty<MathNode>();
            var node = MathTree.Get(Root, selection.Path);
            if (selection.IsInRun)
                return new[] { MathNode.Run(CodePointText.Substring(node.Text, selection.Start, selection.End - selection.Start)) };
            return node.Children.Skip(selection.Start).Take(selection.End - selection.Start).ToList();
        }

        /// <summary>The selection as an equation, or null when nothing is selected.</summary>
        public Equation? GetSelectedEquation()
        {
            var nodes = GetSelectedNodes();
            if (nodes.Count == 0)
                return null;
            var row = BuildUp.BuildAll(MathNode.Row(nodes), InputFormat, IsDisplay);
            return BuildUp.ToEquation(row.Children, IsDisplay);
        }

        /// <summary>Deletes the selection; returns whether something was selected.</summary>
        public bool DeleteSelection()
        {
            if (Selection == null)
                return false;
            PushUndo();
            RemoveSelection();
            OnChanged(true);
            return true;
        }

        /// <summary>
        /// Deletes the code point or the token before the caret; a structure before the caret is selected first, and
        /// deleted the second time, as in Word.
        /// </summary>
        public void Backspace() => DeleteAt(-1);

        /// <summary>Deletes the code point or the token after the caret, as <see cref="Backspace"/> forward.</summary>
        public void Delete() => DeleteAt(1);

        /// <summary>Moves the caret one position to the left, into the structures it meets; with extend, selects.</summary>
        public void MoveLeft(bool extend = false) => MoveHorizontal(-1, extend);

        /// <summary>Moves the caret one position to the right, into the structures it meets; with extend, selects.</summary>
        public void MoveRight(bool extend = false) => MoveHorizontal(1, extend);

        /// <summary>Moves the caret to the slot above (the numerator, the superscript, the row above in a table).</summary>
        public void MoveUp(bool extend = false, IMathGeometry? geometry = null) => MoveVertical(-1, extend, geometry);

        /// <summary>Moves the caret to the slot below (the denominator, the subscript, the row below in a table).</summary>
        public void MoveDown(bool extend = false, IMathGeometry? geometry = null) => MoveVertical(1, extend, geometry);

        /// <summary>Moves the caret to the start of its row, or of the equation if it is there already.</summary>
        public void MoveHome(bool extend = false)
        {
            var (row, index) = RowPosition(Caret);
            var target = index == 0 ? MathTree.RootRow : row;
            Move(Canonical(new MathPosition(target, 0)), extend);
        }

        /// <summary>Moves the caret to the end of its row, or of the equation if it is there already.</summary>
        public void MoveEnd(bool extend = false)
        {
            var (row, index) = RowPosition(Caret);
            var count = MathTree.Get(Root, row).Children.Count;
            var target = index == count ? MathTree.RootRow : row;
            Move(Canonical(new MathPosition(target, MathTree.Get(Root, target).Children.Count)), extend);
        }

        /// <summary>Selects the whole equation.</summary>
        public void SelectAll()
        {
            Anchor = new MathPosition(MathTree.RootRow, 0);
            Caret = new MathPosition(MathTree.RootRow, RootRow.Children.Count);
            typingAt = null;
            OnChanged(false);
        }

        /// <summary>Moves the caret to a position (one the renderer found under the pointer); with extend, selects.</summary>
        /// <exception cref="ArgumentException">The position is not in the equation.</exception>
        public void SetCaret(MathPosition position, bool extend = false)
        {
            if (!IsValid(position))
                throw new ArgumentException("The position is not in the equation.", nameof(position));
            Move(Canonical(position), extend);
        }

        /// <summary>Undoes the last edit.</summary>
        public void Undo()
        {
            if (undo.Count == 0)
                return;
            redo.Push(Snapshot());
            Restore(undo.Pop());
        }

        /// <summary>Redoes the last edit undone.</summary>
        public void Redo()
        {
            if (redo.Count == 0)
                return;
            undo.Push(Snapshot());
            Restore(redo.Pop());
        }

        private static MathNode Empty(bool display) => MathNode.Element(
            "math",
            new[] { MathNode.Row(Array.Empty<MathNode>()) },
            display ? new[] { new KeyValuePair<string, string>("display", "block") } : null);

        private static MathNode FromEquation(Equation equation) => MathNode.FromMathML(equation.ToMathML());

        private MathNode Built() => BuildUp.BuildAll(Root, InputFormat, IsDisplay);

        private MathNode Linearize(MathNode root)
        {
            var row = root.Children[0];
            if (row.Children.Count == 0)
                return root;
            var built = BuildUp.BuildAll(row, InputFormat, IsDisplay);
            var equation = BuildUp.ToEquation(built.Children, root.Attributes);
            var text = equation == null ? string.Concat(row.Children.Select(c => c.IsRun ? c.Text : string.Empty)) : BuildUp.ToLinear(equation, InputFormat);
            return root.WithChild(0, MathNode.Row(text.Length == 0 ? Array.Empty<MathNode>() : new[] { MathNode.Run(text) }));
        }

        private void InsertRune(Rune rune)
        {
            var caret = Canonical(Caret);
            var node = MathTree.Get(Root, caret.Path);
            if (node.IsRun)
            {
                Root = MathTree.Replace(Root, caret.Path, node.WithText(CodePointText.Insert(node.Text, caret.Offset, rune.ToString())));
                Caret = Anchor = new MathPosition(caret.Path, caret.Offset + 1);
            }
            else
            {
                var children = node.Children.ToList();
                children.Insert(caret.Offset, MathNode.Run(rune.ToString()));
                Root = MathTree.Replace(Root, caret.Path, node.WithChildren(children));
                Caret = Anchor = new MathPosition(MathTree.Append(caret.Path, caret.Offset), 1);
            }
        }

        /// <summary>Builds up the argument before the caret, in the run of the caret, after a trigger was typed.</summary>
        private bool TryBuildUpAtCaret(bool space)
        {
            var caret = Caret;
            var run = MathTree.Get(Root, caret.Path);
            if (!run.IsRun)
                return false;

            var rowPath = MathTree.Parent(caret.Path);
            var j = caret.Path[caret.Path.Count - 1];
            var row = MathTree.Get(Root, rowPath);
            var runes = CodePointText.Runes(run.Text);
            var before = runes.Take(caret.Offset).ToList();
            var after = CodePointText.Join(runes.Skip(caret.Offset));
            var argument = space ? before.Take(before.Count - 1).ToList() : before;

            if (argument.All(r => r.Value == ' '))
            {
                if (space)
                {
                    // a space alone builds nothing, and is not kept
                    var rest = CodePointText.Join(argument) + after;
                    ReplaceRun(rowPath, j, rest, argument.Count);
                }
                return false;
            }

            var last = space ? argument[argument.Count - 1] : argument.Count >= 2 ? argument[argument.Count - 2] : (Rune?)null;
            if (last is { } l && BuildUp.ExpectsOperand(l, InputFormat))
                return false;
            var text = CodePointText.Join(argument);
            if (!BuildUp.IsBalanced(text, InputFormat))
                return false;

            var nodes = BuildUp.Parse(row.Children.Take(j).ToList(), text, InputFormat, IsDisplay);
            if (nodes == null)
                return false;

            undo.Push(Snapshot());
            redo.Clear();
            var children = nodes.ToList();
            if (after.Length > 0)
                children.Add(MathNode.Run(after));
            children.AddRange(row.Children.Skip(j + 1));
            Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
            Caret = Anchor = Canonical(new MathPosition(rowPath, nodes.Count));
            return true;
        }

        private void ReplaceRun(int[] rowPath, int index, string text, int caretOffset)
        {
            var row = MathTree.Get(Root, rowPath);
            var children = row.Children.ToList();
            if (text.Length == 0)
            {
                children.RemoveAt(index);
                Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
                Caret = Anchor = Canonical(new MathPosition(rowPath, index));
            }
            else
            {
                children[index] = MathNode.Run(text);
                Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
                Caret = Anchor = new MathPosition(MathTree.Append(rowPath, index), caretOffset);
            }
        }

        private void DeleteAt(int direction)
        {
            if (DeleteSelection())
                return;

            var caret = Canonical(Caret);
            var node = MathTree.Get(Root, caret.Path);
            if (node.IsRun)
            {
                var length = CodePointText.Length(node.Text);
                var at = direction < 0 ? caret.Offset - 1 : caret.Offset;
                if (at >= 0 && at < length)
                {
                    PushUndo();
                    ReplaceRun(MathTree.Parent(caret.Path), caret.Path[caret.Path.Count - 1], CodePointText.Remove(node.Text, at, 1), at);
                    OnChanged(true);
                    return;
                }
            }

            var (rowPath, index) = RowPosition(caret);
            var row = MathTree.Get(Root, rowPath);
            var target = direction < 0 ? index - 1 : index;
            if (target < 0 || target >= row.Children.Count)
            {
                // at the edge of a slot: select the structure it belongs to
                if (MathTree.Owner(Root, rowPath) is { } owner)
                {
                    var parent = MathTree.Parent(owner.Item);
                    var item = owner.Item[owner.Item.Length - 1];
                    Anchor = new MathPosition(parent, item);
                    Caret = new MathPosition(parent, item + 1);
                    typingAt = null;
                    OnChanged(false);
                }
                return;
            }

            var child = row.Children[target];
            if (child.IsToken || child.IsRun)
            {
                PushUndo();
                var children = row.Children.ToList();
                var length = CodePointText.Length(child.Text);
                var rest = direction < 0 ? CodePointText.Substring(child.Text, 0, Math.Max(0, length - 1)) : CodePointText.Substring(child.Text, Math.Min(1, length));
                if (rest.Length == 0)
                {
                    children.RemoveAt(target);
                    ReplaceChildren(rowPath, children, target);
                }
                else
                {
                    children[target] = MathNode.Run(rest);
                    Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
                    var runPath = MathTree.Append(rowPath, target);
                    SetCaret(runPath, direction < 0 ? CodePointText.Length(rest) : 0);
                }
                OnChanged(true);
            }
            else if (child.Children.Count == 0 && !child.IsOpaque)
            {
                PushUndo();
                var children = row.Children.ToList();
                children.RemoveAt(target);
                ReplaceChildren(rowPath, children, target);
                OnChanged(true);
            }
            else
            {
                // a structure is selected first, and deleted the second time
                Anchor = new MathPosition(rowPath, target);
                Caret = new MathPosition(rowPath, target + 1);
                typingAt = null;
                OnChanged(false);
            }
        }

        private void RemoveSelection()
        {
            var selection = Selection;
            if (selection == null)
                return;

            var node = MathTree.Get(Root, selection.Path);
            if (selection.IsInRun)
            {
                var text = CodePointText.Remove(node.Text, selection.Start, selection.End - selection.Start);
                ReplaceRun(MathTree.Parent(selection.Path), selection.Path[selection.Path.Count - 1], text, selection.Start);
                return;
            }

            var children = node.Children.ToList();
            children.RemoveRange(selection.Start, selection.End - selection.Start);
            ReplaceChildren(selection.Path.ToArray(), children, selection.Start);
        }

        /// <summary>
        /// Merges the runs that are next to each other, keeping track of a position among the children: before the
        /// child at the index, or in the run at the index when the offset is not null.
        /// </summary>
        private static List<MathNode> MergeRuns(List<MathNode> children, ref int index, ref int? offset)
        {
            for (var i = children.Count - 1; i >= 1; i--)
            {
                if (!children[i - 1].IsRun || !children[i].IsRun)
                    continue;
                var length = CodePointText.Length(children[i - 1].Text);
                children[i - 1] = MathNode.Run(children[i - 1].Text + children[i].Text);
                children.RemoveAt(i);
                if (index == i)
                {
                    index = i - 1;
                    offset = (offset ?? 0) + length;
                }
                else if (index > i)
                {
                    index--;
                }
            }
            return children;
        }

        private void ReplaceChildren(int[] rowPath, List<MathNode> children, int index, int? offset = null)
        {
            var row = MathTree.Get(Root, rowPath);
            children = MergeRuns(children, ref index, ref offset);
            Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
            SetCaret(offset is { } o ? MathTree.Append(rowPath, index) : rowPath, offset ?? index);
        }

        /// <summary>Splits the run of the caret there, and gives the row and the index where nodes go in.</summary>
        private (int[] RowPath, int Index) SplitRunAtCaret()
        {
            var caret = Canonical(Caret);
            var node = MathTree.Get(Root, caret.Path);
            if (!node.IsRun)
                return (caret.Path.ToArray(), caret.Offset);

            var rowPath = MathTree.Parent(caret.Path);
            var j = caret.Path[caret.Path.Count - 1];
            var length = CodePointText.Length(node.Text);
            if (caret.Offset == 0)
                return (rowPath, j);
            if (caret.Offset == length)
                return (rowPath, j + 1);

            var row = MathTree.Get(Root, rowPath);
            var children = row.Children.ToList();
            children[j] = MathNode.Run(CodePointText.Substring(node.Text, 0, caret.Offset));
            children.Insert(j + 1, MathNode.Run(CodePointText.Substring(node.Text, caret.Offset)));
            Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
            return (rowPath, j + 1);
        }

        private void MoveHorizontal(int direction, bool extend)
        {
            var selection = Selection;
            if (!extend && selection != null)
            {
                Move(Canonical(direction < 0 ? selection.StartPosition : selection.EndPosition), false);
                return;
            }

            var next = Step(Canonical(Caret), direction, extend);
            if (next != null)
                Move(next, extend);
        }

        /// <summary>The position after one step to the left or the right; with atomic, structures are stepped over.</summary>
        private MathPosition? Step(MathPosition caret, int direction, bool atomic)
        {
            var node = MathTree.Get(Root, caret.Path);
            if (node.IsRun)
            {
                var length = CodePointText.Length(node.Text);
                var offset = caret.Offset + direction;
                if (offset > 0 && offset < length)
                    return new MathPosition(caret.Path, offset);
                var rowPath = MathTree.Parent(caret.Path);
                var j = caret.Path[caret.Path.Count - 1];
                if (offset <= 0)
                    return offset == 0 ? Canonical(new MathPosition(rowPath, j)) : Step(new MathPosition(rowPath, j), direction, atomic);
                return offset == length ? Canonical(new MathPosition(rowPath, j + 1)) : Step(new MathPosition(rowPath, j + 1), direction, atomic);
            }

            var row = node;
            var index = caret.Offset;
            var target = direction < 0 ? index - 1 : index;
            if (target >= 0 && target < row.Children.Count)
            {
                var child = row.Children[target];
                var childPath = MathTree.Append(caret.Path, target);
                if (child.IsRun)
                {
                    var length = CodePointText.Length(child.Text);
                    if (length > 1)
                        return new MathPosition(childPath, direction < 0 ? length - 1 : 1);
                    return Canonical(new MathPosition(caret.Path, index + direction));
                }

                var slots = atomic ? Array.Empty<int[]>() : MathSchema.Slots(child);
                if (slots.Count > 0)
                {
                    var slot = MathTree.Append(childPath, direction < 0 ? slots[slots.Count - 1] : slots[0]);
                    var slotRow = MathTree.Get(Root, slot);
                    return Canonical(new MathPosition(slot, direction < 0 ? slotRow.Children.Count : 0));
                }
                return Canonical(new MathPosition(caret.Path, index + direction));
            }

            // at the edge of the row: to the next slot of the structure, or out of it
            if (MathTree.Owner(Root, caret.Path) is not { } owner)
                return null;
            var nextSlot = owner.Index + direction;
            if (!atomic && nextSlot >= 0 && nextSlot < owner.Slots.Length)
            {
                var slot = owner.Slots[nextSlot];
                return Canonical(new MathPosition(slot, direction < 0 ? MathTree.Get(Root, slot).Children.Count : 0));
            }
            var parent = MathTree.Parent(owner.Item);
            var item = owner.Item[owner.Item.Length - 1];
            return Canonical(new MathPosition(parent, direction < 0 ? item : item + 1));
        }

        private void MoveVertical(int direction, bool extend, IMathGeometry? geometry)
        {
            var caret = Canonical(Caret);
            var (rowPath, _) = RowPosition(caret);
            int[]? target = null;
            var current = rowPath;
            while (target == null)
            {
                if (MathTree.Owner(Root, current) is not { } owner)
                    return;
                var relative = current.Skip(owner.Item.Length).ToArray();
                if (MathSchema.Vertical(MathTree.Get(Root, owner.Item), relative, direction) is { } slot)
                {
                    target = MathTree.Append(owner.Item, slot);
                    break;
                }
                current = MathTree.Parent(owner.Item);
            }

            MathPosition? position = null;
            if (geometry != null && geometry.GetCaretX(caret) is { } x)
                position = geometry.GetNearestPosition(target, x);
            position ??= new MathPosition(target, MathTree.Get(Root, target).Children.Count);
            if (IsValid(position))
                Move(Canonical(position), extend);
        }

        private void Move(MathPosition position, bool extend)
        {
            Caret = position;
            if (!extend)
                Anchor = position;
            typingAt = null;
            OnChanged(false);
        }

        private void SetCaret(IReadOnlyList<int> path, int offset)
        {
            Caret = Anchor = Canonical(new MathPosition(path, offset));
        }

        private void SetCaretAtEnd() => SetCaret(MathTree.RootRow, RootRow.Children.Count);

        private bool IsValid(MathPosition position)
        {
            if (!MathTree.TryGet(Root, position.Path, out var node))
                return false;
            if (node.IsRun)
                return position.Offset <= CodePointText.Length(node.Text);
            return node.IsRow && position.Offset <= node.Children.Count;
        }

        /// <summary>The position, inside the run next to it if there is one: a position between a run and its neighbour is the end or the start of the run.</summary>
        internal MathPosition Canonical(MathPosition position)
        {
            var node = MathTree.Get(Root, position.Path);
            if (!node.IsRow)
                return position;
            var i = position.Offset;
            if (i > 0 && node.Children[i - 1].IsRun)
                return new MathPosition(MathTree.Append(position.Path, i - 1), CodePointText.Length(node.Children[i - 1].Text));
            if (i < node.Children.Count && node.Children[i].IsRun)
                return new MathPosition(MathTree.Append(position.Path, i), 0);
            return position;
        }

        /// <summary>The row of a position and the index of the child after it; inside a run, the index of the run.</summary>
        private (int[] Row, int Index) RowPosition(MathPosition position)
        {
            var node = MathTree.Get(Root, position.Path);
            if (!node.IsRun)
                return (position.Path.ToArray(), position.Offset);
            var j = position.Path[position.Path.Count - 1];
            var row = MathTree.Parent(position.Path);
            if (position.Offset == 0)
                return (row, j);
            return (row, position.Offset >= CodePointText.Length(node.Text) ? j + 1 : j);
        }

        private MathSelection? ComputeSelection(MathPosition anchor, MathPosition caret)
        {
            if (anchor == caret)
                return null;

            var anchorNode = MathTree.Get(Root, anchor.Path);
            if (anchorNode.IsRun && anchor.Path.SequenceEqual(caret.Path))
                return new MathSelection(anchor.Path.ToArray(), Math.Min(anchor.Offset, caret.Offset), Math.Max(anchor.Offset, caret.Offset), true);

            var a = Chain(anchor);
            var c = Chain(caret);
            for (var i = Math.Min(a.Count, c.Count) - 1; i >= 0; i--)
            {
                if (!a[i].Row.SequenceEqual(c[i].Row))
                    continue;
                var start = Math.Min(a[i].Start, c[i].Start);
                var end = Math.Max(a[i].End, c[i].End);
                return start == end ? null : new MathSelection(a[i].Row, start, end, false);
            }
            return null;
        }

        /// <summary>For each row from the root down to the position, the range of its children the position is in.</summary>
        private List<(int[] Row, int Start, int End)> Chain(MathPosition position)
        {
            var node = MathTree.Get(Root, position.Path);
            int[] rowPath;
            int start, end;
            if (node.IsRun)
            {
                rowPath = MathTree.Parent(position.Path);
                var j = position.Path[position.Path.Count - 1];
                var length = CodePointText.Length(node.Text);
                (start, end) = position.Offset == 0 ? (j, j) : position.Offset >= length ? (j + 1, j + 1) : (j, j + 1);
            }
            else
            {
                rowPath = position.Path.ToArray();
                start = end = position.Offset;
            }

            var chain = new List<(int[] Row, int Start, int End)>();
            for (var depth = 0; depth < rowPath.Length; depth++)
            {
                var prefix = rowPath.Take(depth).ToArray();
                if (MathTree.Get(Root, prefix).IsRow)
                    chain.Add((prefix, rowPath[depth], rowPath[depth] + 1));
            }
            chain.Add((rowPath, start, end));
            return chain;
        }

        private State Snapshot() => new State(Root, Caret, Anchor, IsLinear);

        private void PushUndo()
        {
            undo.Push(Snapshot());
            redo.Clear();
            typingAt = null;
        }

        private void Restore(State state)
        {
            Root = state.Root;
            Caret = state.Caret;
            Anchor = state.Anchor;
            IsLinear = state.IsLinear;
            typingAt = null;
            OnChanged(true);
        }

        private void OnChanged(bool content)
        {
            if (content)
                ContentChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private sealed class State
        {
            public State(MathNode root, MathPosition caret, MathPosition anchor, bool linear)
            {
                Root = root;
                Caret = caret;
                Anchor = anchor;
                IsLinear = linear;
            }

            public MathNode Root { get; }

            public MathPosition Caret { get; }

            public MathPosition Anchor { get; }

            public bool IsLinear { get; }
        }
    }
}
