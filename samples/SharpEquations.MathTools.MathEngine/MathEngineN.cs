namespace SharpEquations.MathTools.MathEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using AngouriMath;
    using SharpEquations;
    using SharpEquations.MathTools;

    /// <summary>
    /// The engine of Microsoft Mathematics (mathenginen.dll) as an <see cref="ICasEngine"/>, to compare its results with
    /// those of <see cref="AngouriMathEngine"/>. The equation is read by <see cref="MathReader"/> (the letters defined
    /// replaced by their values), written in the linear input of the engine, and the result comes back as MathML.
    /// It needs Windows and an installation of Microsoft Mathematics of the same processor as the process; the
    /// library is never part of this repository (see <see cref="LibraryPath"/>).
    /// </summary>
    public sealed class MathEngineN : ICasEngine, IDisposable
    {
        private const int Linear = 2;
        private const int MathML = 4;
        private const int SolveEquation = 0;
        private const int NonEquation = 2;

        private static readonly Guid Key = new Guid("15D1265B-8A3C-413a-BF1E-38CC9285C186");
        private static readonly object Loading = new object();
        private static IntPtr handle;
        private static bool? available;

        private readonly object gate = new object();
        private readonly Dictionary<string, Entity> definitions = new Dictionary<string, Entity>(StringComparer.Ordinal);
        private Guid context;

        /// <exception cref="PlatformNotSupportedException">The library is not there (<see cref="IsAvailable"/>).</exception>
        public MathEngineN()
        {
            if (!IsAvailable)
                throw new PlatformNotSupportedException("The engine of Microsoft Mathematics is not installed (mathenginen.dll).");
            Check(NativeMethods.CreateContext(Key, out context));
        }

        /// <summary>
        /// Where the library is looked for: the environment variable <c>MATHENGINEN_PATH</c>, else the folder of
        /// Microsoft Mathematics in the program files; null when it is in neither.
        /// </summary>
        public static string? LibraryPath
        {
            get
            {
                if (Environment.GetEnvironmentVariable("MATHENGINEN_PATH") is { Length: > 0 } path)
                    return File.Exists(path) ? path : null;
                foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
                {
                    var file = Path.Combine(Environment.GetFolderPath(folder), "Microsoft Mathematics", NativeMethods.Library);
                    if (File.Exists(file))
                        return file;
                }
                return null;
            }
        }

        /// <summary>Whether the engine can be used: on Windows, with the library of the processor of the process, loaded.</summary>
        public static bool IsAvailable
        {
            get
            {
                lock (Loading)
                    return available ??= Load();
            }
        }

        public string Name => "Microsoft Mathematics";

        public AngleUnit Angles { get; set; }

        /// <summary>Whether equations are solved among the complex numbers (true) or the real ones.</summary>
        public bool Complex { get; set; } = true;

        public IReadOnlyDictionary<string, Equation> Definitions
        {
            get
            {
                lock (gate)
                    return definitions.ToDictionary(d => d.Key, d => AngouriMathEngine.ToEquation(d.Value) ?? Equation.Parse(d.Value.ToString(), EquationFormat.UnicodeMath));
            }
        }

        public void ClearDefinitions()
        {
            lock (gate)
                definitions.Clear();
        }

        public IReadOnlyList<string> Variables(Equation equation)
        {
            if (MathReader.TryReadDefinition(equation, AngleUnit.Radians, out _, out var value))
                return AngouriMathEngine.Variables(Substitute(value));
            return AngouriMathEngine.Variables(MathReader.ReadSystem(equation, AngleUnit.Radians).Select(Substitute).ToArray());
        }

        public CasResult Run(CasAction action, Equation equation, string? variable = null)
        {
            // read in radians: the engine converts the angles itself, by its unit of angles
            List<Entity> system;
            if (MathReader.TryReadDefinition(equation, AngleUnit.Radians, out var name, out var value))
            {
                value = Substitute(value);
                if (action == CasAction.Define)
                {
                    if (value.Vars.Any(v => v.Name == name))
                        throw new NotSupportedException("“" + name + "” cannot be defined by itself.");
                    lock (gate)
                        definitions[name] = value;
                    var values = AngouriMathEngine.ToEquation(value) is { } defined ? new[] { defined } : Array.Empty<Equation>();
                    return new CasResult(CasAction.Define, null, name + " ≔ " + value.ToString()) { Values = values };
                }
                system = new List<Entity> { value };
            }
            else if (action == CasAction.Define)
            {
                throw new NotSupportedException("A definition is a letter, ≔ and its value: a≔5.");
            }
            else
            {
                system = MathReader.ReadSystem(equation, AngleUnit.Radians).Select(Substitute).ToList();
            }

            var letters = AngouriMathEngine.Variables(system.ToArray());
            var x = variable ?? (letters.Count > 0 ? letters[0] : null);
            var command = MathEngineText.Command(action, system, x);
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(context == Guid.Empty, this);
                return action == CasAction.Solve
                    ? Execute(action, command, SolveEquation, system.Count > 1 ? letters : x != null ? new[] { x } : letters)
                    : Execute(action, command, NonEquation, null);
            }
        }

        private CasResult Execute(CasAction action, string command, int mode, IReadOnlyList<string>? unknowns)
        {
            Check(NativeMethods.SetParsingOptions(context, Linear, true, true, true, mode));
            Check(NativeMethods.SetEvaluationProperties(context, Complex ? 1 : 0, Angles == AngleUnit.Degrees ? 1 : 0, false, false, false, true));
            var created = new List<int>();
            try
            {
                var code = NativeMethods.Parse(context, command, out var parsed);
                if (code != 0)
                {
                    var where = NativeMethods.GetParsingErrorRange(context, out var start, out var length) == 0 && start >= 0 && start < command.Length
                        ? " at “" + command.Substring(start, Math.Clamp(length, 1, command.Length - start)) + "”"
                        : "";
                    throw new NotSupportedException(MathEngineText.Error(code) + where + ": " + command);
                }
                created.Add(parsed);

                var target = parsed;
                if (unknowns != null)
                {
                    // the solution is asked through a request of the equation, which takes the letters to solve for
                    if (NativeMethods.CreateEquationRequest(context, parsed, out var request) != 0)
                    {
                        Check(NativeMethods.ExecuteExpression(context, parsed, out var executed));
                        created.Add(executed);
                        Check(NativeMethods.CreateEquationRequest(context, executed, out request));
                    }
                    created.Add(request);
                    if (NativeMethods.EquationRequestRequireVariableList(context, request, out var required) == 0 && required && unknowns.Count > 0)
                        Check(NativeMethods.EquationRequestSetVarNames(context, request, string.Join(",", unknowns.Select(MathReader.Symbol))));
                    target = request;
                }

                Check(NativeMethods.ExecuteExpression(context, target, out var result));
                created.Add(result);
                var mathML = Serialize(result, MathML);
                var text = Serialize(result, Linear);

                var math = Equation.TryParse(mathML, EquationFormat.MathML, new EquationOptions { Display = true }, out var parsedResult) ? parsedResult : null;
                var values = new List<Equation>();
                foreach (var piece in MathEngineText.Values(mathML))
                {
                    if (!Equation.TryParse(piece.ToString(), EquationFormat.MathML, new EquationOptions { Display = true }, out var one))
                    {
                        values.Clear();
                        break;
                    }
                    values.Add(one);
                }
                if (action != CasAction.Solve && values.Count > 1)
                    values.RemoveRange(1, values.Count - 1);
                return new CasResult(action, math, text) { Values = values };
            }
            finally
            {
                foreach (var expression in created.Distinct())
                    NativeMethods.DestroyExpression(context, expression);
            }
        }

        private string Serialize(int expression, int format)
        {
            Check(NativeMethods.SetFormatingOptions(context, format, 13, 1e12, 1e-4, "", false, 1));
            Check(NativeMethods.Serialize(context, expression, out var text));
            return text;
        }

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

        public void Dispose()
        {
            lock (gate)
            {
                if (context == Guid.Empty)
                    return;
                NativeMethods.DestroyContext(context);
                context = Guid.Empty;
            }
        }

        private static void Check(int code)
        {
            if (MathEngineText.Error(code) is { } error)
                throw new InvalidOperationException(error);
        }

        private static bool Load()
        {
            if (!OperatingSystem.IsWindows() || LibraryPath is not { } path)
                return false;
            ushort? machine;
            using (var file = File.OpenRead(path))
                machine = MathEngineText.Machine(file);
            var expected = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => (ushort)0x8664,
                Architecture.X86 => (ushort)0x14C,
                _ => (ushort)0,
            };
            if (machine != expected || !NativeLibrary.TryLoad(path, out handle))
                return false;
            NativeLibrary.SetDllImportResolver(typeof(MathEngineN).Assembly, (name, _, _) => name == NativeMethods.Library ? handle : IntPtr.Zero);
            return true;
        }
    }
}
