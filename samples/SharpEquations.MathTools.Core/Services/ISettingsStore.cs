namespace SharpEquations.MathTools
{
    using System.Collections.Generic;

    /// <summary>
    /// Where the app keeps its settings between sessions, as text by key: a file on the desktop, the local storage of
    /// the page in a browser, nothing in the tests.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>The value of a key, or null if it has none.</summary>
        string? Get(string key);

        /// <summary>Sets the value of a key; null removes it.</summary>
        void Set(string key, string? value);
    }

    /// <summary>Settings kept in memory only, for the session.</summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public string? Get(string key) => values.TryGetValue(key, out var value) ? value : null;

        public void Set(string key, string? value)
        {
            if (value == null)
                values.Remove(key);
            else
                values[key] = value;
        }
    }
}
