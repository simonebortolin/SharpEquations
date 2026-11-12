namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum ElementKind
    {
        Opaque,
        Token,
        Leaf,
        Row,
        InferredRow,
        Fixed,
        Table,
    }

    /// <summary>What the editor knows of each element of MathML: its kind, its slots and how the caret moves between them.</summary>
    internal static class MathSchema
    {
        private static readonly int[] RootOrder = { 1, 0 };

        public static bool IsToken(string name) => name is "mi" or "mn" or "mo" or "mtext" or "ms";

        public static ElementKind KindOf(string name) => name switch
        {
            "mi" or "mn" or "mo" or "mtext" or "ms" => ElementKind.Token,
            "mspace" or "none" or "mprescripts" or "malignmark" or "maligngroup" or "mglyph" => ElementKind.Leaf,
            "mrow" => ElementKind.Row,
            "math" or "msqrt" or "mstyle" or "merror" or "mpadded" or "mphantom" or "menclose" or "mtd" => ElementKind.InferredRow,
            "mfrac" or "mroot" or "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover" or "mmultiscripts" => ElementKind.Fixed,
            "mtable" or "mtr" or "mlabeledtr" => ElementKind.Table,
            _ => ElementKind.Opaque,
        };

        /// <summary>
        /// The slots of a node that is an item of a row, as paths relative to the node, in the order the caret goes
        /// through them from left to right. A row is its own slot (the empty path).
        /// </summary>
        public static IReadOnlyList<int[]> Slots(MathNode node)
        {
            if (node.IsRun || node.IsOpaque)
                return Array.Empty<int[]>();

            switch (KindOf(node.Name))
            {
                case ElementKind.Row:
                    return new[] { Array.Empty<int>() };
                case ElementKind.InferredRow:
                    return node.Children.Count == 1 && node.Children[0].IsRow ? new[] { new[] { 0 } } : Array.Empty<int[]>();
                case ElementKind.Fixed:
                    return SlotOrder(node).Where(i => i < node.Children.Count && node.Children[i].IsRow).Select(i => new[] { i }).ToArray();
                case ElementKind.Table when node.Name == "mtable":
                    var slots = new List<int[]>();
                    for (var r = 0; r < node.Children.Count; r++)
                    {
                        var row = node.Children[r];
                        for (var c = 0; c < row.Children.Count; c++)
                        {
                            if (row.Children[c].Name == "mtd" && row.Children[c].Children.Count == 1 && row.Children[c].Children[0].IsRow)
                                slots.Add(new[] { r, c, 0 });
                        }
                    }
                    return slots;
                default:
                    return Array.Empty<int[]>();
            }
        }

        /// <summary>The children of a structure in the order they are read: the index of a root before its base, prescripts before the base.</summary>
        private static IEnumerable<int> SlotOrder(MathNode node)
        {
            switch (node.Name)
            {
                case "mroot":
                    return RootOrder;
                case "mmultiscripts":
                    var pre = -1;
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        if (node.Children[i].Name == "mprescripts")
                            pre = i;
                    }
                    if (pre < 0)
                        return Enumerable.Range(0, node.Children.Count);
                    return Enumerable.Range(pre + 1, node.Children.Count - pre - 1)
                        .Concat(Enumerable.Repeat(0, 1))
                        .Concat(Enumerable.Range(1, pre - 1));
                default:
                    return Enumerable.Range(0, node.Children.Count);
            }
        }

        /// <summary>
        /// The slot above (direction -1) or below (+1) a slot of a structure, as a path relative to the structure, or
        /// null when the structure has none in that direction.
        /// </summary>
        public static int[]? Vertical(MathNode node, int[] slot, int direction)
        {
            if (slot.Length == 0)
                return null;

            var up = direction < 0;
            var s = slot[0];
            int? target = node.Name switch
            {
                "mfrac" => up ? (s == 1 ? 0 : null) : (s == 0 ? 1 : null),
                "msub" or "munder" => up ? (s == 1 ? 0 : null) : (s == 0 ? 1 : null),
                "msup" or "mover" => up ? (s == 0 ? 1 : null) : (s == 1 ? 0 : null),
                "msubsup" => up ? (s is 0 or 1 ? 2 : null) : (s is 0 or 2 ? 1 : null),
                "munderover" => up ? (s switch { 0 => 2, 1 => 0, _ => null }) : (s switch { 0 => 1, 2 => 0, _ => null }),
                "mroot" => up ? (s == 0 ? 1 : null) : (s == 1 ? 0 : null),
                _ => null,
            };
            if (target is { } t)
                return new[] { t };

            if (node.Name == "mtable" && slot.Length == 3)
            {
                var row = slot[0] + direction;
                if (row < 0 || row >= node.Children.Count)
                    return null;
                var cells = node.Children[row].Children.Count;
                if (cells == 0)
                    return null;
                return new[] { row, Math.Min(slot[1], cells - 1), 0 };
            }
            return null;
        }
    }
}
