namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AngouriMath;
    using SharpEquations;

    /// <summary>The kinds of graph an equation draws.</summary>
    public enum GraphKind
    {
        /// <summary><c>y=f(x)</c>, or an expression of x.</summary>
        Function,

        /// <summary><c>r=f(θ)</c>.</summary>
        Polar,

        /// <summary><c>x=f(t), y=g(t)</c>.</summary>
        Parametric,

        /// <summary>A relation of x and y, <c>x²+y²=25</c>: where it holds.</summary>
        Implicit,
    }

    /// <summary>
    /// What an equation draws: its kind, the functions of the graph (compiled once) and its parameters, the other
    /// letters, which take the values of <see cref="Parameters"/> (1 to start with).
    /// </summary>
    public sealed class GraphPlot
    {
        private readonly double[] values;
        private readonly Func<double[], double> first;
        private readonly Func<double[], double>? second;

        private GraphPlot(GraphKind kind, Func<double[], double> first, Func<double[], double>? second, IReadOnlyList<string> parameters)
        {
            Kind = kind;
            this.first = first;
            this.second = second;
            Parameters = parameters;
            values = new double[2 + parameters.Count];
            for (var i = 0; i < parameters.Count; i++)
                values[2 + i] = 1;
        }

        public GraphKind Kind { get; }

        /// <summary>The letters that are not variables of the graph, in the order of their values, as written (μ, not mu).</summary>
        public IReadOnlyList<string> Parameters { get; }

        /// <summary>The range of the variable of a polar or parametric graph: 0 to 2π.</summary>
        public (double Min, double Max) Range { get; } = (0, 2 * Math.PI);

        /// <summary>The value of a parameter.</summary>
        public double this[string parameter]
        {
            get => values[2 + IndexOf(parameter)];
            set => values[2 + IndexOf(parameter)] = value;
        }

        /// <summary>y for x, of a <see cref="GraphKind.Function"/>.</summary>
        public double Y(double x) => Evaluate(first, x, 0);

        /// <summary>The point for t (θ for a polar graph) of a <see cref="GraphKind.Polar"/> or <see cref="GraphKind.Parametric"/> graph.</summary>
        public (double X, double Y) Point(double t)
        {
            if (Kind == GraphKind.Polar)
            {
                var r = Evaluate(first, t, 0);
                return (r * Math.Cos(t), r * Math.Sin(t));
            }
            return (Evaluate(first, t, 0), Evaluate(second!, t, 0));
        }

        /// <summary>F(x, y) of an <see cref="GraphKind.Implicit"/> graph: the relation is F = 0.</summary>
        public double F(double x, double y) => Evaluate(first, x, y);

        /// <summary>The graph of an equation, with the letters defined by the engine replaced by their values.</summary>
        /// <exception cref="NotSupportedException">The equation has no graph, as the message says.</exception>
        public static GraphPlot FromEquation(Equation equation, AngleUnit angles = AngleUnit.Radians, IReadOnlyDictionary<string, Equation>? definitions = null)
        {
            var defined = (definitions ?? new Dictionary<string, Equation>()).ToDictionary(d => d.Key, d => MathReader.Read(d.Value, angles), StringComparer.Ordinal);
            var system = MathReader.ReadSystem(equation, angles).Select(e => Substitute(e, defined)).ToList();
            switch (system)
            {
                case [Entity.Equalsf { Left: Entity.Variable { Name: "x" }, Right: var fx }, Entity.Equalsf { Left: Entity.Variable { Name: "y" }, Right: var fy }]:
                    return Make(GraphKind.Parametric, fx, fy, "t", "");
                case [Entity.Equalsf { Left: Entity.Variable { Name: "y" }, Right: var f }] when !Has(f, "y"):
                    return Make(GraphKind.Function, f, null, "x", "");
                case [Entity.Equalsf { Left: var f, Right: Entity.Variable { Name: "y" } }] when !Has(f, "y"):
                    return Make(GraphKind.Function, f, null, "x", "");
                case [Entity.Equalsf { Left: Entity.Variable { Name: "r" }, Right: var r }] when !Has(r, "r"):
                    return Make(GraphKind.Polar, r, null, "theta", "");
                case [Entity.Equalsf relation]:
                    if (!Has(relation, "x") && !Has(relation, "y"))
                        throw new NotSupportedException("No x or y to draw: write y=…, r=… (θ), x=…, y=… (t), or a relation of x and y.");
                    return Make(GraphKind.Implicit, relation.Left - relation.Right, null, "x", "y");
                case [var expression] when expression is not Entity.Statement:
                    if (Has(expression, "y"))
                        throw new NotSupportedException("An expression of y: write the relation, with =.");
                    return Make(GraphKind.Function, expression, null, "x", "");
                default:
                    throw new NotSupportedException("No graph: write y=…, r=… (θ), x=…, y=… (t), or a relation of x and y.");
            }
        }

        private static GraphPlot Make(GraphKind kind, Entity f, Entity? g, string u, string v)
        {
            var parameters = f.Vars.Concat(g?.Vars ?? Enumerable.Empty<Entity.Variable>()).Select(l => l.Name)
                .Where(n => n != u && n != v).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            var letters = new[] { u, v }.Concat(parameters).ToList();
            return new GraphPlot(kind, NumericCompiler.Compile(f, letters), g == null ? null : NumericCompiler.Compile(g, letters),
                parameters.Select(MathReader.Symbol).ToList());
        }

        private static Entity Substitute(Entity e, Dictionary<string, Entity> defined)
        {
            foreach (var letter in e.Vars.Where(v => defined.ContainsKey(v.Name)).ToList())
                e = e.Substitute(letter, defined[letter.Name]);
            return e;
        }

        private static bool Has(Entity e, string letter) => e.Vars.Any(v => v.Name == letter);

        private int IndexOf(string parameter)
        {
            for (var i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i] == parameter || Parameters[i] == MathReader.Symbol(parameter))
                    return i;
            }
            throw new ArgumentException("“" + parameter + "” is not a parameter of the graph.", nameof(parameter));
        }

        private double Evaluate(Func<double[], double> f, double u, double v)
        {
            lock (values)
            {
                values[0] = u;
                values[1] = v;
                return f(values);
            }
        }
    }
}
