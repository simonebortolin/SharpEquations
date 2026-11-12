namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AngouriMath;
    using SharpEquations;

    /// <summary>The kinds of graph in space an equation draws.</summary>
    public enum SurfaceKind
    {
        /// <summary><c>z=f(x,y)</c>, or an expression of x and y.</summary>
        Surface,

        /// <summary><c>x=f(t), y=g(t), z=h(t)</c>, on three lines: a curve in space.</summary>
        Curve,
    }

    /// <summary>
    /// What an equation draws in space: its kind, its functions (compiled once) and its parameters, the other letters,
    /// which take the values of <see cref="Parameters"/> (1 to start with).
    /// </summary>
    public sealed class SurfacePlot
    {
        private readonly double[] values;
        private readonly Func<double[], double>[] functions;

        private SurfacePlot(SurfaceKind kind, Func<double[], double>[] functions, IReadOnlyList<string> parameters)
        {
            Kind = kind;
            this.functions = functions;
            Parameters = parameters;
            values = new double[2 + parameters.Count];
            for (var i = 0; i < parameters.Count; i++)
                values[2 + i] = 1;
        }

        public SurfaceKind Kind { get; }

        /// <summary>The letters that are not variables of the graph, as written (μ, not mu).</summary>
        public IReadOnlyList<string> Parameters { get; }

        /// <summary>The range of t of a curve: 0 to 2π.</summary>
        public (double Min, double Max) Range { get; } = (0, 2 * Math.PI);

        /// <summary>The value of a parameter.</summary>
        public double this[string parameter]
        {
            get => values[2 + IndexOf(parameter)];
            set => values[2 + IndexOf(parameter)] = value;
        }

        /// <summary>z for (x, y), of a <see cref="SurfaceKind.Surface"/>.</summary>
        public double Z(double x, double y) => Evaluate(functions[0], x, y);

        /// <summary>The point for t of a <see cref="SurfaceKind.Curve"/>.</summary>
        public (double X, double Y, double Z) Point(double t)
        {
            lock (values)
            {
                values[0] = t;
                values[1] = 0;
                return (functions[0](values), functions[1](values), functions[2](values));
            }
        }

        /// <summary>The graph in space of an equation, with the letters defined by the engine replaced by their values.</summary>
        /// <exception cref="NotSupportedException">The equation has no graph in space, as the message says.</exception>
        public static SurfacePlot FromEquation(Equation equation, AngleUnit angles = AngleUnit.Radians, IReadOnlyDictionary<string, Equation>? definitions = null)
        {
            var defined = (definitions ?? new Dictionary<string, Equation>()).ToDictionary(d => d.Key, d => MathReader.Read(d.Value, angles), StringComparer.Ordinal);
            var system = MathReader.ReadSystem(equation, angles).Select(e => Substitute(e, defined)).ToList();
            switch (system)
            {
                case [Entity.Equalsf { Left: Entity.Variable { Name: "x" }, Right: var fx }, Entity.Equalsf { Left: Entity.Variable { Name: "y" }, Right: var fy }, Entity.Equalsf { Left: Entity.Variable { Name: "z" }, Right: var fz }]:
                    return Make(SurfaceKind.Curve, new[] { fx, fy, fz }, "t", "");
                case [Entity.Equalsf { Left: Entity.Variable { Name: "z" }, Right: var f }] when !Has(f, "z"):
                    return Make(SurfaceKind.Surface, new[] { f }, "x", "y");
                case [Entity.Equalsf { Left: var f, Right: Entity.Variable { Name: "z" } }] when !Has(f, "z"):
                    return Make(SurfaceKind.Surface, new[] { f }, "x", "y");
                case [var expression] when expression is not Entity.Statement && !Has(expression, "z"):
                    return Make(SurfaceKind.Surface, new[] { expression }, "x", "y");
                default:
                    throw new NotSupportedException("No graph in space: write z=f(x,y), or x=…, y=…, z=… of t on three lines.");
            }
        }

        private static SurfacePlot Make(SurfaceKind kind, Entity[] functions, string u, string v)
        {
            var parameters = functions.SelectMany(f => f.Vars).Select(l => l.Name)
                .Where(n => n != u && n != v).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            var letters = new[] { u, v }.Concat(parameters).ToList();
            return new SurfacePlot(kind, functions.Select(f => NumericCompiler.Compile(f, letters)).ToArray(), parameters.Select(MathReader.Symbol).ToList());
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
