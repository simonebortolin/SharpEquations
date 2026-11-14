namespace SharpEquations.Rendering.OpenType
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>The tables of an OpenType font file, or of one font of a collection (<c>.ttc</c>).</summary>
    public sealed class FontFile
    {
        private readonly byte[] data;
        private readonly Dictionary<string, (int Offset, int Length)> tables;

        private FontFile(byte[] data, Dictionary<string, (int, int)> tables)
        {
            this.data = data;
            this.tables = tables;
        }

        /// <summary>
        /// Reads the table directory of a font file. In a collection, the font is the one with that number of glyphs
        /// (when it is not negative; with a MATH table, of the faces that share their glyphs), or else the first with a
        /// MATH table, or the first.
        /// </summary>
        /// <exception cref="FormatException">The bytes are not an OpenType font.</exception>
        public static FontFile Parse(byte[] data, int glyphCount = -1)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            try
            {
                if (data.Length >= 12 && Encoding.ASCII.GetString(data, 0, 4) == "ttcf")
                {
                    var count = (int)U32(data, 8);
                    var faces = new List<Dictionary<string, (int, int)>>();
                    for (var i = 0; i < count; i++)
                        faces.Add(Directory(data, (int)U32(data, 12 + i * 4)));
                    // the faces of a collection can share their glyphs (Cambria and Cambria Math in cambria.ttc), and so
                    // their number: of those, the one with a MATH table
                    var counted = glyphCount < 0 ? faces : faces.FindAll(face => face.TryGetValue("maxp", out var maxp) && U16(data, maxp.Item1 + 4) == glyphCount);
                    var chosen = counted.Find(face => face.ContainsKey("MATH")) ?? (counted.Count > 0 ? counted[0] : null)
                        ?? faces.Find(face => face.ContainsKey("MATH")) ?? faces[0];
                    return new FontFile(data, chosen);
                }
                return new FontFile(data, Directory(data, 0));
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                throw new FormatException("Not an OpenType font.", e);
            }
        }

        /// <summary>The number of glyphs, from the <c>maxp</c> table; 0 without one.</summary>
        public int GlyphCount => tables.TryGetValue("maxp", out var maxp) ? U16(data, maxp.Offset + 4) : 0;

        /// <summary>The units of the design in an em, from the <c>head</c> table; 1000 without one.</summary>
        public int UnitsPerEm => tables.TryGetValue("head", out var head) ? U16(data, head.Offset + 18) : 1000;

        /// <summary>The glyph of a code point, from the Unicode subtable of the <c>cmap</c> table (format 12 or 4).</summary>
        public bool TryGetGlyph(int codePoint, out ushort glyph)
        {
            glyph = 0;
            var cmap = Cmap();
            if (cmap < 0)
                return false;
            switch (U16(data, cmap))
            {
                case 12:
                    var groups = (int)U32(data, cmap + 12);
                    int lo = 0, hi = groups - 1;
                    while (lo <= hi)
                    {
                        var mid = (lo + hi) / 2;
                        var g = cmap + 16 + mid * 12;
                        var start = U32(data, g);
                        var end = U32(data, g + 4);
                        if (codePoint < start)
                        {
                            hi = mid - 1;
                        }
                        else if (codePoint > end)
                        {
                            lo = mid + 1;
                        }
                        else
                        {
                            glyph = (ushort)(U32(data, g + 8) + (uint)codePoint - start);
                            return glyph != 0;
                        }
                    }
                    return false;
                case 4 when codePoint <= 0xFFFF:
                    var segments = U16(data, cmap + 6) / 2;
                    var ends = cmap + 14;
                    var starts = ends + segments * 2 + 2;
                    var deltas = starts + segments * 2;
                    var offsets = deltas + segments * 2;
                    for (var i = 0; i < segments; i++)
                    {
                        if (codePoint > U16(data, ends + i * 2))
                            continue;
                        var start = U16(data, starts + i * 2);
                        if (codePoint < start)
                            return false;
                        var delta = U16(data, deltas + i * 2);
                        var rangeOffset = U16(data, offsets + i * 2);
                        if (rangeOffset == 0)
                        {
                            glyph = (ushort)(codePoint + delta);
                        }
                        else
                        {
                            var at = offsets + i * 2 + rangeOffset + (codePoint - start) * 2;
                            var value = U16(data, at);
                            glyph = value == 0 ? (ushort)0 : (ushort)(value + delta);
                        }
                        return glyph != 0;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>The advance of a glyph, in design units, from the <c>hmtx</c> table.</summary>
        public int Advance(ushort glyph)
        {
            if (!tables.TryGetValue("hhea", out var hhea) || !tables.TryGetValue("hmtx", out var hmtx))
                return 0;
            var metrics = U16(data, hhea.Offset + 34);
            if (metrics == 0)
                return 0;
            var index = Math.Min(glyph, metrics - 1);
            return U16(data, hmtx.Offset + index * 4);
        }

        private int cmapAt = int.MinValue;

        /// <summary>The offset of the best Unicode subtable of the cmap table: full Unicode (format 12) rather than the basic plane (format 4).</summary>
        private int Cmap()
        {
            if (cmapAt != int.MinValue)
                return cmapAt;
            if (!tables.TryGetValue("cmap", out var cmap))
                return cmapAt = -1;
            var count = U16(data, cmap.Offset + 2);
            var best = -1;
            var bestRank = 0;
            for (var i = 0; i < count; i++)
            {
                var record = cmap.Offset + 4 + i * 8;
                var platform = U16(data, record);
                var encoding = U16(data, record + 2);
                var at = cmap.Offset + (int)U32(data, record + 4);
                var format = U16(data, at);
                var rank = (platform, encoding, format) switch
                {
                    (3, 10, 12) or (0, 4, 12) or (0, 6, 12) => 3,
                    (3, 1, 4) or (0, 3, 4) => 2,
                    (0, _, 4) => 1,
                    _ => 0,
                };
                if (rank > bestRank)
                {
                    best = at;
                    bestRank = rank;
                }
            }
            cmapAt = best;
            return cmapAt;
        }

        /// <summary>A copy of a table, or null if the font has none.</summary>
        public byte[]? Table(string tag)
        {
            if (!tables.TryGetValue(tag, out var table) || table.Offset + table.Length > data.Length)
                return null;
            var copy = new byte[table.Length];
            Array.Copy(data, table.Offset, copy, 0, table.Length);
            return copy;
        }

        private static Dictionary<string, (int, int)> Directory(byte[] data, int at)
        {
            var count = U16(data, at + 4);
            var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var record = at + 12 + i * 16;
                tables[Encoding.ASCII.GetString(data, record, 4)] = ((int)U32(data, record + 8), (int)U32(data, record + 12));
            }
            return tables;
        }

        private static int U16(byte[] d, int at) => d[at] << 8 | d[at + 1];

        private static uint U32(byte[] d, int at) => (uint)(d[at] << 24 | d[at + 1] << 16 | d[at + 2] << 8 | d[at + 3]);
    }
}
