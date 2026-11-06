namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using static Js;
    using static UnicodeMathText;

    internal sealed partial class UnicodeMathTranslator
    {
        private static JsObject Attributes(object ast) => (JsObject)Get(V(ast), "attributes");

        private static string Tag(string tagname, JsObject attribs, object value)
        {
            var attributes = "";
            if (attribs != null && attribs.Count > 0)
                attributes = " " + string.Join(" ", attribs.Keys.Select(key => key + "=\"" + Str(attribs[key]) + "\""));
            if (value == null)
                return "<" + tagname + attributes + " />";
            return "<" + tagname + attributes + ">" + Str(value) + "</" + tagname + ">";
        }

        private static void PromoteAttributelessMrowChildren(List<object> value)
        {
            for (int j = 0; j < value.Count; j++)
            {
                var node = value[j];
                if (!Truthy(Get(node, "mrow")))
                    continue;
                var attrs = Attributes(node);
                if (attrs != null && attrs.Count > 0)
                    continue;
                var grandchildren = Get(Get(node, "mrow"), "content") as List<object>;
                int c = grandchildren?.Count ?? 0;
                value.RemoveAt(j);
                int jT = -2;
                for (int i = 0; i < c; i++)
                {
                    var nodeG = grandchildren[i];
                    value.Insert(j++, nodeG);
                    if (jT == -2 && Truthy(Get(nodeG, "mrow")))
                    {
                        var attrsG = Attributes(nodeG);
                        if (attrsG == null || attrsG.Count == 0)
                            jT = j - 1;
                    }
                }
                j--;
                if (jT != -2)
                    j = jT - 1;
            }
        }

        /// <summary>pretty: the MathML text of a MathML syntax tree.</summary>
        private string Pretty(object mast)
        {
            if (mast is List<object> list)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                    sb.Append(Pretty(list[i]));
                return sb.ToString();
            }
            if (!(mast is JsObject))
                return mast == null ? null : Str(mast);

            var key = K(mast);
            var attributes = Attributes(mast) ?? new JsObject();
            var value = Get(V(mast), "content");
            string arg;

            switch (key)
            {
                case "mrow":
                    if (value is List<object> single && single.Count == 1 && !Truthy(attributes["arg"]))
                        return Pretty(O("mrow", O(("attributes", attributes), ("content", single[0]))));
                    if (value is List<object> children && (!Truthy(attributes["intent"]) || !Eq(attributes["intent"], ":fenced")))
                    {
                        var first = At(children, 0);
                        if (Truthy(Get(first, "mrow")))
                        {
                            first = Get(first, "mrow");
                            if (Truthy(Get(first, "content")))
                                first = Get(first, "content");
                            if (first is List<object>)
                                first = At(first, 0);
                        }
                        if (Truthy(Get(first, "munder")) && Eq(Get(Get(Get(first, "munder"), "attributes"), "intent"), ":function"))
                        {
                            attributes["intent"] = ":function";
                            Set(Get(first, "munder"), "attributes", new JsObject());
                        }
                        else
                        {
                            PromoteAttributelessMrowChildren(children);
                        }
                    }
                    else if (attributes.Count == 0)
                    {
                        return Pretty(value);
                    }
                    return Tag(key, attributes, Pretty(value));

                case "a":
                case "math":
                case "menclose":
                case "merror":
                case "mphantom":
                case "mpadded":
                case "msqrt":
                case "mscarry":
                case "mstyle":
                case "mtd":
                {
                    if (value is List<object> elements)
                    {
                        PromoteAttributelessMrowChildren(elements);
                        return Tag(key, attributes, Pretty(elements));
                    }
                    arg = Pretty(value);
                    int i = 0;
                    if (arg != null && arg.StartsWith("<mrow", StringComparison.Ordinal) && arg.EndsWith("</mrow>", StringComparison.Ordinal))
                    {
                        if (arg.StartsWith("<mrow selanchor=\"0\"", StringComparison.Ordinal))
                            i = 20;
                        else if (Ch(arg, 5) == ">")
                            i = 6;
                    }
                    if (i != 0)
                    {
                        arg = Sub(arg, i, i + Len(arg) - 7 - i);
                        if (i == 20)
                        {
                            i = Characters(arg).IndexOf(">");
                            arg = Sub(arg, 0, 0 + i) + " selanchor=\"0\"" + Sub(arg, i);
                        }
                    }
                    return Tag(key, attributes, arg);
                }

                case "mover":
                case "msub":
                case "msubsup":
                case "msup":
                case "munder":
                case "munderover":
                case "mfenced":
                case "mfrac":
                case "mroot":
                case "mtr":
                case "mlabeledtr":
                case "mtable":
                case "mmultiscripts":
                case "mprescripts":
                case "none":
                    return Tag(key, attributes, Pretty(value));

                case "mi":
                {
                    if (Truthy(Get(value, "comment")))
                        value = Str(Get(value, "base")) + "<!--" + Str(Get(value, "comment")) + "-->";
                    var text = value as string;
                    // Arabic math alphabetic symbols (U+1EE00 to U+1EEFF)
                    if (text != null && CodePointAt(text, 0) >= 0x1EE00 && CodePointAt(text, 0) <= 0x1EEFF)
                    {
                        attributes["style"] = "font-family:XITS Math";
                    }
                    else if (text != null && Len(text) > 0)
                    {
                        int cp = CodePointAt(text, 0);
                        if (cp >= 0x1D49C && cp <= 0x1D503 && Len(text) > 1 ||
                            LetterLikeSymbols.TryGetValue(Sub(text, 0, 0 + 1), out var set) && set == 3)
                        {
                            var vs = Ch(text, Len(text) - 1);
                            if (vs == "\uFE00" || vs == "\uFE01")
                            {
                                attributes["style"] = "font-family:STIX Two Math; font-feature-settings: " +
                                    (vs == "\uFE01" ? "'ss01'" : "'ss00'") + " 1";
                            }
                        }
                    }
                    return Tag(key, attributes, value);
                }
                case "mn":
                case "mo":
                case "mtext":
                case "mspace":
                    if (Truthy(Get(value, "comment")))
                        value = Str(Get(value, "base")) + "<!--" + Str(Get(value, "comment")) + "-->";
                    return Tag(key, attributes, value);
                case "maligngroup":
                case "malignmark":
                    if (alignmentMarks)
                        return Tag(key, attributes, null);
                    return key == "maligngroup"
                        ? "</mtd><mtd style='padding-left:0;text-align:right;float:right;display:math'>"
                        : "</mtd><mtd style='padding-left:0;text-align:left'>";
                case "␢":
                    return "";
                default:
                    return value == null ? null : Str(value);
            }
        }
    }
}
