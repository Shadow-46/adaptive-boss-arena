using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Builds the held weapon models the socket has been waiting for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The weapon socket, the equip call and the model field have all existed since the art seams
    /// were laid in, with nothing to put in them — so swapping weapons changed a label and a swing
    /// colour and nothing the player could see in their hands. These fill that slot from code.
    /// </para>
    /// <para>
    /// Three shapes, chosen to be told apart in a fraction of a second from a camera looking almost
    /// straight down: the greatsword is long and broad, the blade is medium and even, and the energy
    /// blade is short, thin and lit. Each is tinted from its weapon's own
    /// <c>SignatureColor</c> — a field that until now was authored, exposed and read by nothing.
    /// </para>
    /// <para>
    /// The socket contract is pivot at the grip and blade along +Z, because
    /// <c>WeaponSocket.Equip</c> zeroes the local position and rotation of whatever it instantiates.
    /// No part carries a collider: the weapon is a picture, and hits are resolved by the attack
    /// overlap system, never by the model.
    /// </para>
    /// </remarks>
    internal static class WeaponModelBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Weapons";

        /// <summary>Grip and crossguard, common to all three so they read as a matched set.</summary>
        private static readonly Color HiltColor = new Color(0.14f, 0.13f, 0.15f);

        /// <summary>Dark, slightly warm steel.</summary>
        private static readonly Color SteelColor = new Color(0.56f, 0.55f, 0.53f);

        /// <summary>
        /// Builds or loads the model for one weapon.
        /// </summary>
        /// <param name="assetName">Weapon asset name, used for the prefab file name.</param>
        /// <param name="signature">The weapon's signature colour, used to tint the blade.</param>
        /// <param name="length">Blade length in metres.</param>
        /// <param name="width">Blade width in metres.</param>
        /// <param name="glows">Whether the blade emits light, for the energy weapon.</param>
        /// <returns>The prefab, or null when it could not be created.</returns>
        public static GameObject GetOrCreate(
            string assetName, Color signature, float length, float width, bool glows)
        {
            // Rebuilt on every run: the model is structure, not tuning, and saving over the same path keeps the
            // prefab's identity, so every weapon pointing at it still does.
            string path = $"{PrefabFolder}/{assetName}Model.prefab";

            AssetAuthoring.EnsureFolderExists(PrefabFolder);

            var root = new GameObject($"{assetName}Model");

            try
            {
                Build(root.transform, assetName, signature, length, width, glows);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                // Built in the scene and saved out, so the temporary original must not be left
                // behind — the same pattern the character prefab builders use.
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Assembles grip, guard and blade around a pivot at the hand.</summary>
        private static void Build(
            Transform root, string assetName, Color signature, float length, float width, bool glows)
        {
            // Opaque colour for the mesh: the signature is authored with alpha for the swing overlay,
            // and a translucent-looking blade would read as a mistake rather than a style.
            var bladeColor = new Color(signature.r, signature.g, signature.b);

            Material hilt = MaterialLibrary.GetOrCreateSurface(
                $"{assetName}Hilt", HiltColor, metallic: 0.5f, smoothness: 0.3f);

            Material blade = glows
                ? MaterialLibrary.GetOrCreateSurface(
                    $"{assetName}Blade", bladeColor * 0.4f, metallic: 0f, smoothness: 0.8f,
                    emission: bladeColor * 2.2f)
                : MaterialLibrary.GetOrCreateSurface(
                    // Steel, barely touched by the signature colour: a fully tinted blade read as a painted toy.
                    $"{assetName}Steel", Color.Lerp(SteelColor, bladeColor, 0.12f), metallic: 0.95f, smoothness: 0.72f);

            // Real shapes rather than stretched boxes: a diamond-section blade narrowing to a point, a crossguard,
            // a grip and a pommel. The grip runs back from the pivot so the hand closes around it.
            AddPart(root, "Grip", hilt, Art.ArmsBuilder.Blade(0.2f, 0.045f, 0.045f, 0.05f),
                new Vector3(0f, 0f, -0.2f), Quaternion.identity);

            AddPart(root, "Pommel", hilt, Art.ArmsBuilder.Blade(0.07f, 0.07f, 0.07f, 0.5f),
                new Vector3(0f, 0f, -0.26f), Quaternion.identity);

            // Built along +Z and turned across the blade, so it crosses the hand the way a guard does.
            AddPart(root, "Guard", hilt, Art.ArmsBuilder.Blade(width * 3.2f, 0.05f, 0.04f, 0.15f),
                new Vector3(-width * 1.6f, 0f, 0.02f), Quaternion.Euler(0f, 90f, 0f));

            AddPart(root, "Blade", blade, Art.ArmsBuilder.Blade(length, width, width * 0.2f, 0.14f),
                new Vector3(0f, 0f, 0.04f), Quaternion.identity);
        }

        /// <summary>Creates one collider-free piece of the weapon.</summary>
        /// <remarks>
        /// No collider: one here would sit on the player's own layer inside the character controller and fight
        /// it. Damage comes from the attack overlap, never from the model.
        /// </remarks>
        private static void AddPart(
            Transform parent, string name, Material material, Mesh mesh, Vector3 position, Quaternion rotation)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;

            if (material != null)
            {
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
