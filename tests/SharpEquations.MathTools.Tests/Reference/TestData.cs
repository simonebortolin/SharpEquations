namespace SharpEquations.MathTools.Tests
{
    using System;
    using System.IO;

    /// <summary>The test data, in tests/data of the repository.</summary>
    internal static class TestData
    {
        private static readonly Lazy<string> Root = new Lazy<string>(() =>
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var data = System.IO.Path.Combine(dir.FullName, "tests", "data");
                if (Directory.Exists(data))
                    return data;
            }
            throw new DirectoryNotFoundException("tests/data not found above " + AppContext.BaseDirectory);
        });

        /// <summary>A path in the test data.</summary>
        public static string Path(params string[] parts)
        {
            var path = Root.Value;
            foreach (var part in parts)
                path = System.IO.Path.Combine(path, part);
            return path;
        }
    }
}
