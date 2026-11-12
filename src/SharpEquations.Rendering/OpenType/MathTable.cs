namespace SharpEquations.Rendering.OpenType
{
    using System;
    using System.Collections.Generic;

    /// <summary>The constants of the OpenType MATH table, in the order of the table.</summary>
    public enum MathConstant
    {
#pragma warning disable CS1591 // the names of the OpenType specification
        ScriptPercentScaleDown,
        ScriptScriptPercentScaleDown,
        DelimitedSubFormulaMinHeight,
        DisplayOperatorMinHeight,
        MathLeading,
        AxisHeight,
        AccentBaseHeight,
        FlattenedAccentBaseHeight,
        SubscriptShiftDown,
        SubscriptTopMax,
        SubscriptBaselineDropMin,
        SuperscriptShiftUp,
        SuperscriptShiftUpCramped,
        SuperscriptBottomMin,
        SuperscriptBaselineDropMax,
        SubSuperscriptGapMin,
        SuperscriptBottomMaxWithSubscript,
        SpaceAfterScript,
        UpperLimitGapMin,
        UpperLimitBaselineRiseMin,
        LowerLimitGapMin,
        LowerLimitBaselineDropMin,
        StackTopShiftUp,
        StackTopDisplayStyleShiftUp,
        StackBottomShiftDown,
        StackBottomDisplayStyleShiftDown,
        StackGapMin,
        StackDisplayStyleGapMin,
        StretchStackTopShiftUp,
        StretchStackBottomShiftDown,
        StretchStackGapAboveMin,
        StretchStackGapBelowMin,
        FractionNumeratorShiftUp,
        FractionNumeratorDisplayStyleShiftUp,
        FractionDenominatorShiftDown,
        FractionDenominatorDisplayStyleShiftDown,
        FractionNumeratorGapMin,
        FractionNumDisplayStyleGapMin,
        FractionRuleThickness,
        FractionDenominatorGapMin,
        FractionDenomDisplayStyleGapMin,
        SkewedFractionHorizontalGap,
        SkewedFractionVerticalGap,
        OverbarVerticalGap,
        OverbarRuleThickness,
        OverbarExtraAscender,
        UnderbarVerticalGap,
        UnderbarRuleThickness,
        UnderbarExtraDescender,
        RadicalVerticalGap,
        RadicalDisplayStyleVerticalGap,
        RadicalRuleThickness,
        RadicalExtraAscender,
        RadicalKernBeforeDegree,
        RadicalKernAfterDegree,
        RadicalDegreeBottomRaisePercent,
#pragma warning restore CS1591
    }

    /// <summary>A part of a glyph assembly: a stretchy glyph built of pieces, some of which (the extenders) repeat.</summary>
    public readonly struct GlyphPart
    {
        internal GlyphPart(ushort glyph, int startConnector, int endConnector, int fullAdvance, bool isExtender)
        {
            Glyph = glyph;
            StartConnector = startConnector;
            EndConnector = endConnector;
            FullAdvance = fullAdvance;
            IsExtender = isExtender;
        }

        /// <summary>The glyph of the part.</summary>
        public ushort Glyph { get; }

        /// <summary>The length of the connector at the start (bottom or left), in design units.</summary>
        public int StartConnector { get; }

        /// <summary>The length of the connector at the end (top or right), in design units.</summary>
        public int EndConnector { get; }

        /// <summary>The advance of the part in the direction of the stretch, in design units.</summary>
        public int FullAdvance { get; }

        /// <summary>The part repeats as many times as needed.</summary>
        public bool IsExtender { get; }
    }

    /// <summary>The variants of a glyph in one direction: bigger glyphs, and an assembly of parts for any size.</summary>
    public sealed class GlyphConstruction
    {
        internal GlyphConstruction(IReadOnlyList<(ushort Glyph, int Advance)> variants, IReadOnlyList<GlyphPart>? assembly, int assemblyItalicCorrection)
        {
            Variants = variants;
            Assembly = assembly;
            AssemblyItalicCorrection = assemblyItalicCorrection;
        }

        /// <summary>The variants, from the smallest, with their size in the direction of the stretch, in design units.</summary>
        public IReadOnlyList<(ushort Glyph, int Advance)> Variants { get; }

        /// <summary>The parts of the assembly, from the bottom or the left; null without one.</summary>
        public IReadOnlyList<GlyphPart>? Assembly { get; }

        /// <summary>The italic correction of the assembly, in design units.</summary>
        public int AssemblyItalicCorrection { get; }
    }

    /// <summary>The OpenType MATH table of a font: its constants, the italic corrections and accent attachments of its glyphs, and their variants.</summary>
    public sealed class MathTable
    {
        private readonly int[] constants;
        private readonly Dictionary<ushort, int> italics;
        private readonly Dictionary<ushort, int> topAccents;
        private readonly HashSet<ushort> extendedShapes;
        private readonly Dictionary<ushort, GlyphConstruction> vertical;
        private readonly Dictionary<ushort, GlyphConstruction> horizontal;

        private MathTable(int[] constants, Dictionary<ushort, int> italics, Dictionary<ushort, int> topAccents, HashSet<ushort> extendedShapes,
            int minConnectorOverlap, Dictionary<ushort, GlyphConstruction> vertical, Dictionary<ushort, GlyphConstruction> horizontal)
        {
            this.constants = constants;
            this.italics = italics;
            this.topAccents = topAccents;
            this.extendedShapes = extendedShapes;
            MinConnectorOverlap = minConnectorOverlap;
            this.vertical = vertical;
            this.horizontal = horizontal;
        }

        /// <summary>The overlap of the parts of an assembly at least, in design units.</summary>
        public int MinConnectorOverlap { get; }

        /// <summary>A constant: a percentage for the first two and the last, a length in design units for the others.</summary>
        public int this[MathConstant constant] => constants[(int)constant];

        /// <summary>The italic correction of a glyph, in design units; 0 without one.</summary>
        public int ItalicCorrection(ushort glyph) => italics.TryGetValue(glyph, out var value) ? value : 0;

        /// <summary>The horizontal position where an accent attaches above the glyph, in design units; null without one.</summary>
        public int? TopAccentAttachment(ushort glyph) => topAccents.TryGetValue(glyph, out var value) ? value : null;

        /// <summary>Whether the glyph is an extended shape (a big operator, a delimiter variant), which scripts attach to differently.</summary>
        public bool IsExtendedShape(ushort glyph) => extendedShapes.Contains(glyph);

        /// <summary>The vertical variants of a glyph, or null.</summary>
        public GlyphConstruction? Vertical(ushort glyph) => vertical.TryGetValue(glyph, out var value) ? value : null;

        /// <summary>The horizontal variants of a glyph, or null.</summary>
        public GlyphConstruction? Horizontal(ushort glyph) => horizontal.TryGetValue(glyph, out var value) ? value : null;

        /// <summary>Reads the table from its bytes (the MATH table of the font, not the font).</summary>
        /// <exception cref="FormatException">The bytes are not a MATH table.</exception>
        public static MathTable Parse(ReadOnlySpan<byte> data) => Parse(data.ToArray());

        /// <summary>Reads the table from its bytes (the MATH table of the font, not the font).</summary>
        /// <exception cref="FormatException">The bytes are not a MATH table.</exception>
        public static MathTable Parse(byte[] data)
        {
            try
            {
                var r = new Reader(data);
                if (r.U16(0) != 1)
                    throw new FormatException("Not a MATH table of version 1.");
                var constantsAt = r.U16(4);
                var glyphInfoAt = r.U16(6);
                var variantsAt = r.U16(8);

                var constants = new int[(int)MathConstant.RadicalDegreeBottomRaisePercent + 1];
                constants[0] = r.S16(constantsAt);
                constants[1] = r.S16(constantsAt + 2);
                constants[2] = r.U16(constantsAt + 4);
                constants[3] = r.U16(constantsAt + 6);
                for (var i = 4; i < constants.Length - 1; i++)
                    constants[i] = r.S16(constantsAt + 8 + (i - 4) * 4);
                constants[constants.Length - 1] = r.S16(constantsAt + 8 + (constants.Length - 5) * 4);

                var italics = new Dictionary<ushort, int>();
                var topAccents = new Dictionary<ushort, int>();
                var extended = new HashSet<ushort>();
                if (glyphInfoAt != 0)
                {
                    ReadValues(r, glyphInfoAt, r.U16(glyphInfoAt), italics);
                    ReadValues(r, glyphInfoAt, r.U16(glyphInfoAt + 2), topAccents);
                    var extendedAt = r.U16(glyphInfoAt + 4);
                    if (extendedAt != 0)
                    {
                        foreach (var glyph in Coverage(r, glyphInfoAt + extendedAt))
                            extended.Add(glyph);
                    }
                }

                var vertical = new Dictionary<ushort, GlyphConstruction>();
                var horizontal = new Dictionary<ushort, GlyphConstruction>();
                var overlap = 0;
                if (variantsAt != 0)
                {
                    overlap = r.U16(variantsAt);
                    var vertCoverage = r.U16(variantsAt + 2);
                    var horizCoverage = r.U16(variantsAt + 4);
                    var vertCount = r.U16(variantsAt + 6);
                    var horizCount = r.U16(variantsAt + 8);
                    ReadConstructions(r, variantsAt, vertCoverage, vertCount, variantsAt + 10, vertical);
                    ReadConstructions(r, variantsAt, horizCoverage, horizCount, variantsAt + 10 + vertCount * 2, horizontal);
                }
                return new MathTable(constants, italics, topAccents, extended, overlap, vertical, horizontal);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new FormatException("The MATH table is truncated.", e);
            }
        }

        private static void ReadValues(Reader r, int parent, int offset, Dictionary<ushort, int> values)
        {
            if (offset == 0)
                return;
            var at = parent + offset;
            var glyphs = Coverage(r, at + r.U16(at));
            var count = r.U16(at + 2);
            for (var i = 0; i < count && i < glyphs.Count; i++)
                values[glyphs[i]] = r.S16(at + 4 + i * 4);
        }

        private static void ReadConstructions(Reader r, int variantsAt, int coverageOffset, int count, int offsetsAt, Dictionary<ushort, GlyphConstruction> result)
        {
            if (coverageOffset == 0 || count == 0)
                return;
            var glyphs = Coverage(r, variantsAt + coverageOffset);
            for (var i = 0; i < count && i < glyphs.Count; i++)
            {
                var at = variantsAt + r.U16(offsetsAt + i * 2);
                var assemblyOffset = r.U16(at);
                var variantCount = r.U16(at + 2);
                var variants = new List<(ushort, int)>(variantCount);
                for (var v = 0; v < variantCount; v++)
                    variants.Add((r.U16(at + 4 + v * 4), r.U16(at + 6 + v * 4)));

                List<GlyphPart>? parts = null;
                var italic = 0;
                if (assemblyOffset != 0)
                {
                    var a = at + assemblyOffset;
                    italic = r.S16(a);
                    var partCount = r.U16(a + 4);
                    parts = new List<GlyphPart>(partCount);
                    for (var p = 0; p < partCount; p++)
                    {
                        var pa = a + 6 + p * 10;
                        parts.Add(new GlyphPart(r.U16(pa), r.U16(pa + 2), r.U16(pa + 4), r.U16(pa + 6), (r.U16(pa + 8) & 1) != 0));
                    }
                }
                result[glyphs[i]] = new GlyphConstruction(variants, parts, italic);
            }
        }

        /// <summary>The glyphs of a coverage table, in the order of their coverage index.</summary>
        private static List<ushort> Coverage(Reader r, int at)
        {
            var glyphs = new List<ushort>();
            switch (r.U16(at))
            {
                case 1:
                    var count = r.U16(at + 2);
                    for (var i = 0; i < count; i++)
                        glyphs.Add(r.U16(at + 4 + i * 2));
                    break;
                case 2:
                    var ranges = r.U16(at + 2);
                    var byIndex = new SortedDictionary<int, ushort>();
                    for (var i = 0; i < ranges; i++)
                    {
                        var ra = at + 4 + i * 6;
                        var start = r.U16(ra);
                        var end = r.U16(ra + 2);
                        var index = r.U16(ra + 4);
                        for (var g = start; g <= end; g++)
                            byIndex[index + g - start] = (ushort)g;
                    }
                    glyphs.AddRange(byIndex.Values);
                    break;
                default:
                    throw new FormatException("Unknown coverage format.");
            }
            return glyphs;
        }

        private readonly struct Reader
        {
            private readonly byte[] data;

            public Reader(byte[] data) => this.data = data;

            public ushort U16(int at) => (ushort)(data[at] << 8 | data[at + 1]);

            public short S16(int at) => (short)(data[at] << 8 | data[at + 1]);
        }
    }
}
