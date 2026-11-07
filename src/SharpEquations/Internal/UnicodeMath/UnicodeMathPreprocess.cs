namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Js;
    using static UnicodeMathText;

    internal sealed partial class UnicodeMathTranslator
    {
        /// <summary>dsty of preprocess: the display mode, and the intent and arg passed down to the next node.</summary>
        private sealed class PreprocessStyle
        {
            public bool Display;
            public string Intent = "";
            public string Arg = "";
        }

        private static readonly Dictionary<string, string> Negs = new Dictionary<string, string>
        {
            ["<"] = "≮", ["="] = "≠", [">"] = "≯", ["~"] = "≁", ["∃"] = "∄", ["∈"] = "∉", ["∋"] = "∌", ["∼"] = "≁",
            ["≃"] = "≄", ["≅"] = "≇", ["≈"] = "≉", ["≍"] = "≭", ["≡"] = "≢", ["≤"] = "≰", ["≥"] = "≱", ["≶"] = "≸",
            ["≷"] = "≹", ["≺"] = "⊀", ["≻"] = "⊁", ["∥"] = "∦", ["⪯"] = "⪱", ["⪰"] = "⪲", ["⊂"] = "⊄", ["⊃"] = "⊅",
            ["⊆"] = "⊈", ["⊇"] = "⊉", ["⊑"] = "⋢", ["⊒"] = "⋣",
        };

        private static readonly Dictionary<string, string> MatrixIntents = new Dictionary<string, string>
        {
            ["⒨"] = ":parenthesized-matrix", ["⒱"] = ":determinant", ["⒩"] = ":normed-matrix",
            ["ⓢ"] = ":bracketed-matrix", ["Ⓢ"] = ":curly-braced-matrix",
        };

        private static readonly Dictionary<string, string> Brackets = new Dictionary<string, string>
        {
            ["⒨"] = "()", ["⒩"] = "‖‖", ["ⓢ"] = "[]", ["Ⓢ"] = "{}", ["⒱"] = "||",
        };

        private static readonly string[] EulerFunctions = { "det", "gcd", "inf", "lim", "lim inf", "lim sup", "max", "min", "Pr", "sup" };

        private object Preprocess(PreprocessStyle dsty, object uast, int? index = null, List<object> arr = null)
        {
            bool intentSup = false;

            if (uast is List<object> list)
            {
                for (int j = 0; j < list.Count; j++)
                {
                    list[j] = Preprocess(dsty, list[j], j, list);
                    if (Eq(Get(Get(list[j], "script"), "intent"), ":levi-civita"))
                        intentSup = true;
                    if (intentSup)
                        dsty.Intent = "sup";
                }
            }

            // a dispatcher with a small frame: the cases live in their own methods, since every level of nesting goes
            // through Preprocess several times and the frame of one large switch overflows the stack on .NET 10
            string key = K(uast);
            object value = V(uast);

            string intent = dsty.Intent;
            string arg = dsty.Arg;
            if (!Truthy(arg))
                arg = Get(uast, "arg") as string;
            if (!Truthy(intent))
                intent = Get(uast, "intent") as string;
            dsty.Intent = dsty.Arg = "";

            return key switch
            {
                "unicodemath" => PreprocessUnicodeMath(dsty, value),
                "expr" => O("expr", Preprocess(dsty, value)),
                "element" => O("element", Preprocess(dsty, value)),
                "array" => O("array", Preprocess(dsty, value)),
                "arows" => O("arows", Preprocess(dsty, value)),
                "arow" => PreprocessArow(dsty, (List<object>)value),
                "specialMatrix" => PreprocessSpecialMatrix(dsty, (List<object>)value, intent, arg),
                "matrix" => O("matrix", Preprocess(dsty, value)),
                "mrows" => O("mrows", Preprocess(dsty, value)),
                "mrow" => PreprocessMrow(dsty, (List<object>)value),
                "nary" => PreprocessNary(dsty, (JsObject)value, intent, arg, index, arr),
                "negatedoperator" => PreprocessNegatedOperator(value),
                "phantom" => PreprocessPhantom(dsty, value, intent, arg),
                "smash" => PreprocessSmash(dsty, value, intent, arg),
                "fraction" => PreprocessFraction(dsty, (JsObject)value, intent, arg, index, arr),
                "unicodefraction" => PreprocessUnicodeFraction(value),
                "atop" or "factorial" => PreprocessWrapped(dsty, key, value, intent, arg),
                "binom" => PreprocessBinom(dsty, (JsObject)value, intent, arg),
                "href" => PreprocessHref(dsty, value),
                "script" => PreprocessScript(dsty, (JsObject)value, intent, arg, index, arr),
                "enclosed" => PreprocessEnclosed(dsty, (JsObject)value, intent, arg),
                "abstractbox" => PreprocessAbstractBox(dsty, value),
                "hbrack" => PreprocessHbrack(dsty, value, intent, arg),
                "intend" => PreprocessIntend(dsty, value, intent, arg),
                "root" => PreprocessRoot(dsty, value, intent, arg),
                "sqrt" => PreprocessSqrt(dsty, value, intent, arg),
                "function" => PreprocessFunction(dsty, (JsObject)value, intent, arg),
                "sizeoverride" => PreprocessSizeOverride(dsty, value),
                "colored" or "bgcolored" => PreprocessColored(dsty, key, value),
                "primed" => PreprocessPrimed(dsty, uast, (JsObject)value, intent, arg, index, arr),
                "atoms" => PreprocessAtoms(dsty, value, intent, arg, index, arr),
                "diacriticized" => PreprocessDiacriticized(dsty, value),
                "spaces" => O("spaces", Preprocess(dsty, value)),
                "bracketedMatrix" => PreprocessBracketedMatrix(dsty, value, arg),
                "bracketed" => PreprocessBracketed(dsty, (JsObject)value, intent, arg),
                "operator" => PreprocessOperator(value, intent, arg, index, arr),
                "fontoverride" => PreprocessFontOverride(value),
                _ => uast
            };
        }

        private JsObject PreprocessUnicodeMath(PreprocessStyle dsty, object value) =>
            O("unicodemath", O(("content", Preprocess(dsty, Get(value, "content"))), ("eqnumber", Get(value, "eqnumber"))));

        private JsObject PreprocessArow(PreprocessStyle dsty, List<object> cells)
        {
            var currAcol = new List<object>();
            var rows = new List<object>();
            int i = 0;
            if (cells[0] == null)
                i = 1;
            for (; i < cells.Count; i++)
            {
                currAcol.Add(i % 2 == 0 ? O("aaligngroup", null) : O("aalignmark", null));
                currAcol.Add(Preprocess(dsty, cells[i]));
            }
            if (currAcol.Count > 0)
                rows.Add(O("acol", currAcol));
            return O("arow", rows);
        }

        private JsObject PreprocessSpecialMatrix(PreprocessStyle dsty, List<object> special, string intent, string arg)
        {
            var type = (string)special[2];
            object value = MatrixRows(ParseInt((string)special[0]), special[1] is string m ? ParseInt(m) : 0);
            if (EmitDefaultIntents && MatrixIntents.TryGetValue(type, out var matrixIntent) && Truthy(matrixIntent))
                intent = matrixIntent;
            if (type != "■")
            {
                var opOpen = Brackets[type].Substring(0, 1);
                var opClose = Brackets[type].Substring(1, 1);
                return O("bracketed", O(("open", opOpen), ("close", opClose), ("intent", intent), ("arg", arg),
                    ("content", O("matrix", Preprocess(dsty, value)))));
            }
            return O("matrix", Preprocess(dsty, value));
        }

        private JsObject PreprocessMrow(PreprocessStyle dsty, List<object> cells)
        {
            var mcols = new List<object>(cells.Count);
            foreach (var c in cells)
                mcols.Add(O("mcol", Preprocess(dsty, c)));
            return O("mrow", mcols);
        }

        private static JsObject PreprocessNegatedOperator(object value) =>
            Negs.TryGetValue(Str(value), out var neg) ? O("operator", neg) : O("negatedoperator", value);

        private JsObject PreprocessPhantom(PreprocessStyle dsty, object value, string intent, string arg) =>
            O("phantom", O(("mask", Get(value, "mask")), ("symbol", Get(value, "symbol")), ("intent", intent), ("arg", arg),
                ("of", Preprocess(dsty, Get(value, "of")))));

        private JsObject PreprocessSmash(PreprocessStyle dsty, object value, string intent, string arg) =>
            O("smash", O(("symbol", Get(value, "symbol")), ("intent", intent), ("arg", arg), ("of", Preprocess(dsty, Get(value, "of")))));

        private static object PreprocessUnicodeFraction(object value)
        {
            var symbol = Str(value);
            var fraction = UnicodeFractions.FirstOrDefault(f => f.Fraction == symbol);
            return fraction.Fraction == null ? value
                : O("fraction", O(("symbol", "⊘"), ("of", A(O("number", fraction.Numerator), O("number", fraction.Denominator)))));
        }

        /// <summary>atop and factorial: the content processed, with the intent and the argument set on it.</summary>
        private JsObject PreprocessWrapped(PreprocessStyle dsty, string key, object value, string intent, string arg)
        {
            value = Preprocess(dsty, value);
            if (Truthy(intent))
                Set(value, "intent", intent);
            if (Truthy(arg))
                Set(value, "arg", arg);
            return O(key, value);
        }

        private JsObject PreprocessBinom(PreprocessStyle dsty, JsObject binom, string intent, string arg)
        {
            binom["top"] = Preprocess(dsty, binom["top"]);
            binom["bottom"] = Preprocess(dsty, binom["bottom"]);
            if (!Truthy(intent) && EmitDefaultIntents)
            {
                var top = GetVariable(binom["top"]);
                var bottom = GetVariable(binom["bottom"]);
                intent = "binomial-coefficient(";
                if (Ch(top, 0) == "$")
                {
                    intent += "$t,";
                    Set(binom["top"], "arg", "t");
                }
                else
                {
                    intent += top + ",";
                }
                if (Ch(bottom, 0) == "$")
                {
                    intent += "$b)";
                    Set(binom["bottom"], "arg", "b");
                }
                else
                {
                    intent += bottom + ")";
                }
            }
            return O("bracketed", O(("intent", intent), ("arg", arg), ("open", "("), ("close", ")"),
                ("content", O("atop", A(binom["top"], binom["bottom"])))));
        }

        private JsObject PreprocessHref(PreprocessStyle dsty, object value)
        {
            var val = FoldMathItalics(Str(Get(value, "url")));
            if (!Truthy(Get(value, "name")))
                Set(value, "name", "");
            return O("href", O(("url", val), ("content", Preprocess(dsty, Get(value, "name")))));
        }

        private object PreprocessEnclosed(PreprocessStyle dsty, JsObject enclosed, string intent, string arg)
        {
            var of = enclosed["of"];
            var symbol = enclosed["symbol"] as string;
            if (of is List<object> ofList && Eq(Get(Get(ofList[0], "colored"), "color"), "#F01"))
            {
                ofList.Insert(0, O("operator", symbol));
                return ofList;
            }
            if (symbol == "Ⓐ" || symbol == "Ⓕ")
            {
                object val = "0";
                if (Truthy(of) && Truthy(Get(of, "expr")))
                {
                    var expr = (List<object>)Get(of, "expr");
                    int last = expr.Count - 1;
                    val = Get(At(expr[last], 0), "number");
                    if (last > 0)
                        val = "-" + Str(val);
                }
                return O("intend", O(("symbol", symbol), ("value", val), ("op", enclosed["mask"])));
            }
            if (symbol != null && string.CompareOrdinal(symbol, "╱") >= 0 && string.CompareOrdinal(symbol, "╳") <= 0)
                // ╱ strikes from bottom left to top right (MathML updiagonalstrike, 128), ╲ the other way (64)
                enclosed["mask"] = symbol == "╱" ? 143 : symbol == "╲" ? 79 : 207;
            return O("enclosed", O(("mask", enclosed["mask"]), ("symbol", symbol), ("intent", intent), ("arg", arg), ("of", Preprocess(dsty, of))));
        }

        private JsObject PreprocessAbstractBox(PreprocessStyle dsty, object value) =>
            O("abstractbox", O(("mask", Get(value, "mask")), ("of", Preprocess(dsty, Get(value, "of")))));

        private JsObject PreprocessHbrack(PreprocessStyle dsty, object value, string intent, string arg) =>
            O("hbrack", O(("intent", intent), ("arg", arg), ("bracket", Get(value, "bracket")), ("of", Preprocess(dsty, Get(value, "of")))));

        private object PreprocessIntend(PreprocessStyle dsty, object value, string intent, string arg)
        {
            var content = Get(value, "content");
            var op = Get(value, "op") as string;
            if (Truthy(Get(content, "expr")) && Length(Get(content, "expr")) > 1)
            {
                var c = Preprocess(dsty, V(content));
                if (op == "ⓘ")
                {
                    Set(c, "intent", Get(Get(value, "intent"), "text"));
                    if (Eq(Get(c, "intent"), "cardinality"))
                        Set(c, "intent", Str(Get(c, "intent")) + "($a)");
                    if (Truthy(arg))
                        Set(c, "arg", arg);
                }
                else
                {
                    Set(c, "arg", Get(Get(value, "intent"), "text"));
                    if (Truthy(intent))
                        Set(c, "intent", intent);
                }
                return c;
            }
            var text = Get(Get(value, "intent"), "text") as string;
            switch (op)
            {
                case "ⓘ":
                    dsty.Intent = text;
                    if (dsty.Intent == "cardinality")
                        dsty.Intent += "($a)";
                    if (Truthy(arg))
                        dsty.Arg = arg;
                    break;
                case "ⓐ":
                    dsty.Arg = text;
                    if (Truthy(intent))
                        dsty.Intent = intent;
                    break;
            }
            return Preprocess(dsty, V(content));
        }

        private JsObject PreprocessRoot(PreprocessStyle dsty, object value, string intent, string arg) =>
            O("root", O(("intent", intent), ("arg", arg), ("degree", Get(value, "degree")), ("of", Preprocess(dsty, Get(value, "of")))));

        private object PreprocessSqrt(PreprocessStyle dsty, object value, string intent, string arg)
        {
            value = Preprocess(dsty, value);
            if (value is List<object> radicand &&
                (Eq(Get(Get(radicand[0], "colored"), "color"), "#F01") || Truthy(Get(radicand[0], "intend")) && radicand.Count == 1))
            {
                radicand.Insert(0, O("operator", "√"));
                return radicand;
            }
            if (Truthy(intent))
                Set(value, "intent", intent);
            if (Truthy(arg))
                Set(value, "arg", arg);
            return O("sqrt", value);
        }

        private JsObject PreprocessSizeOverride(PreprocessStyle dsty, object value) =>
            O("sizeoverride", O(("size", Get(value, "size")), ("of", Preprocess(dsty, Get(value, "of")))));

        /// <summary>colored and bgcolored.</summary>
        private JsObject PreprocessColored(PreprocessStyle dsty, string key, object value) =>
            O(key, O(("color", FoldMathItalics(Str(Get(value, "color")))), ("of", Preprocess(dsty, Get(value, "of")))));

        private JsObject PreprocessDiacriticized(PreprocessStyle dsty, object value) =>
            O("diacriticized", O(("base", Preprocess(dsty, Get(value, "base"))), ("diacritics", Get(value, "diacritics"))));

        private JsObject PreprocessBracketedMatrix(PreprocessStyle dsty, object value, string arg)
        {
            var type = (string)Get(value, "type");
            var opOpen = Brackets[type].Substring(0, 1);
            var opClose = Brackets[type].Substring(1, 1);
            if (!Truthy(Get(value, "intent")) && EmitDefaultIntents && MatrixIntents.TryGetValue(type, out var matrixIntent))
                Set(value, "intent", matrixIntent);
            return O("bracketed", O(("open", opOpen), ("close", opClose), ("intent", Get(value, "intent")), ("arg", arg),
                ("content", Preprocess(dsty, Get(value, "content")))));
        }

        private static JsObject PreprocessOperator(object value, string intent, string arg, int? index, List<object> arr)
        {
            if (value is string op && Len(op) > 1 && op[0] == '\\')
            {
                value = Sub(op, 1, 1 + 1);
                intent = ":text";
            }

            if (Eq(value, "<"))
            {
                value = "&lt;";
            }
            else if (Eq(value, "…") && index > 0)
            {
                var o = Get(arr[index.Value - 1], "operator");
                if (Truthy(o) && Eq(Get(o, "content"), "+"))
                    value = "⋯";
            }
            return O("operator", O(("intent", intent), ("arg", arg), ("content", value)));
        }

        private static JsObject PreprocessFontOverride(object value)
        {
            var style = MathStyles[CodePointAt((string)Get(value, "font"), 0) - 0x61];
            var text = GetTextContent(Get(value, "of"));
            text = GetMathAlphanumerics(FoldMathItalics(Str(text)), style);
            return O("atoms", A(O("chars", text)));
        }

        private JsObject PreprocessNary(PreprocessStyle dsty, JsObject value, string intent, string arg, int? index, List<object> arr)
        {
            var options = NaryOptions(Get(value, "mask"));
            value = value.Clone();
            var script = (JsObject)Get(value["limits"], "script");

            if (options.Contains("nLimitsOpposite"))
            {
                if (script.Has("low") && script.Has("high"))
                {
                    var tmp = script["high"];
                    script["high"] = script["low"];
                    script["low"] = tmp;
                }
                else if (script.Has("low"))
                {
                    script["high"] = script["low"];
                    script.Remove("low");
                }
                else if (script.Has("high"))
                {
                    script["low"] = script["high"];
                    script.Remove("high");
                }
            }
            if (options.Contains("nShowLowLimitPlaceHolder") && !script.Has("low"))
                script["low"] = O("operator", "⬚");
            if (options.Contains("nShowUpLimitPlaceHolder") && !script.Has("high"))
                script["high"] = O("operator", "⬚");

            if (options.Contains("fDontGrowWithContent"))
                value["naryand"] = O("smash", O(("symbol", "⬆"), ("of", value["naryand"])));

            var op = V(script["base"]) as string;
            bool isInt = IsIntegral(op);

            if (options.Contains("nLimitsUnderOver"))
            {
                script["type"] = "abovebelow";
            }
            else if (options.Contains("nLimitsSubSup"))
            {
                script["type"] = "subsup";
            }
            else if (options.Contains("nUpperLimitAsSuperScript"))
            {
                if (!Eq(script["type"], "subsup") && script.Has("high"))
                {
                    var high = script["high"];
                    script.Remove("high");
                    script["base"] = O("script", O(("type", "subsup"), ("base", script["base"]), ("high", high)));
                }
            }
            else if (dsty.Display && !isInt)
            {
                script["type"] = "abovebelow";
            }

            if (Truthy(Get(value["naryand"], "binom")) && arr != null && index < arr.Count - 1 && arr[index.Value + 1] is List<object> following)
            {
                following.Insert(0, value["naryand"]);
                value["naryand"] = following;
                arr.RemoveAt(index.Value + 1);
            }
            else if (isInt && arr != null && index < arr.Count && At(arr, index.Value + 1) is List<object> nextList)
            {
                var next = At(nextList, 0);
                if (Truthy(Get(next, "primed")))
                    next = Get(Get(next, "primed"), "base");
                var atoms = Get(next, "atoms");
                if (atoms is List<object> && Truthy(Get(At(atoms, 0), "chars")) && Ch(Str(Get(At(atoms, 0), "chars")), 0) == "ⅆ")
                {
                    if (value["naryand"] is List<object> naryandList)
                        naryandList.Add(nextList[0]);
                    else
                        value["naryand"] = A(value["naryand"], nextList[0]);
                    arr.RemoveAt(index.Value + 1);
                }
            }
            value["limits"] = Preprocess(dsty, value["limits"]);
            value["naryand"] = Preprocess(dsty, value["naryand"]);
            script = (JsObject)Get(value["limits"], "script");

            bool isArr = value["naryand"] is List<object>;
            if (isArr && value["naryand"] is List<object> n1 && n1.Count == 1 &&
                Truthy(Get(n1[0], "bracketed")) && !Truthy(Get(Get(n1[0], "bracketed"), "open")))
            {
                var content = Get(Get(n1[0], "bracketed"), "content");
                if (Get(content, "expr") is List<object> expr && expr.Count == 1)
                    value["naryand"] = expr[0];
                isArr = false;
            }
            if (!Truthy(intent) && EmitDefaultIntents)
            {
                var arg0 = GetScript(script["low"], "$l");
                var arg1 = GetScript(script["high"], "$h");
                intent = ":largeop(" + arg0 + "," + arg1 + ",$naryand)";
                Set(value["naryand"], "arg", "naryand");
                if (arg0 == "$l")
                    Set(script["low"], "arg", Sub(arg0, 1));
                if (arg1 == "$h")
                    Set(script["high"], "arg", Sub(arg1, 1));
            }
            return O("nary", O(("mask", value["mask"]), ("limits", value["limits"]), ("intent", intent), ("arg", arg), ("naryand", value["naryand"])));
        }

        private JsObject PreprocessFraction(PreprocessStyle dsty, JsObject value, string intent, string arg, int? index, List<object> arr)
        {
            var of = (List<object>)value["of"];
            if (Eq(value["symbol"], "/") && !Truthy(intent) && !Truthy(arg) && EmitDefaultIntents)
            {
                var (chDifferential0, order0, arg0) = GetDifferentialInfo(of, 0);
                if (Truthy(chDifferential0))
                {
                    var (chDifferential1, order1, wrt) = GetDifferentialInfo(of, 1);
                    if (Eq(chDifferential0, chDifferential1) && Eq(order0, order1))
                    {
                        if (!Truthy(arg0))
                        {
                            if (of[1] is List<object> denominator)
                            {
                                var den0 = denominator[0];
                                var f = Get(Get(den0, "function"), "f");
                                if (Truthy(Get(den0, "function")) && Truthy(Get(f, "atoms")) && Truthy(Get(Get(f, "atoms"), "chars")))
                                {
                                    var functionArg = Get(Get(den0, "function"), "of");
                                    denominator[0] = O("atoms", O("chars", Str(Get(Get(f, "atoms"), "chars")).Replace(",", "")));
                                    arr?.Insert(index.Value + 1, functionArg);
                                }
                            }
                            if (of[0] is List<object> numerator && Eq(order0, 1) && Truthy(Get(numerator[0], "script")))
                                arg0 = "$f";
                            if (arr != null && index + 1 < arr.Count)
                            {
                                var ele = arr[index.Value + 1];
                                if (Truthy(Get(ele, "operator")) && Eq(Get(ele, "operator"), "\u2061"))
                                    ele = At(arr, index.Value + 2);
                                if (ele is List<object> eleList)
                                {
                                    if (eleList.Count == 1 && Truthy(Get(eleList[0], "atoms")))
                                        Set(Get(eleList[0], "atoms"), "arg", "$f");
                                    else
                                        eleList.Insert(0, O("arg", "f"));
                                    arg0 = "$f";
                                }
                            }
                        }
                        var arg0s = Str(arg0);
                        var order0s = Str(order0);
                        if ((Len(arg0s) > 0 && Ch(arg0s, 0) == "$") || (Len(order0s) > 0 && Ch(order0s, 0) == "$"))
                        {
                            var ofDiff = of;
                            var ofDiff0 = ofDiff[0] as List<object>;
                            var s = At(ofDiff[0], 0);
                            if (Truthy(Get(s, "script")))
                            {
                                var sScript = Get(s, "script");
                                if (Get(sScript, "high") is List<object> high)
                                {
                                    if ((Len(order0s) > 0 && Ch(order0s, 0) == "$"))
                                    {
                                        if (Truthy(Get(high[0], "bracketed")))
                                            Set(Get(high[0], "bracketed"), "arg", Sub(order0s, 1));
                                        else if (Truthy(Get(high[0], "atoms")))
                                            Set(Get(high[0], "atoms"), "arg", Sub(order0s, 1));
                                    }
                                    else if ((Len(arg0s) > 0 && Ch(arg0s, 0) == "$"))
                                    {
                                        if (ofDiff0.Count == 1)
                                            Set(sScript, "arg", Sub(arg0s, 1));
                                        else if (Truthy(Get(ofDiff0[1], "primed")))
                                            Set(Get(ofDiff0[1], "primed"), "arg", Sub(arg0s, 1));
                                    }
                                }
                                else if (ofDiff0.Count == 2)
                                {
                                    if (Truthy(Get(ofDiff0[1], "script")))
                                        Set(Get(ofDiff0[1], "script"), "arg", Sub(arg0s, 1));
                                    else if (Truthy(Get(ofDiff0[1], "primed")))
                                        Set(Get(ofDiff0[1], "primed"), "arg", Sub(arg0s, 1));
                                    else if (Truthy(Get(ofDiff0[1], "function")))
                                        Set(ofDiff0[1], "arg", Sub(arg0s, 1));
                                }
                                else if (Truthy(Get(sScript, "low")))
                                {
                                    Set(sScript, "arg", Sub(arg0s, 1));
                                }
                                if (ofDiff0.Count == 3 && Truthy(Get(ofDiff0[1], "atoms")) && Truthy(Get(ofDiff0[2], "bracketed")))
                                    of[0] = A(s, A(O("arg", Sub(arg0s, 1)), ofDiff0[1], ofDiff0[2]));
                                else if (ofDiff0.Count == 2)
                                    of[0] = A(s, ofDiff0[1]);
                            }
                            else if (Truthy(Get(s, "primed")))
                            {
                                Set(Get(s, "primed"), "arg", Sub(arg0s, 1));
                            }
                            else if (Truthy(Get(s, "function")))
                            {
                                Set(s, "arg", Sub(arg0s, 1));
                            }
                            else if (ofDiff0 != null && ofDiff0.Count == 2 && Truthy(Get(s, "atoms")) && Truthy(Get(ofDiff0[1], "bracketed")))
                            {
                                var chars = Str(Get(At(Get(s, "atoms"), 0), "chars"));
                                var ch = GetCh(chars, 0);
                                if (Len(chars) > Len(ch))
                                {
                                    of[0] = A(O("atoms", A(O("chars", ch))), A(O("arg", Sub(arg0s, 1)),
                                        O("atoms", A(O("chars", GetCh(chars, Len(ch))))), ofDiff0[1]));
                                }
                            }
                        }
                        intent = "∂𝜕".Contains(Str(chDifferential0)) ? "partial-derivative" : "derivative";
                        intent += "(" + Str(order0) + "," + Str(arg0) + "," + Str(wrt) + ")";
                        return O("fraction", O(("symbol", value["symbol"]), ("intent", intent), ("of", Preprocess(dsty, of))));
                    }
                }
            }
            return O("fraction", O(("symbol", value["symbol"]), ("intent", intent), ("arg", arg), ("of", Preprocess(dsty, of))));
        }

        private object PreprocessScript(PreprocessStyle dsty, JsObject value, string intent, string arg, int? index, List<object> arr)
        {
            Set(value["base"], "inscript", true);
            var ret = O(("type", value["type"]), ("base", Preprocess(dsty, value["base"])));
            if (Truthy(intent))
                ret["intent"] = intent;
            if (Truthy(arg))
                ret["arg"] = arg;
            if (Truthy(value["arg"]))
                ret["arg"] = value["arg"];

            object basE;
            object val;
            switch (value["type"] as string)
            {
                case "subsup":
                {
                    basE = DropSingletonLists(value["base"]);
                    if (index > 0)
                    {
                        var intend = Get(arr[index.Value - 1], "intend");
                        if (Truthy(intend) && Eq(Get(intend, "symbol"), "Ⓐ"))
                        {
                            Set(basE, "selanchor", Get(intend, "value"));
                            ret["base"] = basE;
                        }
                    }

                    if (Truthy(Get(basE, "intend")) && Eq(Get(Get(basE, "intend"), "op"), "Ⓐ"))
                    {
                        val = Get(Get(Get(basE, "intend"), "intent"), "text");
                        ret["base"] = arr[index.Value - 1];
                        ret["high"] = value["high"];
                        ret["low"] = value["low"];
                        arr[index.Value - 1] = O("intend", O("anchor", val));
                    }
                    else if (Truthy(Get(basE, "primed")))
                    {
                        var primes = O("operator", ProcessPrimes(Get(Get(basE, "primed"), "primes")));
                        if (value.Has("low"))
                            ret["low"] = Preprocess(dsty, value["low"]);

                        if (value.Has("high"))
                            ret["high"] = A(primes, Preprocess(dsty, value["high"]));
                        else
                            ret["high"] = primes;
                        basE = ret["base"] = Get(Get(basE, "primed"), "base");
                    }
                    else
                    {
                        if (value.Has("low"))
                        {
                            ret["low"] = Preprocess(dsty, value["low"]);
                            if (IsLeviCivita(basE, ret["low"], arr))
                                ret["intent"] = ":levi-civita";
                        }
                        if (value.Has("high"))
                        {
                            ret["high"] = Preprocess(dsty, value["high"]);
                            if (IsTranspose(ret["high"]))
                                Set(Get(At(ret["high"], 0), "atoms"), "intent", "transpose");
                        }
                    }
                    if (K(basE) != "atoms" || Get(Get(basE, "atoms"), "funct") != null)
                        break;

                    var baseAtoms = Get(basE, "atoms");
                    var n = Length(baseAtoms) is int count && baseAtoms is List<object> ? count : (int?)null;
                    if (n == null)
                        break;
                    if (!(Get(DropSingletonLists(ret["base"]), "atoms") is List<object> retBaseAtoms))
                        break;
                    var ret0 = new List<object>();
                    if (n > 1)
                    {
                        if (Truthy(Get(At(baseAtoms, 0), "spaces")))
                            ret0.Add(At(baseAtoms, 0));
                        retBaseAtoms.RemoveAt(0);
                        n--;
                    }
                    var str = Get(At(baseAtoms, n.Value - 1), "chars") as string;
                    if (str == null)
                        break;
                    int cch = Len(str);
                    var fn = FoldMathItalics(str);
                    if (IsFunctionName(fn))
                    {
                        if (Len(fn) < cch)
                            Set(retBaseAtoms[0], "chars", fn);
                        break;
                    }
                    // the end character: one code point
                    int cchCh = 1;

                    if (cch > cchCh)
                    {
                        Set(retBaseAtoms[0], "chars", Sub(str, cch - cchCh));
                        Delete(ret["base"], "selanchor");
                        ret0.Add(O("atoms", O("chars", Sub(str, 0, 0 + cch - cchCh))));
                        ret0.Add(O("script", ret));
                        return ret0;
                    }
                    else if (ret0.Count > 0)
                    {
                        ret0.Add(O("script", ret));
                        return ret0;
                    }
                    if (Truthy(ret["intent"]) || Ch(str, 0) != "ⅅ" && !str.StartsWith("𝜕", StringComparison.Ordinal) || !EmitDefaultIntents)
                        break;

                    object order = "";
                    if (Truthy(ret["high"]))
                    {
                        if (Ch(str, 0) != "ⅅ")
                            break;
                        if (ret["high"] is List<object> || Truthy(Get(ret["high"], "expr")))
                            order = GetOrder(ret["high"]);
                    }
                    int letters = 1;
                    var chars1 = "";
                    if (Truthy(ret["low"]))
                    {
                        var chars = GetChars(At(ret["low"], 0));
                        int cchLow = Len(chars);
                        for (int i = 0; i < cchLow;)
                        {
                            var ch = GetCh(chars, i);
                            chars1 += ch;
                            i += Len(ch);
                            if (i < Len(chars) && !IsPrime(Ch(chars, i)))
                            {
                                chars1 += ",";
                                letters++;
                            }
                        }
                    }
                    if (!Truthy(order))
                        order = letters;
                    var darg = "";
                    if (arr.Count - 1 > index)
                    {
                        if (arr.Count - 2 == index || !Truthy(Get(arr[index.Value + 2], "bracketed")))
                        {
                            Set(arr[index.Value + 1], "arg", "f");
                            darg = "$f";
                        }
                        else
                        {
                            arr[index.Value + 1] = A(O("arg", "f"), arr[index.Value + 1], arr[index.Value + 2]);
                            darg = "$f";
                            arr.RemoveAt(index.Value + 2);
                            if (Len(chars1) == 0)
                            {
                                var content = Get(Get(((List<object>)arr[index.Value + 1])[2], "bracketed"), "content");
                                if (Truthy(Get(content, "expr")))
                                    content = At(Get(content, "expr"), 0);
                                chars1 = GetChars(content);
                            }
                        }
                    }
                    if (Len(darg) > 0)
                    {
                        ret["intent"] = Ch(str, 0) == "ⅅ" ? "derivative" : "partial-derivative";
                        ret["intent"] = Str(ret["intent"]) + "(" + Str(order) + "," + darg + "," + chars1 + ")";
                    }
                    break;
                }

                case "pre":
                    if (value.Has("prelow"))
                        ret["prelow"] = Preprocess(dsty, value["prelow"]);
                    if (value.Has("prehigh"))
                    {
                        ret["prehigh"] = Preprocess(dsty, value["prehigh"]);
                        if (IsTranspose(ret["prehigh"]))
                            Set(Get(At(ret["prehigh"], 0), "atoms"), "intent", "transpose");
                    }
                    basE = DropSingletonLists(ret["base"]);
                    if (Truthy(Get(basE, "script")) && Eq(Get(Get(basE, "script"), "type"), "subsup"))
                    {
                        var baseScript = Get(basE, "script");
                        if (Has(baseScript, "low"))
                            ret["low"] = Preprocess(dsty, Get(baseScript, "low"));
                        if (Has(baseScript, "high"))
                            ret["high"] = Preprocess(dsty, Get(baseScript, "high"));
                        ret["base"] = Preprocess(dsty, Get(baseScript, "base"));
                    }
                    break;

                case "abovebelow":
                    if (index > 0)
                    {
                        var intend = Get(arr[index.Value - 1], "intend");
                        if (Truthy(intend) && Eq(Get(intend, "symbol"), "Ⓐ"))
                            Set(ret["base"], "selanchor", Get(intend, "value"));
                    }
                    if (value.Has("low"))
                        ret["low"] = Preprocess(dsty, value["low"]);
                    if (value.Has("high"))
                        ret["high"] = Preprocess(dsty, value["high"]);
                    if (Eq(Get(value["base"], "operator"), "\u2061") && index > 0 && Truthy(Get(arr[index.Value - 1], "atoms")) && arr.Count >= 3)
                    {
                        var atoms = Get(arr[index.Value - 1], "atoms");
                        if (atoms is List<object> atomList && Truthy(Get(atomList[0], "chars")))
                        {
                            ret["base"] = O("chars", FoldMathItalics(Str(Get(atomList[0], "chars"))));
                            ret["intent"] = ":function";
                            arr[index.Value - 1] = O("script", ret);
                            return O("operator", "\u2061");
                        }
                    }
                    break;

                default:
                    throw new FormatException("invalid or missing script type");
            }
            return O("script", ret);
        }

        private object PreprocessFunction(PreprocessStyle dsty, JsObject value, string intent, string arg)
        {
            var valuef = Clone(value["f"]);
            if (Truthy(Get(value["f"], "script")))
            {
                var s = (JsObject)Get(valuef, "script");
                var f = Get(Get(s["base"], "atoms"), "chars") as string;
                if (dsty.Display && Eq(s["type"], "subsup") && Truthy(s["low"]) && EulerFunctions.Contains(f))
                {
                    if (!Truthy(s["high"]))
                        s["type"] = "abovebelow";
                    else
                        s = O(("base", O("script", O(("base", s["base"]), ("type", s["type"]), ("high", s["high"])))), ("type", "abovebelow"), ("low", s["low"]));
                }
                Set(valuef, "script", s);
            }
            var ofFunc = Preprocess(dsty, value["of"]);
            if (ofFunc is List<object> ofList)
            {
                var x = At(ofList, 0);
                if (x is List<object>)
                    x = At(x, 0);

                if (Truthy(x) && Truthy(Get(x, "atoms")))
                {
                    var ch = Get(At(Get(x, "atoms"), 0), "chars") as string;
                    if (Ch(ch, 0) == "⒡")
                    {
                        if (Len(ch) == 1)
                            ((List<object>)ofList[0]).RemoveAt(0);
                        else
                            Set(At(Get(ofList[0], "atoms"), 0), "chars", Sub(ch, 1));
                        ofFunc = O("bracketed", O(("open", "("), ("close", ")"), ("content", ofFunc)));
                    }
                }
                else if (Truthy(x) && Truthy(Get(x, "bracketed")) && !Truthy(Get(Get(x, "bracketed"), "open")))
                {
                    var content = Get(Get(x, "bracketed"), "content");
                    if (Get(content, "expr") is List<object> expr && expr.Count == 1)
                    {
                        var inner = expr[0];
                        if (inner is List<object> innerList && innerList.Count == 1)
                            ofFunc = innerList[0];
                    }
                }
            }
            var extra = new List<object>();
            var atoms = Get(valuef, "atoms");
            if (Truthy(atoms) && Truthy(Get(atoms, "chars")))
            {
                Set(atoms, "chars", Str(Get(atoms, "chars")).Replace(",", ""));
                if (Ch((string)Get(atoms, "chars"), 0) == "ⅆ")
                {
                    Set(atoms, "chars", Sub(((string)Get(atoms, "chars")), 1));
                    extra.Add(O("atoms", O("chars", "ⅆ")));
                }
            }
            var ret = O("function", O(("f", Preprocess(dsty, valuef)), ("intent", intent), ("arg", arg), ("of", ofFunc)));
            if (extra.Count > 0)
            {
                extra.Add(ret);
                return extra;
            }
            return ret;
        }

        private object PreprocessPrimed(PreprocessStyle dsty, object uast, JsObject value, string intent, string arg, int? index, List<object> arr)
        {
            if (Truthy(value["arg"]))
                arg = (string)value["arg"];
            var basE = Preprocess(dsty, value["base"]);
            if (!Truthy(Get(uast, "inscript")) && Get(basE, "atoms") is List<object> atoms && Truthy(Get(atoms[0], "chars")))
            {
                var chars = (string)Get(atoms[0], "chars");
                int cch = Len(chars);
                int cchCh = 1;

                if (cch > cchCh)
                {
                    Set(atoms[0], "chars", Sub(chars, cch - cchCh));
                    return A(O("atoms", O("chars", Sub(chars, 0, 0 + cch - cchCh))),
                        O("primed", O(("base", basE), ("intent", intent), ("arg", arg), ("primes", value["primes"]))));
                }
                if (intent == "derivative")
                {
                    intent = "derivative(" + FromCodePoint((int)value["primes"] + 0x30) + "," + chars;
                    if (arr != null && index < arr.Count - 1 && Truthy(Get(arr[index.Value + 1], "bracketed")))
                    {
                        var val = Get(Get(arr[index.Value + 1], "bracketed"), "content");
                        var wrt = "";
                        if (Get(val, "expr") is List<object>)
                            val = At(Get(val, "expr"), 0);
                        if (val is List<object>)
                            val = At(val, 0);
                        if (Truthy(Get(val, "primed")))
                        {
                            wrt = ProcessPrimes(Get(Get(val, "primed"), "primes"));
                            val = Get(Get(val, "primed"), "base");
                        }
                        if (Get(val, "atoms") is List<object> valAtoms)
                        {
                            wrt = Str(Get(valAtoms[0], "chars")) + wrt;
                            intent += "(" + wrt + ")," + wrt + ")";
                        }
                    }
                    else
                    {
                        intent += ",)";
                    }
                }
            }
            return O("primed", O(("base", basE), ("intent", intent), ("arg", arg), ("primes", value["primes"])));
        }

        private JsObject PreprocessAtoms(PreprocessStyle dsty, object value, string intent, string arg, int? index, List<object> arr)
        {
            if (!Truthy(Get(value, "funct")))
            {
                string chars = null;
                var darg = "";

                if (value is List<object> list && IsCharsButNotFunction(list[0]))
                {
                    chars = ItalicizeCharacters((string)Get(list[0], "chars"));
                    Set(list[0], "chars", chars);
                }
                else if (IsCharsButNotFunction(value))
                {
                    chars = ItalicizeCharacters((string)Get(value, "chars"));
                    Set(value, "chars", chars);
                }
                if (Truthy(chars) && Ch(chars, 0) == "ⅅ" && Len(chars) > 1 && !Truthy(intent) && EmitDefaultIntents)
                {
                    if (arr != null && arr.Count - 1 > index && Truthy(Get(arr[index.Value + 1], "bracketed")))
                    {
                        var val = Get(Get(Get(arr[index.Value + 1], "bracketed"), "content"), "expr");
                        if (Truthy(val) && val is List<object>)
                            val = At(val, 0);
                        darg = GetChars(val);
                        if (darg == "undefined")
                        {
                            darg = "$x";
                            if (val is List<object>)
                                val = At(val, 0);
                            Set(val, "arg", "x");
                        }
                    }
                    intent = "derivative(1," + Sub(chars, 1) + "," + darg + ")";
                }
            }
            if (!Truthy(arg) && Truthy(Get(value, "arg")))
                arg = Get(value, "arg") as string;
            value = Preprocess(dsty, value);
            if (Truthy(intent))
                Set(value, "intent", intent);
            if (Truthy(arg))
                Set(value, "arg", arg);
            return O("atoms", value);
        }

        private object PreprocessBracketed(PreprocessStyle dsty, JsObject value, string intent, string arg)
        {
            var separated = Get(value["content"], "separated");
            if (Truthy(separated))
            {
                var sep = Get(separated, "separator");
                if (Eq(value["open"], "⟨") && Eq(sep, "│") && Eq(value["close"], "⟩"))
                    sep = "|";
                value["content"] = O("separated", O(("separator", sep), ("of", Preprocess(dsty, Get(separated, "of")))));
            }
            else
            {
                var valueIntent = value["intent"] as string;
                if (Truthy(valueIntent) && valueIntent.EndsWith("interval", StringComparison.Ordinal) &&
                    value["content"] is List<object> content && content.Count == 3)
                {
                    var arg0 = GetIntervalArg(content, 0);
                    var arg1 = GetIntervalArg(content, 2);
                    if (EmitDefaultIntents)
                    {
                        value["intent"] = valueIntent + "(" + arg0 + "," + arg1 + ")";
                        if (!Truthy(intent))
                            intent = (string)value["intent"];
                    }
                    else
                    {
                        intent = "";
                        value["intent"] = "";
                    }
                    value["content"] = O("expr", A(GetIntervalEndPoint(arg0, content[0]), O("operator", ","), GetIntervalEndPoint(arg1, content[2])));
                    value["content"] = Preprocess(dsty, value["content"]);
                }
                else
                {
                    value["content"] = Preprocess(dsty, value["content"]);
                    // the invisible brackets of a TeX group ({...}), not \left. with a closing fence
                    if (FTeX && !Truthy(value["open"]) && !Truthy(value["close"]))
                        return value["content"];
                    if (!Truthy(value["intent"]) && Eq(value["open"], "{") && !Truthy(value["close"]) &&
                        Get(value["content"], "expr") is List<object> expr && Truthy(Get(expr[0], "array")))
                    {
                        value["intent"] = ":piecewise";
                    }
                    if (!Truthy(arg) && Truthy(value["arg"]))
                        arg = value["arg"] as string;
                    if (!Truthy(intent) && Truthy(value["intent"]))
                    {
                        intent = value["intent"] as string;
                        if (intent == "ⓒ")
                        {
                            var arg0 = GetAbsArg(value["content"]);
                            intent += "(" + arg0 + ")";
                            if (arg0 == "$a")
                                Set(Get(value["content"], "expr"), "arg", "a");
                        }
                    }
                }
            }
            return O("bracketed", O(("open", value["open"]), ("close", value["close"]), ("arg", arg), ("intent", intent), ("content", value["content"])));
        }
    }
}
