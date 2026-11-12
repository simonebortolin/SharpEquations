namespace SharpEquations.MathTools.MathEngine
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The functions of mathenginen.dll the adapter calls. Each returns a code, 0 when it succeeded (see
    /// <see cref="MathEngineText.Error"/>); the expressions are numbers the context gives and takes back.
    /// </summary>
    internal static class NativeMethods
    {
        internal const string Library = "mathenginen.dll";

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int CreateContext(Guid key, out Guid context);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int DestroyContext(Guid context);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int SetParsingOptions(
            Guid context,
            int inputFormat,
            [MarshalAs(UnmanagedType.Bool)] bool allowImplicitBrackets,
            [MarshalAs(UnmanagedType.Bool)] bool allowMetaCommands,
            [MarshalAs(UnmanagedType.Bool)] bool autoCloseParentheses,
            int equationParsingMode);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int SetEvaluationProperties(
            Guid context,
            int numberField,
            int trigUnitMode,
            [MarshalAs(UnmanagedType.Bool)] bool isNumericEvaluation,
            [MarshalAs(UnmanagedType.Bool)] bool parsingOnly,
            [MarshalAs(UnmanagedType.Bool)] bool useConstantInIntegrationResult,
            [MarshalAs(UnmanagedType.Bool)] bool typesetResult);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int SetFormatingOptions(
            Guid context,
            int outputFormat,
            int digits,
            double scientificHigh,
            double scientificLow,
            [MarshalAs(UnmanagedType.BStr)] string mathMLPrefix,
            [MarshalAs(UnmanagedType.Bool)] bool forWorksheetEntry,
            int verbosity);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int Parse(Guid context, [MarshalAs(UnmanagedType.BStr)] string text, out int expression);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int GetParsingErrorRange(Guid context, out int start, out int length);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int ExecuteExpression(Guid context, int expression, out int result);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int Serialize(Guid context, int expression, [MarshalAs(UnmanagedType.BStr)] out string text);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int DestroyExpression(Guid context, int expression);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int CreateEquationRequest(Guid context, int expression, out int request);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int EquationRequestRequireVariableList(Guid context, int request, [MarshalAs(UnmanagedType.Bool)] out bool required);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int EquationRequestSetVarNames(Guid context, int request, [MarshalAs(UnmanagedType.BStr)] string names);

        [DllImport(Library, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int FullSimplifyExpression(Guid context, int expression, out int result);
    }
}
