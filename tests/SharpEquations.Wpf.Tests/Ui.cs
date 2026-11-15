namespace SharpEquations.Wpf.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>The thread of the user interface of the tests (STA, with its dispatcher), and its window.</summary>
    internal static class Ui
    {
        private static readonly Lazy<(Dispatcher Dispatcher, Window Window)> Thread = new Lazy<(Dispatcher, Window)>(Start);

        private static (Dispatcher, Window) Start()
        {
            var ready = new TaskCompletionSource<(Dispatcher, Window)>();
            var thread = new System.Threading.Thread(() =>
            {
                var window = new Window
                {
                    Width = 640,
                    Height = 400,
                    Left = 40,
                    Top = 40,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Title = "SharpEquations.Wpf.Tests",
                };
                window.Show();
                ready.SetResult((Dispatcher.CurrentDispatcher, window));
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "UI",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return ready.Task.GetAwaiter().GetResult();
        }

        /// <summary>Runs work on the thread of the user interface, and gives its result.</summary>
        public static Task<T> Run<T>(Func<Task<T>> work) => Thread.Value.Dispatcher.InvokeAsync(work).Task.Unwrap();

        public static Task<T> Run<T>(Func<T> work) => Thread.Value.Dispatcher.InvokeAsync(work).Task;

        /// <summary>Shows an element alone in the window, white, and waits for it to be loaded and laid out.</summary>
        public static async Task Show(FrameworkElement element)
        {
            var window = Thread.Value.Window;
            window.Content = new Grid
            {
                Background = Brushes.White,
                Children = { new StackPanel { Margin = new Thickness(20), Children = { element } } },
            };
            window.Activate();
            await Settle().ConfigureAwait(true);
        }

        /// <summary>Lets the dispatcher run what is queued: a layout, a render, the floating bar.</summary>
        public static async Task Settle()
        {
            for (var i = 0; i < 3; i++)
            {
                Thread.Value.Window.UpdateLayout();
                await Thread.Value.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            }
        }
    }
}
