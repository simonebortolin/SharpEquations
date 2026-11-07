namespace SharpEquations.XsltGenerator
{
    using System.Text;

    internal sealed class CodeWriter
    {
        private readonly StringBuilder text = new StringBuilder();
        private int indent;

        public CodeWriter(int indent = 0) => this.indent = indent;

        public CodeWriter Line(string line = "")
        {
            if (line.Length > 0)
                text.Append(' ', indent * 4).Append(line);
            text.Append('\n');
            return this;
        }

        public CodeWriter Open(string? line = null)
        {
            if (line != null)
                Line(line);
            Line("{");
            indent++;
            return this;
        }

        public CodeWriter Close(string suffix = "")
        {
            indent--;
            return Line("}" + suffix);
        }

        public CodeWriter Append(CodeWriter other)
        {
            text.Append(other.text);
            return this;
        }

        public override string ToString() => text.ToString();
    }
}
