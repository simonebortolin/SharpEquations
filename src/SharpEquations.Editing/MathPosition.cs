namespace SharpEquations.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A position of the caret: the path of a row (offset between two of its children) or of a run (offset between two
    /// code points of its text), as the indexes of the children from the <c>math</c> element down.
    /// </summary>
    public sealed class MathPosition : IEquatable<MathPosition>
    {
        private readonly int[] path;

        /// <summary>A position in the node at the path.</summary>
        public MathPosition(IEnumerable<int> path, int offset)
        {
            this.path = (path ?? throw new ArgumentNullException(nameof(path))).ToArray();
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            Offset = offset;
        }

        /// <summary>The path of the row or the run, from the <c>math</c> element.</summary>
        public IReadOnlyList<int> Path => path;

        /// <summary>The index of the child after the caret in a row, or the code point after the caret in a run.</summary>
        public int Offset { get; }

        /// <inheritdoc/>
        public bool Equals(MathPosition? other) => other is not null && Offset == other.Offset && path.SequenceEqual(other.path);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as MathPosition);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = Offset;
            foreach (var index in path)
                hash = hash * 31 + index;
            return hash;
        }

        /// <inheritdoc/>
        public override string ToString() => "/" + string.Join("/", path) + ":" + Offset;

        /// <summary>Equality of the positions.</summary>
        public static bool operator ==(MathPosition? left, MathPosition? right) => left is null ? right is null : left.Equals(right);

        /// <summary>Inequality of the positions.</summary>
        public static bool operator !=(MathPosition? left, MathPosition? right) => !(left == right);
    }

    /// <summary>
    /// A selection: a range of the children of a row, or of the code points of a run. A selection that leaves a row
    /// covers the whole structure the row belongs to.
    /// </summary>
    public sealed class MathSelection
    {
        internal MathSelection(IReadOnlyList<int> path, int start, int end, bool inRun)
        {
            Path = path;
            Start = start;
            End = end;
            IsInRun = inRun;
        }

        /// <summary>The path of the row or the run.</summary>
        public IReadOnlyList<int> Path { get; }

        /// <summary>The first child or code point selected.</summary>
        public int Start { get; }

        /// <summary>The child or code point after the last selected.</summary>
        public int End { get; }

        /// <summary>A range of the text of a run rather than of the children of a row.</summary>
        public bool IsInRun { get; }

        /// <summary>The position before the selection.</summary>
        public MathPosition StartPosition => new MathPosition(Path, Start);

        /// <summary>The position after the selection.</summary>
        public MathPosition EndPosition => new MathPosition(Path, End);
    }

    /// <summary>
    /// The geometry of a laid out equation, which the editor asks to move the caret up and down to the nearest
    /// position; the renderer implements it.
    /// </summary>
    public interface IMathGeometry
    {
        /// <summary>The horizontal coordinate of the caret at the position, or null if the position is not laid out.</summary>
        double? GetCaretX(MathPosition position);

        /// <summary>The position in the row (or in a run of it) nearest to the horizontal coordinate.</summary>
        MathPosition? GetNearestPosition(IReadOnlyList<int> rowPath, double x);

        /// <summary>
        /// The position on the line above (direction -1) or below (+1) in the row broken into lines that holds the
        /// position, nearest to its horizontal coordinate; null when that row is on one line or has no such line.
        /// </summary>
        MathPosition? GetPositionOnAdjacentLine(MathPosition position, int direction);
    }
}
