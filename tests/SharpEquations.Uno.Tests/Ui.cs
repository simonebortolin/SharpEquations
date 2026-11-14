namespace SharpEquations.Uno.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;

    /// <summary>The window of the tests, and the thread of the user interface their work runs on.</summary>
    internal static class Ui
    {
        private static Window? window;
        private static DispatcherQueue? queue;

        public static void Start(Window testWindow)
        {
            window = testWindow;
            queue = DispatcherQueue.GetForCurrentThread();
        }

        /// <summary>Runs work on the thread of the user interface, and gives its result.</summary>
        public static Task<T> Run<T>(Func<Task<T>> work)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (queue == null || !queue.TryEnqueue(async () =>
            {
                try
                {
                    done.SetResult(await work());
                }
                catch (Exception e)
                {
                    done.SetException(e);
                }
            }))
            {
                done.SetException(new InvalidOperationException("The app of the tests is not running."));
            }
            return done.Task;
        }

        public static Task<T> Run<T>(Func<T> work) => Run(() => Task.FromResult(work()));

        /// <summary>Shows an element alone in the window, white, and waits for it to be loaded and laid out.</summary>
        public static async Task Show(FrameworkElement element)
        {
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            element.Loaded += (_, _) => loaded.TrySetResult(true);
            window!.Content = new Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                Padding = new Thickness(20),
                Children = { new StackPanel { Children = { element } } },
            };
            await loaded.Task.ConfigureAwait(true);
            await Settle().ConfigureAwait(true);
        }

        /// <summary>Lets the dispatcher run what is queued: a layout, the text changed of a text box, the floating bar.</summary>
        public static async Task Settle()
        {
            for (var i = 0; i < 3; i++)
            {
                window?.Content?.UpdateLayout();
                var idle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                queue!.TryEnqueue(DispatcherQueuePriority.Low, () => idle.SetResult(true));
                await idle.Task.ConfigureAwait(true);
            }
            await Task.Delay(30).ConfigureAwait(true);
            window?.Content?.UpdateLayout();
        }
    }
}
