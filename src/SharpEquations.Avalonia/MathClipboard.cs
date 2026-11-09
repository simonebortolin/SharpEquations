namespace SharpEquations.Avalonia
{
    using System;
    using System.Text;

    /// <summary>
    /// The MathML of the clipboard as bytes: Word writes and reads the "MathML" format as UTF-8 (and other programs the
    /// text of application/mathml+xml), not as the UTF-16 of the text formats of Windows.
    /// </summary>
    public static class MathClipboard
    {
        /// <summary>The bytes of MathML for the clipboard: UTF-8, without a byte order mark.</summary>
        public static byte[] Encode(string mathML) => new UTF8Encoding(false).GetBytes(mathML ?? throw new ArgumentNullException(nameof(mathML)));

        /// <summary>
        /// The MathML of bytes of the clipboard: UTF-8, or UTF-16 with its byte order mark or as the zeros between its
        /// ASCII characters show it, without the zeros that end the data.
        /// </summary>
        public static string Decode(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Trim(Encoding.UTF8.GetString(data, 3, data.Length - 3));
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Trim(Encoding.Unicode.GetString(data, 2, data.Length - 2));
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
                return Trim(Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2));
            if (data.Length >= 4 && data[0] != 0 && data[1] == 0 && data[2] != 0 && data[3] == 0)
                return Trim(Encoding.Unicode.GetString(data));
            if (data.Length >= 4 && data[0] == 0 && data[1] != 0 && data[2] == 0 && data[3] != 0)
                return Trim(Encoding.BigEndianUnicode.GetString(data));
            return Trim(Encoding.UTF8.GetString(data));
        }

        private static string Trim(string text) => text.TrimEnd('\0').Trim();
    }
}
