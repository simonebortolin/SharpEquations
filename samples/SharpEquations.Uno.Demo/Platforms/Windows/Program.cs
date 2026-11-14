namespace SharpEquations.Uno.Demo
{
    using System;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;

    /// <summary>The entry point of the app on WinUI 3 of the Windows App SDK.</summary>
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(parameters =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
    }
}
