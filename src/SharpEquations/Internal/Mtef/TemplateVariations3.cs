namespace SharpEquations.Mtef
{
    /// <summary>
    /// Template selectors and variations of MTEF 3, mapped to their MTEF 5 names as https://github.com/jure/mathtype does,
    /// so that mtef2mml.xsl handles both versions.
    /// </summary>
    internal static class TemplateVariations3
    {
        private static readonly string[] Selectors =
        {
            "tmANGLE", "tmPAREN", "tmBRACE", "tmBRACK", "tmBAR", "tmDBAR", "tmFLOOR", "tmCEILING",
            "tmINTERVAL", "tmINTERVAL", "tmINTERVAL", "tmINTERVAL", "tmINTERVAL", "tmROOT", "tmFRACT", "tmSCRIPT",
            "tmUBAR", "tmOBAR", "tmARROW", "tmARROW", "tmARROW", "tmINTEG", "tmINTEG", "tmINTEG",
            "tmINTEG", "tmINTEG", "tmINTEG", "tmHBRACE", "tmHBRACE", "tmSUM", "tmSUM", "tmPROD",
            "tmPROD", "tmCOPROD", "tmCOPROD", "tmUNION", "tmUNION", "tmINTER", "tmINTER", "tmLIM",
            "tmLDIV", "tmFRACT", "tmINTOP", "tmSUMOP", "tmSCRIPT", "tmDIRAC", "tmVEC", "tmVEC", "tmBOX",
        };

        private static readonly string[] ScriptSelectors = { "tmSUP", "tmSUB", "tmSUBSUP" };

        private static readonly string[][] Fence = { new[] { "tvFENCE_L", "tvFENCE_R" }, new[] { "tvFENCE_L" }, new[] { "tvFENCE_R" } };
        private static readonly string[][] BothFences = { new[] { "tvFENCE_L", "tvFENCE_R" } };
        private static readonly string[][] Bar = { new[] { "tvBAR_SINGLE" }, new[] { "tvBAR_DOUBLE" } };
        private static readonly string[][] SumLimits = { new[] { "tvBO_LOWER", "tvBO_SUM" }, new[] { "tvBO_LOWER", "tvBO_UPPER", "tvBO_SUM" }, new[] { "tvBO_SUM" } };
        private static readonly string[][] IntegralLimits = { new[] { "tvBO_LOWER" }, new[] { "tvBO_LOWER", "tvBO_UPPER" } };

        private static string[][] Integral(string sign) => new[]
        {
            new[] { sign }, new[] { sign, "tvBO_LOWER" }, new[] { sign, "tvBO_LOWER", "tvBO_UPPER" }, new[] { sign }, new[] { sign, "tvBO_LOWER" },
        };

        private static readonly string[][][] Table =
        {
            Fence, Fence, Fence, Fence, Fence, Fence,
            BothFences, BothFences, BothFences, BothFences, BothFences, BothFences, BothFences,
            new[] { new[] { "tvROOT_SQ" }, new[] { "tvROOT_NTH" } },
            new[] { new[] { "tvFR_FULL" }, new[] { "tvFR_SMALL" } },
            null,
            Bar, Bar,
            new[] { new[] { "tvAR_SINGLE", "tvAR_LEFT", "tvAR_TOP" }, new[] { "tvAR_SINGLE", "tvAR_LEFT", "tvAR_BOTTOM" } },
            new[] { new[] { "tvAR_SINGLE", "tvAR_RIGHT", "tvAR_TOP" }, new[] { "tvAR_SINGLE", "tvAR_RIGHT", "tvAR_TOP" } },
            new[] { new[] { "tvAR_SINGLE", "tvAR_RIGHT", "tvAR_LEFT", "tvAR_TOP" }, new[] { "tvAR_SINGLE", "tvAR_RIGHT", "tvAR_LEFT", "tvAR_TOP" } },
            Integral("tvINT_1"), Integral("tvINT_2"), Integral("tvINT_3"),
            new[] { new[] { "tvINT_1", "tvBO_SUM", "tvBO_LOWER", "tvBO_UPPER" }, new[] { "tvINT_1", "tvBO_SUM", "tvBO_LOWER" }, new[] { "tvINT_1", "tvBO_SUM", "tvBO_LOWER" } },
            new[] { new[] { "tvINT_2", "tvBO_SUM", "tvBO_LOWER" }, new[] { "tvINT_2", "tvBO_SUM", "tvBO_LOWER" } },
            new[] { new[] { "tvINT_3", "tvBO_SUM", "tvBO_LOWER" }, new[] { "tvINT_3", "tvBO_SUM", "tvBO_LOWER" } },
            new[] { new[] { "tvHB_TOP" } },
            new[] { new[] { "tvHB_BOT" } },
            SumLimits, IntegralLimits, SumLimits, IntegralLimits, SumLimits, IntegralLimits, SumLimits, IntegralLimits, SumLimits, IntegralLimits,
            new[] { new[] { "tvBO_UPPER" }, new[] { "tvBO_LOWER" }, new[] { "tvBO_LOWER", "tvBO_UPPER" } },
            new[] { new[] { "tvLD_UPPER" } },
            new[] { new[] { "tvFR_SLASH" }, new[] { "tvFR_SLASH", "tvFR_BASE" }, new[] { "tvFR_SLASH", "tvFR_SMALL" } },
            new[] { new[] { "tvBO_LOWER" }, new[] { "tvBO_UPPER" }, new[] { "tvBO_LOWER", "tvBO_UPPER" } },
            new[] { new[] { "tvBO_SUM", "tvBO_LOWER" }, new[] { "tvBO_SUM", "tvBO_UPPER" }, new[] { "tvBO_SUM", "tvBO_LOWER", "tvBO_UPPER" } },
            new[] { new[] { "tvSU_PRECEDES" }, new[] { "tvSU_PRECEDES" }, new[] { "tvSU_PRECEDES" }, new[] { "tvSU_PRECEDES" } },
            new[] { new[] { "tvDI_LEFT", "tvDI_RIGHT" }, new[] { "tvDI_LEFT" }, new[] { "tvDI_RIGHT" } },
            new[] { new[] { "tvVE_UNDER", "tvVE_LEFT" }, new[] { "tvVE_UNDER", "tvVE_RIGHT" }, new[] { "tvVE_UNDER", "tvVE_LEFT", "tvVE_RIGHT" } },
            new[] { new[] { "tvVE_LEFT" }, new[] { "tvVE_RIGHT" }, new[] { "tvVE_LEFT", "tvVE_RIGHT" } },
            null,
        };

        public static string Selector(int selector, int variation)
        {
            if (selector == 15 || selector == 44)
                return variation >= 0 && variation < ScriptSelectors.Length ? ScriptSelectors[variation] : null;
            return selector >= 0 && selector < Selectors.Length ? Selectors[selector] : null;
        }

        public static string[] Variations(int selector, int variation)
        {
            if (selector < 0 || selector >= Table.Length || Table[selector] == null || variation < 0 || variation >= Table[selector].Length)
                return System.Array.Empty<string>();
            return Table[selector][variation];
        }
    }
}
