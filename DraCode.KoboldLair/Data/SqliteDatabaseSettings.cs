using Birko.Data.SQL.SqLite.Stores;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// The one way DraCode's SQLite stores name the database (TASK-177). Returns <see cref="SqLiteSettings"/>, so Birko
    /// puts the file in WAL on first use: on the rollback journal a read on a pooled handle can miss a table another
    /// connection created, and Birko reads a missing table as empty, so the read returns no rows and no error.
    /// </summary>
    /// <remarks>
    /// Birko caches one connector per file, and the first store to build it decides the journal mode, so every store on
    /// the file must come through here; <c>SqliteDatabaseSettingsTests</c> holds each repository to it.
    /// </remarks>
    public static class SqliteDatabaseSettings
    {
        /// <summary>Settings for the database at <paramref name="dbPath"/>; creates its directory if missing.</summary>
        public static SqLiteSettings For(string dbPath)
        {
            var full = Path.GetFullPath(dbPath);
            var directory = Path.GetDirectoryName(full) ?? ".";
            Directory.CreateDirectory(directory);
            return new SqLiteSettings(directory, Path.GetFileName(full));
        }
    }
}
