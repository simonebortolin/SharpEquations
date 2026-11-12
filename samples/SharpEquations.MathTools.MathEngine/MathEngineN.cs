namespace SharpEquations.MathTools.MathEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Collections.Concurrent;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
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
        private const int Formula = 0;
        private const int Linear = 2;
        private const int MathML = 4;
        private const int SolveEquation = 0;
        private const int NonEquation = 2;

        // the error -8 of a request, what the engine answers for a system with no solution
        private const int NoSolution = 17;

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
            context = OnEngineThread(() =>
            {
                Check(NativeMethods.CreateContext(Key, out var created));
                return created;
            });
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

        /// <summary>Whether equations are solved among the complex numbers (true) or the real ones (false, the default).</summary>
        public bool Complex { get; set; }

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
            // read in the unit of angles (30° and the trigonometric functions become radians): the engine computes in radians
            List<Entity> system;
            if (MathReader.TryReadDefinition(equation, Angles, out var name, out var value))
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
                system = MathReader.ReadSystem(equation, Angles).Select(Substitute).ToList();
            }

            var letters = AngouriMathEngine.Variables(system.ToArray());
            var x = variable ?? (letters.Count > 0 ? letters[0] : null);
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(context == Guid.Empty, this);
                if (action == CasAction.Solve)
                {
                    var equations = MathEngineText.Equations(system);
                    var unknowns = system.Count > 1 ? letters : x != null ? new[] { x } : letters;
                    return OnEngineThread(() => Solve(equations, unknowns));
                }
                var command = MathEngineText.Command(action, system, x);
                return OnEngineThread(() => Execute(action, command));
            }
        }

        // as Microsoft Mathematics does: each equation read in the linear format and written as a formula, all of them
        // in SymbolicSolve[List[…]], read as a formula and solved through a request, which takes the letters to solve for
        private CasResult Solve(IReadOnlyList<string> equations, IReadOnlyList<string> unknowns)
        {
            var created = new List<int>();
            try
            {
                var formulas = new List<string>();
                foreach (var equation in equations)
                {
                    var one = Parse(equation, Linear, SolveEquation);
                    created.Add(one);
                    formulas.Add(Serialize(one, Formula));
                }
                var command = MathEngineText.SolveCommand(formulas);
                var parsed = Parse(command, Formula, SolveEquation);
                created.Add(parsed);
                Check(NativeMethods.CreateEquationRequest(context, parsed, out var request));
                created.Add(request);
                // the letters are given even when the request does not ask for them, else it may solve for another one
                if (unknowns.Count > 0)
                {
                    var named = NativeMethods.EquationRequestSetVarNames(context, request, string.Join(",", unknowns.Select(MathReader.Symbol)));
                    if (NativeMethods.EquationRequestRequireVariableList(context, request, out var required) == 0 && required)
                        Check(named);
                }

                SetEvaluation();
                var code = NativeMethods.ExecuteExpression(context, request, out var result);
                if (code == NoSolution)
                    return new CasResult(CasAction.Solve, null, "") { Values = Array.Empty<Equation>() };
                Check(code);
                created.Add(result);
                return Result(CasAction.Solve, result);
            }
            finally
            {
                foreach (var expression in created.Distinct())
                    NativeMethods.DestroyExpression(context, expression);
            }
        }

        private CasResult Execute(CasAction action, string command)
        {
            var created = new List<int>();
            try
            {
                var parsed = Parse(command, Linear, NonEquation);
                created.Add(parsed);
                SetEvaluation();
                Check(NativeMethods.ExecuteExpression(context, parsed, out var result));
                created.Add(result);
                if (action == CasAction.Simplify)
                {
                    // as Microsoft Mathematics simplifies: the evaluated expression, then all of its simplifications
                    Check(NativeMethods.FullSimplifyExpression(context, result, out result));
                    created.Add(result);
                }
                return Result(action, result);
            }
            finally
            {
                foreach (var expression in created.Distinct())
                    NativeMethods.DestroyExpression(context, expression);
            }
        }

        // the input is in radians (see Run): so is the engine
        private void SetEvaluation() =>
            Check(NativeMethods.SetEvaluationProperties(context, Complex ? 1 : 0, 0, false, false, false, true));

        private int Parse(string text, int format, int mode)
        {
            Check(NativeMethods.SetParsingOptions(context, format, true, true, true, mode));
            SetEvaluation();
            var code = NativeMethods.Parse(context, text, out var parsed);
            if (code == 0)
                return parsed;
            var where = NativeMethods.GetParsingErrorRange(context, out var start, out var length) == 0 && start >= 0 && start < text.Length
                ? " at “" + text.Substring(start, Math.Clamp(length, 1, text.Length - start)) + "”"
                : "";
            throw new NotSupportedException(MathEngineText.Error(code) + where + ": " + text);
        }

        // the values of a result: from its linear text, read by AngouriMath, else from its MathML
        private CasResult Result(CasAction action, int result)
        {
            var mathML = MathEngineText.DecodeEntities(Serialize(result, MathML));
            var text = Serialize(result, Linear);
            var display = new EquationOptions { Display = true };
            var math = Equation.TryParse(mathML, EquationFormat.MathML, display, out var parsedResult) ? parsedResult : null;

            var values = new List<Equation>();
            try
            {
                foreach (var value in MathEngineText.ReadLinear(text))
                {
                    if (AngouriMathEngine.ToEquation(value) is not { } one)
                    {
                        values.Clear();
                        break;
                    }
                    values.Add(one);
                }
            }
            catch (FormatException)
            {
                values.Clear();
            }
            if (values.Count == 0)
            {
                foreach (var piece in MathEngineText.Values(mathML))
                {
                    if (!Equation.TryParse(piece.ToString(), EquationFormat.MathML, display, out var one))
                    {
                        values.Clear();
                        break;
                    }
                    values.Add(one);
                }
            }
            if (action != CasAction.Solve && values.Count > 1)
                values.RemoveRange(1, values.Count - 1);
            return new CasResult(action, math, text) { Values = values };
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
                var destroyed = context;
                context = Guid.Empty;
                OnEngineThread(() => NativeMethods.DestroyContext(destroyed));
            }
        }

        /// <summary>
        /// Runs a call of the library on its own thread. The library keeps every context in one table, not guarded
        /// against threads, keeps state per thread and initializes COM on the thread that calls it, as on the one
        /// thread (single-threaded apartment) of Microsoft Mathematics: so all the calls, of every engine, go to one
        /// such thread, one at a time.
        /// </summary>
        private static T OnEngineThread<T>(Func<T> call)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Value.Add(() =>
            {
                try
                {
                    done.SetResult(call());
                }
#pragma warning disable CA1031 // every failure goes back to the caller
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    done.SetException(exception);
                }
            });
            return done.Task.GetAwaiter().GetResult();
        }

        private static readonly Lazy<BlockingCollection<Action>> Calls = new Lazy<BlockingCollection<Action>>(() =>
        {
            var calls = new BlockingCollection<Action>();
            var thread = new Thread(() =>
            {
                foreach (var call in calls.GetConsumingEnumerable())
                    call();
            })
            {
                IsBackground = true,
                Name = "Microsoft Mathematics engine",
            };
            if (OperatingSystem.IsWindows())
                thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return calls;
        });

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
