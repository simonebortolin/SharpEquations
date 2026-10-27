namespace SharpEquations.Mtef
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Template selectors and variation names of MTEF 5, as https://github.com/jure/mathtype names them.
    /// </summary>
    internal static class TemplateVariations5
    {
        private static readonly string[] Selectors =
        {
            "tmANGLE", "tmPAREN", "tmBRACE", "tmBRACK", "tmBAR", "tmDBAR", "tmFLOOR", "tmCEILING", "tmOBRACK",
            "tmINTERVAL", "tmROOT", "tmFRACT", "tmUBAR", "tmOBAR", "tmARROW", "tmINTEG",
            "tmSUM", "tmPROD", "tmCOPROD", "tmUNION", "tmINTER", "tmINTOP", "tmSUMOP", "tmLIM",
            "tmHBRACE", "tmHBRACK", "tmLDIV", "tmSUB", "tmSUP", "tmSUBSUP", "tmDIRAC", "tmVEC",
            "tmTILDE", "tmHAT", "tmARC", "tmJSTATUS", "tmSTRIKE", "tmBOX",
        };

        /// <summary>
        /// A variation: present when its flag is set (or, for a negative flag, not set). A conditional variation
        /// takes the name of the first condition whose flag is also set.
        /// </summary>
        private sealed class Variation
        {
            public int Flag;
            public string Name;
            public (int Flag, string Name)[] Conditions;
        }

        private static Variation V(int flag, string name) => new Variation { Flag = flag, Name = name };

        private static Variation C(int flag, params (int Flag, string Name)[] conditions) => new Variation { Flag = flag, Conditions = conditions };

        private static readonly (int From, int To, Variation[] Variations)[] Table =
        {
            (0, 8, new[] { V(0x0001, "tvFENCE_L"), V(0x0002, "tvFENCE_R") }),
            (9, 9, new[]
            {
                C(0x0002, (0x0020, "tvINTV_LBLB"), (0x0010, "tvINTV_LBRP")),
                C(0x0003, (0x0030, "tvINTV_RBRB"), (0x0020, "tvINTV_RBLB")),
                C(0x0000, (0x0030, "tvINTV_LPRB")),
            }),
            (10, 10, new[] { V(0, "tvROOT_SQ"), V(1, "tvROOT_NTH") }),
            (11, 11, new[] { V(0x0001, "tvFR_SMALL"), V(0x0002, "tvFR_SLASH"), V(0x0004, "tvFR_BASE") }),
            (12, 13, new[] { V(0x0001, "tvBAR_DOUBLE") }),
            (14, 14, new[]
            {
                V(0x0000, "tvAR_SINGLE"),
                V(0x0001, "tvAR_DOUBLE"),
                C(0x0010, (0x0001, "tvAR_LOS"), (0x0002, "tvAR_LOS"), (0x0010, "tvAR_LEFT")),
                C(0x0020, (0x0001, "tvAR_SOL"), (0x0002, "tvAR_SOL"), (0x0010, "tvAR_RIGHT")),
                V(0x0002, "tvAR_HARPOON"),
                C(0x0004, (0x0008, "tvAR_TOPBOTTOM"), (0x0004, "tvAR_TOP")),
                C(0x0008, (0x0004, "tvAR_TOPBOTTOM"), (0x0008, "tvAR_BOTTOM")),
            }),
            (15, 15, new[]
            {
                V(0x0001, "tvINT_1"), V(0x0002, "tvINT_2"), V(0x0003, "tvINT_3"), V(0x0004, "tvINT_LOOP"),
                V(0x0008, "tvINT_CW_LOOP"), V(0x000C, "tvINT_CCW_LOOP"), V(0x0100, "tvINT_EXPAND"),
            }),
            (15, 23, new[] { V(0x0010, "tvBO_LOWER"), V(0x0020, "tvBO_UPPER"), V(0x0040, "tvBO_SUM"), V(-0x0040, "tvBO_INT") }),
            (23, 23, new[] { V(0, "tvSUBAR"), V(1, "tvDUBAR") }),
            (24, 25, new[] { V(0x0001, "tvHB_TOP") }),
            (26, 26, new[] { V(0x0001, "tvLD_UPPER") }),
            (27, 29, new[] { V(0x0001, "tvSU_PRECEDES") }),
            (30, 30, new[] { V(0x0001, "tvDI_LEFT"), V(0x0002, "tvDI_RIGHT") }),
            (31, 31, new[] { V(0x0001, "tvVE_LEFT"), V(0x0002, "tvVE_RIGHT"), V(0x0004, "tvVE_UNDER"), V(0x0008, "tvVE_HARPOON") }),
            (36, 36, new[] { V(0x0001, "tvST_HORIZ"), V(0x0002, "tvST_UP"), V(0x0004, "tvST_DOWN") }),
            (37, 37, new[]
            {
                V(0x0001, "tvBX_ROUND"), V(0x0002, "tvBX_LEFT"), V(0x0004, "tvBX_RIGHT"), V(0x0008, "tvBX_TOP"), V(0x0010, "tvBX_BOTTOM"),
            }),
        };

        public static string Selector(int selector)
        {
            return selector >= 0 && selector < Selectors.Length ? Selectors[selector] : null;
        }

        /// <summary>
        /// The names of the variations present in a variation code. Like Ruby's Hash#merge, a later entry
        /// for the same flag replaces an earlier one in its place.
        /// </summary>
        public static List<string> Variations(int selector, int code)
        {
            var merged = new List<Variation>();
            foreach (var row in Table.Where(r => selector >= r.From && selector <= r.To))
            {
                foreach (var variation in row.Variations)
                {
                    var index = merged.FindIndex(m => m.Flag == variation.Flag);
                    if (index >= 0)
                        merged[index] = variation;
                    else
                        merged.Add(variation);
                }
            }

            var digitMode = selector == 9;
            var names = new List<string>();
            foreach (var variation in merged)
            {
                var present = variation.Flag < 0 ? !Check(-variation.Flag, code, digitMode) : Check(variation.Flag, code, digitMode);
                if (!present)
                    continue;
                var name = variation.Conditions == null
                    ? variation.Name
                    : variation.Conditions.Where(c => Check(c.Flag, code, digitMode)).Select(c => c.Name).FirstOrDefault();
                if (!names.Contains(name))
                    names.Add(name);
            }
            return names;
        }

        private static bool Check(int flag, int code, bool digitMode)
        {
            if (digitMode)
            {
                var digits = 1;
                while (flag >> (4 * digits) != 0)
                    digits++;
                var shift = 4 * digits - 4;
                return ((code >> shift) & 0xF) == ((flag >> shift) & 0xF);
            }
            return flag == 0 ? (code & 0xF) == 0 : (code & flag) == flag;
        }
    }
}
