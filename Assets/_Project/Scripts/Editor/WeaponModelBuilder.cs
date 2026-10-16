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

        /// <summary>Where the generated blade begins, just past its crossguard.</summary>
        private const float GeneratedGuardOffset = 0.04f;

        private const string WeaponArtFolder = "Assets/_Project/Art/Licensed/Weapons";
        private const string GeneratedArtFolder = WeaponArtFolder + "/Generated";
        private const string PackFolder = WeaponArtFolder + "/Low Poly Fantasy Weapon Pack";
        private const string EnergyFolder = WeaponArtFolder + "/Energy Sword";

        /// <summary>
        /// A downloaded sword and how it is held: the share of its length from the pommel to the middle of the
        /// grip, and to where the blade begins past the guard.
        /// </summary>
        /// <remarks>
        /// Read off each model's own shape: the pack's swords have their grip in the lowest eighth and their
        /// guard about a third of the way up; the energy sword has a short grip under a wide guard.
        /// </remarks>
        private static readonly (string Weapon, string Model, float GripShare, float BladeShare, bool Glows)[] Downloaded =
        {
            ("WeaponBlade", PackFolder + "/Longsword.fbx", 0.12f, 0.3f, false),
            ("WeaponGreatsword", PackFolder + "/Greatsword.fbx", 0.12f, 0.33f, false),
            ("WeaponEnergyBlade", EnergyFolder + "/EnergySword.obj", 0.08f, 0.45f, true)
        };

        /// <summary>
        /// Builds or loads the model for one weapon.
        /// </summary>
        /// <param name="assetName">Weapon asset name, used for the prefab file name.</param>
        /// <param name="signature">The weapon's signature colour, used to tint the blade.</param>
        /// <param name="length">Blade length in metres.</param>
        /// <param name="width">Blade width in metres.</param>
        /// <param name="glows">Whether the blade emits light, for the energy weapon.</param>
        /// <param name="bladeBase">Receives where the blade begins, in metres from the grip.</param>
        /// <param name="bladeTip">Receives where its point is.</param>
        /// <returns>The prefab, or null when it could not be created.</returns>
        public static GameObject GetOrCreate(
            string assetName, Color signature, float length, float width, bool glows,
            out float bladeBase, out float bladeTip)
        {
            // The point stays where the fight was tuned for, whichever model is held.
            bladeTip = length + GeneratedGuardOffset;
            bladeBase = GeneratedGuardOffset;
            // Rebuilt on every run: the model is structure, not tuning, and saving over the same path keeps the
            // prefab's identity, so every weapon pointing at it still does.
            string path = $"{PrefabFolder}/{assetName}Model.prefab";

            AssetAuthoring.EnsureFolderExists(PrefabFolder);

            var root = new GameObject($"{assetName}Model");

            try
            {
                if (!TryHoldDownloaded(root.transform, assetName, signature, bladeTip, ref bladeBase))
                {
                    Build(root.transform, assetName, signature, length, width, glows);
                }
                CharacterRendering.Mark(root);

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

        /// <summary>
        /// Puts a downloaded sword in the model, when this checkout has it: turned so its blade runs along +Z
        /// with the flat across X, its grip at the pivot, and scaled so its point reaches the weapon's tip.
        /// </summary>
        /// <remarks>
        /// The downloaded art is licensed and kept out of the repository, so a clone without it builds the
        /// generated swords exactly as before. The model's own axes are whatever its author used; they are read
        /// off its shape - the longest extent is the blade, the next is its width, the low end is the hilt -
        /// rather than trusted.
        /// </remarks>
        /// <returns>True when a downloaded sword was used.</returns>
        private static bool TryHoldDownloaded(
            Transform root, string assetName, Color signature, float tip, ref float bladeBase)
        {
            foreach ((string weapon, string modelPath, float gripShare, float bladeShare, bool glows) in Downloaded)
            {
                if (weapon != assetName)
                {
                    continue;
                }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                Material material = source != null ? DownloadedMaterial(assetName, modelPath, signature, glows) : null;

                if (source == null || material == null)
                {
                    return false;
                }

                var pivot = new GameObject("Model").transform;
                pivot.SetParent(root, false);
                var body = (GameObject)Object.Instantiate(source, pivot);
                body.name = System.IO.Path.GetFileNameWithoutExtension(modelPath);

                MeshFilter filter = body.GetComponentInChildren<MeshFilter>();

                if (filter == null || filter.sharedMesh == null)
                {
                    Object.DestroyImmediate(pivot.gameObject);
                    return false;
                }

                foreach (Collider collider in body.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider);
                }

                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = new[] { material };
                }

                Fit(pivot, filter, gripShare, tip);
                bladeBase = tip * Mathf.Clamp01((bladeShare - gripShare) / (1f - gripShare));
                return true;
            }

            return false;
        }

        /// <summary>Turns, scales and places the model so the grip is at the pivot and the point at the tip.</summary>
        private static void Fit(Transform pivot, MeshFilter filter, float gripShare, float tip)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            int along = LongestAxis(bounds.size, -1);
            int across = LongestAxis(bounds.size, along);

            Vector3 gripLocal = bounds.center;
            gripLocal[along] = bounds.min[along] + bounds.size[along] * gripShare;
            Vector3 tipLocal = bounds.center;
            tipLocal[along] = bounds.max[along];

            Transform root = pivot.parent;
            Vector3 grip = root.InverseTransformPoint(filter.transform.TransformPoint(gripLocal));
            Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(tipLocal));
            Vector3 width = root.InverseTransformDirection(filter.transform.TransformDirection(Unit(across)));

            Vector3 blade = (point - grip).normalized;
            width = Vector3.ProjectOnPlane(width, blade).normalized;
            Quaternion turn = Quaternion.Inverse(Quaternion.LookRotation(blade, Vector3.Cross(blade, width)));
            float scale = tip / Mathf.Max(0.0001f, Vector3.Distance(point, grip));

            pivot.localRotation = turn;
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = -(turn * grip) * scale;
        }

        private static int LongestAxis(Vector3 size, int except)
        {
            int best = -1;

            for (int i = 0; i < 3; i++)
            {
                if (i != except && (best < 0 || size[i] > size[best]))
                {
                    best = i;
                }
            }

            return best;
        }

        private static Vector3 Unit(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        /// <summary>
        /// The material a downloaded sword is drawn with: its own texture, steel-like, or for the energy sword its
        /// own colour and glow maps lit in the weapon's signature colour.
        /// </summary>
        /// <remarks>
        /// Kept beside the licensed art, out of the repository, since it points at the licensed textures.
        /// </remarks>
        private static Material DownloadedMaterial(string assetName, string modelPath, Color signature, bool glows)
        {
            string folder = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/');
            Texture2D albedo = glows
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/EnergySword_Albedo.png")
                : AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Texture.png");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (albedo == null || shader == null)
            {
                return null;
            }

            AssetAuthoring.EnsureFolderExists(GeneratedArtFolder);
            string path = $"{GeneratedArtFolder}/{assetName}Downloaded.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = assetName + "Downloaded" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", glows ? 0f : 0.65f);
            material.SetFloat("_Smoothness", glows ? 0.7f : 0.5f);

            if (glows)
            {
                // Lit in the weapon's own colour, as the generated energy blade was, through the model's glow map.
                var lit = new Color(signature.r, signature.g, signature.b);
                material.SetTexture(
                    "_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/EnergySword_Emission.png"));
                material.SetColor("_EmissionColor", lit * 2.2f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
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
