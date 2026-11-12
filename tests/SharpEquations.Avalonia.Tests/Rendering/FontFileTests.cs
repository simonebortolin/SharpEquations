namespace SharpEquations.Avalonia.Tests.Rendering
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SharpEquations.Rendering.OpenType;

    /// <summary>The face of a font collection that FontFile reads.</summary>
    public class FontFileTests
    {
        /// <summary>
        /// Cambria and Cambria Math in cambria.ttc share their glyphs, and so their number: the face with the MATH table
        /// is the math font, whichever comes first.
        /// </summary>
        [Test]
        [Arguments(false)]
        [Arguments(true)]
        public async Task CollectionWhoseFacesShareTheirGlyphs(bool mathFirst)
        {
            var file = FontFile.Parse(Collection(mathFirst ? new[] { true, false } : new[] { false, true }), 100);
            await Assert.That(file.Table("MATH")).IsNotNull();
        }

        [Test]
        public async Task CollectionOfFacesWithOtherGlyphs()
        {
            var file = FontFile.Parse(Collection(new[] { true, false }, new[] { 120, 100 }), 100);
            await Assert.That(file.Table("MATH")).IsNull();
        }

        // a collection of faces with a maxp table (shared), and a MATH table of their own where asked
        private static byte[] Collection(bool[] math, int[]? glyphs = null)
        {
            var bytes = new List<byte>();
            void U16(int v) { bytes.Add((byte)(v >> 8)); bytes.Add((byte)v); }
            void U32(int v) { U16(v >> 16); U16(v & 0xFFFF); }
            void Tag(string t) { foreach (var c in t) bytes.Add((byte)c); }

            var faces = math.Length;
            var directories = 12 + 4 * faces;
            var sizes = new List<int>();
            for (var i = 0; i < faces; i++)
                sizes.Add(12 + 16 * (math[i] ? 2 : 1));
            var tables = directories;
            foreach (var size in sizes)
                tables += size;

            Tag("ttcf");
            U32(0x00010000);
            U32(faces);
            var at = directories;
            foreach (var size in sizes)
            {
                U32(at);
                at += size;
            }
            for (var i = 0; i < faces; i++)
            {
                U32(0x00010000);
                U16(math[i] ? 2 : 1);
                U16(0);
                U16(0);
                U16(0);
                if (math[i])
                {
                    Tag("MATH");
                    U32(0);
                    U32(tables + 6 * faces);
                    U32(4);
                }
                Tag("maxp");
                U32(0);
                U32(tables + 6 * i);
                U32(6);
            }
            for (var i = 0; i < faces; i++)
            {
                U32(0x00005000);
                U16(glyphs?[i] ?? 100);
            }
            U32(0x00010000);
            return bytes.ToArray();
        }
    }
}
