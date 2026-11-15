namespace SharpEquations.Uno.Demo
{
    using System;
    using global::Uno.UI.Hosting;

    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var host = UnoPlatformHostBuilder.Create()
                .App(() => new App())
                .UseX11()
                .UseLinuxFrameBuffer()
                .UseMacOS()
                .UseWin32()
                .Build();
            host.Run();
        }
    }
}
