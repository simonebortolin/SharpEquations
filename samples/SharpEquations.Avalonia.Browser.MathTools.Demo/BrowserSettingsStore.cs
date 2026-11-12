namespace SharpEquations.Avalonia.MathTools
{
    using System.Runtime.InteropServices.JavaScript;
    using System.Runtime.Versioning;
    using SharpEquations.MathTools;

    /// <summary>Settings kept in the local storage of the page, under keys that start with <c>mathtools.</c>.</summary>
    [SupportedOSPlatform("browser")]
    public sealed partial class BrowserSettingsStore : ISettingsStore
    {
        private const string Prefix = "mathtools.";

        public string? Get(string key) => GetItem(Prefix + key);

        public void Set(string key, string? value)
        {
            if (value == null)
                RemoveItem(Prefix + key);
            else
                SetItem(Prefix + key, value);
        }

        [JSImport("globalThis.localStorage.getItem")]
        private static partial string? GetItem(string key);

        [JSImport("globalThis.localStorage.setItem")]
        private static partial void SetItem(string key, string value);

        [JSImport("globalThis.localStorage.removeItem")]
        private static partial void RemoveItem(string key);
    }
}
