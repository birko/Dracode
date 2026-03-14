namespace DraCode.KoboldLair.Server.Helpers
{
    /// <summary>
    /// Common string truncation helpers used across background services for log output.
    /// </summary>
    internal static class LogFormatHelper
    {
        /// <summary>
        /// Truncates a string for log display, appending "..." if truncated.
        /// </summary>
        public static string Truncate(string? text, int maxLength = 60)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > maxLength ? text[..maxLength] + "..." : text;
        }

        /// <summary>
        /// Returns a short prefix of an ID for log display (default 8 chars).
        /// </summary>
        public static string ShortId(string? id, int length = 8)
        {
            if (string.IsNullOrEmpty(id)) return "";
            return id[..Math.Min(length, id.Length)];
        }
    }
}
