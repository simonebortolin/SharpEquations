namespace SharpEquations.Uno.Tests
{
    using System;
    using System.Threading.Tasks;
    using global::Uno.UI.Hosting;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Starts the Uno app on this thread, and once its window is up runs the tests (TUnit, with the arguments of the
    /// command line) on another; the process ends with their exit code.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var host = UnoPlatformHostBuilder.Create()
                .App(() => new TestApp(args))
                .UseX11()
                .UseMacOS()
                .UseWin32()
                .Build();
            host.Run();
        }

        private sealed class TestApp : Application
        {
            private readonly string[] args;

            public TestApp(string[] args) => this.args = args;

            protected override void OnLaunched(LaunchActivatedEventArgs e)
            {
                Resources.MergedDictionaries.Add(new XamlControlsResources());
                var window = new Window { Title = "SharpEquations.Uno.Tests" };
                window.Activate();
                Ui.Start(window);
                _ = Task.Run(async () =>
                {
                    var code = 1;
                    try
                    {
                        code = await MicrosoftTestingPlatformApplication.RunAsync(args).ConfigureAwait(false);
                    }
                    finally
                    {
                        Environment.Exit(code);
                    }
                });
            }
        }
    }
}
