namespace SharpEquations.WinForms.Demo
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;
    using SharpEquations;

    /// <summary>A form with the editor, the equation as UnicodeMath and LaTeX, and a view of it.</summary>
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24), AutoScroll = true };
            var hint = new Label { Text = "Type UnicodeMath or LaTeX: a space or an operator builds it up.", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 8) };
            var editor = new MathEditBox { FontSize = 28, Width = 720, Margin = new Padding(0, 0, 0, 12) };
            var unicodeMath = new Label { AutoSize = true, MaximumSize = new Size(720, 0) };
            var latex = new Label { AutoSize = true, MaximumSize = new Size(720, 0) };
            var viewLabel = new Label { Text = "MathView:", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 8, 0, 4) };
            var view = new MathView { FontSize = 22, IsDisplay = true, BackColor = Color.White };
            editor.EquationChanged += (_, e) =>
            {
                unicodeMath.Text = "UnicodeMath: " + (e.Equation?.ToString(EquationFormat.UnicodeMath) ?? string.Empty);
                latex.Text = "LaTeX: " + (e.Equation?.ToString(EquationFormat.LaTeX) ?? string.Empty);
                view.Equation = e.Equation;
            };
            panel.Controls.AddRange(new Control[] { hint, editor, unicodeMath, latex, viewLabel, view });
            if (Environment.GetEnvironmentVariable("SHARPEQUATIONS_DEMO_EQUATION") is { Length: > 0 } initial)
                editor.Equation = Equation.Parse(initial, EquationFormat.UnicodeMath, new EquationOptions { Display = true });
            var form = new Form { Text = "SharpEquations for Windows Forms", Width = 800, Height = 500, BackColor = Color.White };
            form.Controls.Add(panel);
            form.Shown += (_, _) => editor.Focus();
            Application.Run(form);
        }
    }
}
