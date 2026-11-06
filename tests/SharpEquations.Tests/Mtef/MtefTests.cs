namespace SharpEquations.Tests
{
    using System.Threading.Tasks;
    using TUnit.Assertions.Enums;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using SharpEquations;
    using SharpEquations.Internal.Mtef;

    public class MtefTests
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        private static string Dir(string name) => TestData.Path(name);

        // the "Equation Native" stream of an OLE object of Equation Editor 3.0 or MathType: its header and the MTEF
        private static byte[] EquationNative(string file) => File.ReadAllBytes(file);

        /// <summary>
        /// Elements as "name=text" (leaves) or "name", in document order.
        /// </summary>
        private static List<string> Canonical(XElement element)
        {
            return element.DescendantsAndSelf()
                .Select(e => e.Name.LocalName + (e.HasElements ? string.Empty : "=" + e.Value.Trim()))
                .ToList();
        }

        /// <summary>
        /// The records of Equation Editor 3.0 (MTEF 3) and MathType (MTEF 5) objects are read into the same XML as
        /// https://github.com/jure/mathtype. Font names are left out: that reader decodes them as UTF-8, SharpEquations as Latin-1.
        /// </summary>
        [Test]
        [MatrixDataSource]
        public async Task ReadsRecordsAsTheMathtypeGem([Matrix("mathtype3", "mathtype5")] string version)
        {
            var files = Directory.GetFiles(Dir(Path.Combine("mtef", "records", version)), "*.eqn");
            await Assert.That(files.Length).IsGreaterThan(5);
            foreach (var file in files)
            {
                var native = EquationNative(file);
                var xml = MtefReader.Read(native.Skip(28).ToArray());
                var expected = XElement.Load(Path.ChangeExtension(file, ".xml"));
                foreach (var e in new[] { xml, expected })
                    e.Descendants("font_name").Remove();
                await Assert.That(Canonical(xml)).IsEquivalentTo(Canonical(expected), CollectionOrdering.Matching).Because(Path.GetFileName(file));
            }
        }

        /// <summary>
        /// MathType objects converted to MathML carry the characters of the reference MathML of
        /// https://github.com/jure/mathtype_to_mathml. Some references were written by MathType's own
        /// translator; those may group tokens differently, so the characters are compared, not the structure.
        /// </summary>
        [Test]
        public async Task ConvertsMathTypeObjects()
        {
            // Pre-scripts combined with post-scripts are nested instead of being one mmultiscripts, which reorders
            // the characters (424, 450, 478); equation14 has characters of a font-specific encoding (Euclid Fraktur).
            var reordered = new HashSet<string> { "424", "450", "478", "equation14" };
            var files = Directory.GetFiles(Dir(Path.Combine("mtef", "mathml")), "*.eqn");
            await Assert.That(files.Length).IsGreaterThan(20);
            foreach (var file in files)
            {
                var math = MtefParser.FromEquationNative(EquationNative(file));
                await Assert.That(math.Name).IsEqualTo(Mml + "math").Because(file);
                await Assert.That(math.DescendantsAndSelf().All(e => e.Name.Namespace == Mml)).IsTrue().Because(file);
                var name = Path.GetFileNameWithoutExtension(file);
                if (reordered.Contains(name))
                    continue;
                var expected = XElement.Load(Path.ChangeExtension(file, ".xml"));
                expected.Descendants().Where(e => e.Name.LocalName == "annotation").Remove();
                await Assert.That(Text(math)).IsEqualTo(Text(expected)).Because(name);
            }
        }

        // the characters, with the italic letters of the normalized MathML (𝑥) as the plain ones of the references
        private static string Text(XElement element) => Regex.Replace(element.Value, @"\s", string.Empty).Normalize(System.Text.NormalizationForm.FormKC);

        [Test]
        public async Task EquationEditor3Fraction()
        {
            var math = MtefParser.FromEquationNative(EquationNative(Path.Combine(Dir(Path.Combine("mtef", "records", "mathtype3")), "frac.eqn")));
            await Assert.That(math.Attribute("display")).IsNull();
            var fraction = math.Descendants(Mml + "mfrac").Single();
            await Assert.That(fraction.Elements().Select(Text)).IsEquivalentTo(new[] { "2d", "204d" }, CollectionOrdering.Matching);
            await Assert.That(math.Descendants(Mml + "mn").Select(e => e.Value).FirstOrDefault(v => v == "100")).IsEqualTo("100");
        }

        [Test]
        public async Task FunctionNamesAndNumbersAreSingleTokens()
        {
            var math = MtefParser.FromEquationNative(EquationNative(Path.Combine(Dir(Path.Combine("mtef", "mathml")), "280.eqn")));
            await Assert.That(math.Descendants(Mml + "mi").Any(e => e.Value == "ln")).IsTrue();
        }

        [Test]
        public async Task RejectsOtherData()
        {
            await Assert.That(() => MtefParser.Parse(new byte[] { 4, 0, 0, 0, 0 })).ThrowsExactly<InvalidDataException>();
            await Assert.That(() => MtefParser.Parse(new byte[] { 5, 1, 0, 6, 9 })).ThrowsExactly<InvalidDataException>();
            await Assert.That(() => MtefParser.FromEquationNative(new byte[] { 28, 0 })).ThrowsExactly<InvalidDataException>();
        }
    }
}
