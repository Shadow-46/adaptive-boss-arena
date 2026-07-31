using System;
using System.IO;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Persists records as JSON files under the platform's save folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes are atomic: the record goes to a temporary file which then replaces the original.
    /// A direct overwrite leaves a truncated file if the process dies mid-write, and the player who
    /// alt-F4s during an autosave is exactly the player who then cannot launch the game.
    /// </para>
    /// <para>
    /// Reads never throw. A corrupt or unreadable file returns false so the caller falls back to
    /// defaults; losing settings is a small annoyance, whereas failing to start is not.
    /// </para>
    /// <para>
    /// <see cref="PlayerPrefs"/> was rejected deliberately. On Windows it writes to the registry,
    /// which is opaque, awkward to back up, and effectively impossible to inspect when diagnosing a
    /// player's problem.
    /// </para>
    /// </remarks>
    public sealed class JsonSaveService : ISaveService
    {
        private const string FileExtension = ".json";
        private const string TempExtension = ".tmp";
        private const string BackupExtension = ".bak";

        private readonly string _rootFolder;

        /// <summary>Creates a save service writing beneath the given folder.</summary>
        /// <param name="rootFolder">
        /// Destination folder. Defaults to the platform's persistent data path, which is the only
        /// location guaranteed to be writable across platforms.
        /// </param>
        public JsonSaveService(string rootFolder = null)
        {
            _rootFolder = string.IsNullOrEmpty(rootFolder)
                ? Application.persistentDataPath
                : rootFolder;

            Directory.CreateDirectory(_rootFolder);
        }

        /// <summary>Folder records are written to.</summary>
        public string RootFolder => _rootFolder;

        /// <inheritdoc />
        public bool Exists(string key) => File.Exists(PathFor(key));

        /// <inheritdoc />
        public void Save<TData>(string key, TData data) where TData : class
        {
            if (data == null)
            {
                return;
            }

            string finalPath = PathFor(key);
            string tempPath = finalPath + TempExtension;
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // The browser build's virtual filesystem does not support File.Replace, and the write
                // is persisted to IndexedDB by the runtime anyway, so a plain write is both correct and
                // free of the console error the atomic path would raise every time settings change.
                File.WriteAllText(finalPath, json);
#else
                File.WriteAllText(tempPath, json);

                // File.Replace is atomic where the platform supports it and keeps the previous
                // version as a backup, which gives a corrupt write something to fall back to.
                if (File.Exists(finalPath))
                {
                    File.Replace(tempPath, finalPath, finalPath + BackupExtension);
                }
                else
                {
                    File.Move(tempPath, finalPath);
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Adaptive Boss Arena] Failed to save '{key}': {exception.Message}");
                TryDelete(tempPath);
            }
        }

        /// <inheritdoc />
        public bool TryLoad<TData>(string key, out TData data) where TData : class
        {
            if (TryReadFrom(PathFor(key), out data))
            {
                return true;
            }

            // The primary file is missing or unreadable. File.Replace leaves the previous version
            // behind precisely for this, so it is worth a look before giving up.
            if (TryReadFrom(PathFor(key) + BackupExtension, out data))
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] Recovered '{key}' from its backup; the primary file was unreadable.");
                return true;
            }

            data = null;
            return false;
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
            TryDelete(PathFor(key));
            TryDelete(PathFor(key) + BackupExtension);
        }

        /// <summary>Reads and parses one file, treating any failure as "not available".</summary>
        private static bool TryReadFrom<TData>(string path, out TData data) where TData : class
        {
            data = null;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                data = JsonUtility.FromJson<TData>(json);
                return data != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] Could not read '{path}': {exception.Message}. Defaults will be used.");
                return false;
            }
        }

        /// <summary>Deletes a file, ignoring failures.</summary>
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Adaptive Boss Arena] Could not delete '{path}': {exception.Message}");
            }
        }

        /// <summary>Resolves the file path for a record key.</summary>
        private string PathFor(string key) => Path.Combine(_rootFolder, key + FileExtension);
    }
}
