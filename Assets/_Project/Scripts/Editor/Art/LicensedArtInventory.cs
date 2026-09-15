using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Reports what the licensed art actually contains, so clips are chosen from facts rather than from file names.
    /// </summary>
    /// <remarks>
    /// Mixamo packs name their variants <c>slash</c>, <c>slash (2)</c>, <c>slash (3)</c>. The lengths and root
    /// speeds here, with the contact sheet, are what tell a quick cut from a wide spinning swing.
    /// </remarks>
    public static class LicensedArtInventory
    {
        /// <summary>Logs one line per clip and per character. Run with <c>-executeMethod</c>.</summary>
        public static void WriteReport()
        {
            if (!LicensedArtPostprocessor.Available)
            {
                Debug.LogWarning("[ART] The licensed Mixamo art is not in this checkout.");
                return;
            }

            foreach (string character in new[] { LicensedArtPostprocessor.KnightCharacter, LicensedArtPostprocessor.BossCharacter })
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(character);
                Avatar avatar = assets.OfType<Avatar>().FirstOrDefault();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(character);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                var animator = model.GetComponentInChildren<Animator>(true);

                string meshes = string.Join(", ", renderers.Select(r =>
                    r.name + ":" + (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null ? skinned.sharedMesh.vertexCount + "v" : "static")));

                Debug.Log($"[ART] CHARACTER {Path.GetFileName(character)} avatarHuman={avatar != null && avatar.isHuman} " +
                          $"valid={avatar != null && avatar.isValid} renderers={renderers.Length} ({meshes}) " +
                          $"materials={string.Join(", ", renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())} " +
                          $"textures={string.Join(", ", assets.OfType<Texture>().Select(t => t.name))} " +
                          $"height={renderers.Select(r => r.bounds.size.y).DefaultIfEmpty(0f).Max():F2}m animator={animator != null}");
            }

            foreach (string file in Directory.GetFiles(LicensedArtPostprocessor.AnimationFolder, "*.fbx", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string path = file.Replace('\\', '/');
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                string set = Path.GetFileName(Path.GetDirectoryName(path));

                if (clip == null)
                {
                    Debug.Log($"[ART] CLIP {set}/{Path.GetFileName(path)} NO CLIP");
                    continue;
                }

                Debug.Log($"[ART] CLIP {set}/{clip.name} length={clip.length:F2}s frames={Mathf.RoundToInt(clip.length * clip.frameRate)} " +
                          $"loop={clip.isLooping} human={clip.isHumanMotion} speed={clip.averageSpeed.magnitude:F2} " +
                          $"turn={clip.averageAngularSpeed:F0} avatar={(importer.sourceAvatar != null ? importer.sourceAvatar.name : "none")}");
            }
        }
    }
}
