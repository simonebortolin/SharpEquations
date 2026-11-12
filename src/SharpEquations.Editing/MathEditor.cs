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
        private readonly History undo = new History();
        private readonly History redo = new History();
        private EquationFormat inputFormat = EquationFormat.UnicodeMath;
        private MathPosition? typingAt;
        private MathPosition? deferredAt;
        private string? dismissedPrefix;

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

        /// <summary>
        /// The math autocorrect, which replaces sequences by their symbols as they are typed (<c>-&gt;</c> by →,
        /// <c>\alpha</c> and a space by α), with the entries of Word by default; null for none. An undo right after a
        /// replacement gives back what was typed.
        /// </summary>
        public MathAutoCorrect? AutoCorrect { get; set; } = new MathAutoCorrect();

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

        /// <summary>
        /// How many edits <see cref="Undo"/> can undo at most (1000 by default); the oldest are forgotten. The states are
        /// immutable trees that share what an edit did not change, so that each costs only the path it changed.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A negative limit.</exception>
        public int UndoLimit
        {
            get => undo.Limit;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                undo.Limit = redo.Limit = value;
            }
        }

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
            var state = Snapshot();
            var corrected = FinishAutoCorrect();
            var built = Built();
            if (!corrected && ReferenceEquals(built, Root))
                return;
            undo.Push(state);
            redo.Clear();
            typingAt = deferredAt = null;
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
                var resumes = deferredAt != null && deferredAt.Equals(Caret);
                deferredAt = null;
                InsertRune(rune);
                if (!IsLinear)
                    built |= TypedAtCaret(rune, resumes);
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
        /// Pastes text: an equation in the format inserted as nodes, or, for UnicodeMath and LaTeX, the text as a run if it
        /// does not parse; MathML or OMML that does not parse inserts nothing. Returns whether the text was read as an
        /// equation.
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

            // markup that does not read is not text to type
            if (format is not (EquationFormat.UnicodeMath or EquationFormat.LaTeX))
                return false;

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

        /// <summary>
        /// The selection in a format, or null when nothing is selected: UnicodeMath, LaTeX (as Word writes it unless the
        /// options say otherwise), MathML or OMML.
        /// </summary>
        /// <exception cref="ArgumentException">MTEF, which cannot be written.</exception>
        public string? GetSelectedText(EquationFormat format, EquationOptions? options = null)
        {
            if (!EquationConverter.CanWrite(format))
                throw new ArgumentException("The format cannot be written.", nameof(format));
            var equation = GetSelectedEquation();
            if (equation == null)
                return null;
            if (options == null && format is EquationFormat.UnicodeMath or EquationFormat.LaTeX)
                return BuildUp.ToLinear(equation, format);
            return equation.ToString(format, options);
        }

        /// <summary>
        /// Inserts a structure with empty slots (<c>mfrac</c>, <c>msqrt</c>, <c>mroot</c>, <c>msub</c>, <c>msup</c>,
        /// <c>msubsup</c>, <c>munder</c>, <c>mover</c>, <c>munderover</c>, <c>menclose</c>...), as the buttons of the
        /// equation tools of Word: the selection goes into its first slot, and the caret into the first empty slot.
        /// </summary>
        /// <exception cref="ArgumentException">An element that is not a structure with slots.</exception>
        public void InsertStructure(string name, IReadOnlyList<KeyValuePair<string, string>>? attributes = null)
        {
            var slots = name switch
            {
                "msqrt" or "menclose" or "mstyle" or "mphantom" or "mpadded" or "merror" => 1,
                "mfrac" or "mroot" or "msub" or "msup" or "munder" or "mover" => 2,
                "msubsup" or "munderover" => 3,
                _ => throw new ArgumentException("Not a structure with slots: " + name, nameof(name)),
            };
            var first = GetSelectedNodes().ToList();
            var rows = Enumerable.Range(0, slots).Select(i => MathNode.Row(i == 0 ? first : new List<MathNode>())).ToList();
            InsertWithSlots(MathNode.Element(name, rows, attributes));
        }

        /// <summary>
        /// Inserts a template: a node whose empty rows are slots to fill (an n-ary operator with its limits, an accent
        /// over an empty base...). The caret goes into its first empty slot, or after it.
        /// </summary>
        public void InsertTemplate(MathNode node) => InsertWithSlots(node ?? throw new ArgumentNullException(nameof(node)));

        /// <summary>Inserts a pair of fences, <c>(</c> and <c>)</c> by default, around the selection or an empty slot.</summary>
        public void InsertFences(string open = "(", string close = ")")
        {
            var children = new List<MathNode> { MathNode.Token("mo", open) };
            children.AddRange(GetSelectedNodes());
            children.Add(MathNode.Token("mo", close));
            InsertWithSlots(MathNode.Row(children));
        }

        /// <summary>Inserts a table of empty cells: a matrix, alone or between fences (such as <c>(</c> and <c>)</c>).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Fewer than one row or column.</exception>
        public void InsertMatrix(int rows, int columns, string? open = null, string? close = null)
        {
            if (rows < 1)
                throw new ArgumentOutOfRangeException(nameof(rows));
            if (columns < 1)
                throw new ArgumentOutOfRangeException(nameof(columns));
            var empty = MathNode.Row(Array.Empty<MathNode>());
            var table = MathNode.Element("mtable", Enumerable.Range(0, rows).Select(_ =>
                MathNode.Element("mtr", Enumerable.Range(0, columns).Select(_ => MathNode.Element("mtd", new[] { empty })))));
            InsertWithSlots(open == null && close == null
                ? table
                : MathNode.Row(new[] { MathNode.Token("mo", open ?? string.Empty), table, MathNode.Token("mo", close ?? string.Empty) }));
        }

        /// <summary>
        /// Moves the caret to the next cell of the table it is in (the previous one, backward), selecting nothing;
        /// returns false when the caret is not in a table or is in its last cell (its first, backward).
        /// </summary>
        public bool MoveToNextCell(bool backward = false)
        {
            var (rowPath, _) = RowPosition(Canonical(Caret));
            var current = rowPath;
            while (MathTree.Owner(Root, current) is { } owner)
            {
                if (MathTree.Get(Root, owner.Item).Name == "mtable")
                {
                    var next = owner.Index + (backward ? -1 : 1);
                    if (next < 0 || next >= owner.Slots.Length)
                        return false;
                    var cell = owner.Slots[next];
                    Move(Canonical(new MathPosition(cell, backward ? MathTree.Get(Root, cell).Children.Count : 0)), false);
                    return true;
                }
                current = MathTree.Parent(owner.Item);
            }
            return false;
        }

        /// <summary>
        /// The number of the equation, as UnicodeMath writes it after <c>#</c> and LaTeX in <c>\tag</c>: the label of an
        /// equation that is one labeled row (<c>mtable</c> with one <c>mlabeledtr</c>); null without one.
        /// </summary>
        public string? EquationNumber => NumberedRow() is { } row ? string.Concat(Text(row.Children[0])) : null;

        /// <summary>Numbers the equation, shown at the right of its line; null removes the number.</summary>
        public void SetEquationNumber(string? number)
        {
            var labeled = NumberedRow();
            if (labeled == null && number == null)
                return;
            PushUndo();
            var label = MathNode.Element("mtd", new[] { MathNode.Row(new[] { MathNode.Token("mtext", number ?? string.Empty) }) });
            var content = labeled == null ? RootRow : labeled.Children[1].Children[0];
            MathNode row;
            if (number == null)
            {
                row = MathNode.Row(content.Children);
            }
            else
            {
                // the id of the label as UnicodeMath gives it: its text without the brackets, between hyphens
                var id = "-" + number.Trim().Trim('(', ')', '[', ']', '{', '}') + "-";
                var labeledRow = MathNode.Element("mlabeledtr", new[] { label, MathNode.Element("mtd", new[] { content }) }, new[] { new KeyValuePair<string, string>("id", id) });
                row = MathNode.Row(new[]
                {
                    MathNode.Element("mtable", new[] { labeledRow }, new[] { new KeyValuePair<string, string>("displaystyle", "true") }),
                });
            }
            Root = Root.WithChild(0, row);
            var contentPath = number == null ? MathTree.RootRow : new[] { 0, 0, 0, 1, 0 };
            SetCaret(contentPath, MathTree.Get(Root, contentPath).Children.Count);
            OnChanged(true);
        }

        /// <summary>
        /// The command being typed before the caret (<c>\al</c>), with its backslash, or null: what
        /// <see cref="GetCompletions"/> completes.
        /// </summary>
        public string? CommandPrefix
        {
            get
            {
                if (Selection != null || Canonical(Caret) is not { } caret || MathTree.Get(Root, caret.Path) is not { IsRun: true } run)
                    return null;
                var before = CodePointText.Substring(run.Text, 0, caret.Offset);
                var i = before.Length;
                while (i > 0 && before[i - 1] < 0x80 && char.IsLetterOrDigit(before[i - 1]))
                    i--;
                return i > 0 && before[i - 1] == '\\' ? before.Substring(i - 1) : null;
            }
        }

        /// <summary>The commands of the input format that complete <see cref="CommandPrefix"/>; empty when none is typed.</summary>
        public IReadOnlyList<EquationCommand> GetCompletions(int max = 20) =>
            CommandPrefix is { } prefix ? EquationCommands.Complete(prefix.Substring(1), InputFormat, max) : Array.Empty<EquationCommand>();

        /// <summary>
        /// Completes the command typed before the caret: a command with a symbol becomes the symbol, as Word's math
        /// autocorrect does; a structure of LaTeX becomes its text with the caret in its first argument
        /// (<c>\frac{|}{}</c>), and one of UnicodeMath its control word and a space.
        /// </summary>
        public void Complete(EquationCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (CommandPrefix is not { } prefix)
                return;

            PushUndo();
            var caret = Canonical(Caret);
            var run = MathTree.Get(Root, caret.Path);
            var start = caret.Offset - CodePointText.Length(prefix);
            var text = command.Symbol.Length > 0 ? command.Symbol : command.Arguments > 0 ? command.Text : command.Text + " ";
            var offset = command.Symbol.Length == 0 && command.Arguments > 0 ? CodePointText.Length("\\" + command.Name) + 1 : CodePointText.Length(text);
            var rest = CodePointText.Remove(run.Text, start, caret.Offset - start);
            Root = MathTree.Replace(Root, caret.Path, run.WithText(CodePointText.Insert(rest, start, text)));
            Caret = Anchor = new MathPosition(caret.Path, start + offset);
            if (!IsLinear && command.Symbol.Length == 0 && command.Arguments == 0)
                TryBuildUpAtCaret(true);
            OnChanged(true);
        }

        /// <summary>
        /// What the floating bar at the caret shows: the completions of the command typed before the caret (unless
        /// <see cref="DismissCompletions"/> closed them), the structures for the selection, the commands of the table of
        /// the caret, or the quick symbols in an empty slot; nothing in the linear format.
        /// </summary>
        /// <param name="maxCompletions">The most completions to give.</param>
        /// <param name="completions">The completions, in <see cref="MathQuickBarMode.Completions"/>; empty otherwise.</param>
        public MathQuickBarMode GetQuickBarMode(int maxCompletions, out IReadOnlyList<EquationCommand> completions)
        {
            completions = Array.Empty<EquationCommand>();
            if (IsLinear)
                return MathQuickBarMode.Hidden;

            var prefix = CommandPrefix;
            if (prefix != null)
            {
                if (prefix.Length > 1 && prefix != dismissedPrefix && GetCompletions(maxCompletions) is { Count: > 0 } found)
                {
                    completions = found;
                    return MathQuickBarMode.Completions;
                }
                return MathQuickBarMode.Hidden;
            }

            if (Selection != null)
                return MathQuickBarMode.Selection;
            if (IsInTable)
                return MathQuickBarMode.Table;
            return IsInEmptySlot() ? MathQuickBarMode.Symbols : MathQuickBarMode.Hidden;
        }

        /// <summary>
        /// Closes the completions of the command typed before the caret (Esc), until the command typed changes:
        /// <see cref="GetQuickBarMode"/> no longer gives them.
        /// </summary>
        public void DismissCompletions() => dismissedPrefix = CommandPrefix;

        private bool IsInEmptySlot() =>
            Caret.Path.Count > 1 && MathTree.TryGet(Root, Caret.Path, out var node) && node is { IsRow: true, Children.Count: 0 };

        /// <summary>Whether the caret is in a cell of a table (a matrix, an equation array): the table commands apply.</summary>
        public bool IsInTable => CellAtCaret() != null;

        /// <summary>Inserts an empty row in the table of the caret, below its row (above, with <paramref name="above"/>), and puts the caret in it.</summary>
        /// <returns>Whether the caret is in a table.</returns>
        public bool InsertTableRow(bool above = false)
        {
            if (CellAtCaret() is not { } cell)
                return false;
            PushUndo();
            var table = MathTree.Get(Root, cell.Table);
            var rows = table.Children.ToList();
            var current = rows[cell.Row];
            var empty = MathNode.Row(Array.Empty<MathNode>());
            var array = ArrayTable() != null && cell.Table.SequenceEqual(ArrayPath);
            var cells = current.Children.Select(_ => MathNode.Element("mtd", new[] { array ? Line(Array.Empty<MathNode>()).Children[0].Children[0] : empty }));
            var at = above ? cell.Row : cell.Row + 1;
            rows.Insert(at, MathNode.Element(current.Name == "mlabeledtr" ? "mtr" : current.Name, current.Name == "mlabeledtr" ? cells.Skip(1) : cells, current.Name == "mlabeledtr" ? null : current.Attributes));
            Root = MathTree.Replace(Root, cell.Table, table.WithChildren(rows));
            SetCaret(MathTree.Append(cell.Table, at, Math.Min(cell.Column, rows[at].Children.Count - 1), 0), array ? 1 : 0);
            OnChanged(true);
            return true;
        }

        /// <summary>Inserts an empty column in the table of the caret, right of its column (left, with <paramref name="before"/>), and puts the caret in it.</summary>
        /// <returns>Whether the caret is in a table.</returns>
        public bool InsertTableColumn(bool before = false)
        {
            if (CellAtCaret() is not { } cell)
                return false;
            PushUndo();
            var table = MathTree.Get(Root, cell.Table);
            var at = before ? cell.Column : cell.Column + 1;
            var rows = table.Children.Select(row =>
            {
                var cells = row.Children.ToList();
                cells.Insert(Math.Min(at, cells.Count), MathNode.Element("mtd", new[] { MathNode.Row(Array.Empty<MathNode>()) }));
                return row.WithChildren(cells);
            });
            Root = MathTree.Replace(Root, cell.Table, table.WithChildren(rows));
            SetCaret(MathTree.Append(cell.Table, cell.Row, at, 0), 0);
            OnChanged(true);
            return true;
        }

        /// <summary>Deletes the row of the caret from its table; the table itself when it is its only row.</summary>
        /// <returns>Whether the caret is in a table.</returns>
        public bool DeleteTableRow()
        {
            if (CellAtCaret() is not { } cell)
                return false;
            PushUndo();
            var table = MathTree.Get(Root, cell.Table);
            if (table.Children.Count == 1)
                return RemoveTable(cell.Table);
            var rows = table.Children.ToList();
            rows.RemoveAt(cell.Row);
            Root = MathTree.Replace(Root, cell.Table, table.WithChildren(rows));
            var row = Math.Min(cell.Row, rows.Count - 1);
            SetCaret(MathTree.Append(cell.Table, row, Math.Min(cell.Column, rows[row].Children.Count - 1), 0), 0);
            OnChanged(true);
            return true;
        }

        /// <summary>Deletes the column of the caret from its table; the table itself when it is its only column.</summary>
        /// <returns>Whether the caret is in a table.</returns>
        public bool DeleteTableColumn()
        {
            if (CellAtCaret() is not { } cell)
                return false;
            PushUndo();
            var table = MathTree.Get(Root, cell.Table);
            if (table.Children.All(r => r.Children.Count <= 1))
                return RemoveTable(cell.Table);
            var rows = table.Children.Select(row =>
            {
                var cells = row.Children.ToList();
                if (cell.Column < cells.Count && cells.Count > 1)
                    cells.RemoveAt(cell.Column);
                return row.WithChildren(cells);
            }).ToList();
            Root = MathTree.Replace(Root, cell.Table, table.WithChildren(rows));
            SetCaret(MathTree.Append(cell.Table, cell.Row, Math.Min(cell.Column, rows[cell.Row].Children.Count - 1), 0), 0);
            OnChanged(true);
            return true;
        }

        private bool RemoveTable(int[] tablePath)
        {
            var parent = MathTree.Parent(tablePath);
            var row = MathTree.Get(Root, parent);
            var children = row.Children.ToList();
            children.RemoveAt(tablePath[tablePath.Length - 1]);
            ReplaceChildren(parent, children, tablePath[tablePath.Length - 1]);
            OnChanged(true);
            return true;
        }

        /// <summary>The innermost table cell of the caret: the path of the table, and the row and the column of the cell.</summary>
        private (int[] Table, int Row, int Column)? CellAtCaret()
        {
            var (current, _) = RowPosition(Canonical(Caret));
            while (MathTree.Owner(Root, current) is { } owner)
            {
                if (MathTree.Get(Root, owner.Item).Name == "mtable")
                {
                    var slot = owner.Slots[owner.Index];
                    return (owner.Item, slot[owner.Item.Length], slot[owner.Item.Length + 1]);
                }
                current = MathTree.Parent(owner.Item);
            }
            return null;
        }

        /// <summary>The number of lines of the equation: those of its equation array, or 1.</summary>
        public int LineCount => ArrayTable() is { } table ? table.Children.Count : 1;

        /// <summary>
        /// Breaks the line at the caret, as Shift+Enter in Word: the equation becomes an equation array (the
        /// <c>█(...@...)</c> of UnicodeMath, <c>aligned</c> of LaTeX), or gets a line more, with what is after the caret
        /// on the new line. The lines line up at their first <c>&amp;</c> (an alignment mark), and are centred without one.
        /// </summary>
        public void InsertLineBreak()
        {
            if (IsLinear)
                return;
            PushUndo();
            if (Selection != null)
                RemoveSelection();

            var (lineRow, index) = LineAtCaret();
            var row = MathTree.Get(Root, lineRow);
            var before = row.Children.Take(index).ToList();
            var after = row.Children.Skip(index).ToList();
            if (ArrayTable() is { } table)
            {
                var line = lineRow[2];
                var lines = table.Children.ToList();
                lines[line] = Line(before);
                lines.Insert(line + 1, Line(after));
                Root = MathTree.Replace(Root, ArrayPath, table.WithChildren(lines));
                SetCaret(LinePath(line + 1), 1);
            }
            else
            {
                var array = MathNode.Element("mtable", new[] { Line(before), Line(after) }, new[] { new KeyValuePair<string, string>("columnspacing", "0pt") });
                Root = Root.WithChild(0, MathNode.Row(new[] { array }));
                SetCaret(LinePath(1), 1);
            }
            OnChanged(true);
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
            typingAt = deferredAt = null;
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

        /// <summary>
        /// Acts on a key: the arrows, Home and End move the caret (Shift extends the selection; Up and Down to the nearest
        /// position of the <paramref name="geometry"/>, when given); Backspace and Delete delete; Tab moves to the next
        /// cell of a table (Shift+Tab to the previous one); Enter builds up the whole equation, Shift+Enter breaks the
        /// line. Read only, the keys that edit are not used.
        /// </summary>
        /// <returns>Whether the key was used.</returns>
        public bool ProcessKey(MathKey key, MathKeyModifiers modifiers, IMathGeometry? geometry = null, bool readOnly = false)
        {
            var shift = (modifiers & MathKeyModifiers.Shift) != 0;
            switch (key)
            {
                case MathKey.Left:
                    MoveLeft(shift);
                    return true;
                case MathKey.Right:
                    MoveRight(shift);
                    return true;
                case MathKey.Up:
                    MoveUp(shift, geometry);
                    return true;
                case MathKey.Down:
                    MoveDown(shift, geometry);
                    return true;
                case MathKey.Home:
                    MoveHome(shift);
                    return true;
                case MathKey.End:
                    MoveEnd(shift);
                    return true;
                case MathKey.Backspace when !readOnly:
                    Backspace();
                    return true;
                case MathKey.Delete when !readOnly:
                    Delete();
                    return true;
                case MathKey.Tab when (modifiers & ~MathKeyModifiers.Shift) == 0 && MoveToNextCell(shift):
                    return true;
                case MathKey.Enter when !readOnly && shift:
                    InsertLineBreak();
                    return true;
                case MathKey.Enter when !readOnly:
                    BuildUpAll();
                    return true;
                default:
                    return false;
            }
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

        /// <summary>Inserts a node with slots, and puts the caret in its first empty slot (or after it).</summary>
        private void InsertWithSlots(MathNode node)
        {
            PushUndo();
            if (Selection != null)
                RemoveSelection();
            var (rowPath, index) = SplitRunAtCaret();
            var row = MathTree.Get(Root, rowPath);
            var children = row.Children.ToList();
            children.Insert(index, node);
            Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
            var item = MathTree.Append(rowPath, index);
            if (FirstEmptySlot(item) is { } empty)
                SetCaret(empty, 0);
            else if (node.IsRow)
                SetCaret(item, node.Children.Count - 1);
            else
                SetCaret(rowPath, index + 1);
            OnChanged(true);
        }

        private static readonly int[] ArrayPath = { 0, 0 };

        private static int[] LinePath(int line) => new[] { 0, 0, line, 0, 0 };

        /// <summary>A line of an equation array: a row in a cell, which starts with maligngroup.</summary>
        private static MathNode Line(IEnumerable<MathNode> nodes)
        {
            var children = nodes.ToList();
            if (children.Count == 0 || children[0].Name != "maligngroup")
                children.Insert(0, MathNode.Element("maligngroup", Array.Empty<MathNode>()));
            return MathNode.Element("mtr", new[] { MathNode.Element("mtd", new[] { MathNode.Row(children) }) });
        }

        /// <summary>The table of the equation when the equation is an equation array (its lines), or null.</summary>
        private MathNode? ArrayTable()
        {
            if (RootRow.Children.Count != 1 || RootRow.Children[0] is not { Name: "mtable" } table || table.Children.Count == 0)
                return null;
            return table.Children.All(r => r.Name == "mtr" && r.Children.Count == 1 && r.Children[0].Children.Count == 1
                && r.Children[0].Children[0] is { IsRow: true } row && row.Children.Count > 0 && row.Children[0].Name == "maligngroup")
                ? table
                : null;
        }

        /// <summary>The row of the line of the caret (the equation, or a line of its equation array) and the index in it where the caret is, or after the structure it is in.</summary>
        private (int[] Row, int Index) LineAtCaret()
        {
            var (rowPath, index) = SplitRunAtCaret();
            var line = ArrayTable() != null && rowPath.Length >= 5 ? rowPath.Take(5).ToArray() : MathTree.RootRow;
            if (rowPath.SequenceEqual(line))
                return (line, index);
            return (line, rowPath[line.Length] + 1);
        }

        /// <summary>Merges a line of the equation array with the next one; an array of one line becomes the equation.</summary>
        private void MergeLines(int line)
        {
            var table = ArrayTable()!;
            var lines = table.Children.ToList();
            var first = lines[line].Children[0].Children[0];
            var second = lines[line + 1].Children[0].Children[0];
            var junction = first.Children.Count;
            var merged = first.Children.Concat(second.Children.Skip(1)).ToList();
            lines[line] = Line(merged);
            lines.RemoveAt(line + 1);
            if (lines.Count == 1)
            {
                Root = Root.WithChild(0, MathNode.Row(merged.Skip(1)));
                SetCaret(MathTree.RootRow, junction - 1);
            }
            else
            {
                Root = MathTree.Replace(Root, ArrayPath, table.WithChildren(lines));
                SetCaret(LinePath(line), junction);
            }
        }

        /// <summary>The labeled row of an equation that is one, or null.</summary>
        private MathNode? NumberedRow()
        {
            if (RootRow.Children.Count != 1 || RootRow.Children[0] is not { Name: "mtable" } table || table.Children.Count != 1)
                return null;
            var row = table.Children[0];
            return row.Name == "mlabeledtr" && row.Children.Count == 2 && row.Children.All(c => c.Children.Count == 1 && c.Children[0].IsRow) ? row : null;
        }

        private static IEnumerable<string> Text(MathNode node) =>
            node.IsToken || node.IsRun ? new[] { node.Text } : node.Children.SelectMany(Text);

        private int[]? FirstEmptySlot(int[] item)
        {
            foreach (var slot in MathSchema.Slots(MathTree.Get(Root, item)))
            {
                var path = MathTree.Append(item, slot);
                var row = MathTree.Get(Root, path);
                if (row.Children.Count == 0)
                    return path;
                for (var i = 0; i < row.Children.Count; i++)
                {
                    if (FirstEmptySlot(MathTree.Append(path, i)) is { } inner)
                        return inner;
                }
            }
            return null;
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

        /// <summary>
        /// Corrects and builds up what the character just typed ends: the autocorrect first, then the build up of a
        /// trigger, as one edit with the autocorrect, so that an undo gives back what was typed. A trigger that starts a
        /// sequence of the autocorrect (the <c>+</c> of <c>+-</c>) waits for the next character, which builds up what the
        /// trigger would have (<paramref name="resumes"/>) when it does not continue the sequence. Returns whether the
        /// equation changed.
        /// </summary>
        private bool TypedAtCaret(Rune rune, bool resumes)
        {
            var corrected = TryAutoCorrect(rune, out var consumesSpace);
            var trigger = BuildUp.IsTrigger(rune, InputFormat);
            if (!trigger && !resumes)
                return corrected;

            var space = rune.Value == ' ';
            // a character that is no trigger waits with the trigger only if they start a sequence together
            if (!space && AutoCorrect != null && MathTree.Get(Root, Caret.Path) is { IsRun: true } run
                && AutoCorrect.Pending(CodePointText.Substring(run.Text, 0, Caret.Offset), InputFormat) > (trigger ? 0 : 1))
            {
                deferredAt = Caret;
                return corrected;
            }

            var built = trigger ? TryBuildUpAtCaret(space, recordUndo: !corrected) : TryBuildUpAtCaret(false, held: 1, recordUndo: !corrected);
            if (!built && consumesSpace)
                RemoveSpaceBeforeCaret();
            return corrected || built;
        }

        /// <summary>
        /// Replaces the sequence of the autocorrect that ends before the character just typed, in the run of the caret,
        /// and the one the character ends when nothing can continue it (<c>!=</c> as soon as the <c>=</c> is typed), as
        /// an edit of its own: an undo gives back what was typed. <paramref name="consumesSpace"/>: the character is a
        /// space that ended a command or a shortcut, which is not kept unless it builds up.
        /// </summary>
        private bool TryAutoCorrect(Rune typed, out bool consumesSpace)
        {
            consumesSpace = false;
            var caret = Caret;
            if (AutoCorrect == null || MathTree.Get(Root, caret.Path) is not { IsRun: true } run)
                return false;

            var state = Snapshot();
            var replaced = false;
            var before = CodePointText.Substring(run.Text, 0, caret.Offset - 1);
            var entry = AutoCorrect.Match(before, typed.ToString(), InputFormat);
            if (entry == null && typed.Value == ' ')
                entry = AutoCorrect.MatchCommand(before, InputFormat);
            if (entry is { } e)
            {
                ApplyAutoCorrect(caret, run, caret.Offset - 1, e, 1);
                consumesSpace = typed.Value == ' ';
                replaced = true;
            }

            caret = Caret;
            run = MathTree.Get(Root, caret.Path);
            if (AutoCorrect.Match(CodePointText.Substring(run.Text, 0, caret.Offset), string.Empty, InputFormat) is { } complete)
            {
                ApplyAutoCorrect(caret, run, caret.Offset, complete, 0);
                replaced = true;
            }
            if (replaced)
            {
                undo.Push(state);
                redo.Clear();
            }
            return replaced;
        }

        /// <summary>Replaces the sequence that ends at the caret, when nothing more is typed (Enter, the end of the input).</summary>
        private bool FinishAutoCorrect()
        {
            var caret = Canonical(Caret);
            if (AutoCorrect == null || MathTree.Get(Root, caret.Path) is not { IsRun: true } run)
                return false;
            if (AutoCorrect.Match(CodePointText.Substring(run.Text, 0, caret.Offset), string.Empty, InputFormat, final: true) is not { } entry)
                return false;
            ApplyAutoCorrect(caret, run, caret.Offset, entry, 0);
            return true;
        }

        /// <summary>Replaces the sequence of an entry that ends at an offset of the run, with the caret after the replacement and <paramref name="after"/> code points.</summary>
        private void ApplyAutoCorrect(MathPosition caret, MathNode run, int end, KeyValuePair<string, string> entry, int after)
        {
            var start = end - CodePointText.Length(entry.Key);
            var text = CodePointText.Insert(CodePointText.Remove(run.Text, start, end - start), start, entry.Value);
            Root = MathTree.Replace(Root, caret.Path, run.WithText(text));
            Caret = Anchor = new MathPosition(caret.Path, start + CodePointText.Length(entry.Value) + after);
        }

        private void RemoveSpaceBeforeCaret()
        {
            var caret = Caret;
            if (MathTree.Get(Root, caret.Path) is not { IsRun: true } run || caret.Offset == 0
                || CodePointText.Substring(run.Text, caret.Offset - 1, 1) != " ")
                return;
            ReplaceRun(MathTree.Parent(caret.Path), caret.Path[caret.Path.Count - 1], CodePointText.Remove(run.Text, caret.Offset - 1, 1), caret.Offset - 1);
        }

        /// <summary>
        /// Builds up the argument before the caret, in the run of the caret, after a trigger was typed; with
        /// <paramref name="held"/>, the trigger was typed before the last code points, which stay in a run with the caret.
        /// </summary>
        private bool TryBuildUpAtCaret(bool space, int held = 0, bool recordUndo = true)
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
            var argument = space ? before.Take(before.Count - 1).ToList() : before.Take(before.Count - held).ToList();
            if (held > 0)
            {
                after = CodePointText.Join(before.Skip(argument.Count)) + after;
                if (argument.All(r => r.Value == ' '))
                    return false;
            }

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
            if (!BuildUp.IsBalanced(text, InputFormat) || InputFormat == EquationFormat.LaTeX && BuildUp.AwaitsArguments(text))
                return false;

            var start = BuildUp.OperandStart(row.Children, j);
            var nodes = BuildUp.Parse(row.Children.Skip(start).Take(j - start).ToList(), text, InputFormat, IsDisplay);
            if (nodes == null || space && BuildUp.EndsWithPendingCommand(text, nodes))
                return false;

            if (recordUndo)
            {
                undo.Push(Snapshot());
                redo.Clear();
            }
            var children = row.Children.Take(start).Concat(nodes).ToList();
            if (after.Length > 0)
                children.Add(MathNode.Run(after));
            children.AddRange(row.Children.Skip(j + 1));
            Root = MathTree.Replace(Root, rowPath, row.WithChildren(children));
            Caret = Anchor = held > 0
                ? new MathPosition(MathTree.Append(rowPath, start + nodes.Count), held)
                : Canonical(new MathPosition(rowPath, start + nodes.Count));
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

            // at the start (after its maligngroup) or the end of a line of an equation array: the lines merge
            if (ArrayTable() is { } array && rowPath.Length == 5 && rowPath.Take(2).SequenceEqual(ArrayPath))
            {
                var line = rowPath[2];
                if (direction < 0 && index <= 1 && line > 0)
                {
                    PushUndo();
                    MergeLines(line - 1);
                    OnChanged(true);
                    return;
                }
                if (direction > 0 && index == row.Children.Count && line < array.Children.Count - 1)
                {
                    PushUndo();
                    MergeLines(line);
                    OnChanged(true);
                    return;
                }
                if (direction < 0 && index <= 1)
                    return;
            }

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
                    typingAt = deferredAt = null;
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
                typingAt = deferredAt = null;
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
                {
                    // no slot above or below: the line above or below, in an equation broken into lines
                    if (geometry?.GetPositionOnAdjacentLine(caret, direction) is { } onLine && IsValid(onLine))
                        Move(Canonical(onLine), extend);
                    return;
                }
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
            typingAt = deferredAt = null;
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
            // never before the maligngroup that starts a line of an equation array
            if (i == 0 && node.Children.Count > 0 && node.Children[0].Name == "maligngroup")
                i = 1;
            if (i > 0 && node.Children[i - 1].IsRun)
                return new MathPosition(MathTree.Append(position.Path, i - 1), CodePointText.Length(node.Children[i - 1].Text));
            if (i < node.Children.Count && node.Children[i].IsRun)
                return new MathPosition(MathTree.Append(position.Path, i), 0);
            return i == position.Offset ? position : new MathPosition(position.Path, i);
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
            typingAt = deferredAt = null;
        }

        private void Restore(State state)
        {
            Root = state.Root;
            Caret = state.Caret;
            Anchor = state.Anchor;
            IsLinear = state.IsLinear;
            typingAt = deferredAt = null;
            OnChanged(true);
        }

        private void OnChanged(bool content)
        {
            if (dismissedPrefix != null && CommandPrefix != dismissedPrefix)
                dismissedPrefix = null;
            if (content)
                ContentChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>A stack of states that forgets the oldest beyond its limit.</summary>
        private sealed class History
        {
            private readonly LinkedList<State> states = new LinkedList<State>();

            public int Limit { get; set; } = 1000;

            public int Count => states.Count;

            public void Push(State state)
            {
                states.AddLast(state);
                while (states.Count > Limit)
                    states.RemoveFirst();
            }

            public State Pop()
            {
                var state = states.Last!.Value;
                states.RemoveLast();
                return state;
            }

            public void Clear() => states.Clear();
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
