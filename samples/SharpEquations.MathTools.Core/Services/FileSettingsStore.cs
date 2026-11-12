namespace SharpEquations.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>Settings kept in a JSON file, written again at each change (the store of the desktop app).</summary>
    public sealed class FileSettingsStore : ISettingsStore
    {
        private readonly string path;
        private readonly Dictionary<string, string> values;

        public FileSettingsStore(string path)
        {
            this.path = path;
            values = Load(path);
        }

        /// <summary>The file of the settings of the desktop app, in the application data of the user.</summary>
        public static string DefaultPath { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SharpEquations", "MathTools", "settings.json");

        public string? Get(string key) => values.TryGetValue(key, out var value) ? value : null;

        public void Set(string key, string? value)
        {
            if (value == null ? !values.Remove(key) : values.TryGetValue(key, out var old) && old == value)
                return;
            if (value != null)
                values[key] = value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(values, MathToolsJson.Default.DictionaryStringString));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // settings that cannot be written are kept for the session
            }
        }

        private static Dictionary<string, string> Load(string path)
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), MathToolsJson.Default.DictionaryStringString) ?? new() : new();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return new Dictionary<string, string>();
            }
        }
    }
}
