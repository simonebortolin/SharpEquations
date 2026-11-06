namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using static Js;
    using static UnicodeMathText;

    internal sealed partial class UnicodeMathTranslator
    {
        /// <summary>getCh: the character at index i, a code point (str and i count code points).</summary>
        private static string GetCh(string str, int i) => Ch(str, i);

        private static bool IsPrime(string ch) => ch != null && "′″‴⁗\t".Contains(ch);

        private static string ProcessPrimes(object primes)
        {
            switch (primes)
            {
                case 4:
                    return "⁗";
                case 3:
                    return "‴";
                case 2:
                    return "″";
                case int n when n > 0:
                    return new string('′', n);
                default:
                    return "";
            }
        }

        private static bool IsIntegral(string op) => InRange("∫", op, "∳") || op == "⨌";

        private static bool IsTranspose(object value)
        {
            return value is List<object> list && Get(list[0], "atoms") is List<object> atoms &&
                Truthy(Get(atoms[0], "chars")) && Eq(Get(atoms[0], "chars"), "⊺");
        }

        private static bool IsCharsButNotFunction(object value)
        {
            return Get(value, "chars") is string chars && Len(chars) > 0 && Ch(chars, 0) != "Ⅎ" && !IsFunctionName(chars);
        }

        private static object DropSingletonLists(object uast)
        {
            while (uast is List<object> list && list.Count == 1)
                uast = list[0];
            return uast;
        }

        private static object DropOutermostParens(object uast)
        {
            if (Truthy(Get(uast, "expr")))
                return O("expr", DropOutermostParens(Get(uast, "expr")));

            if (uast is List<object> list)
            {
                if (list.Count == 1)
                    return A(DropOutermostParens(list[0]));
                if (list.Count == 2 && Truthy(Get(list[0], "bracketed")) && Truthy(Get(list[1], "intend")))
                    return A(DropOutermostParens(list[0]), list[1]);
            }
            if (!Truthy(Get(uast, "bracketed")))
                return uast;

            var v = V(uast);
            if (Eq(Get(v, "open"), "(") && Eq(Get(v, "close"), ")") && !Truthy(Get(Get(v, "content"), "separated")))
                return Get(v, "content");
            return uast;
        }

        private static JsObject MatrixRows(int n, int m)
        {
            var rows = new List<object>();
            bool identity = false;
            if (m == 0)
            {
                m = n;
                identity = true;
            }
            for (int i = 0; i < n; i++)
            {
                var row = new List<object>();
                for (int j = 0; j < m; j++)
                {
                    object x = "⬚";
                    if (identity)
                        x = i == j ? 1 : 0;
                    row.Add(O("expr", A(A(O("number", x)))));
                }
                rows.Add(O("mrow", row));
            }
            return O("mrows", rows);
        }

        private static List<string> NaryOptions(object maskValue)
        {
            int mask = maskValue is int m ? m : 0;
            if (mask < 0 || mask > 159)
                throw new FormatException("nary mask is not between 0 and 159");

            var options = new List<string>();
            switch (mask % 4)
            {
                case 0:
                    options.Add("nLimitsDefault");
                    break;
                case 1:
                    options.Add("nLimitsUnderOver");
                    break;
                case 2:
                    options.Add("nLimitsSubSup");
                    break;
                case 3:
                    options.Add("nUpperLimitAsSuperScript");
                    break;
            }
            mask -= mask % 4;

            switch (mask % 32)
            {
                case 4:
                    options.Add("nLimitsOpposite");
                    break;
                case 8:
                    options.Add("nShowLowLimitPlaceHolder");
                    break;
                case 12:
                    options.Add("nLimitsOpposite");
                    options.Add("nShowLowLimitPlaceHolder");
                    break;
                case 16:
                    options.Add("nShowUpLimitPlaceHolder");
                    break;
                case 20:
                    options.Add("nLimitsOpposite");
                    options.Add("nShowUpLimitPlaceHolder");
                    break;
                case 24:
                    options.Add("nShowLowLimitPlaceHolder");
                    options.Add("nShowUpLimitPlaceHolder");
                    break;
                case 28:
                    options.Add("nLimitsOpposite");
                    options.Add("nShowLowLimitPlaceHolder");
                    options.Add("nShowUpLimitPlaceHolder");
                    break;
            }
            mask -= mask % 32;

            if (mask == 64)
                options.Add("fDontGrowWithContent");
            else if (mask == 128)
                options.Add("fGrowWithContent");
            return options;
        }

        private static readonly Dictionary<string, string> SymbolClasses = new Dictionary<string, string>
        {
            ["▭"] = "box", ["\u0304"] = "top", ["▁"] = "bottom", ["▢"] = "roundedbox", ["◯"] = "circle", ["⟌"] = "longdiv",
            ["\u20E7"] = "actuarial", ["⬭"] = "circle", ["╱"] = "cancel", ["╲"] = "bcancel", ["╳"] = "xcancel",
        };

        private static readonly string[] MaskClasses =
            { "top", "bottom", "left", "right", "horizontalstrike", "verticalstrike", "downdiagonalstrike", "updiagonalstrike" };

        private static string EnclosureAttrs(object maskValue, string symbol)
        {
            if (maskValue is int mask)
            {
                if (mask < 0 || mask > 255)
                    throw new FormatException("enclosure mask is not between 0 and 255");
                mask ^= 15;
                var classes = new List<string>();
                for (int i = 7; i >= 0; i--)
                {
                    if ((mask & (1 << i)) != 0)
                        classes.Add(MaskClasses[i]);
                }
                return string.Join(" ", classes);
            }
            if (symbol != null)
                return SymbolClasses.TryGetValue(symbol, out var cls) ? cls : "undefined";
            return "";
        }

        private static List<string> AbstractBoxOptions(object maskValue)
        {
            int mask = maskValue is int m ? m : 0;
            var options = new List<string>();
            options.Add(mask % 2 == 0 ? "nAlignBaseline" : "nAlignCenter");
            mask -= mask % 4;
            switch (mask % 32)
            {
                case 0: options.Add("nSpaceDefault"); break;
                case 4: options.Add("nSpaceUnary"); break;
                case 8: options.Add("nSpaceBinary"); break;
                case 12: options.Add("nSpaceRelational"); break;
                case 16: options.Add("nSpaceSkip"); break;
                case 20: options.Add("nSpaceOrd"); break;
                case 24: options.Add("nSpaceDifferential"); break;
            }
            mask -= mask % 32;
            switch (mask % 128)
            {
                case 0: options.Add("nSizeDefault"); break;
                case 32: options.Add("nSizeText"); break;
                case 64: options.Add("nSizeScript"); break;
                case 96: options.Add("nSizeScriptScript"); break;
            }
            mask -= mask % 128;
            if (mask != 0)
            {
                if (mask % 128 == 0)
                    options.Add("fBreakable");
                if (mask % 256 == 0)
                    options.Add("fXPositioning");
                if (mask % 512 == 0)
                    options.Add("fXSpacing");
            }
            return options;
        }

        private static readonly string[] Overlays =
        {
            "\u0334", "\u0335", "\u0336", "\u0337", "\u0338", "\u20D2", "\u20D3", "\u20D8", "\u20D9", "\u20DA", "\u20DD", "\u20DE",
            "\u20DF", "\u20E0", "\u20E2", "\u20E3", "\u20E4", "\u20E5", "\u20E6", "\u20EA", "\u20EB",
        };

        private static readonly string[] Belows =
        {
            "\u0316", "\u0317", "\u0318", "\u0319", "\u031C", "\u031D", "\u031E", "\u031F", "\u0320", "\u0321", "\u0322", "\u0323",
            "\u0324", "\u0325", "\u0326", "\u0327", "\u0328", "\u0329", "\u032A", "\u032B", "\u032C", "\u032D", "\u032E", "\u032F",
            "\u0330", "\u0331", "\u0332", "\u0333", "\u0339", "\u033A", "\u033B", "\u033C", "\u0345", "\u0347", "\u0348", "\u0349",
            "\u034D", "\u034E", "\u0353", "\u0354", "\u0355", "\u0356", "\u0359", "\u035A", "\u035C", "\u035F", "\u0362", "\u20E8",
            "\u20EC", "\u20ED", "\u20EE", "\u20EF",
        };

        private static int DiacriticPosition(string d)
        {
            if (Overlays.Contains(d))
                return 0;
            if (Belows.Contains(d))
                return -1;
            return 1;
        }

        private static string FontSize(object n)
        {
            double size = Math.Pow(1.25, n is int i ? i : 0);
            return size.ToString("R", CultureInfo.InvariantCulture) + "em";
        }

        private static string GetAbsArg(object content)
        {
            if (content is List<object> list && Get(list[0], "atoms") is List<object> atoms &&
                atoms.Count == 1 && Get(atoms[0], "chars") is string arg && Len(arg) > 0)
            {
                var ch = GetCh(arg, 0);
                if (Len(ch) == Len(arg))
                    return ch;
            }
            return "$a";
        }

        private static string GetIntervalArg(List<object> content, int n)
        {
            if (n != 0 && n != 2)
                return "";
            var argValue = content[n];
            string arg = argValue is List<object> list ? JoinFlat(list) : Str(argValue);
            var ch = GetCh(arg, 0) ?? "";
            if (Len(arg) > Len(ch) && !IsAsciiDigit(Ch(arg, 0)) && !Characters("-−+∞").Contains(Ch(arg, 0)))
                arg = "$" + (n != 0 ? "b" : "a");
            return arg;
        }

        /// <summary>array.flat().join(''): one level flattened, undefined and null as "".</summary>
        private static string JoinFlat(List<object> list)
        {
            var sb = new StringBuilder();
            foreach (var item in list)
            {
                if (item is List<object> inner)
                {
                    foreach (var x in inner)
                        sb.Append(x == null ? "" : Str(x));
                }
                else if (item != null)
                {
                    sb.Append(Str(item));
                }
            }
            return sb.ToString();
        }

        private static JsObject GetIntervalEndPoint(string arg, object content)
        {
            if (Len(arg) > 0 && Ch(arg, 0) == "$")
            {
                var atoms = A(O("chars", content is List<object> list ? JoinFlat(list) : Str(content)));
                var ret = O("atoms", atoms);
                Set(atoms, "arg", Sub(arg, 1));
                return ret;
            }
            return IsAsciiDigit(Ch(arg, 0)) ? O("number", arg) : O("atoms", A(O("chars", arg)));
        }

        private static object GetOrder(object high)
        {
            if (Truthy(Get(high, "expr")))
                return Get(At(At(Get(high, "expr"), 0), 0), "number");
            if (Truthy(Get(high, "atoms")))
                return Get(At(Get(high, "atoms"), 0), "chars");
            if (high is List<object> list)
            {
                if (Truthy(Get(list[0], "number")))
                    return Get(list[0], "number");
                if (Truthy(Get(list[0], "operator")))
                    return "";
                if (Truthy(Get(list[0], "atoms")))
                    return Get(At(Get(list[0], "atoms"), 0), "chars");
            }
            return "$n";
        }

        private static string GetScript(object limit, string reference)
        {
            if (limit == null)
                return "";
            if (!(limit is List<object>))
            {
                if (!(Get(limit, "expr") is List<object> expr))
                    return "";
                limit = expr[0];
            }
            if (Length(limit) > 1)
                return reference;
            limit = At(limit, 0);

            var atoms = Get(limit, "atoms");
            if (Truthy(atoms))
            {
                if (Truthy(Get(atoms, "chars")))
                    return Str(Get(atoms, "chars"));
                if (atoms is List<object> atomList && Truthy(Get(atomList[0], "chars")))
                    return Str(Get(atomList[0], "chars"));
            }
            if (Truthy(Get(limit, "number")))
                return Str(Get(limit, "number"));
            return reference;
        }

        private static string GetVariable(object arg)
        {
            if (!(arg is List<object> list) || !(Get(list[0], "atoms") is List<object> atoms) || atoms.Count > 1 ||
                !(Get(atoms[0], "chars") is string chars) || Len(chars) == 0)
            {
                return "$";
            }
            var ch = GetCh(chars, 0);
            return Len(ch) == Len(chars) ? ch : "$";
        }

        /// <summary>getChars: the characters (with primes) of a script base, primed or atoms; "undefined" where
        /// JavaScript concatenates an undefined value.</summary>
        private static string GetChars(object value)
        {
            var chars = "";
            int n = 1;
            object primes = null;
            var val = value;

            if (value is List<object> list)
            {
                n = list.Count;
                val = At(list, 0);
                if (val is List<object>)
                    val = At(val, 0);
            }
            for (int i = 0; i < n; val = At(value, ++i))
            {
                if (Truthy(Get(val, "script")))
                    val = Get(Get(val, "script"), "base");
                if (Truthy(Get(val, "primed")))
                {
                    primes = Get(Get(val, "primed"), "primes");
                    val = Get(Get(val, "primed"), "base");
                }
                if (Truthy(Get(val, "atoms")))
                {
                    val = Get(val, "atoms");
                    if (val is List<object>)
                        val = At(val, 0);
                    chars += Truthy(primes) ? Str(Get(val, "chars")) + ProcessPrimes(primes) : Str(Get(val, "chars"));
                }
                else if (Truthy(Get(val, "number")))
                {
                    chars = Str(Get(val, "number"));
                }
                if (n == 1)
                    break;
            }
            return chars;
        }

        private static string GetChD(object value)
        {
            var chars = GetChars(value);
            if (Len(chars) == 0)
                return "";
            var chD = GetCh(chars, 0);
            return "dⅆ∂𝑑𝜕".Contains(chD) ? chD : "";
        }

        /// <summary>[differential d, order, of/wrt] of the numerator (n = 0) or denominator (n = 1) of a derivative.</summary>
        private static (object, object, object) GetDifferentialInfo(List<object> of, int n)
        {
            var arg = At(of, n);
            object darg = "";
            object order = "1";
            bool script = false;
            object arg1 = null;

            if (n != 0 && n != 1 || !(arg is List<object>))
                return (0, 0, 0);

            arg = At(arg, 0);
            if (arg == null)
                return (0, 0, 0);

            if (Truthy(Get(arg, "expr")))
            {
                arg = Get(arg, "expr");
                if (arg is List<object>)
                    arg = At(arg, 0);
                if (arg is List<object> argList)
                {
                    if (argList.Count > 1)
                        arg1 = argList[1];
                    arg = argList[0];
                }
            }

            if (Truthy(Get(arg, "function")))
            {
                arg = Get(Get(arg, "function"), "f");
                if (n == 1 && Truthy(Get(arg, "atoms")) && Truthy(Get(Get(arg, "atoms"), "chars")))
                {
                    var chars = Str(Get(Get(arg, "atoms"), "chars"));
                    var chD0 = GetCh(chars, 0);
                    int iOff = Len(chD0);
                    if (!"dⅆ∂𝑑𝜕".Contains(chD0))
                        return (0, 0, 0);
                    if (Ch(chars, iOff) == ",")
                        iOff++;
                    return (chD0, order, Sub(chars, iOff));
                }
            }
            var chD = GetChD(arg);
            if (Len(chD) == 0)
                return (0, 0, 0);
            int cchChD = Len(chD);

            if (Truthy(Get(arg, "script")))
            {
                if (Truthy(Get(Get(arg, "script"), "high")))
                {
                    order = GetOrder(Get(Get(arg, "script"), "high"));
                    if (!Truthy(arg1) && !Truthy(order))
                    {
                        order = "1";
                        darg = "$f";
                    }
                }
                arg = Get(Get(arg, "script"), "base");
                script = true;
            }

            if (n == 1)
            {
                if (script)
                {
                    var chars = GetChars(arg);
                    darg = cchChD <= Len(chars) ? Sub(chars, cchChD) : "";
                }
                else
                {
                    object primes = 0;
                    if (Truthy(Get(arg, "primed")))
                    {
                        primes = Get(Get(arg, "primed"), "primes");
                        arg = Get(Get(arg, "primed"), "base");
                    }
                    var argChars = Str(Get(At(Get(arg, "atoms"), 0), "chars"));
                    int cch = Len(argChars);
                    var chD1 = chD;
                    int k = 1;
                    var wrtList = new List<object>();

                    for (int i = cchChD; cch > i && chD1 == chD; k++)
                    {
                        var chWrt = GetCh(argChars, i);
                        if (Truthy(primes) && i + Len(chWrt) >= cch)
                            chWrt += ProcessPrimes(primes);
                        wrtList.Add(chWrt);
                        i += Len(chWrt);
                        if (cch <= i)
                            break;
                        chD1 = GetCh(argChars, i);
                        i += cchChD;
                    }
                    darg = wrtList;
                    order = k.ToString(CultureInfo.InvariantCulture);
                }
            }
            else
            {
                var of0 = of[0] as List<object>;
                if (script)
                {
                    if (!Truthy(arg1) && of0.Count > 1)
                        arg1 = of0[1];
                    if (Truthy(arg1))
                    {
                        if (Truthy(Get(arg1, "script")) || Truthy(Get(arg1, "primed")) || Truthy(Get(arg1, "function")) || of0.Count > 2)
                            darg = "$f";
                        else if (Truthy(Get(arg1, "atoms")))
                            darg = GetCh(Str(Get(At(Get(arg1, "atoms"), 0), "chars")), 0);
                    }
                }
                else if (of0.Count > 0 && Truthy(Get(arg, "atoms")))
                {
                    arg = Get(arg, "atoms");
                    if (arg is List<object>)
                        arg = At(arg, 0);
                    var chars = Str(Get(arg, "chars"));
                    if (Len(chars) == cchChD)
                    {
                        if (of0.Count > 1 && Truthy(Get(of0[1], "bracketed")))
                        {
                            Set(Get(of0[1], "bracketed"), "arg", "f");
                            darg = "$f";
                        }
                    }
                    else if (Truthy(Get(arg, "funct")))
                    {
                        darg = "$f";
                    }
                    else
                    {
                        if (Ch(chars, cchChD) == ",")
                            cchChD++;
                        darg = GetCh(chars, cchChD);
                        if (of0.Count > 1)
                            darg = "$f";
                    }
                }
                else if (Truthy(Get(arg, "primed")))
                {
                    darg = "$f";
                }
            }
            return (chD, order, darg);
        }

        private static string GetSup(object value)
        {
            var script = Get(value, "script");
            if (!Truthy(script) || !Truthy(Get(script, "high")) || Truthy(Get(script, "low")))
                return "";
            value = Get(script, "high");
            if (Truthy(Get(value, "expr")))
                value = Get(value, "expr");
            if (value is List<object>)
                value = At(value, 0);
            if (value is List<object>)
                value = At(value, 0);
            if (!Truthy(Get(value, "atoms")))
                return "";
            value = Get(value, "atoms");
            if (value is List<object>)
                value = At(value, 0);
            return Truthy(Get(value, "chars")) ? Str(Get(value, "chars")) : "";
        }

        private static bool IsLeviCivita(object basE, object low, List<object> arr)
        {
            if (!Truthy(Get(basE, "atoms")) || arr == null || arr.Count != 4)
                return false;
            basE = Get(basE, "atoms");
            if (basE is List<object>)
                basE = At(basE, 0);
            if (!Eq(Get(basE, "chars"), "𝜀"))
                return false;
            if (Truthy(Get(low, "expr")))
                low = Get(low, "expr");
            if (low is List<object> lowList)
            {
                if (lowList.Count != 1)
                    return false;
                low = lowList[0];
            }
            if (low is List<object>)
                low = At(low, 0);
            if (!Truthy(Get(low, "atoms")))
                return false;
            low = Get(low, "atoms");
            if (low is List<object>)
                low = At(low, 0);
            if (!(Get(low, "chars") is string lowChars) || Len(lowChars) != 3 && Len(lowChars) != 6)
                return false;

            var leviChars = FoldMathItalics(lowChars);
            var civiChars = "";
            for (int i = 1; i < 4; i++)
                civiChars += GetSup(arr[i]);
            return FoldMathItalics(civiChars) == leviChars;
        }

        private static object GetTextContent(object ast)
        {
            if (Truthy(Get(ast, "expr")))
                ast = Get(ast, "expr");
            if (!(ast is List<object> list))
            {
                if (Truthy(Get(ast, "atoms")))
                    return Get(At(Get(ast, "atoms"), 0), "chars");
                if (Truthy(Get(ast, "operator")))
                    return Get(ast, "operator");
                if (Truthy(Get(ast, "number")))
                    return Get(ast, "number");
                return ast;
            }
            var chars = "";
            foreach (var node in list)
            {
                if (node is List<object>)
                    chars += Str(GetTextContent(node));
                else if (Truthy(Get(node, "atoms")))
                    chars += Str(Get(At(Get(node, "atoms"), 0), "chars"));
                else if (Truthy(Get(node, "operator")))
                    chars += Str(Get(node, "operator"));
                else if (Truthy(Get(node, "number")))
                    chars += Str(Get(node, "number"));
            }
            return chars;
        }

        private static string GetMathAlphanumerics(string str, string mathStyle)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Len(str); i++)
                sb.Append(GetMathAlphanumeric(Ch(str, i), mathStyle));
            return sb.ToString();
        }
    }
}
