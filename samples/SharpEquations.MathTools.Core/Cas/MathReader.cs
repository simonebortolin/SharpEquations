namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using AngouriMath;
    using SharpEquations;

    /// <summary>
    /// Reads what an equation means from its normalized presentation MathML, as an expression of AngouriMath: numbers,
    /// letters and constants (π, e, i), the operators, implicit multiplication (<c>2x</c>), functions applied with the
    /// invisible function application of the parsers (<c>sin x</c>, <c>sin²x</c>, <c>log_2 x</c>), fractions, roots,
    /// powers, absolute values, factorials, sums and integrals with their limits, and <c>d/dx</c>.
    /// </summary>
    public static class MathReader
    {
        /// <summary>Reads an equation; a relation (<c>a=b</c>) is an equality of AngouriMath, and a trailing <c>=</c> is dropped.</summary>
        /// <exception cref="NotSupportedException">The equation has something that has no meaning here, as the message says.</exception>
        public static Entity Read(Equation equation) => new Reader().Relation(Items(equation.ToMathML()));

        private static IEnumerable<XElement> Items(XElement row) =>
            row.Elements().Where(e => e.Name.LocalName is not ("mspace" or "annotation" or "annotation-xml"));

        private static string Name(XElement e) => e.Name.LocalName;

        private static bool IsOperator(XElement e, params string[] texts) => Name(e) == "mo" && texts.Contains(e.Value.Trim());

        private sealed class Reader
        {
            private static readonly Dictionary<string, Func<Entity, Entity>> Functions = new Dictionary<string, Func<Entity, Entity>>(StringComparer.Ordinal)
            {
                ["sin"] = MathS.Sin,
                ["cos"] = MathS.Cos,
                ["tan"] = MathS.Tan,
                ["cot"] = MathS.Cotan,
                ["sec"] = MathS.Sec,
                ["csc"] = MathS.Cosec,
                ["arcsin"] = MathS.Arcsin,
                ["arccos"] = MathS.Arccos,
                ["arctan"] = MathS.Arctan,
                ["sinh"] = MathS.Hyperbolic.Sinh,
                ["cosh"] = MathS.Hyperbolic.Cosh,
                ["tanh"] = MathS.Hyperbolic.Tanh,
                ["ln"] = MathS.Ln,
                ["log"] = x => MathS.Log(10, x),
                ["exp"] = x => MathS.Pow(MathS.e, x),
                ["sgn"] = MathS.Signum,
            };

            private List<XElement> items = new List<XElement>();
            private int at;

            private XElement? Peek => at < items.Count ? items[at] : null;

            public Entity Relation(IEnumerable<XElement> row)
            {
                var parts = Split(row.ToList(), e => IsOperator(e, "="));
                if (parts.Count > 1 && parts[^1].Count == 0)
                    parts.RemoveAt(parts.Count - 1);
                if (parts.Count > 2)
                    throw new NotSupportedException("More than one = in the equation.");
                var left = Expression(parts[0]);
                return parts.Count == 2 ? left.Equalizes(Expression(parts[1])) : left;
            }

            private Entity Expression(IEnumerable<XElement> row)
            {
                var saved = (items, at);
                items = Flatten(row).ToList();
                at = 0;
                if (items.Count == 0)
                    throw new NotSupportedException("Something is missing: an empty place.");
                var result = Sum();
                if (Peek is { } rest)
                    throw new NotSupportedException("Cannot read “" + Text(rest) + "” here.");
                (items, at) = saved;
                return result;
            }

            private Entity Sum()
            {
                var result = Product();
                while (Peek is { } op && IsOperator(op, "+", "-", "−"))
                {
                    at++;
                    var right = Product();
                    result = op.Value.Trim() == "+" ? result + right : result - right;
                }
                return result;
            }

            private Entity Product()
            {
                var result = Signed();
                while (Peek is { } next && !IsOperator(next, "+", "-", "−", "=", ",") && !IsDifferential())
                {
                    if (IsOperator(next, "×", "·", "⋅", "*", "∗", "⁢"))
                    {
                        at++;
                        result *= Signed();
                    }
                    else if (IsOperator(next, "/", "÷", "∕"))
                    {
                        at++;
                        result /= Signed();
                    }
                    else
                    {
                        result *= Signed();
                    }
                }
                return result;
            }

            private Entity Signed()
            {
                if (Peek is { } op && IsOperator(op, "-", "−", "+"))
                {
                    at++;
                    var operand = Signed();
                    return op.Value.Trim() == "+" ? operand : -operand;
                }
                return Postfix();
            }

            private Entity Postfix()
            {
                var result = Primary();
                while (Peek is { } op && IsOperator(op, "!"))
                {
                    at++;
                    result = MathS.Factorial(result);
                }
                return result;
            }

            private Entity Primary()
            {
                if (at >= items.Count)
                    throw new NotSupportedException("Something is missing after “" + Text(items[^1]) + "”.");
                var item = items[at++];

                // a function applied: sin x, sin²x, log_2 x
                if (Peek is { } apply && IsOperator(apply, "⁡"))
                {
                    at++;
                    var argument = Postfix();
                    return Apply(item, argument);
                }

                if (BigOperator(item) is { } big)
                    return Big(big.Symbol, big.Lower, big.Upper);

                if (Derivative(item) is { } variable)
                    return MathS.Derivative(Postfix(), variable);

                return Node(item);
            }

            private Entity Apply(XElement function, Entity argument)
            {
                switch (Name(function))
                {
                    case "mi" when Functions.TryGetValue(function.Value.Trim(), out var f):
                        return f(argument);
                    case "msup" when function.Elements().First() is { } name && Functions.TryGetValue(name.Value.Trim(), out var f):
                        var power = function.Elements().ElementAt(1);
                        // sin^(-1) x is the arcsine, not the cosecant
                        if (IsMinusOne(power) && Functions.TryGetValue("arc" + name.Value.Trim(), out var inverse))
                            return inverse(argument);
                        return MathS.Pow(f(argument), Node(power));
                    case "msub" when function.Elements().First().Value.Trim() == "log":
                        return MathS.Log(Node(function.Elements().ElementAt(1)), argument);
                    default:
                        throw new NotSupportedException("Unknown function “" + Text(function) + "”.");
                }
            }

            // ∑_(k=1)^n k², ∫_0^1 x dx: the operand of a sum is the next item (the parsers group it); the operand of an
            // integral goes on to its differential, dx or ⅆx
            private Entity Big(string symbol, XElement? lower, XElement? upper)
            {
                switch (symbol)
                {
                    case "∑" or "∏":
                        if (lower == null || upper == null)
                            throw new NotSupportedException(symbol + " needs its limits: ∑_(k=1)^n.");
                        var bound = Split(Items(Row(lower)).ToList(), e => IsOperator(e, "="));
                        if (bound.Count != 2 || bound[0].Count != 1 || Name(bound[0][0]) != "mi")
                            throw new NotSupportedException("The lower limit of " + symbol + " is a variable and its start: k=1.");
                        var index = MathS.Var(Identifier(bound[0][0].Value));
                        var from = Expression(bound[1]);
                        var to = Expression(Items(Row(upper)));
                        var body = Postfix();
                        return symbol == "∑" ? MathS.Sum(body, index, from, to) : MathS.Product(body, index, from, to);
                    case "∫":
                        // the operand grouped with its differential (∫_0^1 x² dx): its items go on in the row
                        if (Peek is { } group && Name(group) == "mrow" && !(group.Elements().FirstOrDefault() is { } open && Name(open) == "mo"))
                        {
                            items.RemoveAt(at);
                            items.InsertRange(at, Items(group));
                        }
                        var start = at;
                        while (at < items.Count && !IsDifferential())
                            at++;
                        if (at >= items.Count)
                            throw new NotSupportedException("The integral needs its differential: ∫ x dx.");
                        var integrand = Expression(items.Skip(start).Take(at - start));
                        at++;
                        var variable = MathS.Var(Identifier(items[at++].Value));
                        return lower != null && upper != null
                            ? MathS.Integral(integrand, variable, Node(lower), Node(upper))
                            : MathS.Integral(integrand, variable);
                    default:
                        throw new NotSupportedException("Cannot compute “" + symbol + "”.");
                }
            }

            // the parsers group an integral with the operand before its differential (∫_0^1 x, then dx): the row goes on
            // with what the integral holds, so that the integral reaches its differential
            private static IEnumerable<XElement> Flatten(IEnumerable<XElement> row)
            {
                foreach (var item in row)
                {
                    if (Name(item) == "mrow" && item.Elements().FirstOrDefault() is { } first && BigOperator(first) is { Symbol: "∫" })
                    {
                        foreach (var inner in Flatten(Items(item)))
                            yield return inner;
                    }
                    else
                    {
                        yield return item;
                    }
                }
            }

            // d or ⅆ, then a letter: the differential that ends an integral
            private bool IsDifferential() =>
                at + 1 < items.Count && Name(items[at]) == "mi" && items[at].Value.Trim() is "d" or "ⅆ" && Name(items[at + 1]) == "mi";

            private static (string Symbol, XElement? Lower, XElement? Upper)? BigOperator(XElement item)
            {
                var name = Name(item);
                if (name == "mo" && item.Value.Trim() is "∑" or "∏" or "∫")
                    return (item.Value.Trim(), null, null);

                if (name is "msubsup" or "munderover" or "msub" or "munder" && item.Elements().First() is { } symbol && IsOperator(symbol, "∑", "∏", "∫"))
                {
                    var limits = item.Elements().Skip(1).ToList();
                    return (symbol.Value.Trim(), limits[0], limits.Count > 1 ? limits[1] : null);
                }
                return null;
            }

            // d/dx, ⅆ/ⅆx: a fraction of d over d and a letter
            private static Entity.Variable? Derivative(XElement item)
            {
                if (Name(item) != "mfrac")
                    return null;
                var top = Items(Row(item.Elements().First())).ToList();
                var bottom = Items(Row(item.Elements().ElementAt(1))).ToList();
                if (top.Count == 1 && top[0].Value.Trim() is "d" or "ⅆ" && bottom.Count == 2 && bottom[0].Value.Trim() is "d" or "ⅆ" && Name(bottom[1]) == "mi")
                    return MathS.Var(Identifier(bottom[1].Value));
                return null;
            }

            private Entity Node(XElement e)
            {
                switch (Name(e))
                {
                    case "mn":
                        return Number(e.Value.Trim());
                    case "mi":
                        return Letter(e.Value.Trim());
                    case "mrow" or "mstyle" or "mpadded" or "menclose" or "math":
                        return Fenced(Items(e).ToList());
                    case "mfrac" when e.Attribute("linethickness")?.Value is "0" or "0pt":
                        return MathS.Binomial(Node(e.Elements().First()), Node(e.Elements().ElementAt(1)));
                    case "mfrac":
                        return Node(e.Elements().First()) / Node(e.Elements().ElementAt(1));
                    case "msqrt":
                        return MathS.Sqrt(Expression(Items(e)));
                    case "mroot":
                        return MathS.Pow(Node(e.Elements().First()), 1 / Node(e.Elements().ElementAt(1)));
                    case "msup":
                        var exponent = e.Elements().ElementAt(1);
                        if (exponent.Value.Trim() is "′" or "″")
                            throw new NotSupportedException("Primes are not read: write d/dx.");
                        return MathS.Pow(Node(e.Elements().First()), Node(exponent));
                    case "msub" when e.Elements().All(c => Name(c) is "mi" or "mn"):
                        // x₁ is a letter of its own
                        return MathS.Var(Identifier(e.Elements().First().Value) + "_" + Identifier(e.Elements().ElementAt(1).Value));
                    default:
                        throw new NotSupportedException("Cannot compute “" + Text(e) + "”.");
                }
            }

            // (…), [ ], |x|, ‖x‖, ⌊x⌋, ⌈x⌉, or a row without fences
            private Entity Fenced(List<XElement> row)
            {
                if (row.Count >= 2 && Name(row[0]) == "mo" && Name(row[^1]) == "mo")
                {
                    var inner = row.Skip(1).Take(row.Count - 2);
                    switch (row[0].Value.Trim() + row[^1].Value.Trim())
                    {
                        case "()" or "[]" or "{}":
                            return Expression(inner);
                        case "||" or "‖‖":
                            return MathS.Abs(Expression(inner));
                        case "⌊⌋":
                            return MathS.Floor(Expression(inner));
                        case "⌈⌉":
                            return -MathS.Floor(-Expression(inner));
                    }
                }
                return Expression(row);
            }

            private static Entity Number(string text)
            {
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    throw new NotSupportedException("“" + text + "” is not a number.");
                return MathS.FromString(text);
            }

            private static Entity Letter(string text) => text switch
            {
                "π" => MathS.pi,
                "e" or "ⅇ" => MathS.e,
                "i" or "ⅈ" => MathS.i,
                "∞" => MathS.oo,
                _ => MathS.Var(Identifier(text)),
            };

            private static string Identifier(string text)
            {
                var name = text.Trim() switch
                {
                    "α" => "alpha",
                    "β" => "beta",
                    "γ" => "gamma",
                    "δ" => "delta",
                    "θ" => "theta",
                    "λ" => "lambda",
                    "μ" => "mu",
                    "σ" => "sigma",
                    "φ" or "ϕ" => "phi",
                    "ω" => "omega",
                    var other => other,
                };
                if (name.Length == 0 || !name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'))
                    throw new NotSupportedException("“" + text + "” is not a letter that can be computed.");
                return name;
            }

            private static bool IsMinusOne(XElement e) => string.Concat(Items(Row(e)).Select(c => c.Value.Trim())) is "−1" or "-1";

            private static XElement Row(XElement e) => Name(e) == "mrow" ? e : new XElement(e.Name.Namespace + "mrow", e);
        }

        private static List<List<XElement>> Split(List<XElement> row, Func<XElement, bool> separator)
        {
            var parts = new List<List<XElement>> { new List<XElement>() };
            foreach (var item in row)
            {
                if (separator(item))
                    parts.Add(new List<XElement>());
                else
                    parts[^1].Add(item);
            }
            return parts;
        }

        private static string Text(XElement e) => string.Concat(e.DescendantsAndSelf().Where(d => !d.HasElements).Select(d => d.Value));
    }
}
