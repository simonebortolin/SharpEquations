namespace SharpEquations.RegexPolyfill
{
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Operations;

    /// <summary>
    /// SYSLIB1045 where .NET has no regular expression generator to report it: a Regex built from a constant pattern,
    /// with its constructor or a static method (Regex.IsMatch, Regex.Replace...), is a [GeneratedRegex] partial method.
    /// It has the ID of the diagnostic of .NET, so that one severity in .editorconfig sets both.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GeneratedRegexAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor UseGeneratedRegex = new DiagnosticDescriptor(
            "SYSLIB1045",
            "Use 'GeneratedRegexAttribute' to generate the regular expression implementation at compile-time",
            "Use 'GeneratedRegexAttribute' to generate the regular expression implementation at compile-time",
            "Performance",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(UseGeneratedRegex);

        public override void Initialize(AnalysisContext context)
        {
            // the Regex of the methods that the generator writes is generated code
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var regex = start.Compilation.GetTypeByMetadataName("System.Text.RegularExpressions.Regex");
                if (regex == null)
                    return;
                start.RegisterOperationAction(c =>
                {
                    var creation = (IObjectCreationOperation)c.Operation;
                    if (SymbolEqualityComparer.Default.Equals(creation.Type, regex) && HasConstantPattern(creation.Arguments))
                        c.ReportDiagnostic(Diagnostic.Create(UseGeneratedRegex, creation.Syntax.GetLocation()));
                }, OperationKind.ObjectCreation);
                start.RegisterOperationAction(c =>
                {
                    var invocation = (IInvocationOperation)c.Operation;
                    var method = invocation.TargetMethod;
                    if (method.IsStatic && SymbolEqualityComparer.Default.Equals(method.ContainingType, regex) &&
                        method.Name is not "Escape" and not "Unescape" && HasConstantPattern(invocation.Arguments))
                        c.ReportDiagnostic(Diagnostic.Create(UseGeneratedRegex, invocation.Syntax.GetLocation()));
                }, OperationKind.Invocation);
            });
        }

        /// <summary>Whether the pattern argument is a constant, which a generated regular expression can hold.</summary>
        private static bool HasConstantPattern(ImmutableArray<IArgumentOperation> arguments) =>
            arguments.FirstOrDefault(a => a.Parameter?.Name == "pattern") is { } pattern && pattern.Value.ConstantValue.HasValue;
    }
}
