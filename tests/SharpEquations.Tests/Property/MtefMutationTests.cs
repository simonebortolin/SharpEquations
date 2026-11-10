#nullable enable

namespace SharpEquations.Tests.Property
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpEquations;

    /// <summary>
    /// MTEF that no one wrote: the "Equation Native" streams of the test data with bytes changed, inserted or removed
    /// at random, or cut short, are read to an equation or an InvalidDataException, never another exception, and what
    /// is read is written in every format. The seed is fixed, so a failure repeats: the message names the file and the
    /// case.
    /// </summary>
    public class MtefMutationTests
    {
        private const int Cases = 4000;
        private const int Seed = 20240611;

        private static readonly byte[] Interesting = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x7F, 0x80, 0x81, 0xD8, 0xDC, 0xDF, 0xFE, 0xFF };

        private static byte[] Mutate(byte[] source, Random random)
        {
            var bytes = source.ToList();
            for (int changes = random.Next(1, 5); changes > 0; changes--)
            {
                // the header of the stream is left alone most of the time, so that the records are what changes
                var at = random.Next(random.Next(4) == 0 ? 0 : Math.Min(28, bytes.Count), bytes.Count + 1);
                switch (random.Next(6))
                {
                    case 0:
                        if (at < bytes.Count)
                            bytes[at] = (byte)random.Next(256);
                        break;
                    case 1:
                        if (at < bytes.Count)
                            bytes[at] = Interesting[random.Next(Interesting.Length)];
                        break;
                    case 2:
                        if (at < bytes.Count)
                            bytes[at] ^= (byte)(1 << random.Next(8));
                        break;
                    case 3:
                        bytes.Insert(at, random.Next(2) == 0 ? (byte)random.Next(256) : Interesting[random.Next(Interesting.Length)]);
                        break;
                    case 4:
                        if (at < bytes.Count)
                            bytes.RemoveRange(at, random.Next(1, Math.Min(4, bytes.Count - at) + 1));
                        break;
                    default:
                        bytes.RemoveRange(at, bytes.Count - at);
                        break;
                }
            }
            return bytes.ToArray();
        }

        private static string? Check(byte[] mtef)
        {
            try
            {
                var equation = Equation.Parse(mtef, EquationFormat.Mtef);
                _ = equation.ToString() + equation.ToString(EquationFormat.LaTeX) + equation.ToString(EquationFormat.UnicodeMath) + equation.ToString(EquationFormat.Omml);
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        [Test]
        public async Task ReadsMutatedMtefOrRejectsItAsInvalidData()
        {
            var files = Directory.GetFiles(TestData.Path("mtef"), "*.eqn", SearchOption.AllDirectories)
                .OrderBy(file => file.Replace('\\', '/'), StringComparer.Ordinal).ToList();
            await Assert.That(files.Count).IsGreaterThan(50);
            var sources = files.Select(File.ReadAllBytes).ToList();
            var random = new Random(Seed);
            var failures = new List<string>();
            for (int n = 0; n < Cases; n++)
            {
                var index = random.Next(sources.Count);
                var mutated = Mutate(sources[index], random);
                // the same records without the header of the stream, as an MTEF equation of its own
                var result = Check(mutated) ?? (n % 4 == 0 && mutated.Length > 28 ? Check(mutated.Skip(28).ToArray()) : null);
                if (result != null)
                    failures.Add("case " + n + " of " + Path.GetFileName(files[index]) + ": " + result);
            }
            await Assert.That(failures).IsEmpty();
        }
    }
}
