namespace SharpEquations.MathTools.Tests
{
    using System.IO;
    using System.Threading.Tasks;
    using SharpEquations.MathTools;

    /// <summary>The settings of the desktop app, kept in a file from one session to the next.</summary>
    public class SettingsTests
    {
        [Test]
        public async Task FileKeepsTheSettings()
        {
            var directory = Directory.CreateTempSubdirectory();
            try
            {
                var path = Path.Combine(directory.FullName, "sub", "settings.json");
                var store = new FileSettingsStore(path);
                store.Set("tab", "2");
                store.Set("equation", "x^2-1");
                store.Set("equation", null);
                var again = new FileSettingsStore(path);
                await Assert.That(again.Get("tab")).IsEqualTo("2");
                await Assert.That(again.Get("equation")).IsNull();
            }
            finally
            {
                directory.Delete(true);
            }
        }

        [Test]
        public async Task UnreadableFileIsNoSettings()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "{ not json");
                var store = new FileSettingsStore(path);
                await Assert.That(store.Get("tab")).IsNull();
                store.Set("tab", "1");
                await Assert.That(new FileSettingsStore(path).Get("tab")).IsEqualTo("1");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
