namespace SharpEquations.Rendering.OpenType
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The outlines of the glyphs of a font and their ink boxes: the contours of the <c>glyf</c> table of a TrueType
    /// font, or the outlines of the charstrings of the <c>CFF</c> table of an OpenType font, so that the layout depends
    /// on no rasterizer, and a toolkit can draw a glyph as a path (<see cref="Outline"/>).
    /// </summary>
    public sealed class GlyphOutlines
    {
        private readonly Func<ushort, GlyphBounds?> reader;
        private readonly Func<ushort, List<MathPathCommand>, bool> outliner;
        private readonly Dictionary<ushort, GlyphBounds> cache = new Dictionary<ushort, GlyphBounds>();
        private readonly Dictionary<ushort, IReadOnlyList<MathPathCommand>> outlineCache = new Dictionary<ushort, IReadOnlyList<MathPathCommand>>();

        private GlyphOutlines(Func<ushort, GlyphBounds?> reader, Func<ushort, List<MathPathCommand>, bool> outliner)
        {
            this.reader = reader;
            this.outliner = outliner;
        }

        /// <summary>
        /// The outlines of a font from its tables, given by their tag (<c>head</c>, <c>loca</c>, <c>glyf</c>,
        /// <c>CFF </c>); null if the font has neither TrueType nor CFF outlines.
        /// </summary>
        public static GlyphOutlines? Create(Func<string, byte[]?> table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));
            try
            {
                if (table("CFF ") is { Length: > 0 } cff)
                {
                    var charStrings = new Cff(cff);
                    return new GlyphOutlines(charStrings.Bounds, charStrings.Trace);
                }
                if (table("glyf") is { Length: > 0 } glyf && table("loca") is { } loca && table("head") is { Length: >= 54 } head)
                {
                    var longOffsets = (head[50] << 8 | head[51]) != 0;
                    return new GlyphOutlines(glyph => TrueType(glyf, loca, longOffsets, glyph),
                        (glyph, path) => TrueTypeOutline(glyf, loca, longOffsets, glyph, path, 0, 1, 0, 0, 1, 0, 0));
                }
            }
            catch (IndexOutOfRangeException)
            {
            }
            return null;
        }

        /// <summary>The ink box of a glyph in design units; empty for a glyph without outline (a space).</summary>
        public GlyphBounds Bounds(ushort glyph)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(glyph, out var bounds))
                {
                    try
                    {
                        bounds = reader(glyph) ?? default;
                    }
                    catch (Exception e) when (e is IndexOutOfRangeException or InvalidOperationException or ArgumentOutOfRangeException)
                    {
                        bounds = default;
                    }
                    cache[glyph] = bounds;
                }
                return bounds;
            }
        }

        /// <summary>
        /// The outline of a glyph in design units, y up: contours of lines and quadratic (TrueType) or cubic (CFF)
        /// curves, each closed; empty for a glyph without outline (a space).
        /// </summary>
        public IReadOnlyList<MathPathCommand> Outline(ushort glyph)
        {
            lock (outlineCache)
            {
                if (!outlineCache.TryGetValue(glyph, out var outline))
                {
                    var path = new List<MathPathCommand>();
                    try
                    {
                        if (!outliner(glyph, path))
                            path.Clear();
                    }
                    catch (Exception e) when (e is IndexOutOfRangeException or InvalidOperationException or ArgumentOutOfRangeException)
                    {
                        path.Clear();
                    }
                    outlineCache[glyph] = outline = path;
                }
                return outline;
            }
        }

        private static GlyphBounds? TrueType(byte[] glyf, byte[] loca, bool longOffsets, ushort glyph)
        {
            if (Range(loca, longOffsets, glyph) is not var (start, end) || end <= start)
                return null;
            return new GlyphBounds(S16(glyf, start + 2), S16(glyf, start + 4), S16(glyf, start + 6), S16(glyf, start + 8));
        }

        private static (int Start, int End)? Range(byte[] loca, bool longOffsets, ushort glyph)
        {
            if (longOffsets)
                return glyph * 4 + 8 <= loca.Length ? ((int)U32(loca, glyph * 4), (int)U32(loca, glyph * 4 + 4)) : null;
            return glyph * 2 + 4 <= loca.Length ? (U16(loca, glyph * 2) * 2, U16(loca, glyph * 2 + 2) * 2) : null;
        }

        /// <summary>
        /// Adds the contours of a TrueType glyph to a path, through the transform of a composite glyph that uses it
        /// (x' = a x + c y + dx, y' = b x + d y + dy).
        /// </summary>
        private static bool TrueTypeOutline(byte[] glyf, byte[] loca, bool longOffsets, ushort glyph, List<MathPathCommand> path, int depth,
            double a, double b, double c, double d, double dx, double dy)
        {
            if (depth > 8 || Range(loca, longOffsets, glyph) is not var (start, end) || end <= start)
                return true;
            var contours = S16(glyf, start);
            if (contours < 0)
                return TrueTypeComposite(glyf, loca, longOffsets, start + 10, path, depth, a, b, c, d, dx, dy);

            var endPoints = new int[contours];
            for (var i = 0; i < contours; i++)
                endPoints[i] = U16(glyf, start + 10 + i * 2);
            var points = contours == 0 ? 0 : endPoints[contours - 1] + 1;
            var at = start + 10 + contours * 2;
            at += 2 + U16(glyf, at);
            var flags = new byte[points];
            for (var i = 0; i < points;)
            {
                var flag = glyf[at++];
                flags[i++] = flag;
                if ((flag & 8) != 0)
                {
                    for (var repeat = glyf[at++]; repeat > 0 && i < points; repeat--)
                        flags[i++] = flag;
                }
            }
            var xs = new int[points];
            var ys = new int[points];
            at = Coordinates(glyf, at, flags, xs, 2, 16);
            Coordinates(glyf, at, flags, ys, 4, 32);

            var first = 0;
            foreach (var last in endPoints)
            {
                Contour(path, flags, xs, ys, first, last, (x, y) => (a * x + c * y + dx, b * x + d * y + dy));
                first = last + 1;
            }
            return true;
        }

        private static int Coordinates(byte[] glyf, int at, byte[] flags, int[] values, int shortBit, int sameBit)
        {
            var value = 0;
            for (var i = 0; i < flags.Length; i++)
            {
                var flag = flags[i];
                if ((flag & shortBit) != 0)
                {
                    var delta = glyf[at++];
                    value += (flag & sameBit) != 0 ? delta : -delta;
                }
                else if ((flag & sameBit) == 0)
                {
                    value += S16(glyf, at);
                    at += 2;
                }
                values[i] = value;
            }
            return at;
        }

        /// <summary>
        /// A closed contour of TrueType points: on-curve points joined by lines, off-curve points the controls of
        /// quadratic curves, two in a row implying the on-curve point between them.
        /// </summary>
        private static void Contour(List<MathPathCommand> path, byte[] flags, int[] xs, int[] ys, int first, int last, Func<double, double, (double X, double Y)> map)
        {
            var count = last - first + 1;
            if (count <= 0)
                return;
            bool On(int i) => (flags[first + i % count] & 1) != 0;
            (double X, double Y) P(int i) => (xs[first + i % count], ys[first + i % count]);
            static (double X, double Y) Mid((double X, double Y) p, (double X, double Y) q) => ((p.X + q.X) / 2, (p.Y + q.Y) / 2);
            void Add(MathPathCommandKind kind, (double X, double Y)? control, (double X, double Y) to)
            {
                var c = control is { } k ? map(k.X, k.Y) : default;
                var t = map(to.X, to.Y);
                path.Add(new MathPathCommand(kind, c.X, c.Y, 0, 0, t.X, t.Y));
            }

            var startIndex = -1;
            for (var i = 0; i < count && startIndex < 0; i++)
            {
                if (On(i))
                    startIndex = i;
            }
            var start = startIndex >= 0 ? P(startIndex) : Mid(P(0), P(1));
            Add(MathPathCommandKind.MoveTo, null, start);
            (double X, double Y)? pending = null;
            var from = startIndex >= 0 ? startIndex + 1 : 1;
            for (var k = 0; k < count; k++)
            {
                var i = from + k;
                var p = P(i);
                if (On(i))
                {
                    Add(pending == null ? MathPathCommandKind.LineTo : MathPathCommandKind.QuadraticTo, pending, p);
                    pending = null;
                }
                else
                {
                    if (pending is { } q)
                        Add(MathPathCommandKind.QuadraticTo, q, Mid(q, p));
                    pending = p;
                }
            }
            if (pending is { } control)
                Add(MathPathCommandKind.QuadraticTo, control, start);
            path.Add(new MathPathCommand(MathPathCommandKind.Close, 0, 0, 0, 0, 0, 0));
        }

        private static bool TrueTypeComposite(byte[] glyf, byte[] loca, bool longOffsets, int at, List<MathPathCommand> path, int depth,
            double a, double b, double c, double d, double dx, double dy)
        {
            const int ArgsAreWords = 1, ArgsAreXY = 2, HaveScale = 8, MoreComponents = 32, HaveXYScale = 64, HaveTwoByTwo = 128;
            int flags;
            do
            {
                flags = U16(glyf, at);
                var component = (ushort)U16(glyf, at + 2);
                at += 4;
                double e, f;
                if ((flags & ArgsAreWords) != 0)
                {
                    e = S16(glyf, at);
                    f = S16(glyf, at + 2);
                    at += 4;
                }
                else
                {
                    e = (sbyte)glyf[at];
                    f = (sbyte)glyf[at + 1];
                    at += 2;
                }
                if ((flags & ArgsAreXY) == 0)
                    e = f = 0;
                double ca = 1, cb = 0, cc = 0, cd = 1;
                if ((flags & HaveScale) != 0)
                {
                    ca = cd = F2Dot14(glyf, at);
                    at += 2;
                }
                else if ((flags & HaveXYScale) != 0)
                {
                    ca = F2Dot14(glyf, at);
                    cd = F2Dot14(glyf, at + 2);
                    at += 4;
                }
                else if ((flags & HaveTwoByTwo) != 0)
                {
                    ca = F2Dot14(glyf, at);
                    cb = F2Dot14(glyf, at + 2);
                    cc = F2Dot14(glyf, at + 4);
                    cd = F2Dot14(glyf, at + 6);
                    at += 8;
                }
                TrueTypeOutline(glyf, loca, longOffsets, component, path, depth + 1,
                    a * ca + c * cb, b * ca + d * cb, a * cc + c * cd, b * cc + d * cd, a * e + c * f + dx, b * e + d * f + dy);
            }
            while ((flags & MoreComponents) != 0);
            return true;
        }

        private static double F2Dot14(byte[] d, int at) => S16(d, at) / 16384.0;

        private static int U16(byte[] d, int at) => d[at] << 8 | d[at + 1];

        private static short S16(byte[] d, int at) => (short)(d[at] << 8 | d[at + 1]);

        private static uint U32(byte[] d, int at) => (uint)(d[at] << 24 | d[at + 1] << 16 | d[at + 2] << 8 | d[at + 3]);

        /// <summary>A CFF table: its charstrings and subroutines, and an interpreter of Type 2 charstrings that follows the outline.</summary>
        private sealed class Cff
        {
            private readonly byte[] data;
            private readonly List<(int Start, int End)> charStrings;
            private readonly List<(int Start, int End)> globalSubrs;
            private readonly List<List<(int Start, int End)>> localSubrs = new List<List<(int, int)>>();
            private readonly byte[]? fdSelect;
            private readonly int fdSelectAt;
            private readonly int glyphCount;

            public Cff(byte[] data)
            {
                this.data = data;
                int at = data[2];
                var names = Index(ref at);
                var topDicts = Index(ref at);
                Index(ref at);
                globalSubrs = Index(ref at);
                if (names.Count == 0 || topDicts.Count == 0)
                    throw new InvalidOperationException("No font in the CFF table.");

                var top = Dict(topDicts[0].Start, topDicts[0].End);
                if (!top.TryGetValue(17, out var cs))
                    throw new InvalidOperationException("No charstrings.");
                var csAt = (int)cs[0];
                charStrings = Index(ref csAt);
                glyphCount = charStrings.Count;

                if (top.TryGetValue(1236, out var fdArray) && top.TryGetValue(1237, out var select))
                {
                    var fdAt = (int)fdArray[0];
                    foreach (var (start, end) in Index(ref fdAt))
                        localSubrs.Add(PrivateSubrs(Dict(start, end)));
                    fdSelect = data;
                    fdSelectAt = (int)select[0];
                }
                else
                {
                    localSubrs.Add(PrivateSubrs(top));
                }
            }

            private List<(int, int)> PrivateSubrs(Dictionary<int, double[]> dict)
            {
                if (!dict.TryGetValue(18, out var priv) || priv.Length < 2)
                    return new List<(int, int)>();
                var size = (int)priv[0];
                var offset = (int)priv[1];
                var privateDict = Dict(offset, offset + size);
                if (!privateDict.TryGetValue(19, out var subrs))
                    return new List<(int, int)>();
                var at = offset + (int)subrs[0];
                return Index(ref at);
            }

            private int FontIndex(ushort glyph)
            {
                if (fdSelect == null)
                    return 0;
                var format = data[fdSelectAt];
                if (format == 0)
                    return data[fdSelectAt + 1 + glyph];
                if (format == 3)
                {
                    var ranges = U16(data, fdSelectAt + 1);
                    for (var i = 0; i < ranges; i++)
                    {
                        var r = fdSelectAt + 3 + i * 3;
                        var first = U16(data, r);
                        var next = U16(data, r + 3);
                        if (glyph >= first && glyph < next)
                            return data[r + 2];
                    }
                }
                return 0;
            }

            public GlyphBounds? Bounds(ushort glyph)
            {
                if (glyph >= glyphCount)
                    return null;
                var outline = new Outline(this, Local(glyph), null);
                outline.Run(charStrings[glyph], 0);
                return outline.HasPoints ? new GlyphBounds(outline.XMin, outline.YMin, outline.XMax, outline.YMax) : null;
            }

            public bool Trace(ushort glyph, List<MathPathCommand> path)
            {
                if (glyph >= glyphCount)
                    return false;
                var outline = new Outline(this, Local(glyph), path);
                outline.Run(charStrings[glyph], 0);
                outline.Close();
                return true;
            }

            private List<(int, int)> Local(ushort glyph) =>
                localSubrs.Count == 0 ? new List<(int, int)>() : localSubrs[Math.Min(FontIndex(glyph), localSubrs.Count - 1)];

            private List<(int Start, int End)> Index(ref int at)
            {
                var count = U16(data, at);
                var list = new List<(int, int)>(count);
                if (count == 0)
                {
                    at += 2;
                    return list;
                }
                var offSize = data[at + 2];
                var offsets = at + 3;
                var dataStart = offsets + (count + 1) * offSize - 1;
                int Offset(int i)
                {
                    var value = 0;
                    for (var b = 0; b < offSize; b++)
                        value = value << 8 | data[offsets + i * offSize + b];
                    return value;
                }
                for (var i = 0; i < count; i++)
                    list.Add((dataStart + Offset(i), dataStart + Offset(i + 1)));
                at = dataStart + Offset(count);
                return list;
            }

            private Dictionary<int, double[]> Dict(int start, int end)
            {
                var dict = new Dictionary<int, double[]>();
                var operands = new List<double>();
                var at = start;
                while (at < end)
                {
                    int b0 = data[at];
                    if (b0 <= 21)
                    {
                        var op = b0;
                        at++;
                        if (b0 == 12)
                            op = 1200 + data[at++];
                        dict[op] = operands.ToArray();
                        operands.Clear();
                    }
                    else if (b0 == 28)
                    {
                        operands.Add((short)(data[at + 1] << 8 | data[at + 2]));
                        at += 3;
                    }
                    else if (b0 == 29)
                    {
                        operands.Add((int)U32(data, at + 1));
                        at += 5;
                    }
                    else if (b0 == 30)
                    {
                        // a real number: its nibbles until 0xf; the operators read here are integers
                        at++;
                        while (at < end && (data[at] & 0x0F) != 0x0F && (data[at] & 0xF0) != 0xF0)
                            at++;
                        at++;
                        operands.Add(0);
                    }
                    else if (b0 >= 32 && b0 <= 246)
                    {
                        operands.Add(b0 - 139);
                        at++;
                    }
                    else if (b0 >= 247 && b0 <= 250)
                    {
                        operands.Add((b0 - 247) * 256 + data[at + 1] + 108);
                        at += 2;
                    }
                    else if (b0 >= 251 && b0 <= 254)
                    {
                        operands.Add(-(b0 - 251) * 256 - data[at + 1] - 108);
                        at += 2;
                    }
                    else
                    {
                        at++;
                    }
                }
                return dict;
            }

            private static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

            /// <summary>The state of the interpreter of a charstring: the pen, the stack, the stems, and the box of the outline so far.</summary>
            private sealed class Outline
            {
                private readonly Cff cff;
                private readonly List<(int Start, int End)> local;
                private readonly List<double> stack = new List<double>();
                private readonly List<MathPathCommand>? path;
                private bool open;
                private double x;
                private double y;
                private int stems;
                private bool widthDone;
                private bool done;

                public Outline(Cff cff, List<(int, int)> local, List<MathPathCommand>? path)
                {
                    this.cff = cff;
                    this.local = local;
                    this.path = path;
                }

                /// <summary>Closes the contour being drawn: a moveto and the end of the charstring close it.</summary>
                public void Close()
                {
                    if (open)
                        path!.Add(new MathPathCommand(MathPathCommandKind.Close, 0, 0, 0, 0, 0, 0));
                    open = false;
                }

                private void Start()
                {
                    if (path == null || open)
                        return;
                    path.Add(new MathPathCommand(MathPathCommandKind.MoveTo, 0, 0, 0, 0, x, y));
                    open = true;
                }

                public double XMin { get; private set; } = double.MaxValue;

                public double YMin { get; private set; } = double.MaxValue;

                public double XMax { get; private set; } = double.MinValue;

                public double YMax { get; private set; } = double.MinValue;

                public bool HasPoints => XMin <= XMax;

                public void Run((int Start, int End) charString, int depth)
                {
                    if (depth > 10)
                        return;
                    var d = cff.data;
                    var at = charString.Start;
                    while (at < charString.End && !done)
                    {
                        int b0 = d[at];
                        if (b0 >= 32 || b0 == 28)
                        {
                            at = Number(d, at);
                            continue;
                        }

                        at++;
                        switch (b0)
                        {
                            case 1 or 3 or 18 or 23: // hstem vstem hstemhm vstemhm
                                Width(stack.Count % 2 == 1);
                                stems += stack.Count / 2;
                                stack.Clear();
                                break;
                            case 19 or 20: // hintmask cntrmask
                                Width(stack.Count % 2 == 1);
                                stems += stack.Count / 2;
                                stack.Clear();
                                at += (stems + 7) / 8;
                                break;
                            case 21: // rmoveto
                                Width(stack.Count > 2);
                                MoveTo(x + Arg(-2), y + Arg(-1));
                                break;
                            case 22: // hmoveto
                                Width(stack.Count > 1);
                                MoveTo(x + Arg(-1), y);
                                break;
                            case 4: // vmoveto
                                Width(stack.Count > 1);
                                MoveTo(x, y + Arg(-1));
                                break;
                            case 5: // rlineto
                                for (var i = 0; i + 1 < stack.Count; i += 2)
                                    LineTo(x + stack[i], y + stack[i + 1]);
                                stack.Clear();
                                break;
                            case 6 or 7: // hlineto vlineto
                                var horizontal = b0 == 6;
                                foreach (var v in stack)
                                {
                                    if (horizontal)
                                        LineTo(x + v, y);
                                    else
                                        LineTo(x, y + v);
                                    horizontal = !horizontal;
                                }
                                stack.Clear();
                                break;
                            case 8: // rrcurveto
                                for (var i = 0; i + 5 < stack.Count; i += 6)
                                    CurveBy(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);
                                stack.Clear();
                                break;
                            case 24: // rcurveline
                            {
                                var i = 0;
                                for (; i + 5 < stack.Count - 2; i += 6)
                                    CurveBy(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);
                                if (i + 1 < stack.Count)
                                    LineTo(x + stack[i], y + stack[i + 1]);
                                stack.Clear();
                                break;
                            }
                            case 25: // rlinecurve
                            {
                                var i = 0;
                                for (; i + 1 < stack.Count - 6; i += 2)
                                    LineTo(x + stack[i], y + stack[i + 1]);
                                if (i + 5 < stack.Count)
                                    CurveBy(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);
                                stack.Clear();
                                break;
                            }
                            case 26: // vvcurveto
                            {
                                var i = 0;
                                double dx1 = 0;
                                if (stack.Count % 4 == 1)
                                    dx1 = stack[i++];
                                for (; i + 3 < stack.Count; i += 4)
                                {
                                    CurveBy(dx1, stack[i], stack[i + 1], stack[i + 2], 0, stack[i + 3]);
                                    dx1 = 0;
                                }
                                stack.Clear();
                                break;
                            }
                            case 27: // hhcurveto
                            {
                                var i = 0;
                                double dy1 = 0;
                                if (stack.Count % 4 == 1)
                                    dy1 = stack[i++];
                                for (; i + 3 < stack.Count; i += 4)
                                {
                                    CurveBy(stack[i], dy1, stack[i + 1], stack[i + 2], stack[i + 3], 0);
                                    dy1 = 0;
                                }
                                stack.Clear();
                                break;
                            }
                            case 30 or 31: // vhcurveto hvcurveto
                            {
                                var horizontalFirst = b0 == 31;
                                var i = 0;
                                while (i + 3 < stack.Count)
                                {
                                    var last = i + 4 == stack.Count - 1;
                                    var extra = last ? stack[i + 4] : 0;
                                    if (horizontalFirst)
                                        CurveBy(stack[i], 0, stack[i + 1], stack[i + 2], extra, stack[i + 3]);
                                    else
                                        CurveBy(0, stack[i], stack[i + 1], stack[i + 2], stack[i + 3], extra);
                                    horizontalFirst = !horizontalFirst;
                                    i += last ? 5 : 4;
                                }
                                stack.Clear();
                                break;
                            }
                            case 10 or 29: // callsubr callgsubr
                            {
                                if (stack.Count == 0)
                                    return;
                                var subrs = b0 == 10 ? local : cff.globalSubrs;
                                var index = (int)stack[stack.Count - 1] + Bias(subrs.Count);
                                stack.RemoveAt(stack.Count - 1);
                                if (index >= 0 && index < subrs.Count)
                                    Run(subrs[index], depth + 1);
                                break;
                            }
                            case 11: // return
                                return;
                            case 14: // endchar
                                Width(stack.Count == 1 || stack.Count == 5);
                                done = true;
                                return;
                            case 12:
                                Escape(d[at++]);
                                break;
                            default:
                                stack.Clear();
                                break;
                        }
                    }
                }

                private void Escape(int op)
                {
                    var s = stack;
                    switch (op)
                    {
                        case 35 when s.Count >= 12: // flex
                            CurveBy(s[0], s[1], s[2], s[3], s[4], s[5]);
                            CurveBy(s[6], s[7], s[8], s[9], s[10], s[11]);
                            break;
                        case 34 when s.Count >= 7: // hflex
                        {
                            var y0 = y;
                            CurveBy(s[0], 0, s[1], s[2], s[3], 0);
                            CurveBy(s[4], 0, s[5], y0 - y, s[6], 0);
                            break;
                        }
                        case 36 when s.Count >= 9: // hflex1
                        {
                            var y0 = y;
                            CurveBy(s[0], s[1], s[2], s[3], s[4], 0);
                            CurveBy(s[5], 0, s[6], s[7], s[8], y0 - y - s[7]);
                            break;
                        }
                        case 37 when s.Count >= 11: // flex1
                        {
                            var x0 = x;
                            var y0 = y;
                            var dx = s[0] + s[2] + s[4] + s[6] + s[8];
                            var dy = s[1] + s[3] + s[5] + s[7] + s[9];
                            CurveBy(s[0], s[1], s[2], s[3], s[4], s[5]);
                            if (Math.Abs(dx) > Math.Abs(dy))
                                CurveBy(s[6], s[7], s[8], s[9], s[10], y0 - (y + s[7] + s[9]));
                            else
                                CurveBy(s[6], s[7], s[8], s[9], x0 - (x + s[6] + s[8]), s[10]);
                            break;
                        }
                    }
                    stack.Clear();
                }

                private int Number(byte[] d, int at)
                {
                    int b0 = d[at];
                    if (b0 == 28)
                    {
                        stack.Add((short)(d[at + 1] << 8 | d[at + 2]));
                        return at + 3;
                    }
                    if (b0 <= 246)
                    {
                        stack.Add(b0 - 139);
                        return at + 1;
                    }
                    if (b0 <= 250)
                    {
                        stack.Add((b0 - 247) * 256 + d[at + 1] + 108);
                        return at + 2;
                    }
                    if (b0 <= 254)
                    {
                        stack.Add(-(b0 - 251) * 256 - d[at + 1] - 108);
                        return at + 2;
                    }
                    stack.Add((int)U32(d, at + 1) / 65536.0);
                    return at + 5;
                }

                private double Arg(int fromEnd) => stack.Count + fromEnd >= 0 ? stack[stack.Count + fromEnd] : 0;

                /// <summary>The first operator that clears the stack may have the width of the glyph before its arguments.</summary>
                private void Width(bool hasWidth)
                {
                    if (!widthDone && hasWidth && stack.Count > 0)
                        stack.RemoveAt(0);
                    widthDone = true;
                }

                private void MoveTo(double nx, double ny)
                {
                    if (path != null)
                        Close();
                    x = nx;
                    y = ny;
                    stack.Clear();
                }

                private void LineTo(double nx, double ny)
                {
                    Start();
                    Add(x, y);
                    x = nx;
                    y = ny;
                    Add(x, y);
                    path?.Add(new MathPathCommand(MathPathCommandKind.LineTo, 0, 0, 0, 0, x, y));
                }

                private void CurveBy(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
                {
                    var x0 = x;
                    var y0 = y;
                    var x1 = x0 + dx1;
                    var y1 = y0 + dy1;
                    var x2 = x1 + dx2;
                    var y2 = y1 + dy2;
                    var x3 = x2 + dx3;
                    var y3 = y2 + dy3;
                    Start();
                    path?.Add(new MathPathCommand(MathPathCommandKind.CubicTo, x1, y1, x2, y2, x3, y3));
                    Add(x0, y0);
                    Add(x3, y3);
                    foreach (var t in Extrema(x0, x1, x2, x3))
                        Add(Bezier(x0, x1, x2, x3, t), Bezier(y0, y1, y2, y3, t));
                    foreach (var t in Extrema(y0, y1, y2, y3))
                        Add(Bezier(x0, x1, x2, x3, t), Bezier(y0, y1, y2, y3, t));
                    x = x3;
                    y = y3;
                }

                private static double Bezier(double p0, double p1, double p2, double p3, double t)
                {
                    var u = 1 - t;
                    return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
                }

                /// <summary>The parameters in (0, 1) where a cubic Bézier is at an extreme in one coordinate.</summary>
                private static IEnumerable<double> Extrema(double p0, double p1, double p2, double p3)
                {
                    var a = -p0 + 3 * p1 - 3 * p2 + p3;
                    var b = 2 * (p0 - 2 * p1 + p2);
                    var c = p1 - p0;
                    if (Math.Abs(a) < 1e-9)
                    {
                        if (Math.Abs(b) > 1e-9 && -c / b is var t0 and > 0 and < 1)
                            yield return t0;
                        yield break;
                    }
                    var disc = b * b - 4 * a * c;
                    if (disc < 0)
                        yield break;
                    var sq = Math.Sqrt(disc);
                    foreach (var t in new[] { (-b + sq) / (2 * a), (-b - sq) / (2 * a) })
                    {
                        if (t > 0 && t < 1)
                            yield return t;
                    }
                }

                private void Add(double px, double py)
                {
                    XMin = Math.Min(XMin, px);
                    XMax = Math.Max(XMax, px);
                    YMin = Math.Min(YMin, py);
                    YMax = Math.Max(YMax, py);
                }
            }
        }
    }
}
