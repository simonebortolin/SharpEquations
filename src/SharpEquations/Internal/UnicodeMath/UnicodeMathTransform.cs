namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using static Js;
    using static UnicodeMathText;

    internal sealed partial class UnicodeMathTranslator
    {
        private const string OverBrackets = "⎴⏜⏞⏠¯";
        private const string StretchyOperators = "←→↔⇐⇒⇔↩↪↼⇀↽⇁⊢⊣⟵⟶⟷⟸⟹⟺↦⊨";

        private static readonly string[] IndicDigits = { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };

        private static readonly Dictionary<string, string> DoubleStruckUsTech = new Dictionary<string, string>
        {
            ["ⅅ"] = "𝐷", ["ⅆ"] = "𝑑", ["ⅇ"] = "𝑒", ["ⅈ"] = "𝑖", ["ⅉ"] = "𝑗",
        };

        private static readonly Dictionary<string, int> LetterLikeSymbols = new Dictionary<string, int>
        {
            ["ℂ"] = 6, ["ℊ"] = 3, ["ℋ"] = 3, ["ℌ"] = 5, ["ℍ"] = 6, ["ℎ"] = 1, ["ℐ"] = 3, ["ℑ"] = 5, ["ℒ"] = 3, ["ℕ"] = 6,
            ["ℙ"] = 6, ["ℚ"] = 6, ["ℛ"] = 3, ["ℜ"] = 5, ["ℝ"] = 6, ["ℤ"] = 6, ["ℨ"] = 5, ["ℬ"] = 3, ["ℭ"] = 5, ["ℯ"] = 3,
            ["ℰ"] = 3, ["ℱ"] = 3, ["ℳ"] = 3, ["ℴ"] = 3,
        };

        private static JsObject NoAttr(object v) => O(("attributes", new JsObject()), ("content", v));

        private static JsObject WithAttrs(JsObject attrs, object v) => O(("attributes", attrs), ("content", v));

        private static string DoublestruckChar(string value) => DoubleStruckUsTech.TryGetValue(value, out var ch) ? ch : null;

        private static bool IsAsciiAlphabetic(string ch) => ch != null && ch.Any(c => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z');

        private static bool IsGreek(string ch) => InRange("Α", ch, "ϵ");

        private static bool IsLcGreek(string ch) => InRange("α", ch, "ϵ");

        private static bool IsDoubleStruck(string ch) => InRange("ⅅ", ch, "ⅉ");

        private JsObject GetAttrs(object value, string deflt = null)
        {
            var attrs = new JsObject();
            if (Truthy(selanchor))
            {
                attrs["selanchor"] = selanchor;
                selanchor = null;
            }
            if (Truthy(Get(value, "selanchor")))
                attrs["selanchor"] = Get(value, "selanchor");
            if (Truthy(selfocus))
            {
                attrs["selfocus"] = selfocus;
                selfocus = null;
            }
            if (Truthy(Get(value, "selfocus")))
                attrs["selfocus"] = Get(value, "selfocus");
            if (Truthy(Get(value, "arg")))
                attrs["arg"] = Get(value, "arg");
            if (Truthy(Get(value, "intent")))
                attrs["intent"] = Get(value, "intent");
            else if (Truthy(deflt) && EmitDefaultIntents)
                attrs["intent"] = deflt;
            return attrs;
        }

        private static JsObject GetAttrsDoublestruck(string ch, string str)
        {
            var attrs = new JsObject();
            if (ch != str && EmitDefaultIntents)
                attrs["intent"] = str;
            if (string.CompareOrdinal(ch, "z") <= 0)
                attrs["mathvariant"] = "normal";
            return attrs;
        }

        private object GetScriptArg(int dsty, object value)
        {
            var arg = Get(value, "arg");
            var intent = Get(value, "intent");

            if (!Truthy(arg) && value is List<object> list && Truthy(Get(list[0], "bracketed")))
                arg = Get(Get(list[0], "bracketed"), "arg");

            if (!Truthy(arg) && !Truthy(intent))
                return Transform(dsty, DropOutermostParens(value));

            value = DropOutermostParens(value);
            if (value is List<object> valueList && Truthy(Get(valueList[0], "expr")))
            {
                if (Truthy(arg))
                    Set(Get(valueList[0], "expr"), "arg", arg);
                if (Truthy(intent))
                    Set(Get(valueList[0], "expr"), "intent", intent);
            }
            else
            {
                if (Truthy(arg))
                    Set(value, "arg", arg);
                if (Truthy(intent))
                    Set(value, "intent", intent);
            }
            return Transform(dsty, value);
        }

        private static string CheckCardinalityIntent(string intent, object miContent)
        {
            if (Truthy(intent))
            {
                var s = "";
                switch (intent[0])
                {
                    case '⒜':
                        s = "absolute-value";
                        break;
                    case 'ⓒ':
                        s = "cardinality";
                        break;
                }
                if (Len(s) > 0)
                    intent = s + Sub(intent, 1);
                else
                    s = "cardinality";
                if (Truthy(miContent) && intent.StartsWith(s, StringComparison.Ordinal))
                    intent = s + "(" + Str(miContent) + ")";
            }
            return intent;
        }

        private static bool HasSingleMrow(object value) => value is List<object> list && list.Count == 1 && Truthy(Get(list[0], "mrow"));

        /// <summary>mtransform: the MathML syntax tree of a preprocessed UnicodeMath syntax tree (dsty: bit 0 display, bit 1 right to left).</summary>
        private object Transform(int dsty, object puast)
        {
            if (puast is List<object> list && list.Count > 0)
            {
                object first = null;
                if (Truthy(Get(list[0], "script")))
                    first = Get(list[0], "script");
                else if (Truthy(Get(list[0], "primed")))
                    first = Get(list[0], "primed");

                if (Truthy(first) && Get(first, "intent") is string firstIntent && firstIntent.Contains("derivative"))
                {
                    var derivativeAttrs = GetAttrs(first);
                    Set(first, "intent", "");
                    return O("mrow", WithAttrs(derivativeAttrs, list.Select(e => Transform(dsty, e)).ToList()));
                }
                var argAttrs = new JsObject();
                if (Truthy(Get(list, "arg")))
                {
                    argAttrs = O("arg", Get(list, "arg"));
                }
                else if (Truthy(Get(list[0], "arg")))
                {
                    argAttrs = (JsObject)list[0];
                    list.RemoveAt(0);
                }
                var items = new List<object>();
                for (int i = 0; i < list.Count; i++)
                {
                    var item = Transform(dsty, list[i]);
                    if (Truthy(item))
                        items.Add(item);
                }
                if (items.Count == 0)
                    return "";
                return items.Count == 1 && list.Count > 1 ? items[0] : O("mrow", WithAttrs(argAttrs, items));
            }

            JsObject attrs;
            object mask;
            object ret = "";
            object selanchor1;
            string str;
            var key = K(puast);
            var value = V(puast);
            object val;
            if (Truthy(value) && !Truthy(Get(value, "arg")) && Truthy(Get(puast, "arg")))
                Set(value, "arg", Get(puast, "arg"));

            switch (key)
            {
                case "unicodemath":
                {
                    attrs = GetAttrs(value, "");
                    if ((dsty & 1) != 0)
                        attrs["display"] = "block";
                    if ((dsty & 2) != 0)
                        attrs["dir"] = "rtl";
                    var eqnumber = Get(value, "eqnumber") as string;
                    if (eqnumber == null)
                        return O("math", WithAttrs(attrs, Transform(dsty, Get(value, "content"))));
                    var id = Regex.Replace(eqnumber, "[^A-Za-z0-9_]", "-");
                    var row = A(O("mtd", NoAttr(O("mtext", NoAttr(eqnumber)))), O("mtd", NoAttr(Transform(dsty, Get(value, "content")))));
                    var table = O("mtable", WithAttrs(O("displaystyle", true), O("mlabeledtr", WithAttrs(O("id", id), row))));
                    return O("math", WithAttrs(attrs, table));
                }

                case "newline":
                    return O("mspace", WithAttrs(O("linebreak", "newline"), null));

                case "expr":
                    if (value is List<object> exprList && At(exprList, 0) is List<object> first0)
                    {
                        if (Truthy(Get(At(first0, 0), "intent")) || Truthy(Get(At(first0, 0), "arg")))
                        {
                            var c = Transform(dsty, first0[0]);
                            Set(Get(c, "mrow"), "attributes", GetAttrs(first0[0], ""));
                            return c;
                        }
                        if (Truthy(Get(At(first0, 0), "intend")) && Eq(Get(Get(first0[0], "intend"), "symbol"), "Ⓐ"))
                        {
                            int n = first0.Count;
                            if (n == 1 || n == 2 && Truthy(Get(first0[1], "intend")) && Eq(Get(Get(first0[1], "intend"), "symbol"), "Ⓕ"))
                            {
                                var selarr = (List<object>)exprList[0];
                                exprList.RemoveAt(0);
                                var c = Transform(dsty, exprList);
                                if (Truthy(c) && Truthy(Get(c, "mrow")))
                                {
                                    Set(Get(Get(c, "mrow"), "attributes"), "selanchor", Get(Get(selarr[0], "intend"), "value"));
                                    if (n == 2)
                                        Set(Get(Get(c, "mrow"), "attributes"), "selfocus", exprList.Count);
                                    return c;
                                }
                            }
                        }
                    }
                    return Transform(dsty, value);

                case "operator":
                    // an empty operator (the 〖 of an unclosed bracket) is content too, where JavaScript finds "" falsy
                    var hasContent = Get(value, "content") is string || Truthy(Get(value, "content"));
                    attrs = hasContent ? GetAttrs(value, "") : new JsObject();
                    val = hasContent ? Get(value, "content") : value;
                    if (Eq(attrs["intent"], ":text") && Eq(val, "/"))
                    {
                        attrs["lspace"] = "0pt";
                        attrs["rspace"] = "0pt";
                    }
                    if (val is string op && Len(op) == 1 && StretchyOperators.Contains(op))
                        attrs["stretchy"] = true;
                    return O("mo", WithAttrs(attrs, val));

                case "negatedoperator":
                    return O("mo", NoAttr(Str(value) + "\u0338"));

                case "element":
                    return Transform(dsty, value);

                case "array":
                    value = Transform(dsty, value);
                    attrs = GetAttrs(value, ":system-of-equations");
                    attrs["columnspacing"] = "0pt";
                    return O("mtable", WithAttrs(attrs, value));
                case "arows":
                    return ((List<object>)value).Select(r => (object)O("mtr", NoAttr(Transform(dsty, r)))).ToList();
                case "arow":
                    return ((List<object>)value).Select(c => (object)O("mtd", NoAttr(Transform(dsty, c)))).ToList();
                case "acol":
                    return ((List<object>)value).Select(c => Transform(dsty, c)).ToList();
                case "aalignmark":
                    return O("malignmark", NoAttr(null));
                case "aaligngroup":
                    return O("maligngroup", NoAttr(null));

                case "matrix":
                {
                    var rows = (List<object>)Transform(dsty, value);
                    str = ":array(" + rows.Count + "," + Length(Get(Get(rows[0], "mtr"), "content")) + ")";
                    attrs = GetAttrs(rows, str);
                    return O("mtable", WithAttrs(attrs, rows));
                }
                case "mrows":
                    return ((List<object>)value).Select(r => (object)O("mtr", NoAttr(Transform(dsty, r)))).ToList();
                case "mrow":
                    return ((List<object>)value).Select(c => (object)O("mtd", NoAttr(Transform(dsty, c)))).ToList();
                case "mcol":
                    return Transform(dsty, value);

                case "nary":
                {
                    attrs = GetAttrs(value, "n-ary");
                    var attrsn = GetAttrs(Get(value, "naryand"));
                    Set(value, "limits", Transform(dsty, Get(value, "limits")));
                    Set(value, "naryand", Transform(dsty, Get(value, "naryand")));
                    Set(Get(value, "naryand"), "attributes", attrsn);
                    return O("mrow", WithAttrs(attrs, A(Get(value, "limits"), Get(value, "naryand"))));
                }

                case "opnary":
                    attrs = GetAttrs(value, "");
                    return O("mo", WithAttrs(attrs, value));

                case "phantom":
                    attrs = GetAttrs(value, "");
                    mask = Get(value, "mask");
                    if (mask is int phantomMask && phantomMask != 0)
                    {
                        if ((phantomMask & 2) != 0)
                            attrs["width"] = 0;
                        if ((phantomMask & 4) != 0)
                            attrs["height"] = 0;
                        if ((phantomMask & 8) != 0)
                            attrs["depth"] = 0;
                        if ((phantomMask & 1) != 0)
                            return O("mpadded", WithAttrs(attrs, Transform(dsty, Get(value, "of"))));
                        return O("mpadded", WithAttrs(attrs, O("mphantom", NoAttr(Transform(dsty, Get(value, "of"))))));
                    }
                    if (Eq(Get(value, "symbol"), "⬄") || Eq(Get(value, "symbol"), "⇳"))
                    {
                        if (Eq(Get(value, "symbol"), "⬄"))
                        {
                            attrs["depth"] = 0;
                            attrs["height"] = 0;
                        }
                        else
                        {
                            attrs["width"] = 0;
                        }
                        return O("mpadded", WithAttrs(attrs, O("mphantom", NoAttr(Transform(dsty, Get(value, "of"))))));
                    }
                    return O("mphantom", WithAttrs(attrs, Transform(dsty, Get(value, "of"))));

                case "smash":
                    attrs = GetAttrs(value, "");
                    switch (Get(value, "symbol") as string)
                    {
                        case "⬍":
                            attrs["height"] = 0;
                            attrs["depth"] = 0;
                            break;
                        case "⬆":
                            attrs["height"] = 0;
                            break;
                        case "⬇":
                            attrs["depth"] = 0;
                            break;
                        case "⬌":
                            attrs["width"] = 0;
                            break;
                        default:
                            throw new FormatException("invalid smash symbol");
                    }
                    return O("mpadded", WithAttrs(attrs, Transform(dsty, Get(value, "of"))));

                case "fraction":
                {
                    attrs = GetAttrs(value, "");
                    var ofFrac = ((List<object>)Get(value, "of")).Select(e => Transform(dsty, DropOutermostParens(e))).ToList();
                    switch (Get(value, "symbol") as string)
                    {
                        case "⊘":
                            attrs["displaystyle"] = "false";
                            return O("mfrac", WithAttrs(attrs, ofFrac));
                        case "/":
                            return O("mfrac", WithAttrs(attrs, ofFrac));
                        case "⁄":
                            return O("mfrac", WithAttrs(O("bevelled", true), ofFrac));
                        case "∕":
                            return O("mrow", NoAttr(A(ofFrac[0], O("mo", NoAttr("/")), ofFrac[1])));
                    }
                    return null;
                }

                case "atop":
                {
                    attrs = GetAttrs(value, "");
                    attrs["linethickness"] = 0;
                    var atop = (List<object>)value;
                    var arg0 = Get(atop[0], "arg");
                    var arg1 = Get(atop[1], "arg");
                    var top = Transform(dsty, DropOutermostParens(atop[0]));
                    var bottom = Transform(dsty, DropOutermostParens(atop[1]));
                    if (Truthy(arg0))
                    {
                        Set(Get(Get(top, "mrow"), "attributes"), "arg", arg0);
                        if (HasSingleMrow(Get(Get(top, "mrow"), "content")))
                            Set(Get(top, "mrow"), "content", Get(Get(At(Get(Get(top, "mrow"), "content"), 0), "mrow"), "content"));
                    }
                    if (Truthy(arg1))
                    {
                        Set(Get(Get(bottom, "mrow"), "attributes"), "arg", arg1);
                        if (HasSingleMrow(Get(Get(bottom, "mrow"), "content")))
                            Set(Get(bottom, "mrow"), "content", Get(Get(At(Get(Get(bottom, "mrow"), "content"), 0), "mrow"), "content"));
                    }
                    return O("mfrac", WithAttrs(attrs, A(top, bottom)));
                }

                case "binom":
                    return Transform(dsty, O("bracketed", O(("intent", Get(value, "intent")), ("arg", Get(value, "arg")), ("open", "("), ("close", ")"),
                        ("content", O("atop", A(Get(value, "top"), Get(value, "bottom")))))));

                case "href":
                    attrs = GetAttrs(value, "");
                    attrs["href"] = Get(value, "url");
                    if (!Truthy(Get(value, "content")))
                        return O("a", WithAttrs(attrs, "<mtext>" + Str(Get(value, "url")) + "</mtext>"));
                    return O("a", WithAttrs(attrs, Transform(dsty, Get(value, "content"))));

                case "script":
                    return TransformScript(dsty, (JsObject)value);

                case "enclosed":
                {
                    var symbol = Get(value, "symbol") as string;
                    attrs = GetAttrs(value, "");
                    if (symbol == "ⓓ")
                    {
                        attrs["displaystyle"] = true;
                        return O("mstyle", WithAttrs(attrs, Transform(dsty, DropOutermostParens(Get(value, "of")))));
                    }
                    attrs["notation"] = EnclosureAttrs(Get(value, "mask"), symbol);
                    return O("menclose", WithAttrs(attrs, Transform(dsty, DropOutermostParens(Get(value, "of")))));
                }

                case "abstractbox":
                {
                    var options = AbstractBoxOptions(Get(value, "mask"));
                    attrs = O("class", string.Join(" ", options));
                    if (Truthy(Get(value, "intent")))
                        attrs["intent"] = Get(value, "intent");
                    if (options.Contains("nSizeText"))
                        attrs["scriptlevel"] = 0;
                    else if (options.Contains("nSizeScript"))
                        attrs["scriptlevel"] = 1;
                    else if (options.Contains("nSizeScriptScript"))
                        attrs["scriptlevel"] = 2;
                    return O("mrow", WithAttrs(attrs, Transform(dsty, Get(value, "of"))));
                }

                case "hbrack":
                    return TransformHbrack(dsty, (JsObject)value);

                case "root":
                    return O("mroot", WithAttrs(GetAttrs(value, ""), A(Transform(dsty, DropOutermostParens(Get(value, "of"))), Transform(dsty, Get(value, "degree")))));
                case "sqrt":
                    val = Transform(dsty, DropOutermostParens(value));
                    attrs = GetAttrs(value, "");
                    return O("msqrt", WithAttrs(attrs, val));

                case "function":
                {
                    var selanchorSave = selanchor;
                    var selfocusSave = selfocus;
                    attrs = GetAttrs(value, ":function");
                    if (Truthy(selfocusSave) && Ch(selfocusSave, 0) == "-")
                    {
                        attrs.Remove("selanchor");
                        attrs.Remove("selfocus");
                        selanchor = selanchorSave;
                        selfocus = selfocusSave;
                    }
                    val = Transform(dsty, Get(value, "f"));
                    return O("mrow", WithAttrs(attrs, A(val, O("mo", NoAttr("\u2061")), Transform(dsty, Get(value, "of")))));
                }
                case "text":
                {
                    attrs = GetAttrs(value, "");
                    var text = Str(value);
                    if (Len(text) == 1 && (IsAsciiAlphabetic(text) || IsLcGreek(text)))
                    {
                        attrs["mathvariant"] = "normal";
                        return O("mi", WithAttrs(attrs, text));
                    }
                    return O("mtext", WithAttrs(attrs, text.Replace(" ", "\u00A0")));
                }

                case "sizeoverride":
                    switch (Get(value, "size") as string)
                    {
                        case "A":
                            return O("mstyle", WithAttrs(O("fontsize", FontSize(1)), Transform(dsty, Get(value, "of"))));
                        case "B":
                            return O("mstyle", WithAttrs(O("fontsize", FontSize(2)), Transform(dsty, Get(value, "of"))));
                        case "C":
                            return O("mstyle", WithAttrs(O("fontsize", FontSize(-1)), Transform(dsty, Get(value, "of"))));
                        case "D":
                            return O("mstyle", WithAttrs(O("fontsize", FontSize(-2)), Transform(dsty, Get(value, "of"))));
                    }
                    return null;

                case "colored":
                    attrs = GetAttrs(Get(value, "of"), "");
                    attrs["mathcolor"] = Get(value, "color");
                    Set(value, "of", Transform(dsty, Get(value, "of")));
                    if (Truthy(Get(Get(value, "of"), "mo")))
                        return O("mo", WithAttrs(attrs, Get(Get(Get(value, "of"), "mo"), "content")));
                    return O("mstyle", WithAttrs(attrs, Get(value, "of")));

                case "bgcolored":
                    attrs = GetAttrs(Get(value, "of"), "");
                    attrs["mathbackground"] = Get(value, "color");
                    return O("mstyle", WithAttrs(attrs, Transform(dsty, Get(value, "of"))));

                case "remark":
                {
                    var remarkBase = Str(Get(value, "base"));
                    var c0 = Ch(remarkBase, 0);
                    if (IsAsciiDigit(c0))
                        return O("mn", NoAttr(value));
                    if (IsAsciiAlphabetic(c0) || IsGreek(c0) || Less("〗", c0) || c0 != null && LetterLikeSymbols.ContainsKey(c0))
                        return O("mi", NoAttr(value));
                    return O("mo", NoAttr(value));
                }

                case "comment":
                    return O("␢", NoAttr(null));
                case "tt":
                    return O("mstyle", WithAttrs(O("fontfamily", "monospace"), O("mtext", NoAttr(Str(value).Replace(" ", "\u00A0")))));

                case "primed":
                    attrs = GetAttrs(value, "");
                    return O("msup", WithAttrs(attrs, A(Transform(dsty, Get(value, "base")), O("mo", NoAttr(ProcessPrimes(Get(value, "primes")))))));

                case "factorial":
                    attrs = GetAttrs(value, "");
                    return O("mrow", WithAttrs(attrs, A(Transform(dsty, value), O("mo", NoAttr("!")))));

                case "atoms":
                    return TransformAtoms(dsty, value);

                case "chars":
                {
                    var chars = Str(value);
                    if (Len(chars) > 1)
                        return O("mi", NoAttr(chars));
                    if (string.CompareOrdinal(chars, "Α") >= 0 && string.CompareOrdinal(chars, "Ω") <= 0)
                        return O("mi", WithAttrs(O("mathvariant", "normal"), chars));
                    return O("mi", NoAttr(ItalicizeCharacters(chars)));
                }

                case "diacriticized":
                {
                    string notation = "";
                    ret = Transform(dsty, Get(value, "base"));
                    foreach (var diacritic in (List<object>)Get(value, "diacritics"))
                    {
                        var d = Str(diacritic);
                        switch (d)
                        {
                            case "\u0305":
                                notation = "top";
                                break;
                            case "\u0332":
                                notation = "bottom";
                                break;
                            case "\u20E0":
                                ret = O("menclose", WithAttrs(O("notation", "downdiagonalstrike"), ret));
                                notation = "circle";
                                break;
                            case "\u20DD":
                                notation = "circle";
                                break;
                            case "\u20DE":
                                notation = "box";
                                break;
                            default:
                                var tag = DiacriticPosition(d) == -1 ? "munder" : "mover";
                                d = "&#x" + CodePointAt(d, 0).ToString("x", CultureInfo.InvariantCulture) + ";";
                                ret = O(tag, WithAttrs(O("accent", true), A(ret, O("mo", NoAttr(d)))));
                                continue;
                        }
                        ret = O("menclose", WithAttrs(O("notation", notation), ret));
                    }
                    return ret;
                }

                case "spaces":
                {
                    var spaces = "";
                    if (value is List<object> spaceList)
                    {
                        foreach (var space in spaceList)
                            spaces += "&#x" + CodePointAt(Str(space), 0).ToString("x", CultureInfo.InvariantCulture) + ";";
                    }
                    else if (value is string spaceString)
                    {
                        foreach (var space in spaceString)
                            spaces += "&#x" + ((int)space).ToString("x", CultureInfo.InvariantCulture) + ";";
                    }
                    return O("mo", NoAttr(spaces));
                }

                case "number":
                    if ((dsty & 2) != 0 && value is string digit && IsAsciiDigit(digit))
                        value = IndicDigits[digit[0] - '0'];
                    return O("mn", WithAttrs(GetAttrs(value, ""), value));

                case "bracketed":
                    return TransformBracketed(dsty, (JsObject)value);

                case "intend":
                    if (Eq(Get(value, "symbol"), "Ⓐ"))
                        selanchor = Str(Get(value, "value"));
                    else if (Eq(Get(value, "symbol"), "Ⓕ"))
                        selfocus = Str(Get(value, "value"));
                    return Truthy(Get(value, "op")) ? O("mo", WithAttrs(GetAttrs(value, ""), Get(value, "op"))) : (object)"";

                default:
                    return value;
            }
        }

        private object TransformScript(int dsty, JsObject value)
        {
            var attrs = GetAttrs(value, "");
            object selanchor1;
            switch (value["type"] as string)
            {
                case "subsup":
                    selanchor1 = Get(value["base"], "selanchor");
                    value["base"] = Transform(dsty, value["base"]);
                    if (Truthy(selanchor1))
                    {
                        Set(Get(Get(value["base"], "mi"), "attributes"), "selanchor", selanchor1);
                        attrs.Remove("selanchor");
                    }
                    if (value.Has("low"))
                        value["low"] = GetScriptArg(dsty, value["low"]);
                    if (value.Has("high"))
                    {
                        value["high"] = GetScriptArg(dsty, value["high"]);
                        if (value.Has("low"))
                            return O("msubsup", WithAttrs(attrs, A(value["base"], value["low"], value["high"])));
                        return O("msup", WithAttrs(attrs, A(value["base"], value["high"])));
                    }
                    if (value.Has("low"))
                        return O("msub", WithAttrs(attrs, A(value["base"], value["low"])));
                    return value["base"];

                case "pre":
                {
                    var ret = A(Transform(dsty, value["base"]));
                    if (value.Has("low") && value.Has("high"))
                    {
                        ret.Add(Transform(dsty, DropOutermostParens(value["low"])));
                        ret.Add(Transform(dsty, DropOutermostParens(value["high"])));
                    }
                    else if (value.Has("low"))
                    {
                        ret.Add(Transform(dsty, DropOutermostParens(value["low"])));
                        ret.Add(O("none", NoAttr(null)));
                    }
                    else if (value.Has("high"))
                    {
                        ret.Add(O("none", NoAttr(null)));
                        ret.Add(Transform(dsty, DropOutermostParens(value["high"])));
                    }
                    ret.Add(O("mprescripts", NoAttr(null)));
                    if (value.Has("prelow") && value.Has("prehigh"))
                    {
                        ret.Add(Transform(dsty, DropOutermostParens(value["prelow"])));
                        ret.Add(Transform(dsty, DropOutermostParens(value["prehigh"])));
                    }
                    else if (value.Has("prelow"))
                    {
                        ret.Add(Transform(dsty, DropOutermostParens(value["prelow"])));
                        ret.Add(O("none", NoAttr(null)));
                    }
                    else if (value.Has("prehigh"))
                    {
                        ret.Add(O("none", NoAttr(null)));
                        ret.Add(Transform(dsty, DropOutermostParens(value["prehigh"])));
                    }
                    else
                    {
                        throw new FormatException("neither presubscript nor presuperscript present in prescript");
                    }
                    return O("mmultiscripts", NoAttr(ret));
                }

                case "abovebelow":
                    selanchor1 = Get(value["base"], "selanchor");
                    value["base"] = Transform(dsty, DropOutermostParens(value["base"]));
                    if (Truthy(selanchor1))
                    {
                        Set(Get(Get(value["base"], "mi"), "attributes"), "selanchor", selanchor1);
                        attrs.Remove("selanchor");
                    }
                    if (value.Has("low"))
                        value["low"] = GetScriptArg(dsty, value["low"]);
                    if (value.Has("high"))
                    {
                        value["high"] = GetScriptArg(dsty, value["high"]);
                        if (value.Has("low"))
                            return O("munderover", WithAttrs(attrs, A(value["base"], value["low"], value["high"])));
                        return O("mover", WithAttrs(attrs, A(value["base"], value["high"])));
                    }
                    if (value.Has("low"))
                        return O("munder", WithAttrs(attrs, A(value["base"], value["low"])));
                    return value["base"];

                default:
                    throw new FormatException("invalid or missing script type");
            }
        }

        private JsObject TransformHbrack(int dsty, JsObject value)
        {
            var basE = DropSingletonLists(value["of"]);
            var attrs = GetAttrs(value, "");
            var bracket = Str(value["bracket"]);
            var mtag = OverBrackets.Contains(bracket) ? "mover" : "munder";

            if (Truthy(value["intent"]))
                attrs["intent"] = value["intent"];

            var script = Get(basE, "script");
            if (Truthy(script) && (Eq(Get(script, "type"), "subsup") || Eq(Get(script, "type"), "abovebelow")))
            {
                var expLow = Get(script, "low");
                var expHigh = Get(script, "high");
                var type = Get(script, "type");
                basE = DropOutermostParens(Get(script, "base"));

                object exp;
                if (mtag == "mover")
                {
                    exp = expHigh;
                    if (expLow != null)
                        basE = O("script", O(("base", basE), ("type", type), ("low", expLow)));
                }
                else
                {
                    exp = expLow;
                    if (expHigh != null)
                        basE = O("script", O(("base", basE), ("type", type), ("high", expHigh)));
                }
                if (exp == null)
                    throw new FormatException("hbrack bracket type doesn't match script type");

                return O(mtag, WithAttrs(new JsObject(), A(
                    O(mtag, WithAttrs(attrs, A(Transform(dsty, basE), O("mo", WithAttrs(O("stretchy", true), bracket))))),
                    Transform(dsty, DropOutermostParens(exp)))));
            }
            return O(mtag, WithAttrs(attrs, A(Transform(dsty, DropOutermostParens(value["of"])), O("mo", WithAttrs(O("stretchy", true), bracket)))));
        }

        private object TransformAtoms(int dsty, object value)
        {
            var attrs = GetAttrs(value, "");

            if (Get(value, "funct") != null)
                return O("mi", WithAttrs(attrs, Get(value, "chars")));

            int? n = value is List<object> ? Length(value) : null;
            var mis = new List<object>();
            string str;

            if (n == null)
            {
                str = Get(value, "chars") as string;
                n = 1;
            }
            else
            {
                str = Get(At(value, 0), "chars") as string;
            }

            var starts = new List<int>();
            for (int i = 0; i < n;)
            {
                starts.Add(mis.Count);
                if (str == null || Len(str) > 0 && Ch(str, 0) == "Ⅎ" || IsFunctionName(str))
                {
                    var val = Transform(dsty, value is List<object> list ? list[i] : value);
                    if (Truthy(attrs["intent"]) || Truthy(attrs["arg"]))
                        mis.Add(O("mrow", WithAttrs(attrs, val)));
                    else
                        mis.Add(val);
                }
                else
                {
                    if (n == 3 && Truthy(Get(At(value, 1), "spaces")) && Ch(str, 0) == "ⅆ" && Truthy(Get(At(value, 0), "chars")))
                    {
                        // the atom and the spaces before the differential are written again with it: once only
                        str = Str(Get(At(value, 0), "chars")) + "\u2009" + str;
                        mis.RemoveRange(starts[0], mis.Count - starts[0]);
                    }
                    int cch = Len(str);

                    if (cch > 1)
                    {
                        int cchCh = 1;
                        for (int j = 0; j < cch; j += cchCh)
                        {
                            cchCh = 1;
                            var chj = Ch(str, j);

                            if (IsDoubleStruck(chj))
                            {
                                if (j > 0 && chj == "ⅆ" && Ch(str, j - 1) != "\u2009")
                                    mis.Add(O("mi", NoAttr("\u2009")));
                                var ch = DoublestruckChar(chj);
                                var attrsDoublestruck = GetAttrsDoublestruck(ch, chj);
                                var intentD = attrsDoublestruck["intent"];
                                foreach (var attr in attrs.Keys)
                                    attrsDoublestruck[attr] = attrs[attr];
                                if (chj == "ⅅ")
                                    attrsDoublestruck["intent"] = intentD;
                                mis.Add(O("mi", WithAttrs(attrsDoublestruck, ch)));
                            }
                            else if ("-−,+".Contains(chj))
                            {
                                if (IsAsciiDigit(Ch(str, j + 1)))
                                {
                                    mis.Add(O("mn", NoAttr(Sub(str, j))));
                                    break;
                                }
                                mis.Add(O("mo", NoAttr(chj)));
                            }
                            else
                            {
                                if (InRange("\uFE00", Ch(str, j + cchCh), "\uFE0F"))
                                    cchCh++;
                                mis.Add(O("mi", NoAttr(Sub(str, j, j + Math.Min(cchCh, cch - j)))));
                            }
                        }
                    }
                    else
                    {
                        if (string.CompareOrdinal(str, "ⅅ") >= 0 && string.CompareOrdinal(str, "ⅉ") <= 0)
                        {
                            var ch = DoublestruckChar(str);
                            var attrsDoublestruck = GetAttrsDoublestruck(ch, str);
                            foreach (var attr in attrs.Keys)
                                attrsDoublestruck[attr] = attrs[attr];
                            mis.Add(O("mi", WithAttrs(attrsDoublestruck, ch)));
                        }
                        else if (str == "⊺" && Eq(Get(value, "intent"), "transpose"))
                        {
                            var ch = "T";
                            attrs["mathvariant"] = "normal";
                            mis.Add(O("mi", WithAttrs(attrs, ch)));
                        }
                        else
                        {
                            var val = Transform(dsty, value is List<object> list ? list[i] : value);
                            var miAttributes = Get(Get(val, "mi"), "attributes");
                            if (Truthy(attrs["selanchor"]))
                                Set(miAttributes, "selanchor", attrs["selanchor"]);
                            if (Truthy(attrs["selfocus"]))
                                Set(miAttributes, "selfocus", attrs["selfocus"]);
                            if (Truthy(attrs["arg"]))
                                Set(miAttributes, "arg", attrs["arg"]);
                            if (Truthy(attrs["intent"]))
                                Set(miAttributes, "intent", attrs["intent"]);
                            mis.Add(val);
                        }
                    }
                }
                i++;
                if (i >= n)
                    break;
                str = Get(At(value, i), "chars") as string;
            }
            if (mis.Count > 1)
                return O("mrow", WithAttrs(attrs, mis));
            return At(mis, 0);
        }

        private List<object> TransformBracketed(int dsty, JsObject value)
        {
            object content;
            var defaultIntent = ":fenced";
            object miContent = null;
            var separator = "";

            var separated = Get(value["content"], "separated");
            if (Truthy(separated))
            {
                separator = Str(Get(separated, "separator"));
                value["content"] = Get(separated, "of");
            }

            if (Eq(value["open"], "|") && Eq(value["close"], "|"))
            {
                content = Transform(dsty, DropOutermostParens(value["content"]));
                defaultIntent = "absolute-value($a)";
                var mrow = Get(content, "mrow");
                if (Truthy(mrow) && Get(mrow, "content") is List<object>)
                {
                    var c = mrow;
                    if (Length(Get(c, "content")) == 1)
                    {
                        c = At(Get(c, "content"), 0);
                        if (Truthy(Get(c, "mtable")))
                        {
                            defaultIntent = "determinant($a)";
                            c = Get(c, "mtable");
                        }
                        else if (Truthy(Get(c, "mfrac")))
                        {
                            c = Get(c, "mfrac");
                        }
                        else if (Truthy(Get(c, "mrow")))
                        {
                            c = Get(c, "mrow");
                            if (Length(Get(c, "content")) == 1)
                            {
                                var c0 = At(Get(c, "content"), 0);
                                if (Truthy(Get(c0, "mi")))
                                {
                                    miContent = Get(Get(c0, "mi"), "content");
                                    defaultIntent = "absolute-value(" + Str(miContent) + ")";
                                }
                                else if (Truthy(Get(c0, "mtext")))
                                {
                                    c = Get(c0, "mtext");
                                }
                            }
                        }
                    }
                    if (!Truthy(miContent))
                        Set(c, "attributes", O("arg", "a"));
                }
            }
            else if (Len(separator) == 0)
            {
                if (Eq(value["open"], "{") && !Truthy(value["close"]))
                    value["close"] = "\u200B";
                content = Transform(dsty, value["content"]);
            }
            else
            {
                var items = (List<object>)Get(Get(Transform(dsty, value["content"]), "mrow"), "content");
                var separatedItems = new List<object>();
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0)
                        separatedItems.Add(O("mo", NoAttr(separator)));
                    separatedItems.Add(items[i]);
                }
                content = O("mrow", NoAttr(separatedItems));
            }

            var ret = new List<object>();
            if ((dsty & 2) != 0)
            {
                var open = value["open"];
                value["open"] = value["close"];
                value["close"] = open;
            }
            if (value["open"] is string openString)
            {
                ret.Add(O("mo", NoAttr(openString)));
            }
            else
            {
                var openSize = FontSize(Get(value["open"], "size"));
                ret.Add(O("mo", WithAttrs(O(("minsize", openSize), ("maxsize", openSize)), Get(value["open"], "bracket"))));
            }
            ret.Add(content);
            if (value["close"] is string closeString)
            {
                ret.Add(O("mo", NoAttr(closeString)));
            }
            else
            {
                var closeSize = FontSize(Get(value["close"], "size"));
                ret.Add(O("mo", WithAttrs(O(("minsize", closeSize), ("maxsize", closeSize)), Get(value["close"], "bracket"))));
            }
            var attrs = GetAttrs(value, defaultIntent);
            if (Truthy(attrs["intent"]))
                attrs["intent"] = CheckCardinalityIntent(Str(attrs["intent"]), miContent);
            return A(O("mrow", WithAttrs(attrs, ret)));
        }
    }
}
