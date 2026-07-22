namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Persists settings and records between sessions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Abstracted rather than calling <c>PlayerPrefs</c> or <c>File</c> at the call site so that the
    /// storage backend, the serialisation format and the schema-version migration all stay in one
    /// place, and so tests can substitute an in-memory implementation.
    /// </para>
    /// <para>
    /// The implementation arriving in the save-system phase writes atomically, to a temporary file
    /// that then replaces the original. A player alt-F4ing mid-write would otherwise be left with a
    /// truncated settings file and a game that fails to start.
    /// </para>
    /// </remarks>
    public interface ISaveService
    {
        /// <summary>True when a record exists under the given key.</summary>
        /// <param name="key">Record name.</param>
        /// <returns>Whether the record exists.</returns>
        bool Exists(string key);

        /// <summary>Writes a record, overwriting any existing one.</summary>
        /// <typeparam name="TData">Serialisable record type.</typeparam>
        /// <param name="key">Record name.</param>
        /// <param name="data">Record contents.</param>
        void Save<TData>(string key, TData data) where TData : class;

        /// <summary>
        /// Reads a record.
        /// </summary>
        /// <typeparam name="TData">Serialisable record type.</typeparam>
        /// <param name="key">Record name.</param>
        /// <param name="data">Receives the record, or null when absent or unreadable.</param>
        /// <returns>
        /// False when the record is missing or corrupt. Callers must fall back to defaults rather
        /// than treating a failed load as fatal; a player with a damaged settings file should still
        /// reach the main menu.
        /// </returns>
        bool TryLoad<TData>(string key, out TData data) where TData : class;

        /// <summary>Deletes a record if it exists.</summary>
        /// <param name="key">Record name.</param>
        void Delete(string key);
    }
}
