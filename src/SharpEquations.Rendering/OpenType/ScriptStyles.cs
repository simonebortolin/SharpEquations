namespace SharpEquations.Rendering.OpenType
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// The script style variants of the glyphs of a math font: the <c>ssty</c> feature of its GSUB table, glyphs drawn
    /// for the sizes of scripts (level 1) and of scripts of scripts (level 2), as MathML Core uses them.
    /// </summary>
    public sealed class ScriptStyles
    {
        private readonly Dictionary<ushort, ushort[]> variants;

        private ScriptStyles(Dictionary<ushort, ushort[]> variants) => this.variants = variants;

        /// <summary>The variants of a font from its GSUB table; null if it has none.</summary>
        public static ScriptStyles? Parse(byte[]? gsub)
        {
            if (gsub == null || gsub.Length < 10)
                return null;
            try
            {
                var variants = new Dictionary<ushort, ushort[]>();
                var featureList = U16(gsub, 6);
                var lookupList = U16(gsub, 8);
                var lookups = new SortedSet<int>();
                var features = U16(gsub, featureList);
                for (var i = 0; i < features; i++)
                {
                    var record = featureList + 2 + i * 6;
                    if (Encoding.ASCII.GetString(gsub, record, 4) != "ssty")
                        continue;
                    var feature = featureList + U16(gsub, record + 4);
                    var count = U16(gsub, feature + 2);
                    for (var j = 0; j < count; j++)
                        lookups.Add(U16(gsub, feature + 4 + j * 2));
                }

                // the lookups of the feature in order: each gives the variant of one level more, or the variants of all
                var level = 0;
                foreach (var index in lookups)
                {
                    var lookup = lookupList + U16(gsub, lookupList + 2 + index * 2);
                    var type = U16(gsub, lookup);
                    var subtables = U16(gsub, lookup + 4);
                    for (var k = 0; k < subtables; k++)
                    {
                        var subtable = lookup + U16(gsub, lookup + 6 + k * 2);
                        var subtableType = type;
                        if (type == 7)
                        {
                            subtableType = U16(gsub, subtable + 2);
                            subtable += (int)U32(gsub, subtable + 4);
                        }
                        Read(gsub, subtable, subtableType, level, variants);
                    }
                    level++;
                }
                return variants.Count == 0 ? null : new ScriptStyles(variants);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>The variant of a glyph for a script level (1 or 2, higher is 2); the glyph itself without one.</summary>
        public ushort Variant(ushort glyph, int level)
        {
            if (level <= 0 || !variants.TryGetValue(glyph, out var list))
                return glyph;
            var index = Math.Min(level, 2) - 1;
            for (var i = Math.Min(index, list.Length - 1); i >= 0; i--)
            {
                if (list[i] != 0)
                    return list[i];
            }
            return glyph;
        }

        private static void Read(byte[] d, int at, int type, int level, Dictionary<ushort, ushort[]> variants)
        {
            var format = U16(d, at);
            var coverage = Coverage(d, at + U16(d, at + 2));
            void Set(ushort glyph, int lvl, ushort variant)
            {
                if (!variants.TryGetValue(glyph, out var list))
                    variants[glyph] = list = new ushort[2];
                if (lvl < 2 && list[lvl] == 0)
                    list[lvl] = variant;
            }

            switch (type)
            {
                case 1 when format == 1:
                    var delta = (short)U16(d, at + 4);
                    foreach (var glyph in coverage)
                        Set(glyph, level, (ushort)(glyph + delta));
                    break;
                case 1 when format == 2:
                    var count = U16(d, at + 4);
                    for (var i = 0; i < count && i < coverage.Count; i++)
                        Set(coverage[i], level, (ushort)U16(d, at + 6 + i * 2));
                    break;
                case 3:
                    // an alternate for each level, in order
                    var sets = U16(d, at + 4);
                    for (var i = 0; i < sets && i < coverage.Count; i++)
                    {
                        var set = at + U16(d, at + 6 + i * 2);
                        var alternates = U16(d, set);
                        for (var a = 0; a < alternates && a < 2; a++)
                            Set(coverage[i], a, (ushort)U16(d, set + 2 + a * 2));
                    }
                    break;
            }
        }

        private static List<ushort> Coverage(byte[] d, int at)
        {
            var glyphs = new List<ushort>();
            switch (U16(d, at))
            {
                case 1:
                    var count = U16(d, at + 2);
                    for (var i = 0; i < count; i++)
                        glyphs.Add((ushort)U16(d, at + 4 + i * 2));
                    break;
                case 2:
                    var ranges = U16(d, at + 2);
                    var byIndex = new SortedDictionary<int, ushort>();
                    for (var i = 0; i < ranges; i++)
                    {
                        var r = at + 4 + i * 6;
                        var start = U16(d, r);
                        var end = U16(d, r + 2);
                        var index = U16(d, r + 4);
                        for (var g = start; g <= end; g++)
                            byIndex[index + g - start] = (ushort)g;
                    }
                    glyphs.AddRange(byIndex.Values);
                    break;
            }
            return glyphs;
        }

        private static int U16(byte[] d, int at) => d[at] << 8 | d[at + 1];

        private static uint U32(byte[] d, int at) => (uint)(d[at] << 24 | d[at + 1] << 16 | d[at + 2] << 8 | d[at + 3]);
    }
}
