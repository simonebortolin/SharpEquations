namespace SharpEquations.WinForms.Tests
{
    using System;
    using System.Drawing;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    /// <summary>The thread of the user interface of the tests (STA, with its message loop), and its form.</summary>
    internal static class Ui
    {
        private static readonly Lazy<Form> Thread = new Lazy<Form>(Start);

        private static Form Start()
        {
            var ready = new TaskCompletionSource<Form>();
            var thread = new System.Threading.Thread(() =>
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                var form = new Form
                {
                    Width = 640,
                    Height = 400,
                    Left = 40,
                    Top = 40,
                    StartPosition = FormStartPosition.Manual,
                    Text = "SharpEquations.WinForms.Tests",
                    BackColor = Color.White,
                };
                form.Shown += (_, _) => ready.SetResult(form);
                Application.Run(form);
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
        public static Task<T> Run<T>(Func<Task<T>> work)
        {
            var result = new TaskCompletionSource<T>();
            Thread.Value.BeginInvoke(new Action(async () =>
            {
                try
                {
                    result.SetResult(await work().ConfigureAwait(true));
                }
                catch (Exception e)
                {
                    result.SetException(e);
                }
            }));
            return result.Task;
        }

        public static Task<T> Run<T>(Func<T> work) => Run(() => Task.FromResult(work()));

        /// <summary>Shows a control alone in the form, and waits for it to be laid out and painted.</summary>
        public static async Task Show(Control control)
        {
            var form = Thread.Value;
            foreach (Control old in form.Controls)
                old.Dispose();
            form.Controls.Clear();
            control.Location = new Point(20, 20);
            if (control is MathEditBox)
                control.Width = 560;
            form.Controls.Add(control);
            form.Activate();
            await Settle().ConfigureAwait(true);
        }

        /// <summary>Lets the message loop run what is queued: a layout, a paint, the floating bar.</summary>
        public static async Task Settle()
        {
            for (var i = 0; i < 3; i++)
            {
                Thread.Value.Refresh();
                await Task.Delay(20).ConfigureAwait(true);
                Application.DoEvents();
            }
        }
    }
}
