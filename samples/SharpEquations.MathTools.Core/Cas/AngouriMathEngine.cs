namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AngouriMath;
    using SharpEquations;

    /// <summary>
    /// The computer algebra of the math tools, with AngouriMath: the equation is read by <see cref="MathReader"/>, and
    /// the results come back through LaTeX, which AngouriMath writes and SharpEquations reads.
    /// </summary>
    public sealed class AngouriMathEngine : ICasEngine
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, Entity> definitions = new Dictionary<string, Entity>(StringComparer.Ordinal);

        public string Name => "AngouriMath";

        public AngleUnit Angles { get; set; }

        public IReadOnlyDictionary<string, Equation> Definitions
        {
            get
            {
                lock (gate)
                    return definitions.ToDictionary(d => d.Key, d => Parse(Latex(d.Value)) ?? Equation.Parse(d.Value.ToString(), EquationFormat.UnicodeMath));
            }
        }

        public void ClearDefinitions()
        {
            lock (gate)
                definitions.Clear();
        }

        public IReadOnlyList<string> Variables(Equation equation)
        {
            if (MathReader.TryReadDefinition(equation, Angles, out _, out var value))
                return Variables(Substitute(value));
            return Variables(MathReader.ReadSystem(equation, Angles).Select(Substitute));
        }

        public CasResult Run(CasAction action, Equation equation, string? variable = null)
        {
            if (MathReader.TryReadDefinition(equation, Angles, out var name, out var value))
            {
                value = Substitute(value);
                if (action != CasAction.Define)
                    return Run(action, value, variable);
                if (value.Vars.Any(v => v.Name == name))
                    throw new NotSupportedException("“" + name + "” cannot be defined by itself.");
                lock (gate)
                    definitions[name] = value;
                var defined = Result(CasAction.Define, Latex(MathS.Var(name)) + ":=" + Latex(value), name + " ≔ " + value.ToString());
                return defined with { Values = Equations(new[] { value }) };
            }
            if (action == CasAction.Define)
                throw new NotSupportedException("A definition is a letter, ≔ and its value: a≔5.");

            var system = MathReader.ReadSystem(equation, Angles).Select(Substitute).ToList();
            if (system.Count == 1)
                return Run(action, system[0], variable);
            if (action != CasAction.Solve)
                throw new NotSupportedException("A system of equations: it can only be solved.");
            return SolveSystem(system);
        }

        /// <summary>The letters of expressions, x first when there is one.</summary>
        public static IReadOnlyList<string> Variables(params Entity[] expressions) => Variables((IEnumerable<Entity>)expressions);

        private static List<string> Variables(IEnumerable<Entity> expressions) =>
            expressions.SelectMany(e => e.Vars).Select(v => v.Name).Distinct()
                .OrderBy(v => v == "x" ? 0 : v == "y" ? 1 : v == "z" ? 2 : 3).ThenBy(v => v, StringComparer.Ordinal).ToList();

        /// <summary>Does an action to an expression of AngouriMath.</summary>
        public static CasResult Run(CasAction action, Entity expression, string? variable)
        {
            var letters = Variables(expression);
            var x = MathS.Var(variable ?? (letters.Count > 0 ? letters[0] : "x"));
            switch (action)
            {
                case CasAction.Evaluate:
                    return Value(expression);
                case CasAction.Solve:
                    var equation = expression is Entity.Equalsf ? expression : expression.Equalizes(0);
                    var solutions = Solve(equation, x);
                    return Approximations(Result(action, Latex(x) + @"\in " + Latex(solutions), x.ToString() + " ∈ " + solutions.ToString()) with
                    {
                        Values = solutions is Entity.Set.FiniteSet set ? Equations(set.Elements) : Array.Empty<Equation>(),
                    }, x, solutions);
                case CasAction.Simplify:
                    return Result(action, Tidy(expression.Simplify()));
                case CasAction.Expand:
                    return Result(action, Tidy(expression.Expand()));
                case CasAction.Factor:
                    return Result(action, Tidy(expression.Factorize()));
                case CasAction.Differentiate:
                    return Result(action, Tidy(expression.Differentiate(x).Simplify()));
                case CasAction.Integrate:
                    return Result(action, Tidy(expression.Integrate(x).Simplify()));
                case CasAction.Define:
                    throw new NotSupportedException("A definition is a letter, ≔ and its value: a≔5.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Not an action of the engine.");
            }
        }

        // the letters defined replaced by their values, which may use other letters defined before
        private Entity Substitute(Entity expression)
        {
            lock (gate)
            {
                for (var round = 0; round < 8 && expression.Vars.Any(v => definitions.ContainsKey(v.Name)); round++)
                    foreach (var letter in expression.Vars.Where(v => definitions.ContainsKey(v.Name)).ToList())
                        expression = expression.Substitute(letter, definitions[letter.Name]);
            }
            return expression;
        }

        // the solutions as a set; an absolute value of the letter splits the equation in two (|u|=v: u=v or −u=v), and
        // the solutions of either that do not solve the equation are dropped
        private static Entity Solve(Entity equation, Entity.Variable x)
        {
            if (equation.Nodes.OfType<Entity.Absf>().FirstOrDefault(a => a.Vars.Contains(x)) is not { } abs)
                return Tidy(equation.Solve(x).Simplify());
            var candidates = new List<Entity>();
            foreach (var branch in new[] { abs.Argument, -abs.Argument })
            {
                if (Solve(equation.Substitute(abs, branch), x) is not Entity.Set.FiniteSet set)
                    return Tidy(equation.Solve(x).Simplify());
                candidates.AddRange(set.Elements);
            }
            var solutions = candidates.Where(c => IsSolution(equation, x, c)).GroupBy(c => c.ToString()).Select(g => g.First()).ToArray();
            return MathS.Sets.Finite(solutions);
        }

        private static bool IsSolution(Entity equation, Entity.Variable x, Entity value)
        {
            if (equation is not Entity.Equalsf eq)
                return true;
            return (eq.Left - eq.Right).Substitute(x, value).EvalNumerical() is Entity.Number.Complex difference
                && difference.RealPart.EDecimal.ToDouble() is var re && difference.ImaginaryPart.EDecimal.ToDouble() is var im
                && Math.Abs(re) < 1e-9 && Math.Abs(im) < 1e-9
                && value.EvalNumerical() is Entity.Number.Complex number && Math.Abs(number.ImaginaryPart.EDecimal.ToDouble()) < 1e-12;
        }

        private static CasResult SolveSystem(IReadOnlyList<Entity> system)
        {
            var letters = Variables(system).Select(MathS.Var).ToArray();
            var equations = system.Select(e => e is Entity.Equalsf eq ? eq.Left - eq.Right : e).ToArray();
            var solutions = MathS.Equations(equations).Solve(letters);
            if (solutions == null || solutions.RowCount == 0)
                return new CasResult(CasAction.Solve, Parse(@"\text{no solution}"), "No solution");
            var latex = new List<string>();
            var text = new List<string>();
            var values = new List<Entity>();
            for (var row = 0; row < solutions.RowCount; row++)
            {
                var line = Enumerable.Range(0, letters.Length).Select(i => (Letter: letters[i], Value: Tidy(solutions[row, i].Simplify()))).ToList();
                values.AddRange(line.Select(l => l.Value));
                latex.Add(string.Join(@",\ ", line.Select(l => Latex(l.Letter) + "=" + Latex(l.Value))));
                text.Add(string.Join(", ", line.Select(l => l.Letter.ToString() + " = " + l.Value.ToString())));
            }
            return Result(CasAction.Solve, string.Join(@";\quad ", latex), string.Join("; ", text)) with { Values = Equations(values) };
        }

        // what AngouriMath writes that is not a value: the conditions of a result (x+1 provided x≠1) are dropped, and
        // ln(e^u) is u (the letters are real)
        private static Entity Tidy(Entity value) => value.Replace(e => e switch
        {
            Entity.Providedf provided => provided.Expression,
            Entity.Logf { Base: var b, Antilogarithm: Entity.Powf { Base: var p, Exponent: var u } } when b == MathS.e && p == MathS.e => u,
            _ => e,
        });

        // the exact value, and the decimal one when it differs: 1/3 ≈ 0.333333, √2 ≈ 1.41421
        private static CasResult Value(Entity expression)
        {
            if (expression is Entity.Equalsf || expression.Vars.Any())
                return Result(CasAction.Evaluate, Tidy(expression.Simplify()));

            var exact = Tidy(expression.Simplify());
            var approximate = Decimal(exact.EvalNumerical());
            var exactLatex = Latex(exact);
            var exactText = exact.ToString();
            var values = Equations(new[] { exact });
            if (approximate == null || approximate == exactText)
                return Result(CasAction.Evaluate, exactLatex, exactText) with { Values = values };
            return Result(CasAction.Evaluate, exactLatex + @"\approx " + approximate, exactText + " ≈ " + approximate) with { Values = values };
        }

        // x ∈ {…} with radicals gets a second line, x ≈ {1.2071, -0.5+0.866i}: the same solutions as decimal numbers
        private static CasResult Approximations(CasResult result, Entity.Variable x, Entity solutions)
        {
            if (solutions is not Entity.Set.FiniteSet set || !set.Elements.Any())
                return result;
            var exact = set.Elements.ToList();
            if (exact.All(e => e is Entity.Number))
                return result;

            var values = new List<string>();
            foreach (var element in exact)
            {
                string? value;
                try
                {
                    value = Decimal(element.EvalNumerical(), 6);
                }
                catch (AngouriMath.Core.Exceptions.AngouriMathBaseException)
                {
                    value = null;
                }
                if (value == null)
                    return result;
                values.Add(value);
            }

            var list = string.Join(", ", values);
            var approximate = Result(CasAction.Solve, Latex(x) + @"\approx \left\{" + string.Join(@",\: ", values) + @"\right\}", x.ToString() + " ≈ {" + list + "}");
            return result with { Approximate = approximate.Math, ApproximateText = approximate.Text };
        }

        /// <summary>A number of AngouriMath as the calculator shows it: <paramref name="digits"/> significant digits, <c>a+bi</c> for a complex one; null for what is not a number.</summary>
        public static string? Decimal(Entity value, int digits = 10)
        {
            switch (value)
            {
                case Entity.Number.Real real:
                    return Format(real.EDecimal.ToDouble(), digits);
                case Entity.Number.Complex complex:
                    var re = complex.RealPart.EDecimal.ToDouble();
                    var im = complex.ImaginaryPart.EDecimal.ToDouble();
                    if (Math.Abs(im) < 1e-12 * Math.Max(1, Math.Abs(re)))
                        return Format(re, digits);
                    var imaginary = (Math.Abs(im) == 1 ? "" : Format(Math.Abs(im), digits)) + "i";
                    return Math.Abs(re) < 1e-12 * Math.Abs(im) ? (im < 0 ? "-" : "") + imaginary : Format(re, digits) + (im < 0 ? "-" : "+") + imaginary;
                default:
                    return null;
            }
        }

        private static string Format(double value, int digits) =>
            double.IsInfinity(value) ? (value > 0 ? "∞" : "-∞") : value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        private static CasResult Result(CasAction action, Entity value) =>
            Result(action, Latex(value), value.ToString()) with { Values = Equations(new[] { value }) };

        // the values that read back from their LaTeX (all, or none)
        private static IReadOnlyList<Equation> Equations(IEnumerable<Entity> values)
        {
            var equations = new List<Equation>();
            foreach (var value in values)
            {
                if (Parse(Latex(value)) is not { } equation)
                    return Array.Empty<Equation>();
                equations.Add(equation);
            }
            return equations;
        }

        private static Equation? Parse(string latex) =>
            Equation.TryParse(latex, EquationFormat.LaTeX, new EquationOptions { Display = true }, out var parsed)
            && !parsed!.ToMathML().Descendants().Any(e => e.Name.LocalName == "merror")
                ? parsed
                : null;

        private static CasResult Result(CasAction action, string latex, string text)
        {
            return new CasResult(action, Parse(latex), text);
        }

        // the LaTeX of AngouriMath, with its constants as the LaTeX reader knows them
        private static string Latex(Entity value) => value.Latexize()
            .Replace(@"\mathrm{\pi}", @"\pi", StringComparison.Ordinal)
            .Replace(@"\mathrm{e}", "e", StringComparison.Ordinal)
            .Replace(@"\mathrm{i}", "i", StringComparison.Ordinal);
    }
}
