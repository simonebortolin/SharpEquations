namespace SharpEquations.Uno.Demo
{
    using Microsoft.UI.Xaml;

    /// <summary>The app of the demo: a window with <see cref="MainPage"/>.</summary>
    public sealed partial class App : Application
    {
        private Window? window;

        public App() => InitializeComponent();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            window = new Window { Content = new MainPage() };
            window.Activate();
        }
    }
}
