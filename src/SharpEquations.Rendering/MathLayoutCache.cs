namespace SharpEquations.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using SharpEquations.Editing;
    using SharpEquations.Rendering.Layout;

    /// <summary>
    /// What the layouts of an editor or a view keep from one to the next: the boxes of the nodes laid out, by node and
    /// by the style they were laid out in, so that after an edit only the nodes it replaced are laid out again (the
    /// <see cref="MathNode"/>s are immutable, and an edit shares those it does not change), and the metrics of the
    /// glyphs, asked of the font once. A change of font or of the options other than the size and the display style
    /// empties it. It is for one control at a time: it is not safe to use from several threads at once.
    /// </summary>
    public sealed class MathLayoutCache
    {
        private readonly Dictionary<LayoutKey, LayoutEntry> entries = new Dictionary<LayoutKey, LayoutEntry>();
        private IMathFont? font;
        private double? lineWidth;
        private bool breakLines;
        private bool showPlaceholders;
        private int generation;
        private int live;

        /// <summary>The number of boxes kept.</summary>
        public int Count => entries.Count;

        internal GlyphMetrics? Metrics { get; private set; }

        /// <summary>Forgets all that is kept.</summary>
        public void Clear()
        {
            entries.Clear();
            Metrics = null;
            font = null;
            live = 0;
        }

        /// <summary>Starts a layout: empties the cache if the font or the options it depends on changed.</summary>
        internal GlyphMetrics Begin(IMathFont layoutFont, MathLayoutOptions options)
        {
            if (!ReferenceEquals(font, layoutFont) || lineWidth != options.LineWidth || breakLines != options.BreakLines || showPlaceholders != options.ShowPlaceholders)
            {
                Clear();
                font = layoutFont;
                lineWidth = options.LineWidth;
                breakLines = options.BreakLines;
                showPlaceholders = options.ShowPlaceholders;
            }
            return Metrics ??= new GlyphMetrics(layoutFont);
        }

        internal bool TryGet(in LayoutKey key, out LayoutEntry entry) => entries.TryGetValue(key, out entry!);

        internal void Add(LayoutEntry entry) => entries[entry.Key] = entry;

        /// <summary>
        /// Ends a layout: when the boxes kept have grown well beyond those of the last equation, keeps only those its
        /// root reaches.
        /// </summary>
        internal void End(LayoutEntry root)
        {
            if (entries.Count <= 2 * live + 256)
                return;
            generation++;
            var stack = new Stack<LayoutEntry>();
            root.Mark = generation;
            stack.Push(root);
            live = 0;
            while (stack.Count > 0)
            {
                var entry = stack.Pop();
                live++;
                if (entry.Dependencies == null)
                    continue;
                foreach (var dependency in entry.Dependencies)
                {
                    if (dependency.Mark != generation)
                    {
                        dependency.Mark = generation;
                        stack.Push(dependency);
                    }
                }
            }
            var dead = new List<LayoutKey>();
            foreach (var pair in entries)
            {
                if (pair.Value.Mark != generation)
                    dead.Add(pair.Key);
            }
            foreach (var key in dead)
                entries.Remove(key);
        }
    }

    /// <summary>
    /// A node laid out in a style: the node by reference, whether its rows are positions of the caret, and whether it is
    /// the row of the equation, which breaks into lines.
    /// </summary>
    internal readonly struct LayoutKey : IEquatable<LayoutKey>
    {
        public LayoutKey(MathNode node, Style style, Stretch stretch, bool hasPath, bool isLineRow)
        {
            Node = node;
            Style = style;
            Stretch = stretch;
            HasPath = hasPath;
            IsLineRow = isLineRow;
        }

        public MathNode Node { get; }

        public Style Style { get; }

        public Stretch Stretch { get; }

        public bool HasPath { get; }

        public bool IsLineRow { get; }

        public bool Equals(LayoutKey other) =>
            ReferenceEquals(Node, other.Node)
            && Style.Size.Equals(other.Style.Size) && Style.Level == other.Style.Level && Style.Display == other.Style.Display
            && Style.Cramped == other.Style.Cramped && string.Equals(Style.Color, other.Style.Color, StringComparison.Ordinal)
            && Stretch.Height.Equals(other.Stretch.Height) && Stretch.Depth.Equals(other.Stretch.Depth) && Stretch.Width.Equals(other.Stretch.Width)
            && Stretch.Accent == other.Stretch.Accent && Stretch.Under == other.Stretch.Under
            && HasPath == other.HasPath && IsLineRow == other.IsLineRow;

        public override bool Equals(object? obj) => obj is LayoutKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RuntimeHelpers.GetHashCode(Node);
                hash = hash * 31 + Style.Size.GetHashCode();
                hash = hash * 31 + Style.Level;
                hash = hash * 31 + (Style.Display ? 1 : 0) + (Style.Cramped ? 2 : 0) + (HasPath ? 4 : 0) + (IsLineRow ? 8 : 0) + (Stretch.Accent ? 16 : 0) + (Stretch.Under ? 32 : 0);
                hash = hash * 31 + Stretch.Height.GetHashCode();
                hash = hash * 31 + Stretch.Depth.GetHashCode();
                hash = hash * 31 + Stretch.Width.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>A box kept, with the boxes it was made of, which live as long as it does.</summary>
    internal sealed class LayoutEntry
    {
        public LayoutEntry(LayoutKey key) => Key = key;

        public LayoutKey Key { get; }

        public Box Box { get; set; } = null!;

        public List<LayoutEntry>? Dependencies { get; private set; }

        public int Mark { get; set; }

        public void Depend(LayoutEntry entry) => (Dependencies ??= new List<LayoutEntry>()).Add(entry);
    }
}
