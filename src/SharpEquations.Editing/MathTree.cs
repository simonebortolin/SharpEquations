namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Paths in a tree of <see cref="MathNode"/>s.</summary>
    internal static class MathTree
    {
        public static MathNode Get(MathNode root, IReadOnlyList<int> path)
        {
            var node = root;
            foreach (var index in path)
                node = node.Children[index];
            return node;
        }

        public static bool TryGet(MathNode root, IReadOnlyList<int> path, out MathNode node)
        {
            node = root;
            foreach (var index in path)
            {
                if (index < 0 || index >= node.Children.Count)
                    return false;
                node = node.Children[index];
            }
            return true;
        }

        public static MathNode Replace(MathNode root, IReadOnlyList<int> path, MathNode node) => Replace(root, path, 0, node);

        private static MathNode Replace(MathNode current, IReadOnlyList<int> path, int depth, MathNode node) =>
            depth == path.Count ? node : current.WithChild(path[depth], Replace(current.Children[path[depth]], path, depth + 1, node));

        public static int[] Append(IReadOnlyList<int> path, params int[] more) => path.Concat(more).ToArray();

        public static int[] Parent(IReadOnlyList<int> path) => path.Take(path.Count - 1).ToArray();

        public static bool StartsWith(IReadOnlyList<int> path, IReadOnlyList<int> prefix) =>
            path.Count >= prefix.Count && prefix.Select((index, i) => path[i] == index).All(b => b);

        /// <summary>The path of the row of the <c>math</c> element.</summary>
        public static readonly int[] RootRow = { 0 };

        /// <summary>
        /// The item of a row that a row is a slot of: its path (the row of the item is its parent), the paths of the
        /// slots of the item, and the index of the row among them; null for the row of the <c>math</c> element.
        /// </summary>
        public static (int[] Item, int[][] Slots, int Index)? Owner(MathNode root, IReadOnlyList<int> rowPath)
        {
            if (rowPath.Count == 0)
                return null;

            var parentPath = Parent(rowPath);
            if (Get(root, parentPath).IsRow)
            {
                var self = rowPath.ToArray();
                return (self, new[] { self }, 0);
            }

            var item = parentPath;
            while (item.Length > 0 && !Get(root, Parent(item)).IsRow)
                item = Parent(item);
            if (item.Length == 0)
                return null;

            var slots = MathSchema.Slots(Get(root, item)).Select(s => Append(item, s)).ToArray();
            var index = Array.FindIndex(slots, s => s.SequenceEqual(rowPath));
            return index < 0 ? null : (item, slots, index);
        }
    }
}
