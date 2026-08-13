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
            string path = $"{PrefabFolder}/{assetName}Model.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                return existing;
            }

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
                    $"{assetName}Blade", bladeColor, metallic: 0.92f, smoothness: 0.62f);

            AddPart(root, "Grip", hilt, new Vector3(0f, 0f, -0.09f), new Vector3(0.05f, 0.05f, 0.2f));

            AddPart(root, "Guard", hilt, new Vector3(0f, 0f, 0.02f), new Vector3(width * 2.6f, 0.06f, 0.07f));

            // Pushed forward by half its own length so the pivot stays at the grip.
            AddPart(root, "Blade", blade,
                new Vector3(0f, 0f, 0.04f + (length * 0.5f)),
                new Vector3(width, 0.035f, length));

            // A tapered tip costs one more box and is most of what stops the blade reading as a
            // plank from directly above.
            AddPart(root, "Tip", blade,
                new Vector3(0f, 0f, 0.04f + length + (length * 0.06f)),
                new Vector3(width * 0.45f, 0.03f, length * 0.14f));
        }

        /// <summary>Creates one collider-free piece of the weapon.</summary>
        private static void AddPart(
            Transform parent, string name, Material material, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            // A collider here would sit on the player's own layer inside the character controller
            // and fight it. Damage comes from the attack overlap, never from the model.
            Object.DestroyImmediate(part.GetComponent<Collider>());

            if (material != null)
            {
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
