namespace SharpEquations.Internal.UnicodeMath
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using static Js;

    /// <summary>
    /// The actions of the UnicodeMath grammar (the JavaScript blocks of src/unicodemathml-parser.pegjs of
    /// UnicodeMathML): they build the same syntax tree as PEG.js does, with lists for sequences and repetitions.
    /// </summary>
    internal static class UnicodeMathGrammarActions
    {
        public static JsObject Label(string label, object value) => O(label, value);

        /// <summary>[a, b, c] => {label: [{label: [a, b]}, c]}</summary>
        public static object NestRight(string label, List<object> items)
        {
            object result = items[0];
            for (int i = 1; i < items.Count; i++)
                result = Label(label, A(result, items[i]));
            return result;
        }

        /// <summary>[[a, +], [b, /], c] => {label: {symbol: /, of: [{label: {symbol: +, of: [a, b]}}, c]}}</summary>
        public static object NestRight2(string label, List<object> items)
        {
            var first = (List<object>)items[0];
            object result = first[0];
            object symbol = first[1];
            for (int i = 1; i < items.Count; i++)
            {
                if (i == items.Count - 1)
                {
                    result = Label(label, O(("symbol", symbol), ("of", A(result, items[i]))));
                }
                else
                {
                    var item = (List<object>)items[i];
                    result = Label(label, O(("symbol", symbol), ("of", A(result, item[0]))));
                    symbol = item[1];
                }
            }
            return result;
        }

        /// <summary>The characters of chars found in sources, replaced by the characters at the same place in targets.</summary>
        public static string ReplaceMap(string sources, string targets, object chars)
        {
            var sourceList = UnicodeMathText.Characters(sources);
            var targetList = UnicodeMathText.Characters(targets);
            var sb = new StringBuilder();
            foreach (var c in Characters(chars))
            {
                int index = sourceList.IndexOf(c);
                if (index >= 0 && index < targetList.Count)
                    sb.Append(targetList[index]);
            }
            return sb.ToString();
        }

        private static IEnumerable<string> Characters(object chars)
        {
            if (chars is string s)
                return UnicodeMathText.Characters(s);
            return ((List<object>)chars).SelectMany(x => UnicodeMathText.Characters(Js.Str(x)));
        }

        /// <summary>Flattens one level: [[1, 2], 3, [4]] => [1, 2, 3, 4].</summary>
        public static List<object> Flatten(List<object> items)
        {
            var flat = new List<object>();
            foreach (var item in items)
            {
                if (item is List<object> list)
                    flat.AddRange(list);
                else
                    flat.Add(item);
            }
            return flat;
        }

        /// <summary>A string joined as JavaScript joins an array: nested arrays joined with commas, null as "".</summary>
        public static string Join(List<object> items, string separator = "")
        {
            return string.Join(separator, items.Select(x => x == null ? "" : Js.Str(x)));
        }

        public static string GetIntervalIntent(string open, string close)
        {
            switch (open)
            {
                case "(":
                    return close == "]" ? "open-closed-interval" : "";
                case "[":
                    return close == "]" ? "closed-interval" : (close == "[" || close == ")") ? "closed-open-interval" : "";
                case "]":
                    return close == "]" ? "open-closed-interval" : close == "[" ? "open-interval" : "";
            }
            return "";
        }

        public static JsObject GetBracketedInterval(string open, object content, string close)
        {
            var intent = GetIntervalIntent(open, close);
            if (Js.Len(intent) == 0)
                content = O("atoms", O("chars", Join(Flatten((List<object>)content))));
            return O("bracketed", O(("open", open), ("close", close), ("intent", intent), ("content", content)));
        }

        /// <summary>atoms = atom+: successive characters grouped in one "chars" node.</summary>
        public static JsObject Atoms(List<object> atoms)
        {
            var result = new List<object>();
            var chars = new StringBuilder();
            foreach (JsObject atom in atoms)
            {
                if (atom.Key == "char")
                {
                    chars.Append((string)atom.Value);
                }
                else
                {
                    if (chars.Length > 0)
                    {
                        result.Add(O("chars", chars.ToString()));
                        chars.Clear();
                    }
                    result.Add(atom);
                }
            }
            if (chars.Length > 0)
                result.Add(O("chars", chars.ToString()));
            return O("atoms", result);
        }

        /// <summary>Moves low/high of a script to prelow/prehigh, for prescripts.</summary>
        public static JsObject PreScript(JsObject script, object operand)
        {
            script["base"] = operand;
            script["type"] = "pre";
            if (Truthy(script["low"]))
            {
                script["prelow"] = script["low"];
                script.Remove("low");
            }
            if (Truthy(script["high"]))
            {
                script["prehigh"] = script["high"];
                script.Remove("high");
            }
            return O("script", script);
        }

        /// <summary>subL / supL: nested scripts a_b_c as {script: {base: b, low: c}}.</summary>
        public static object NestScripts(List<object> operands, string key)
        {
            object previous = operands[0];
            for (int i = 1; i < operands.Count; i++)
                previous = O("script", O(("type", "subsup"), ("base", previous), (key, operands[i])));
            return previous;
        }

        /// <summary>{low, high} of a subscript and a superscript, or the one of them that is not null.</summary>
        public static object SubSup(object sub, object sup)
        {
            if (sub == null)
                return sup;
            if (sup == null)
                return sub;
            return O(("low", Get(sub, "low")), ("high", Get(sup, "high")));
        }

        /// <summary>["fraction", op, [operand, op]..., operand] of a fraction after its first operand.</summary>
        public static List<object> FractionTail(object op, object operand, IReadOnlyList<(object, object)> steps)
        {
            var items = new List<object> { "fraction", op };
            foreach (var (nextOp, next) in steps)
            {
                items.Add(A(operand, nextOp));
                operand = next;
            }
            items.Add(operand);
            return items;
        }

        /// <summary>["atop", operand...] of an atop after its first operand.</summary>
        public static List<object> AtopTail(IReadOnlyList<object> operands, object last)
        {
            var items = new List<object> { "atop" };
            items.AddRange(operands);
            items.Add(last);
            return items;
        }

        /// <summary>An element that starts with an operand: the operand, a fraction, an atop, a binomial or an operand
        /// followed by a colored fraction or choose operator.</summary>
        public static object OperandElement(object first, List<object> tail)
        {
            switch (tail == null ? null : (string)tail[0])
            {
                case null:
                    return first;
                case "fraction":
                    var fraction = new List<object> { A(first, tail[1]) };
                    fraction.AddRange(tail.Skip(2));
                    return NestRight2("fraction", fraction);
                case "atop":
                    var atop = new List<object> { first };
                    atop.AddRange(tail.Skip(1));
                    return NestRight("atop", atop);
                case "binom":
                    return O("binom", O(("top", first), ("bottom", tail[1])));
                default:
                    return A(first, ColoredOperator(tail[1]));
            }
        }

        /// <summary>The contents of a bracket: exp, or exps separated by │ or ∣ ([separator, [exp...]]).</summary>
        public static object BracketContents(object first, List<object> separated)
        {
            if (separated == null)
                return first;
            var of = new List<object> { first };
            of.AddRange((List<object>)separated[1]);
            return O("separated", O(("separator", separated[0]), ("of", of)));
        }

        /// <summary>
        /// A bracket opened by "(": (exp|exp) as ["|", exp], contents and close as ["contents", tail, close], or (exp)
        /// with diacritics as ["diacriticized", diacritics, atoms], the atoms that start with it.
        /// </summary>
        public static object ParenBracket(object first, List<object> rest)
        {
            if ((string)rest[0] == "diacriticized")
            {
                var atoms = new List<object> { O("diacriticized", O(("base", first), ("diacritics", rest[1]))) };
                atoms.AddRange((List<object>)rest[2]);
                return new DiacriticizedParen(Atoms(atoms));
            }
            if ((string)rest[0] == "|")
                return O("bracketed", O(("open", "("), ("close", ")"), ("content", O("separated", O(("separator", "│"), ("of", A(first, rest[1])))))));
            return O("bracketed", O(("open", "("), ("close", rest[2]), ("content", BracketContents(first, (List<object>)rest[1]))));
        }

        /// <summary>The factor of an entity that does not start with a function name, from what follows it.</summary>
        public static object FactorEntity(object entity, List<object> suffix)
        {
            switch (suffix == null ? null : (string)suffix[0])
            {
                case null:
                case "plain":
                    return entity;
                case "primedscript":
                    var primed = O("primed", O(("base", entity), ("primes", suffix[1])));
                    return suffix[2] == null ? primed : ScriptedBase(primed, (List<object>)suffix[2]);
                case "primed":
                case "script":
                case "factorial":
                case "factorial2":
                    return EntityFactor(entity, suffix);
                default:
                    return ScriptedBase(entity, suffix);
            }
        }

        /// <summary>An entity with what follows it in sfactor: primes, a Unicode script or a factorial sign.</summary>
        public static object EntityFactor(object entity, List<object> suffix)
        {
            if (suffix == null)
                return entity;
            switch ((string)suffix[0])
            {
                case "primed":
                    var primed = O("primed", O(("base", entity), ("primes", suffix[1])));
                    if (suffix[2] == null)
                        return primed;
                    var primedScript = (JsObject)suffix[2];
                    primedScript["base"] = primed;
                    return O("script", primedScript);
                case "script":
                    var script = (JsObject)suffix[1];
                    script["base"] = entity;
                    return O("script", script);
                case "factorial2":
                    return O("factorial", O("factorial", entity));
                default:
                    return O("factorial", entity);
            }
        }

        /// <summary>A script base with the suffix parsed after it: [kind, value] of the scriptSuffix rule.</summary>
        public static object ScriptedBase(object scriptBase, List<object> suffix)
        {
            switch ((string)suffix[0])
            {
                case "subcaret":
                    return A(scriptBase, O("operator", "_"), suffix[1], ColoredOperator("^"));
                case "trailing":
                    return A(scriptBase, ColoredOperator(suffix[1]));
                default:
                    var script = (JsObject)suffix[1];
                    script["base"] = scriptBase;
                    script["type"] = suffix[0];
                    return O("script", script);
            }
        }

        public static JsObject ColoredOperator(object op) => O("colored", O(("color", "#F01"), ("of", O("operator", op))));

        /// <summary>function: the chars of a function name, without a trailing function apply character.</summary>
        public static string FunctionName(object name, bool trimApply)
        {
            if (name is List<object> list)
            {
                var text = Join(list);
                if (trimApply && Js.Len(text) > 0 && Js.Ch(text, Js.Len(text) - 1) == "\u2061")
                    text = Js.Sub(text, 0, Js.Len(text) - 1);
                return text;
            }
            return (string)name;
        }
    }

    /// <summary>
    /// The atoms that start with (exp) and its diacritics: what diacriticbase would read after the bracket that the
    /// diacritics make the entity refuse, read once with the bracket.
    /// </summary>
    internal sealed class DiacriticizedParen
    {
        public DiacriticizedParen(object atoms) => Atoms = atoms;

        public object Atoms { get; }
    }

    /// <summary>
    /// The offsets of the input at which the brackets with contents failed, the memoization the generated parser
    /// lacks: per thread, for the input being parsed (the same string instance, as the result at an offset depends only
    /// on the input).
    /// </summary>
    internal static class BracketFailures
    {
        [System.ThreadStatic]
        private static string input;

        [System.ThreadStatic]
        private static HashSet<int> offsets;

        private static HashSet<int> Of(string buffer)
        {
            if (!ReferenceEquals(buffer, input) || offsets == null)
            {
                input = buffer;
                offsets = new HashSet<int>();
            }
            return offsets;
        }

        public static bool Untried(string buffer, int offset) => !Of(buffer).Contains(offset);

        /// <summary>Remembers the failure at the offset, and fails.</summary>
        public static bool Fail(string buffer, int offset)
        {
            Of(buffer).Add(offset);
            return false;
        }
    }

    /// <summary>
    /// The entities the grammar tried on this thread: a measure of the work of its parses that is the same on every
    /// machine, which the tests bound (an alternative that parses again what another one parsed makes it exponential).
    /// </summary>
    internal static class UnicodeMathGrammarWork
    {
        [System.ThreadStatic]
        public static long Steps;

        /// <summary>Counts an entity tried, and goes on.</summary>
        public static bool Step()
        {
            Steps++;
            return true;
        }
    }
}
