using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Helpers for creating and populating configuration assets from editor code.
    /// </summary>
    /// <remarks>
    /// Configuration fields are private and exposed through read-only properties, which is correct
    /// for runtime code but leaves generators with no way in. <see cref="SerializedObject"/> is the
    /// sanctioned route, and wrapping it here keeps the generators readable instead of burying their
    /// intent under serialization boilerplate.
    /// </remarks>
    internal static class AssetAuthoring
    {
        /// <summary>Loads an asset at a path, creating it if absent.</summary>
        /// <typeparam name="TAsset">ScriptableObject type to create.</typeparam>
        /// <param name="assetPath">Project-relative path including the file extension.</param>
        /// <param name="created">Receives true when a new asset was created.</param>
        /// <returns>The existing or newly created asset.</returns>
        public static TAsset CreateOrLoad<TAsset>(string assetPath, out bool created)
            where TAsset : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);

            // A plain load can miss an asset that exists on disk but has not been imported yet,
            // which happens whenever the setup chain runs in one batch invocation. Without this
            // second attempt the generator would conclude the asset was absent and overwrite it,
            // destroying hand-tuned values on every run.
            if (existing == null)
            {
                existing = GeneratedAssets.ForceImportAndLoad<TAsset>(assetPath);
            }

            if (existing != null)
            {
                created = false;
                return existing;
            }

            EnsureFolderExists(Path.GetDirectoryName(assetPath));

            var asset = ScriptableObject.CreateInstance<TAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            created = true;
            return asset;
        }

        /// <summary>Creates every folder in a project-relative path that does not yet exist.</summary>
        /// <param name="folderPath">Project-relative folder path, using either slash convention.</param>
        public static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] segments = folderPath.Split('/');
            string accumulated = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{accumulated}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(accumulated, segments[i]);
                }

                accumulated = next;
            }
        }

        /// <summary>Opens an asset for serialized editing through a scoped helper.</summary>
        /// <param name="asset">Asset to edit.</param>
        /// <returns>A writer that applies its changes when disposed.</returns>
        public static AssetWriter Edit(UnityEngine.Object asset) => new AssetWriter(asset);

        /// <summary>
        /// Scoped writer that applies and marks its target dirty when disposed.
        /// </summary>
        /// <remarks>
        /// Disposal semantics mean a generator cannot forget to call
        /// <see cref="SerializedObject.ApplyModifiedProperties"/>, which fails silently and produces
        /// an asset full of defaults with no error to explain it.
        /// </remarks>
        public sealed class AssetWriter : IDisposable
        {
            private readonly SerializedObject _serialized;
            private readonly UnityEngine.Object _asset;

            internal AssetWriter(UnityEngine.Object asset)
            {
                _asset = asset;
                _serialized = new SerializedObject(asset);
            }

            /// <summary>Assigns a floating-point field.</summary>
            /// <param name="fieldName">Backing field name, including its underscore prefix.</param>
            /// <param name="value">Value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Float(string fieldName, float value)
            {
                Require(fieldName).floatValue = value;
                return this;
            }

            /// <summary>Assigns an integer or enum field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Int(string fieldName, int value)
            {
                SerializedProperty property = Require(fieldName);

                if (property.propertyType == SerializedPropertyType.Enum)
                {
                    property.enumValueIndex = value;
                }
                else
                {
                    property.intValue = value;
                }

                return this;
            }

            /// <summary>Assigns an enum field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Enum value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Enum(string fieldName, System.Enum value) =>
                Int(fieldName, Convert.ToInt32(value));

            /// <summary>Assigns a string field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter String(string fieldName, string value)
            {
                Require(fieldName).stringValue = value;
                return this;
            }

            /// <summary>Assigns a boolean field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Bool(string fieldName, bool value)
            {
                Require(fieldName).boolValue = value;
                return this;
            }

            /// <summary>Assigns a three-component vector field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Vector3(string fieldName, UnityEngine.Vector3 value)
            {
                Require(fieldName).vector3Value = value;
                return this;
            }

            /// <summary>Assigns a colour field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Value to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Color(string fieldName, UnityEngine.Color value)
            {
                Require(fieldName).colorValue = value;
                return this;
            }

            /// <summary>Assigns an object reference field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="value">Reference to assign.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter Reference(string fieldName, UnityEngine.Object value)
            {
                Require(fieldName).objectReferenceValue = value;
                return this;
            }

            /// <summary>Replaces an object-reference array field.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="values">References to assign, in order.</param>
            /// <returns>This writer, for chaining.</returns>
            public AssetWriter ReferenceArray(string fieldName, params UnityEngine.Object[] values)
            {
                SerializedProperty property = Require(fieldName);
                property.arraySize = values.Length;

                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                return this;
            }

            /// <summary>Resizes an array field and returns it for element-by-element editing.</summary>
            /// <param name="fieldName">Backing field name.</param>
            /// <param name="size">Number of elements.</param>
            /// <returns>The resized array property.</returns>
            public SerializedProperty Array(string fieldName, int size)
            {
                SerializedProperty property = Require(fieldName);
                property.arraySize = size;
                return property;
            }

            /// <summary>Finds a property, failing loudly when a field has been renamed.</summary>
            private SerializedProperty Require(string fieldName)
            {
                SerializedProperty property = _serialized.FindProperty(fieldName);

                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"Field '{fieldName}' was not found on '{_asset.name}' ({_asset.GetType().Name}). " +
                        "The generator and the configuration type have drifted apart.");
                }

                return property;
            }

            /// <summary>Applies pending changes and marks the asset dirty.</summary>
            public void Dispose()
            {
                _serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_asset);
            }
        }
    }
}
