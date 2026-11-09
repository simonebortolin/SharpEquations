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
        /// (when it is not negative) or else the first with a MATH table, or the first.
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
                    foreach (var face in faces)
                    {
                        if (glyphCount >= 0 && face.TryGetValue("maxp", out var maxp) && U16(data, maxp.Item1 + 4) == glyphCount)
                            return new FontFile(data, face);
                    }
                    foreach (var face in faces)
                    {
                        if (face.ContainsKey("MATH"))
                            return new FontFile(data, face);
                    }
                    return new FontFile(data, faces[0]);
                }
                return new FontFile(data, Directory(data, 0));
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                throw new FormatException("Not an OpenType font.", e);
            }
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
