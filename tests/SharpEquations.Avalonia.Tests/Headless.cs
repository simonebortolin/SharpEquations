namespace SharpEquations.Avalonia.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Avalonia;
    using global::Avalonia.Headless;
    using global::Avalonia.Themes.Fluent;

    /// <summary>The application of the headless session, which renders with Skia so that the fonts are real.</summary>
    public class TestApp : Application
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

        public override void Initialize() => Styles.Add(new FluentTheme());
    }

    /// <summary>
    /// Runs code on the dispatcher of the one headless session of the tests. The tests that use it do not run in
    /// parallel (<c>[NotInParallel("headless")]</c>): showing a window or typing runs the jobs of the dispatcher, which
    /// would run another test in the middle of this one.
    /// </summary>
    internal static class Headless
    {
        private static readonly Lazy<HeadlessUnitTestSession> Session = new Lazy<HeadlessUnitTestSession>(
            () => HeadlessUnitTestSession.StartNew(typeof(TestApp)), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Task Run(Action action) => Session.Value.Dispatch(action, CancellationToken.None);

        public static Task<T> Run<T>(Func<T> function) => Session.Value.Dispatch(function, CancellationToken.None);
    }
}
