namespace SharpEquations.MathTools.MathEngine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using AngouriMath;
    using SharpEquations.MathTools;

    /// <summary>
    /// What the adapter of mathenginen.dll writes and reads, without the library (so that it is tested everywhere): the
    /// linear input of an expression, the command of an action, the meaning of an error code, the values of a result
    /// and the processor a library is built for.
    /// </summary>
    public static class MathEngineText
    {
        /// <summary>The linear input of an expression of AngouriMath: <c>sin(x)^2+(1/2)*x</c>.</summary>
        /// <exception cref="NotSupportedException">Something the engine has no input for.</exception>
        public static string Linear(Entity value) => value switch
        {
            Entity.Number.Integer n => n.EInteger.ToString(),
            Entity.Number.Rational q => "(" + q.ERational.Numerator.ToString() + "/" + q.ERational.Denominator.ToString() + ")",
            Entity.Number.Real r => Real(r),
            Entity.Number.Complex c => "(" + Real(c.RealPart) + "+" + Real(c.ImaginaryPart) + "*i)",
            Entity.Variable v when v == MathS.pi => "pi",
            Entity.Variable v when v == MathS.e => "e",
            Entity.Variable v => MathReader.Symbol(v.Name),
            Entity.Sumf(var a, var b) => Child(a, 1) + "+" + Child(b, 1),
            Entity.Minusf(var a, var b) => Child(a, 1) + "-" + Child(b, 2),
            Entity.Mulf(var a, var b) => Child(a, 2) + "*" + Child(b, 2),
            Entity.Divf(var a, var b) => Child(a, 2) + "/" + Child(b, 3),
            Entity.Powf(var a, var b) => Child(a, 4) + "^" + Child(b, 4),
            Entity.Sinf(var a) => Call("sin", a),
            Entity.Cosf(var a) => Call("cos", a),
            Entity.Tanf(var a) => Call("tan", a),
            Entity.Cotanf(var a) => Call("cot", a),
            Entity.Secantf(var a) => Call("sec", a),
            Entity.Cosecantf(var a) => Call("csc", a),
            Entity.Arcsinf(var a) => Call("arcsin", a),
            Entity.Arccosf(var a) => Call("arccos", a),
            Entity.Arctanf(var a) => Call("arctan", a),
            Entity.Arccotanf(var a) => Call("arccot", a),
            Entity.Logf(var b, var x) when b == MathS.e => Call("ln", x),
            Entity.Logf(var b, var x) => Call("ln", x) + "/" + Call("ln", b),
            Entity.Absf(var a) => Call("abs", a),
            Entity.Factorialf(var a) => Child(a, 4) + "!",
            Entity.Equalsf(var a, var b) => Linear(a) + "=" + Linear(b),
            _ => throw new NotSupportedException("The engine of Microsoft Mathematics has no input for “" + value.ToString() + "”."),
        };

        private static string Real(Entity.Number.Real r) =>
            r.EDecimal.ToDouble().ToString("R", CultureInfo.InvariantCulture);

        private static string Call(string name, Entity argument) => name + "(" + Linear(argument) + ")";

        // a child in brackets when it binds less than its place needs: 1 sums, 2 products, 3 powers, 4 the rest
        private static string Child(Entity value, int needed)
        {
            var text = Linear(value);
            return Binding(value, text) < needed ? "(" + text + ")" : text;
        }

        private static int Binding(Entity value, string text) => value switch
        {
            Entity.Sumf or Entity.Minusf => 1,
            Entity.Mulf or Entity.Divf => 2,
            Entity.Powf => 3,
            Entity.Equalsf => 0,
            _ when text.StartsWith('-') => 1,
            _ => 4,
        };

        /// <summary>
        /// The input of an action on a system of equations (or a single expression): the equation itself for
        /// <see cref="CasAction.Solve"/> (an expression is set equal to 0, the equations of a system are separated by
        /// commas) and for <see cref="CasAction.Evaluate"/>, a function of the engine for the others.
        /// </summary>
        public static string Command(CasAction action, IReadOnlyList<Entity> system, string? variable)
        {
            if (system.Count == 0)
                throw new ArgumentException("No equation.", nameof(system));
            if (action == CasAction.Solve)
                return string.Join(", ", system.Select(e => e is Entity.Equalsf ? Linear(e) : Linear(e) + "=0"));
            if (system.Count > 1)
                throw new NotSupportedException("A system of equations: it can only be solved.");
            var expression = Linear(system[0]);
            var x = variable != null ? MathReader.Symbol(variable) : null;
            return action switch
            {
                CasAction.Evaluate => expression,
                CasAction.Simplify => "simplify(" + expression + ")",
                CasAction.Expand => "expand(" + expression + ")",
                CasAction.Factor => "factor(" + expression + ")",
                CasAction.Differentiate => "deriv(" + expression + "," + (x ?? "x") + ")",
                CasAction.Integrate => "integral(" + expression + "," + (x ?? "x") + ")",
                _ => throw new NotSupportedException("The engine of Microsoft Mathematics does not do “" + action + "”."),
            };
        }

        /// <summary>What an error code of the engine means, or null for 0.</summary>
        public static string? Error(int code)
        {
            if (code == 0)
                return null;
            var kind = (code >> 16) & 0xFFFF;
            var low = code & 0xFFFF;
            var number = low % 2 == 1 ? -(low / 2) : low / 2;
            return kind switch
            {
                2 => "The computation was stopped.",
                1 => "The engine of Microsoft Mathematics cannot read this (error " + number.ToString(CultureInfo.InvariantCulture) + ").",
                _ => "The engine of Microsoft Mathematics cannot compute this (error " + number.ToString(CultureInfo.InvariantCulture) + ").",
            };
        }

        /// <summary>
        /// The values of a result in MathML, one <c>math</c> element each: the pieces of its row between commas,
        /// semicolons and “or”, each the right side of its last <c>=</c>, without what follows <c>≈</c>.
        /// </summary>
        public static IReadOnlyList<XElement> Values(string mathML)
        {
            var root = XElement.Parse(mathML);
            XNamespace ns = root.Name.Namespace;
            var row = root;
            while (row.Elements().Count() == 1 && row.Elements().Single().Name.LocalName is "mrow" or "mstyle" or "semantics")
                row = row.Elements().Single();
            if (row.Name.LocalName == "semantics")
                row = row.Elements().First();

            var values = new List<XElement>();
            var piece = new List<XElement>();
            foreach (var item in row.Elements().Where(e => e.Name.LocalName is not ("annotation" or "annotation-xml")))
            {
                if (IsSeparator(item))
                {
                    Add();
                    continue;
                }
                piece.Add(item);
            }
            Add();
            return values;

            void Add()
            {
                var approximately = piece.FindIndex(e => IsOperator(e, "≈"));
                var kept = approximately >= 0 ? piece.Take(approximately).ToList() : piece;
                var equals = kept.FindLastIndex(e => IsOperator(e, "="));
                if (equals >= 0)
                    kept = kept.Skip(equals + 1).ToList();
                if (kept.Count > 0)
                    values.Add(new XElement(ns + "math", new XElement(ns + "mrow", kept.Select(e => new XElement(e)))));
                piece = new List<XElement>();
            }
        }

        private static bool IsSeparator(XElement e) =>
            IsOperator(e, ",", ";", "∨", "or") || (e.Name.LocalName == "mtext" && e.Value.Trim() == "or");

        private static bool IsOperator(XElement e, params string[] texts) =>
            e.Name.LocalName == "mo" && texts.Contains(e.Value.Trim());

        /// <summary>The processor of a Windows library (0x8664 for x64, 0x14C for x86), or null if it is not one.</summary>
        public static ushort? Machine(Stream library)
        {
            using var reader = new BinaryReader(library, System.Text.Encoding.ASCII, leaveOpen: true);
            if (library.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
                return null;
            library.Position = 0x3C;
            var header = reader.ReadInt32();
            if (header < 0 || header + 6 > library.Length)
                return null;
            library.Position = header;
            if (reader.ReadUInt32() != 0x00004550)
                return null;
            return reader.ReadUInt16();
        }
    }
}
