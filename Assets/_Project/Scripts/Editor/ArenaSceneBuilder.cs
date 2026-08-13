using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Builds the arena scene procedurally from <see cref="ArenaConfig"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generating the scene rather than hand-placing it keeps the layout derived from the
    /// configuration asset, so changing the arena radius and rebuilding is the entire workflow.
    /// It also means the scene can be reconstructed from source on a fresh clone, which matters
    /// because scene files merge poorly and are effectively unreviewable in a pull request.
    /// </para>
    /// <para>
    /// Phase one produces the arena shell: floor, boundary, lighting, camera and an empty managers
    /// root. Combatants are added by later phases as their prefabs come into being.
    /// </para>
    /// </remarks>
    public static class ArenaSceneBuilder
    {
        private const string ArenaRootName = "Arena";
        private const string ManagersRootName = "--- Managers ---";
        private const string SpawnRootName = "Spawns";

        private const float FloorThickness = 0.1f;
        private const float WallThickness = 0.5f;

        private static readonly Color FloorColor = new Color(0.16f, 0.16f, 0.19f);
        private static readonly Color WallColor = new Color(0.09f, 0.09f, 0.11f);

        /// <summary>Weathered stone for the pillars and rubble.</summary>
        private static readonly Color StoneColor = new Color(0.20f, 0.19f, 0.21f);

        /// <summary>A bruised, overcast sky for the arena to sit under.</summary>
        private static readonly Color SkyTint = new Color(0.32f, 0.20f, 0.24f);

        private static readonly Color SkyGroundColor = new Color(0.05f, 0.04f, 0.06f);

        /// <summary>Kept low, so the sky frames the fight rather than competing with it.</summary>
        private const float SkyExposure = 0.55f;

        /// <summary>
        /// Brazier fire, above one so it blooms.
        /// </summary>
        /// <remarks>
        /// The post-processing bloom threshold is 0.9, so anything brighter than that glows without
        /// further work. This is the cheapest light source in the scene.
        /// </remarks>
        private static readonly Color EmberGlow = new Color(2.4f, 1.1f, 0.35f);

        private static readonly Color BrazierLightColor = new Color(1f, 0.62f, 0.3f);

        private const float BrazierIntensity = 2.6f;
        private const float BrazierRange = 11f;

        /// <summary>How far outside the wall ring the pillars stand.</summary>
        private const float PillarRingOffset = 1.9f;

        private const int RubbleCount = 22;

        /// <summary>
        /// Seed for the dressing layout.
        /// </summary>
        /// <remarks>
        /// Fixed so the arena is identical in every regeneration and every build. Scattering with
        /// <c>UnityEngine.Random</c> would make the scene differ between two runs of the generator
        /// and produce a meaningless diff each time — the same reason the fight itself never uses it.
        /// </remarks>
        private const uint DressingSeed = 20260812u;

        /// <summary>Creates the arena scene, replacing any previously generated one.</summary>
        [MenuItem(EditorMenus.Setup + "3. Build Arena Scene", priority = EditorMenus.SetupPriorityBuildScene)]
        public static void BuildArenaScene()
        {
            ArenaConfig config = LoadArenaConfig();
            if (config == null)
            {
                return;
            }

            // The save prompt is modal and cannot be answered on a build machine.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Resolved before the scene is replaced. Creating a new scene unloads assets, and any
            // lookup afterwards returns null even for assets that are present and valid, which
            // previously left the whole interface wired to nothing.
            HudChannels channels = HudBuilder.ResolveChannels();
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                InputActionsGenerator.AssetPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildArenaGeometry(config);
            Light directionalLight = BuildLighting();
            BuildSpawnMarkers(config);
            BuildManagers();

            GameObject player = SpawnCombatant(
                PlayerPrefabBuilder.PrefabPath, config.PlayerSpawn, "Player");

            GameObject boss = SpawnCombatant(
                BossPrefabBuilder.PrefabPath, config.BossSpawn, "Boss");

            FaceEachOther(player, boss);
            BuildCamera(config, player, boss);
            BuildInterface(config, actions, channels);

            Object.FindAnyObjectByType<ArenaAtmosphere>()?.Bind(directionalLight, channels.BossPhase);

            Object.FindAnyObjectByType<ScreenEffects>()?.Bind(
                Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>(),
                channels.PlayerHealth, channels.Deflect);

            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedSceneFolder);
            EditorSceneManager.SaveScene(scene, EditorMenus.ArenaScenePath);
            AddSceneToBuildSettings(EditorMenus.ArenaScenePath);

            Debug.Log($"[Adaptive Boss Arena] Arena scene built at '{EditorMenus.ArenaScenePath}'.");
        }

        /// <summary>Finds the arena configuration asset, reporting clearly when it is missing.</summary>
        private static ArenaConfig LoadArenaConfig()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ArenaConfig)}");

            if (guids.Length == 0)
            {
                Debug.LogError(
                    "[Adaptive Boss Arena] No ArenaConfig asset found. " +
                    "Run 'Adaptive Boss Arena/Setup/2. Generate Default Assets' first.");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<ArenaConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>Creates the floor disc and the ring of wall segments enclosing it.</summary>
        private static void BuildArenaGeometry(ArenaConfig config)
        {
            var arenaRoot = new GameObject(ArenaRootName);
            arenaRoot.layer = Layers.Arena;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.layer = Layers.Arena;
            floor.transform.SetParent(arenaRoot.transform);

            // Unity's cylinder primitive is one unit in DIAMETER and two units tall, so the
            // horizontal scale is the full diameter and the vertical scale is half the thickness.
            // Scaling by the radius instead produces a floor half the intended size, which reads as
            // the walls having been placed wrongly rather than the floor.
            floor.transform.localScale =
                new Vector3(config.Radius * 2f, FloorThickness * 0.5f, config.Radius * 2f);
            floor.transform.position = new Vector3(0f, -FloorThickness * 0.5f, 0f);
            MaterialLibrary.Apply(floor, "ArenaFloor", FloorColor);

            ReplaceFloorCollider(floor, config);
            BuildWalls(config, arenaRoot.transform);
            BuildDressing(config, arenaRoot.transform);
            BuildEnvironment(config, arenaRoot.transform);
        }

        /// <summary>
        /// Instantiates the optional set-dressing prefab and, if asked, hides the placeholder meshes.
        /// </summary>
        /// <remarks>
        /// The background-art seam. Null today, so the arena stays the procedural floor and wall ring.
        /// When a prefab is supplied it is dressed on top; the procedural mesh renderers are optionally
        /// disabled — <em>after</em> nothing, and before the environment is added, so only the
        /// placeholders are hidden — while every collider stays live, keeping the proven boundary that
        /// bounds the fight regardless of what the imported set looks like.
        /// </remarks>
        private static void BuildEnvironment(ArenaConfig config, Transform arenaRoot)
        {
            if (config.EnvironmentPrefab == null)
            {
                return;
            }

            if (config.HideProceduralMeshesWithEnvironment)
            {
                // Colliders are separate components and stay enabled; only the visible meshes go.
                foreach (MeshRenderer renderer in arenaRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.enabled = false;
                }
            }

            // Instantiated after the hide pass, so the environment's own renderers are never touched.
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(
                config.EnvironmentPrefab, arenaRoot);
            environment.transform.localPosition = Vector3.zero;
            environment.transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Swaps the cylinder primitive's capsule collider for a flat box.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unity's cylinder primitive ships with a <see cref="CapsuleCollider"/>, not a mesh
        /// collider, and a capsule does not survive non-uniform scaling. Scaled to a wide, thin
        /// disc it takes its radius from the largest horizontal axis and its height from the
        /// vertical one; because the resulting height is far less than twice the radius, the
        /// capsule collapses into a sphere half the arena wide.
        /// </para>
        /// <para>
        /// The visible result is a flat floor that behaves like an invisible dome: characters stand
        /// on top of it, slide down the curve, and fall off the world. Nothing about the scene
        /// looks wrong, which is what makes it worth this much explanation.
        /// </para>
        /// <para>
        /// A box is the right replacement rather than a mesh collider. The play area is bounded by
        /// the wall ring, so the corners of the box are unreachable, and a primitive collider is far
        /// cheaper to test against than a mesh.
        /// </para>
        /// </remarks>
        private static void ReplaceFloorCollider(GameObject floor, ArenaConfig config)
        {
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var colliderObject = new GameObject("FloorCollider")
            {
                layer = Layers.Arena
            };

            colliderObject.transform.SetParent(floor.transform.parent, worldPositionStays: false);
            colliderObject.transform.localPosition = new Vector3(0f, -FloorThickness * 0.5f, 0f);

            BoxCollider box = colliderObject.AddComponent<BoxCollider>();
            box.size = new Vector3(config.Radius * 2f, FloorThickness, config.Radius * 2f);
        }

        /// <summary>Approximates the circular boundary with a ring of box segments.</summary>
        /// <summary>
        /// Adds pillars, braziers and rubble around the fighting floor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The arena was a grey disc inside a ring of grey boxes, which gave the eye nothing to
        /// measure movement against. Standing geometry around the edge does that for free, and the
        /// braziers give the single directional light some company — their emissive bowls sit above
        /// the bloom threshold, so they glow without any extra work.
        /// </para>
        /// <para>
        /// Everything is placed from a seeded source, so the layout is identical on every
        /// regeneration and in every build. That is the same reason the fight itself uses a seeded
        /// provider, and the reason <c>UnityEngine.Random</c> is not used anywhere in this project.
        /// </para>
        /// <para>
        /// Purely decorative: it is all outside the wall ring or flat against the floor, and none of
        /// it carries a collider, so it can never block a dash or catch an attack.
        /// </para>
        /// </remarks>
        private static void BuildDressing(ArenaConfig config, Transform parent)
        {
            var dressingRoot = new GameObject("Dressing");
            dressingRoot.transform.SetParent(parent);

            var random = new XorShiftRandomProvider(DressingSeed);

            Material stone = MaterialLibrary.GetOrCreateSurface(
                "ArenaStone", StoneColor, metallic: 0f, smoothness: 0.12f);

            Material ember = MaterialLibrary.GetOrCreateSurface(
                "ArenaEmber", Color.black, metallic: 0f, smoothness: 0.5f, emission: EmberGlow);

            int pillars = Mathf.Max(4, config.WallSegments / 4);

            for (int i = 0; i < pillars; i++)
            {
                float angle = i * (360f / pillars) * Mathf.Deg2Rad;
                float radius = config.Radius + PillarRingOffset;
                var footprint = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                float height = config.WallHeight * random.NextFloat(2.1f, 2.7f);

                GameObject pillar = AddDecoration(
                    dressingRoot.transform, $"Pillar_{i:D2}", PrimitiveType.Cube, stone,
                    footprint + new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(1.1f, height, 1.1f));

                pillar.transform.rotation = Quaternion.Euler(0f, -i * (360f / pillars), 0f);

                // A bowl of fire at head height on alternate pillars. Alternating rather than every
                // one keeps the ring from reading as a regular pattern.
                if (i % 2 != 0)
                {
                    continue;
                }

                AddDecoration(
                    dressingRoot.transform, $"Brazier_{i:D2}", PrimitiveType.Sphere, ember,
                    footprint + new Vector3(0f, height * 0.82f, 0f),
                    Vector3.one * 0.66f);

                var light = new GameObject($"BrazierLight_{i:D2}");
                light.transform.SetParent(dressingRoot.transform);
                light.transform.position = footprint + new Vector3(0f, height * 0.82f, 0f);

                Light brazier = light.AddComponent<Light>();
                brazier.type = LightType.Point;
                brazier.color = BrazierLightColor;
                brazier.intensity = BrazierIntensity;
                brazier.range = BrazierRange;

                // No shadows: eight shadow-casting point lights would cost far more than they add
                // against untextured geometry.
                brazier.shadows = LightShadows.None;
            }

            for (int i = 0; i < RubbleCount; i++)
            {
                float angle = random.NextFloat(0f, Mathf.PI * 2f);
                float radius = config.Radius + random.NextFloat(1.6f, 5.5f);
                float size = random.NextFloat(0.35f, 1.15f);

                GameObject rubble = AddDecoration(
                    dressingRoot.transform, $"Rubble_{i:D2}", PrimitiveType.Cube, stone,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        size * random.NextFloat(0.1f, 0.35f),
                        Mathf.Sin(angle) * radius),
                    Vector3.one * size);

                rubble.transform.rotation = Quaternion.Euler(
                    random.NextFloat(-20f, 20f),
                    random.NextFloat(0f, 360f),
                    random.NextFloat(-20f, 20f));
            }
        }

        /// <summary>Creates one decorative object, stripped of its collider.</summary>
        /// <remarks>
        /// The collider always goes. Dressing that could be collided with would change the fight,
        /// and a scaled primitive's collider is the exact trap the floor already had to work around.
        /// </remarks>
        private static GameObject AddDecoration(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Material material,
            Vector3 position,
            Vector3 scale)
        {
            GameObject decoration = GameObject.CreatePrimitive(primitive);
            decoration.name = name;
            decoration.layer = Layers.Arena;
            decoration.transform.SetParent(parent);
            decoration.transform.position = position;
            decoration.transform.localScale = scale;

            Object.DestroyImmediate(decoration.GetComponent<Collider>());

            if (material != null)
            {
                decoration.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return decoration;
        }

        private static void BuildWalls(ArenaConfig config, Transform parent)
        {
            var wallRoot = new GameObject("Walls");
            wallRoot.transform.SetParent(parent);

            int segments = Mathf.Max(3, config.WallSegments);
            float degreesPerSegment = 360f / segments;

            // Chord length of one segment, widened slightly so neighbouring segments overlap and
            // leave no seam for a fast-moving character to squeeze through.
            float segmentWidth = 2f * config.Radius * Mathf.Sin(Mathf.PI / segments) * 1.05f;

            for (int i = 0; i < segments; i++)
            {
                float angleDegrees = i * degreesPerSegment;
                float angleRadians = angleDegrees * Mathf.Deg2Rad;

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i:D2}";
                wall.layer = Layers.Arena;
                wall.transform.SetParent(wallRoot.transform);

                float radialOffset = config.Radius + WallThickness * 0.5f;
                wall.transform.position = new Vector3(
                    Mathf.Cos(angleRadians) * radialOffset,
                    config.WallHeight * 0.5f,
                    Mathf.Sin(angleRadians) * radialOffset);

                // Rotate each segment to face the arena centre.
                wall.transform.rotation = Quaternion.Euler(0f, -angleDegrees, 0f);
                wall.transform.localScale = new Vector3(WallThickness, config.WallHeight, segmentWidth);

                MaterialLibrary.Apply(wall, "ArenaWall", WallColor);
            }
        }

        /// <summary>Adds a single directional light angled to give the primitives readable form.</summary>
        /// <returns>The directional light, so the atmosphere system can recolour it per phase.</returns>
        private static Light BuildLighting()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();

            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // The sky is a backdrop only: ambient light stays on the three-band Trilight the
            // atmosphere controller drives per phase, so the sky can be dressed without the arena's
            // lighting mood being handed over to it.
            RenderSettings.skybox = MaterialLibrary.GetOrCreateSkybox(
                "ArenaSky", SkyTint, SkyGroundColor, SkyExposure);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.24f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.14f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.08f);

            return light;
        }

        /// <summary>
        /// Builds the camera as a rig parent with the camera itself as a child.
        /// </summary>
        /// <remarks>
        /// The split matters: the parent handles framing and the child handles shake. Putting both on
        /// one transform makes the follow easing chase the shake offset and turns a sharp impact into
        /// a lingering wobble.
        /// </remarks>
        private static void BuildCamera(ArenaConfig config, GameObject player, GameObject boss)
        {
            var rigObject = new GameObject("Camera Rig");
            rigObject.transform.position = new Vector3(0f, config.CameraHeight, -config.CameraDistance);
            rigObject.transform.rotation = Quaternion.Euler(config.CameraPitchDegrees, 0f, 0f);

            ArenaCameraRig rig = rigObject.AddComponent<ArenaCameraRig>();
            rig.SetConfig(config);

            // Framing leans from the player toward the boss, so the space between them — where the
            // information the player needs actually is — stays on screen.
            rig.SetTargets(
                player != null ? player.transform : null,
                boss != null ? boss.transform : null);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(rigObject.transform, false);

            Camera camera = cameraObject.AddComponent<Camera>();

            // Cleared to a flat colour before, so the world stopped at the wall tops with nothing
            // beyond them. The background colour is kept as the fallback for the case where the sky
            // shader cannot be resolved.
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;

            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CameraShaker>();

            // Post-processing has to be enabled per-camera in URP as well as configured globally;
            // leaving this off produces a correct volume profile that does nothing at all.
            UniversalAdditionalCameraData cameraData =
                cameraObject.GetComponent<UniversalAdditionalCameraData>()
                ?? cameraObject.AddComponent<UniversalAdditionalCameraData>();

            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            BuildPostProcessingVolume();
        }

        /// <summary>Adds the global volume carrying the generated post-processing profile.</summary>
        private static void BuildPostProcessingVolume()
        {
            UnityEngine.Rendering.VolumeProfile profile = PostProcessingConfigurator.LoadProfile();

            if (profile == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] No post-processing profile found; the arena will render " +
                    "without bloom or grading. Run 'Configure Post Processing'.");
                return;
            }

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>();

            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        /// <summary>Adds the object that creates services, drives the clock and hosts debug tooling.</summary>
        private static void BuildManagers()
        {
            var managersRoot = new GameObject(ManagersRootName);

            managersRoot.AddComponent<GameBootstrapper>();
            managersRoot.AddComponent<AdaptationDebugOverlay>();
            managersRoot.AddComponent<EncounterDirector>();
            managersRoot.AddComponent<AudioService>();
            managersRoot.AddComponent<CombatAudioDirector>();
            managersRoot.AddComponent<CombatEffectsDirector>();
            managersRoot.AddComponent<ArenaAtmosphere>();
            managersRoot.AddComponent<ScreenEffects>();

            // Pools the ground hazards the boss's heavy attacks leave behind. The boss finds it by
            // type at start; its absence simply means no scars, so tests and stripped scenes are fine.
            managersRoot.AddComponent<Combat.HazardField>();
        }

        /// <summary>
        /// Builds the heads-up display and connects it to the encounter director.
        /// </summary>
        /// <remarks>
        /// Built last, because the director it wires into needs the combatants to already exist for
        /// its automatic lookups to find them.
        /// </remarks>
        private static void BuildInterface(
            ArenaConfig config,
            UnityEngine.InputSystem.InputActionAsset actions,
            HudChannels channels)
        {
            HudReferences hud = HudBuilder.Build(actions, channels);

            var director = Object.FindAnyObjectByType<EncounterDirector>();

            if (director == null)
            {
                Debug.LogWarning(
                    "[Adaptive Boss Arena] No EncounterDirector found, so the outcome screens will " +
                    "not be driven.");
                return;
            }

            director.Bind(
                config, hud.EndScreen, hud.PauseMenu, hud.RoundIntro,
                channels.PlayerDied, channels.BossDefeated, channels.Deflect, channels.PerfectDodge);

            var audioDirector = Object.FindAnyObjectByType<CombatAudioDirector>();
            audioDirector?.Bind(
                channels.Deflect, channels.PerfectDodge, channels.BossPhase, channels.PlayerFocus,
                channels.Overbalance, channels.WeaponSwingCue);

            var effectsDirector = Object.FindAnyObjectByType<CombatEffectsDirector>();
            effectsDirector?.Bind(channels.PerfectDodge, channels.BossPhase, channels.Overbalance);
        }

        /// <summary>Places a generated combatant prefab at its spawn point.</summary>
        private static GameObject SpawnCombatant(string prefabPath, Vector3 position, string label)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[Adaptive Boss Arena] {label} prefab not found at '{prefabPath}', so it has been " +
                    "left out of the scene. Generate the prefab and rebuild.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;

            return instance;
        }

        /// <summary>Turns the combatants to face one another at the start of the fight.</summary>
        private static void FaceEachOther(GameObject player, GameObject boss)
        {
            if (player == null || boss == null)
            {
                return;
            }

            Vector3 between = boss.transform.position - player.transform.position;
            between.y = 0f;

            if (between.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            player.transform.rotation = Quaternion.LookRotation(between, Vector3.up);
            boss.transform.rotation = Quaternion.LookRotation(-between, Vector3.up);
        }

        /// <summary>Creates empty markers at the configured spawn positions.</summary>
        private static void BuildSpawnMarkers(ArenaConfig config)
        {
            var spawnRoot = new GameObject(SpawnRootName);

            var playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.SetParent(spawnRoot.transform);
            playerSpawn.transform.position = config.PlayerSpawn;

            var bossSpawn = new GameObject("BossSpawn");
            bossSpawn.transform.SetParent(spawnRoot.transform);
            bossSpawn.transform.position = config.BossSpawn;
        }

        /// <summary>Adds the scene to the build settings list if it is not already present.</summary>
        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            foreach (EditorBuildSettingsScene scene in existing)
            {
                if (scene.path == scenePath)
                {
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(scenePath, true);

            EditorBuildSettings.scenes = updated;
        }
    }
}
