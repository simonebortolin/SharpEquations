namespace SharpEquations.Internal.Mtef
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    /// <summary>
    /// Reads MathType Equation Format (MTEF) version 3 (Equation Editor 3.0) and version 5 (MathType 5+)
    /// into the XML written by https://github.com/jure/mathtype (MIT), which mtef2mml.xsl converts to MathML:
    /// one element per record, named after the record, holding its fields.
    /// </summary>
    /// <remarks>A ref struct, so that it reads the bytes the caller gives without copying them.</remarks>
    internal ref struct MtefReader
    {
        private static readonly string[] HAlign = { null, "left", "center", "right", "al", "dec" };
        private static readonly string[] VAlign = { "top_baseline", "center_baseline", "bottom_baseline", "center", "axis" };

        private static readonly string[] Embellishments =
        {
            null, null, "emb1DOT", "emb2DOT", "emb3DOT", "emb1PRIME", "emb2PRIME", "embBPRIME", "embTILDE", "embHAT",
            "embNOT", "embRARROW", "embLARROW", "embBARROW", "embR1ARROW", "embL1ARROW", "embMBAR", "embOBAR",
            "emb3PRIME", "embFROWN", "embSMILE", "embX_BARS", "embUP_BAR", "embDOWN_BAR", "emb4DOT", "embU_1DOT",
            "embU_2DOT", "embU_3DOT", "embU_4DOT", "embU_BAR", "embU_TILDE", "embU_FROWN", "embU_SMILE",
            "embU_RARROW", "embU_LARROW", "embU_BARROW", "embU_R1ARROW", "embU_L1ARROW",
        };

        private readonly ReadOnlySpan<byte> data;
        private int position;
        private int bits;
        private int bitCount;
        private int version;
        private int depth;

        private MtefReader(ReadOnlySpan<byte> data)
        {
            this.data = data;
            position = 0;
            bits = 0;
            bitCount = 0;
            version = 0;
            depth = 0;
        }

        /// <summary>
        /// Reads an MTEF equation (without the OLE "Equation Native" header) into a root element holding an mtef element.
        /// </summary>
        public static XElement Read(ReadOnlySpan<byte> mtef)
        {
            var reader = new MtefReader(mtef);
            return reader.ReadEquation();
        }

        private XElement ReadEquation()
        {
            version = data.Length > 0 ? data[0] : 0;
            if (version != 3 && version != 5)
                throw new InvalidDataException("Only MTEF versions 3 and 5 are supported, not " + version + ".");
            var mtef = new XElement("mtef",
                Field("mtef_version", Byte()),
                Field("platform", Byte()),
                Field("product", Byte()),
                Field("product_version", Byte()),
                Field("product_subversion", Byte()));
            if (version == 5)
            {
                mtef.Add(Field("application_key", StringZ()));
                mtef.Add(Field("equation_options", Byte() == 1 ? "inline" : "block"));
            }
            mtef.Add(Records());
            return new XElement("root", mtef);
        }

        /// <summary>
        /// Records up to and including the END record.
        /// </summary>
        private List<XElement> Records()
        {
            if (++depth > EquationStylesheets.MaxDepth / 2)
                throw new InvalidDataException("The MTEF equation is nested too deeply.");
            var records = new List<XElement>();
            while (true)
            {
                var record = version == 5 ? Record5(out var type) : Record3(out type);
                records.Add(record);
                if (type == 0)
                {
                    depth--;
                    return records;
                }
            }
        }

        #region MTEF 5

        private XElement Record5(out int type)
        {
            type = SByte();
            switch (type)
            {
                case 0: return new XElement("end");
                case 1: return Line(SByte());
                case 2: return Char5();
                case 3: return Template5();
                case 4: return Pile(SByte());
                case 5: return Matrix(SByte(), false);
                case 6: return Embellishment5();
                case 7: return Ruler();
                case 8: return new XElement("font_style_def", Field("font_def_index", MtUInt()), Field("char_style", SByte()));
                case 9: return Size(null);
                case 10: case 11: case 12: case 13: case 14: return TypeSize(type);
                case 15: return new XElement("color", Field("color_def_index", MtUInt()));
                case 16: return ColorDefinition();
                case 17: return new XElement("font_def", Field("enc_def_index", MtUInt()), Field("font_name", StringZ()));
                case 18: return EquationPreferences();
                case 19: return new XElement("encoding_def", Field("name", StringZ()));
                case 100:
                    var skip = Byte();
                    Skip(skip);
                    return new XElement("future", Field("skip", skip));
                case 102:
                    return new XElement("mt_comment", Field("comment_length", MtUInt()), Field("comment_type", StringZ()), Field("comment_data", StringZ()));
                default:
                    throw new InvalidDataException("Unknown MTEF record type " + type + ".");
            }
        }

        private XElement Char5()
        {
            var options = SByte();
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            var typeface = SByte() + 128;
            int? code = (options & 0x20) == 0 ? UInt16() : (int?)null;
            int? fontPosition = (options & 0x04) != 0 ? Byte() : (options & 0x10) != 0 ? UInt16() : (int?)null;
            var embellishments = (options & 0x01) != 0 ? Records() : null;
            return new XElement("char",
                nudge,
                Field("typeface", typeface),
                code.HasValue ? Field("mt_code_value", Code(code.Value)) : null,
                Field("options", options),
                fontPosition.HasValue ? Field("font_position", fontPosition.Value) : null,
                Field("variation", typeface == 1 || typeface == 9 || typeface == 10 ? "textmode" : "mathmode"),
                embellishments);
        }

        private XElement Template5()
        {
            var options = SByte();
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            var selector = SByte();
            var first = SByte();
            var second = (first & 0x80) != 0 ? SByte() : 0;
            var specific = SByte();
            var subobjects = Records();
            var template = new XElement("tmpl", Field("selector", TemplateVariations5.Selector(selector)));
            template.Add(TemplateVariations5.Variations(selector, (first & 0x7F) | (second << 8)).Select(v => new XElement("variation", v)));
            template.Add(Field("template_specific_options", specific), nudge, subobjects);
            return template;
        }


        /// <summary>
        /// The (count + 1) partition line styles of a matrix: 2 bits each, packed from the low bits of each byte.
        /// </summary>
        private List<int> LineParts(int count)
        {
            var values = new List<int>();
            var bytes = Math.Max(0, (count + 4) / 4);
            for (var i = 0; i < bytes; i++)
            {
                var b = Byte();
                values.Add(b & 0x03);
                values.Add((b >> 2) & 0x03);
                values.Add((b >> 4) & 0x03);
                values.Add((b >> 6) & 0x03);
            }
            return values.Take(count + 1).ToList();
        }

        private XElement Embellishment5()
        {
            var options = SByte();
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            return new XElement("embell", Field("options", options), nudge, Field("embell", Name(Embellishments, SByte())));
        }

        private XElement ColorDefinition()
        {
            var options = SByte();
            var color = new XElement("color_def", Field("options", options));
            if ((options & 0x01) == 0)
                color.Add(new XElement("rgb", Field("r", UInt16()), Field("g", UInt16()), Field("b", UInt16())));
            else
                color.Add(new XElement("cmyk", Field("c", UInt16()), Field("m", UInt16()), Field("y", UInt16()), Field("k", UInt16())));
            if ((options & 0x04) != 0)
                color.Add(Field("color_name", StringZ()));
            return color;
        }

        private XElement EquationPreferences()
        {
            var preferences = new XElement("eqn_prefs", Field("options", SByte()));
            var sizes = SByte();
            preferences.Add(Field("sizes_count", sizes));
            for (var i = 0; i < sizes; i++)
                preferences.Add(Dimension("sizes"));
            var spaces = SByte();
            preferences.Add(Field("spaces_count", spaces));
            for (var i = 0; i < spaces; i++)
                preferences.Add(Dimension("spaces"));
            var styles = SByte();
            preferences.Add(Field("styles_count", styles));
            for (var i = 0; i < styles; i++)
            {
                var fontDef = SByte();
                preferences.Add(new XElement("styles", Field("font_def", fontDef), fontDef != 0 ? Field("font_style", SByte()) : null));
            }
            bitCount = 0;
            return preferences;
        }

        /// <summary>
        /// A dimension of the equation preferences: a unit nibble followed by nibbles up to the 0xF terminator.
        /// </summary>
        private XElement Dimension(string name)
        {
            var dimension = new XElement(name, Field("unit", Bits(4)));
            int nibble;
            do
            {
                nibble = Bits(4);
                dimension.Add(Field("nibbles", nibble));
            }
            while (nibble != 0xF);
            return dimension;
        }

        #endregion

        #region MTEF 3

        private XElement Record3(out int type)
        {
            var options = Bits(4);
            type = Bits(4);
            switch (type)
            {
                case 0: return new XElement("end");
                case 1: return Line(options);
                case 2: return Char3(options);
                case 3: return Template3(options);
                case 4: return Pile(options);
                case 5: return Matrix(options, true);
                case 6: return Embellishment3(options);
                case 7: return Ruler();
                case 8:
                    var typeface = SByte() + 128;
                    var style = SByte();
                    return new XElement("font", Field("options", options), Field("name", StringZ()), Field("typeface", typeface), Field("style", style));
                case 9: return Size(options);
                case 10: case 11: case 12: case 13: case 14: return TypeSize(type);
                default:
                    throw new InvalidDataException("Unknown MTEF record type " + type + ".");
            }
        }

        // the records MTEF 3 and 5 write alike, after the options (in the record byte in MTEF 3, a byte of their own in 5)

        private static readonly string[] TypeSizes = { "full", "sub", "sub2", "sym", "subsym" };

        /// <summary>The records 10 to 14, which select a type size (full, subscript...) and hold nothing else.</summary>
        private static XElement TypeSize(int type) => new XElement(TypeSizes[type - 10]);

        private XElement Line(int options)
        {
            var slot = new XElement("slot", Field("options", options));
            if ((options & 0x08) != 0)
                slot.Add(Nudge());
            if ((options & 0x04) != 0)
                slot.Add(Field("line_spacing", UInt16()));
            if ((options & 0x02) != 0)
                slot.Add(Ruler());
            if ((options & 0x01) == 0)
                slot.Add(Records());
            return slot;
        }

        private XElement Char3(int options)
        {
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            var typeface = SByte() + 128;
            var code = UInt16();
            return new XElement("char",
                Field("options", options), nudge, Field("typeface", typeface), Field("mt_code_value", Code(code)),
                (options & 0x02) != 0 ? Records() : null);
        }

        private XElement Template3(int options)
        {
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            var selector = SByte();
            var variation = SByte();
            var specific = SByte();
            var subobjects = Records();
            var template = new XElement("tmpl", Field("options", options), Field("selector", TemplateVariations3.Selector(selector, variation)));
            template.Add(TemplateVariations3.Variations(selector, variation).Select(v => new XElement("variation", v)));
            template.Add(Field("template_specific_options", specific), nudge, subobjects);
            return template;
        }

        private XElement Pile(int options)
        {
            if ((options & 0x08) != 0)
                Nudge();
            var halign = SByte();
            var valign = SByte();
            if ((options & 0x02) != 0)
                Ruler();
            return new XElement("pile", Field("options", options), Field("halign", Name(HAlign, halign)), Field("valign", Name(VAlign, valign)), Records());
        }

        /// <summary>A matrix, whose partition lines MTEF 3 (<see cref="BitParts"/>) and 5 (<see cref="LineParts"/>) pack differently.</summary>
        private XElement Matrix(int options, bool mtef3)
        {
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            var valign = SByte();
            var hJust = SByte();
            var vJust = SByte();
            var rows = SByte();
            var cols = SByte();
            var rowParts = mtef3 ? BitParts(rows) : LineParts(rows);
            var colParts = mtef3 ? BitParts(cols) : LineParts(cols);
            return new XElement("matrix",
                Field("options", options), nudge,
                Field("valign", Name(VAlign, valign)), Field("h_just", Name(HAlign, hJust)), Field("v_just", Name(VAlign, vJust)),
                Field("rows", rows), Field("cols", cols),
                rowParts.Select(p => Field("row_parts", p)), colParts.Select(p => Field("col_parts", p)),
                Records());
        }

        /// <summary>
        /// The (count + 1) partition line styles of an MTEF 3 matrix: 2 bits each from the high bits, padded to a byte.
        /// </summary>
        private List<int> BitParts(int count)
        {
            var values = new List<int>();
            for (var i = 0; i <= count; i++)
                values.Add(Bits(2));
            var padding = (count + 1) * 2 % 8;
            if (padding != 0)
                Bits(8 - padding);
            return values;
        }

        private XElement Embellishment3(int options)
        {
            var nudge = (options & 0x08) != 0 ? Nudge() : null;
            var embellishment = SByte();
            SByte();
            return new XElement("embell", Field("options", options), nudge, Field("embell", Name(Embellishments, embellishment)));
        }

        #endregion

        private XElement Ruler()
        {
            var stops = SByte();
            var ruler = new XElement("ruler", Field("n_stops", stops));
            for (var i = 0; i < stops; i++)
                ruler.Add(new XElement("tab_stops", Field("tab_stop_type", SByte()), Field("tab_stop", UInt16())));
            return ruler;
        }

        /// <summary>
        /// A SIZE record. The values follow https://github.com/jure/mathtype: dsize in points (32nds divided by 32,
        /// rounded down), lsize the size selector, point_size the negated explicit point size.
        /// </summary>
        private XElement Size(int? options)
        {
            var select = SByte();
            var pointSize = select == 101 ? UInt16() : 0;
            var lsizeLarge = select == 100 ? Byte() : 0;
            var dsizeSmall = select != 100 && select != 101 ? Byte() : 0;
            var dsizeLarge = select == 100 ? UInt16() : 0;

            int? dsize;
            if (version == 5)
            {
                if (select == 100)
                    dsize = FloorDiv(dsizeLarge > 255 ? -(dsizeLarge & 0xFF) : dsizeLarge, 32);
                else if (select == 101)
                    dsize = 0;
                else
                    dsize = FloorDiv(dsizeSmall - 128, 32);
            }
            else
            {
                dsize = select == 100 ? dsizeLarge / 32 : select == 101 ? (int?)null : FloorDiv(dsizeSmall - 128, 32);
            }
            int? lsize = select == 100 ? lsizeLarge : select == 101 ? (int?)null : select;

            return new XElement("size",
                options.HasValue ? Field("options", options.Value) : null,
                lsize.HasValue ? Field("lsize", lsize.Value) : null,
                dsize.HasValue ? Field("dsize", dsize.Value) : null,
                pointSize != 0 ? Field("point_size", -pointSize) : null);
        }

        private static int FloorDiv(int value, int divisor)
        {
            var quotient = value / divisor;
            return value % divisor != 0 && value < 0 ? quotient - 1 : quotient;
        }

        /// <summary>
        /// A nudge: two offsets with 128 added, or two 128s followed by 16-bit offsets.
        /// The small offsets are read as signed bytes, as https://github.com/jure/mathtype does.
        /// </summary>
        private XElement Nudge()
        {
            var dx = SByte();
            var dy = SByte();
            if (dx == -128 && dy == -128)
                return new XElement("nudge", Field("dx", UInt16()), Field("dy", UInt16()));
            return new XElement("nudge", Field("dx", dx - 128), Field("dy", dy - 128));
        }

        private static string Code(int code)
        {
            return "0x" + code.ToString("X4", CultureInfo.InvariantCulture);
        }

        private static string Name(string[] names, int index)
        {
            return index >= 0 && index < names.Length ? names[index] : null;
        }

        private static XElement Field(string name, object value)
        {
            if (value == null)
                return null;
            return new XElement(name, value is int i ? i.ToString(CultureInfo.InvariantCulture) : value);
        }

        private int Byte()
        {
            bitCount = 0;
            if (position >= data.Length)
                throw new InvalidDataException("The MTEF data ends inside a record.");
            return data[position++];
        }

        private int SByte()
        {
            return (sbyte)Byte();
        }

        private int UInt16()
        {
            var low = Byte();
            return low | (Byte() << 8);
        }

        /// <summary>
        /// MTEF 5 variable-length unsigned integer: one byte, or 0xFF followed by a 16-bit value.
        /// </summary>
        private int MtUInt()
        {
            var first = Byte();
            return first < 0xFF ? first : UInt16();
        }

        private string StringZ()
        {
            var start = position;
            while (Byte() != 0)
            {
            }
            // one path on every target: .NET Standard 2.0 has no Encoding.GetString for spans, so the few bytes of
            // the string (a font or encoding name, a comment) are copied
            return Encoding.GetEncoding("ISO-8859-1").GetString(data.Slice(start, position - start - 1).ToArray());
        }

        private void Skip(int count)
        {
            bitCount = 0;
            position = Math.Min(data.Length, position + count);
        }

        /// <summary>
        /// Reads bits from the most significant bit of the current byte; reading a byte drops the unread bits.
        /// </summary>
        private int Bits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                if (bitCount == 0)
                {
                    if (position >= data.Length)
                        throw new InvalidDataException("The MTEF data ends inside a record.");
                    bits = data[position++];
                    bitCount = 8;
                }
                bitCount--;
                value = (value << 1) | ((bits >> bitCount) & 1);
            }
            return value;
        }
    }
}
