namespace SharpEquations.Internal.UnicodeMath
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using SharpEquations.Internal.Text;

    /// <summary>
    /// A JavaScript object of the UnicodeMathML port: its keys keep their insertion order, and a missing key
    /// reads as null (undefined). The syntax trees of UnicodeMathML are such objects, nested with lists
    /// (<see cref="List{T}"/> of object), strings, numbers (int or double) and booleans, so that its code
    /// translates line by line.
    /// </summary>
    internal sealed class JsObject
    {
        private readonly List<string> keys = new List<string>();
        private readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        public JsObject()
        {
        }

        public JsObject(string key, object value)
        {
            this[key] = value;
        }

        public object this[string key]
        {
            get => values.TryGetValue(key, out var value) ? value : null;
            set
            {
                if (!values.ContainsKey(key))
                    keys.Add(key);
                values[key] = value;
            }
        }

        public int Count => keys.Count;

        public IReadOnlyList<string> Keys => keys;

        public bool Has(string key) => values.ContainsKey(key);

        public void Remove(string key)
        {
            if (values.Remove(key))
                keys.Remove(key);
        }

        /// <summary>The first key, as k(ast) of UnicodeMathML.</summary>
        public string Key => keys.Count > 0 ? keys[0] : null;

        /// <summary>The first value, as v(ast) of UnicodeMathML.</summary>
        public object Value => keys.Count > 0 ? values[keys[0]] : null;

        public JsObject Clone()
        {
            var copy = new JsObject();
            foreach (var key in keys)
                copy[key] = Js.Clone(values[key]);
            return copy;
        }

        public override string ToString() => Js.Stringify(this);
    }

    /// <summary>
    /// JavaScript semantics used by the UnicodeMathML port.
    /// </summary>
    internal static class Js
    {
        public static JsObject O(string key, object value) => new JsObject(key, value);

        public static JsObject O(params (string Key, object Value)[] entries)
        {
            var o = new JsObject();
            foreach (var (key, value) in entries)
                o[key] = value;
            return o;
        }

        public static List<object> A(params object[] items) => new List<object>(items);

        /// <summary>JavaScript truthiness: null, false, 0, NaN and "" are false.</summary>
        public static bool Truthy(object value)
        {
            switch (value)
            {
                case null: return false;
                case bool b: return b;
                case string s: return s.Length > 0;
                case int i: return i != 0;
                case double d: return d != 0 && !double.IsNaN(d);
                default: return true;
            }
        }

        // properties of arrays: UnicodeMathML sets arg, intent and the like on arrays as well as on objects
        private static readonly ConditionalWeakTable<List<object>, JsObject> ArrayProperties = new ConditionalWeakTable<List<object>, JsObject>();

        /// <summary>The value of key in an object or an array (null for other values), as value.key in JavaScript.</summary>
        public static object Get(object value, string key)
        {
            if (value is JsObject o)
                return o[key];
            if (value is List<object> a && ArrayProperties.TryGetValue(a, out var properties))
                return properties[key];
            return null;
        }

        /// <summary>value.key = x: ignored for strings, numbers and null, as JavaScript ignores it for primitives.</summary>
        public static void Set(object value, string key, object x)
        {
            if (value is JsObject o)
                o[key] = x;
            else if (value is List<object> a)
                ArrayProperties.GetOrCreateValue(a)[key] = x;
        }

        /// <summary>key in value.</summary>
        public static bool Has(object value, string key)
        {
            if (value is JsObject o)
                return o.Has(key);
            return value is List<object> a && ArrayProperties.TryGetValue(a, out var properties) && properties.Has(key);
        }

        /// <summary>delete value.key.</summary>
        public static void Delete(object value, string key)
        {
            if (value is JsObject o)
                o.Remove(key);
            else if (value is List<object> a && ArrayProperties.TryGetValue(a, out var properties))
                properties.Remove(key);
        }

        /// <summary>value[i] of an array or a string (a one-character string); null out of range or for other values.</summary>
        public static object At(object value, int i)
        {
            if (value is List<object> a)
                return i >= 0 && i < a.Count ? a[i] : null;
            if (value is string s)
                return Ch(s, i);
            return null;
        }

        /// <summary>
        /// The code point at index i of a string, indexed by code points (never by UTF-16 units), as a string; null out of range.
        /// The strings of the port are read this way where the JavaScript reads UTF-16 units (str[i]).
        /// </summary>
        public static string Ch(string s, int i)
        {
            if (s == null || i < 0)
                return null;
            foreach (var codePoint in CodePoints.Of(s))
            {
                if (i-- == 0)
                    return CodePoints.ToString(codePoint);
            }
            return null;
        }

        /// <summary>The number of code points of a string.</summary>
        public static int Len(string s) => s == null ? 0 : CodePoints.Of(s).Count();

        /// <summary>The code point value at index i (by code points) of a string; -1 out of range.</summary>
        public static int CodeAt(string s, int i)
        {
            if (s == null || i < 0)
                return -1;
            foreach (var codePoint in CodePoints.Of(s))
            {
                if (i-- == 0)
                    return CodePoints.Value(codePoint);
            }
            return -1;
        }

        /// <summary>str.substring(start, end) by code points: indexes clamped, and swapped when start is after end.</summary>
        public static string Sub(string s, int start, int? end = null)
        {
            var codePoints = CodePoints.Of(s).ToList();
            int from = Math.Max(0, Math.Min(start, codePoints.Count));
            int to = Math.Max(0, Math.Min(end ?? codePoints.Count, codePoints.Count));
            if (from > to)
                (from, to) = (to, from);
            return string.Concat(codePoints.Skip(from).Take(to - from).Select(CodePoints.ToString));
        }

        /// <summary>value.length of an array or a string; null (undefined) for other values.</summary>
        public static int? Length(object value)
        {
            if (value is List<object> a)
                return a.Count;
            if (value is string s)
                return Len(s);
            return null;
        }

        /// <summary>a &lt; b for strings in JavaScript: false when either is undefined.</summary>
        public static bool Less(string a, string b) => a != null && b != null && string.CompareOrdinal(a, b) < 0;

        /// <summary>a == b in JavaScript for the strings and numbers of the syntax trees.</summary>
        public static bool Eq(object a, object b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            if (a is string sa && b is string sb)
                return sa == sb;
            if (a is string || b is string || a is int || b is int || a is double || b is double)
            {
                var da = ToNumber(a);
                var db = ToNumber(b);
                if (!double.IsNaN(da) || !double.IsNaN(db))
                    return da == db;
                return Str(a) == Str(b);
            }
            return ReferenceEquals(a, b) || a.Equals(b);
        }

        private static double ToNumber(object value)
        {
            switch (value)
            {
                case int i: return i;
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s:
                    return s.Trim().Length == 0 ? 0
                        : double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
                default: return double.NaN;
            }
        }

        /// <summary>k(ast): the first key of an object; for an array, its first index ("0") as Object.keys does.</summary>
        public static string K(object value)
        {
            if (value is JsObject o)
                return o.Key;
            if (value is List<object> a)
                return a.Count > 0 ? "0" : null;
            if (value is string s)
                return s.Length > 0 ? "0" : null;
            return null;
        }

        /// <summary>v(ast): the first value of an object; for an array, its first element.</summary>
        public static object V(object value)
        {
            if (value is JsObject o)
                return o.Value;
            if (value is List<object> a)
                return a.Count > 0 ? a[0] : null;
            if (value is string s)
                return Ch(s, 0);
            return null;
        }

        public static object Clone(object value)
        {
            switch (value)
            {
                case JsObject o: return o.Clone();
                case List<object> a:
                    var copy = a.Select(Clone).ToList();
                    if (ArrayProperties.TryGetValue(a, out var properties))
                        ArrayProperties.Add(copy, properties.Clone());
                    return copy;
                default: return value;
            }
        }

        /// <summary>The string conversion of JavaScript (String(value)).</summary>
        public static string Str(object value)
        {
            switch (value)
            {
                case null: return "undefined";
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case List<object> a: return string.Join(",", a.Select(x => x == null ? "" : Str(x)));
                default: return "[object Object]";
            }
        }

        public static string Stringify(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case List<object> a: return "[" + string.Join(",", a.Select(Stringify)) + "]";
                case JsObject o: return "{" + string.Join(",", o.Keys.Select(k => Stringify(k) + ":" + Stringify(o[k]))) + "}";
                default: return Str(value);
            }
        }

        /// <summary>parseInt for the digit strings of the grammar.</summary>
        public static int ParseInt(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
