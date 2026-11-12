namespace SharpEquations.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using BenchmarkDotNet.Attributes;
    using SharpEquations.Editing;
    using SharpEquations.Rendering;
    using SharpEquations.Rendering.OpenType;

    /// <summary>
    /// A keystroke in the editor on large equations: the edit (with its build up) and the layout of the equation after
    /// it, in Libertinus Math read without a toolkit.
    /// </summary>
    [MemoryDiagnoser]
    public class EditorBenchmarks
    {
        private static readonly Dictionary<string, string> Equations = new()
        {
            ["matrix10x10"] = "(■(" + string.Join("@", Enumerable.Range(1, 10).Select(r => string.Join("&", Enumerable.Range(1, 10).Select(c => $"a_({r}{c})")))) + "))",
            ["lines30"] = "█(" + string.Join("@", Enumerable.Range(1, 30).Select(i => $"x_{i}&=a_{i}+b_{i}^2/(c_{i}+1)")) + ")",
            ["polynomial60"] = "f(x)=" + string.Join("+", Enumerable.Range(0, 61).Select(i => $"a_{i} x^{i}")),
        };

        private MathEditor editor = new();
        private BenchmarkFont font = null!;
        private MathLayoutOptions options = new();
        private MathLayoutCache cache = new();

        [Params("matrix10x10", "lines30", "polynomial60")]
        public string Equation { get; set; } = "";

        [GlobalSetup]
        public void Setup()
        {
            font = BenchmarkFont.Load();
            editor = new MathEditor(SharpEquations.Equation.Parse(Equations[Equation], EquationFormat.UnicodeMath, new EquationOptions { Display = true }));
            editor.MoveEnd();
            options = new MathLayoutOptions { FontSize = 20, ShowPlaceholders = true };
            cache = new MathLayoutCache();
            MathLayoutEngine.Layout(editor.Root, font, options, cache);
        }

        /// <summary>A character typed and undone, with the build up of the operator.</summary>
        [Benchmark]
        public void Edit()
        {
            editor.Insert("+");
            editor.Undo();
        }

        /// <summary>The layout of the whole equation, from nothing.</summary>
        [Benchmark(Baseline = true)]
        public MathLayout Layout() => MathLayoutEngine.Layout(editor.Root, font, options);

        /// <summary>The layout of the equation again, from the cache: what a move of the caret or a new measure costs.</summary>
        [Benchmark]
        public MathLayout LayoutCached() => MathLayoutEngine.Layout(editor.Root, font, options, cache);

        /// <summary>
        /// A keystroke as the control does it: the edit and the layout after it with the cache of the layouts before,
        /// twice (typed and undone).
        /// </summary>
        [Benchmark]
        public MathLayout Keystroke()
        {
            editor.Insert("+");
            MathLayoutEngine.Layout(editor.Root, font, options, cache);
            editor.Undo();
            return MathLayoutEngine.Layout(editor.Root, font, options, cache);
        }

        /// <summary>A keystroke with the whole equation laid out again after each edit, without a cache.</summary>
        [Benchmark]
        public MathLayout KeystrokeUncached()
        {
            editor.Insert("+");
            MathLayoutEngine.Layout(editor.Root, font, options);
            editor.Undo();
            return MathLayoutEngine.Layout(editor.Root, font, options);
        }
    }

    /// <summary>A math font read from its file, as the layout needs it, without a toolkit.</summary>
    internal sealed class BenchmarkFont : IMathFont
    {
        private readonly FontFile file;
        private readonly GlyphOutlines? outlines;
        private readonly ScriptStyles? scriptStyles;

        private BenchmarkFont(FontFile file)
        {
            this.file = file;
            Math = file.Table("MATH") is { } table ? MathTable.Parse(table) : null;
            outlines = GlyphOutlines.Create(file.Table);
            scriptStyles = ScriptStyles.Parse(file.Table("GSUB"));
        }

        public static BenchmarkFont Load() =>
            new(FontFile.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "data", "fonts", "LibertinusMath-Regular.otf"))));

        public double UnitsPerEm => file.UnitsPerEm;

        public MathTable? Math { get; }

        public bool TryGetGlyph(int codePoint, out ushort glyph) => file.TryGetGlyph(codePoint, out glyph);

        public double GetAdvance(ushort glyph) => file.Advance(glyph);

        public GlyphBounds GetBounds(ushort glyph) => outlines?.Bounds(glyph) ?? default;

        public ushort GetScriptVariant(ushort glyph, int scriptLevel) => scriptStyles?.Variant(glyph, scriptLevel) ?? glyph;

        public IMathFont? GetFallback(int codePoint) => null;
    }
}
