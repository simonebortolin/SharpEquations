#nullable enable

namespace SharpEquations.Tests.Mtef
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using SharpEquations;

    /// <summary>
    /// MTEF built byte by byte: the records of Equation Editor 3.0 (MTEF 3) and MathType (MTEF 5) that carry no
    /// mathematics (fonts, colors, rulers, nudges, comments) are read and skipped, and broken data is an
    /// InvalidDataException.
    /// </summary>
    public class MtefRecordTests
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        private static byte[] Z(string text) => Encoding.ASCII.GetBytes(text + "\0");

        private static byte[] Bytes(params object[] parts)
        {
            var bytes = new List<byte>();
            foreach (var part in parts)
            {
                if (part is byte[] array)
                    bytes.AddRange(array);
                else
                    bytes.Add(Convert.ToByte(part));
            }
            return bytes.ToArray();
        }

        // header, then a line with the character x (typeface 3, variable), the end of the line and of the equation
        private static readonly byte[] Mtef3Header = { 3, 1, 1, 3, 0 };
        private static readonly byte[] Mtef3X = { 2, 0x83, (byte)'x', 0 };
        private static readonly byte[] Mtef5Header = Bytes(5, 1, 0, 7, 0, Z("DSMT7"), 0);
        private static readonly byte[] Mtef5X = { 2, 0, 0x83, (byte)'x', 0 };

        private static string Identifiers(byte[] mtef) =>
            string.Concat(Equation.Parse(mtef.AsSpan(), EquationFormat.Mtef).ToMathML().Descendants(Mml + "mi").Select(e => e.Value));

        [Test]
        public async Task ReadsMtef3()
        {
            await Assert.That(Identifiers(Bytes(Mtef3Header, 1, Mtef3X, 0, 0))).IsEqualTo("x");
        }

        [Test]
        public async Task SkipsMtef3RecordsWithoutMathematics()
        {
            var mtef = Bytes(Mtef3Header,
                0x08, 0x83, 0, Z("Times New Roman"),   // font
                10, 11, 12, 13, 14,                     // sizes: full, sub, sub2, sym, subsym
                7, 1, 0, 0x10, 0x00,                    // ruler with one tab stop
                0x21, 1, 0, 0x20, 0x00,                 // line with a ruler
                Mtef3X, 0,
                0x81, 0x81, 0x81,                       // line with a nudge
                Mtef3X, 0,
                0x41, 0x10, 0x00,                       // line with its spacing
                Mtef3X, 0,
                0x11,                                   // empty line
                0);
            await Assert.That(Identifiers(mtef)).IsEqualTo("xxx");
        }

        [Test]
        public async Task ReadsMtef5()
        {
            var equation = Equation.Parse(Bytes(Mtef5Header, 1, 0, Mtef5X, 0, 0).AsSpan(), EquationFormat.Mtef);
            await Assert.That(equation.ToMathML().Descendants(Mml + "mi").Single().Value).IsEqualTo("x");
            await Assert.That(equation.IsDisplay).IsTrue().Because("equation options 0: a display equation");
            await Assert.That(Equation.Parse(Bytes(5, 1, 0, 7, 0, Z("DSMT7"), 1, 1, 0, Mtef5X, 0, 0).AsSpan(), EquationFormat.Mtef).IsDisplay).IsFalse();
        }

        [Test]
        public async Task SkipsMtef5RecordsWithoutMathematics()
        {
            var mtef = Bytes(Mtef5Header,
                19, Z("UNICODE"),                                 // encoding definition
                17, 1, Z("Times New Roman"),                      // font definition
                8, 1, 0,                                          // font style definition
                16, 0x05, 0, 0, 0xFF, 0xFF, 0, 0, 0, 0, Z("Cyan"), // CMYK color definition with a name
                16, 0, 0xFF, 0xFF, 0, 0, 0, 0,                    // RGB color definition
                15, 1,                                            // color
                10, 11, 12, 13, 14,                               // sizes
                100, 2, 0xAA, 0xBB,                               // future record of two bytes
                102, 0, Z("type"), Z("data"),                     // comment
                7, 1, 0, 0x10, 0x00,                              // ruler
                1, 0x0E, 0x81, 0x81, 0x10, 0x00, 1, 0, 0x20, 0,   // line with a nudge, its spacing and a ruler
                Mtef5X, 0,
                1, 0x01,                                          // empty line
                1, 0, 2, 0x08, 0x81, 0x81, 0x83, (byte)'y', 0, 0, // character with a nudge
                0);
            await Assert.That(Identifiers(mtef)).IsEqualTo("xy");
        }

        [Test]
        public async Task ReadsTheEquationNativeHeader()
        {
            var mtef = Bytes(Mtef3Header, 1, Mtef3X, 0, 0);
            var header = new byte[28];
            header[0] = 28;
            BitConverter.GetBytes(mtef.Length).CopyTo(header, 6);
            await Assert.That(Equation.Parse(Bytes(header, mtef).AsSpan(), EquationFormat.Mtef)).IsEqualTo(Equation.Parse(mtef.AsSpan(), EquationFormat.Mtef));
        }

        [Test]
        [Arguments(new byte[] { 4, 1, 1, 3, 0, 0 }, "Only MTEF versions 3 and 5 are supported, not 4.")]
        [Arguments(new byte[] { 3, 1, 1, 3, 0, 2, 0x83 }, "The MTEF data ends inside a record.")]
        [Arguments(new byte[] { 3, 1, 1, 3, 0, 1, 2, 0x83, (byte)'x', 0 }, "The MTEF data ends inside a record.")]
        [Arguments(new byte[] { 3, 1, 1, 3, 0, 0x0F }, "Unknown MTEF record type 15.")]
        [Arguments(new byte[] { 5, 1, 0, 7, 0, 0, 0, 50 }, "Unknown MTEF record type 50.")]
        public async Task RejectsBrokenData(byte[] mtef, string message)
        {
            await Assert.That(() => Equation.Parse(mtef.AsSpan(), EquationFormat.Mtef)).Throws<InvalidDataException>().WithMessage(message);
        }

        [Test]
        public async Task RejectsEquationsNestedTooDeeply()
        {
            var mtef = Bytes(Mtef3Header, Enumerable.Repeat((byte)1, 1000).ToArray());
            await Assert.That(() => Equation.Parse(mtef.AsSpan(), EquationFormat.Mtef)).Throws<InvalidDataException>().WithMessage("The MTEF equation is nested too deeply.");
        }
    }
}
