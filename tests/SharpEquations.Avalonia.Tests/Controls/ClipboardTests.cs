namespace SharpEquations.Avalonia.Tests.Controls
{
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>The bytes of the MathML of the clipboard, as Word and others write them.</summary>
    public class ClipboardTests
    {
        private const string MathML = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>A</mi><mo>=</mo><mi>π</mi><msup><mi>r</mi><mn>2</mn></msup></math>";

        [Test]
        public async Task Utf8AsWordWritesIt()
        {
            await Assert.That(MathClipboard.Decode(Encoding.UTF8.GetBytes(MathML + "\0"))).IsEqualTo(MathML);
            await Assert.That(MathClipboard.Decode(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(MathML)))).IsEqualTo(MathML);
            await Assert.That(MathClipboard.Encode(MathML)).IsEquivalentTo(Encoding.UTF8.GetBytes(MathML));
        }

        [Test]
        public async Task Utf16WithOrWithoutItsMark()
        {
            await Assert.That(MathClipboard.Decode(Encoding.Unicode.GetBytes(MathML + "\0"))).IsEqualTo(MathML);
            await Assert.That(MathClipboard.Decode(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(MathML)))).IsEqualTo(MathML);
            await Assert.That(MathClipboard.Decode(Encoding.BigEndianUnicode.GetBytes(MathML))).IsEqualTo(MathML);
        }
    }

    internal static class ByteArrays
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
