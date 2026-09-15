using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Game;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Environment
{
    /// <summary>
    /// Builds the ruined cathedral the fight takes place in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fight happens on the crossing of a broken nave: eight columns in an octagon around the
    /// fighting floor, lintels between the ones still standing, an outer wall with tall window openings,
    /// an aisle roof that has partly fallen in and a crossing open to the sky, so the sun comes down
    /// onto the floor in shafts.
    /// </para>
    /// <para>
    /// Nothing here changes the fight. The playable radius, the collision ring and the layers all stay as
    /// the arena builder made them: every piece of the cathedral stands outside the ring or lies flat
    /// under it, and none carries a collider. The ring's own boxes stay live and only their meshes are
    /// hidden behind textured ones, so the boundary the AI's positioning was tuned against is untouched.
    /// </para>
    /// <para>
    /// Everything is placed from constants and a fixed seed, for the same reason the fight never uses
    /// <c>UnityEngine.Random</c>: two regenerations must produce the same scene.
    /// </para>
    /// </remarks>
    public static class CathedralBuilder
    {
        /// <summary>Name of the root object, which tests look for.</summary>
        public const string RootName = "Cathedral";

        /// <summary>The sun's rotation, shared with the arena's directional light so the shafts line up with it.</summary>
        public static readonly Quaternion SunRotation = Quaternion.Euler(58f, -35f, 0f);

        private const uint LayoutSeed = 20260914u;

        private const int ColumnCount = 8;
        private const float ColumnRingOffset = 3.5f;
        private const float ColumnHeight = 14f;
        private const float ColumnWidth = 1.8f;
        private const float OuterWallApothemOffset = 12f;
        private const float OuterWallHeight = 18f;
        private const float OuterWallThickness = 1.5f;
        private const float WindowSill = 5f;
        private const float WindowHead = 13f;
        private const int DebrisCount = 34;
        private const int ShaftCount = 5;
        private const float ParapetCrownHeight = 0.9f;
        private const int ParapetCrownPieces = 5;

        /// <summary>Live debris ceilings: the plan's physics budget of 40 bodies on WebGL, a generous 150 on desktop.</summary>
        private const int WebDebrisCapacity = 40;

        private const int DesktopDebrisCapacity = 150;

        private static readonly Color StoneTint = new Color(0.78f, 0.76f, 0.74f);
        private static readonly Color BrickTint = new Color(0.72f, 0.70f, 0.70f);
        private static readonly Color BlockTint = new Color(0.82f, 0.80f, 0.78f);
        private static readonly Color CandleFlame = new Color(3.2f, 1.6f, 0.55f);
        private static readonly Color CandleLight = new Color(1f, 0.66f, 0.34f);
        private static readonly Color ShaftColour = new Color(1f, 0.93f, 0.78f, 0.11f);

        /// <summary>Under half the desktop shafts' strength: the browser renders the same additive planes much brighter.</summary>
        private static readonly Color WebShaftColour = new Color(1f, 0.93f, 0.78f, 0.045f);
        private static readonly Color DustColour = new Color(1f, 0.95f, 0.85f, 0.5f);

        private static Material _floor;
        private static Material _brick;
        private static Material _block;

        /// <summary>Columns 2 and 5 have fallen. Two, and not adjacent, so the ring still reads as a ring.</summary>
        private static bool IsCollapsed(int column) => column == 2 || column == 5;

        /// <summary>Outer wall faces whose upper wall and roof bay have come down.</summary>
        private static bool IsRuinedBay(int face) => face == 1 || face == 4 || face == 6;

        /// <summary>Builds the cathedral around the arena.</summary>
        /// <param name="config">Arena dimensions.</param>
        /// <param name="arenaRoot">The arena root, whose floor and wall ring already exist.</param>
        public static void Build(ArenaConfig config, Transform arenaRoot)
        {
            _floor = MaterialLibrary.GetOrCreateTexturedLit("CathedralFloor", "stone_tiles_02", StoneTint);
            _brick = MaterialLibrary.GetOrCreateTexturedLit("CathedralBrick", "castle_brick_07", BrickTint);
            _block = MaterialLibrary.GetOrCreateTexturedLit("CathedralBlock", "medieval_blocks_02", BlockTint);

            var root = new GameObject(RootName);
            root.transform.SetParent(arenaRoot, false);

            float radius = config.Radius;

            BuildFloor(root.transform, arenaRoot, radius);
            ReskinWallRing(root.transform, arenaRoot);
            BuildColumns(root.transform, radius);
            BuildOuterWalls(root.transform, radius);
            BuildDebris(root.transform, radius);
            BuildShafts(root.transform);
            BuildDust(root.transform, radius);

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Static batching folds the whole ruin into a handful of draw calls in a build, which is
                // what keeps a hundred-odd stones inside the WebGL draw-call budget.
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>
        /// Lays a tiled stone floor across the nave and hides the flat placeholder disc.
        /// </summary>
        /// <remarks>
        /// The disc's UVs spread one texture across the whole 32-metre floor, which would show each
        /// flagstone the size of a car. Its collider is a separate object and stays exactly as it was.
        /// </remarks>
        private static void BuildFloor(Transform root, Transform arenaRoot, float radius)
        {
            Transform disc = arenaRoot.Find("Floor");

            if (disc != null && disc.TryGetComponent(out MeshRenderer discRenderer))
            {
                discRenderer.enabled = false;
            }

            float span = (radius + OuterWallApothemOffset + OuterWallThickness) * 2f + 4f;
            Block(root, "NaveFloor", new Vector3(span, 0.3f, span), 2.5f, _floor,
                new Vector3(0f, -0.15f, 0f), Quaternion.identity);
        }

        /// <summary>
        /// Hides the collision ring's grey boxes behind brick, keeping every collider where it is.
        /// </summary>
        /// <remarks>
        /// The visible box is replaced by a separate object rather than edited in place: its scale is what
        /// sizes its collider, and a tiled mesh needs a scale of one.
        /// </remarks>
        private static void ReskinWallRing(Transform root, Transform arenaRoot)
        {
            Transform walls = arenaRoot.Find("Walls");

            if (walls == null)
            {
                return;
            }

            var parapet = new GameObject("Parapet").transform;
            parapet.SetParent(root, false);

            foreach (Transform wall in walls)
            {
                if (wall.TryGetComponent(out MeshRenderer renderer))
                {
                    renderer.enabled = false;
                }

                BuildBreakableParapet(parapet, wall);
            }

            // Beside the cathedral rather than inside it: the pool's pieces carry colliders, and the
            // dressing is held to carrying none. Theirs are on the debris layer, which no fighter touches.
            var destruction = new GameObject("Destruction");
            destruction.transform.SetParent(arenaRoot, false);

            DebrisPool pool = destruction.AddComponent<DebrisPool>();
            pool.Bind(_brick, WebDebrisCapacity, DesktopDebrisCapacity);
            destruction.AddComponent<DestructibleField>().Bind(pool);
        }

        /// <summary>
        /// One segment of the parapet: a solid base, and a crown of stone on top that a slam or a body
        /// driven into the wall knocks off. Both are dressing over the unchanged collider.
        /// </summary>
        private static void BuildBreakableParapet(Transform parent, Transform wall)
        {
            Vector3 size = wall.localScale;
            float baseHeight = Mathf.Max(0.5f, size.y - ParapetCrownHeight);

            Block(parent, wall.name + "_Stone", new Vector3(size.x, baseHeight, size.z), 2f, _brick,
                wall.position + Vector3.down * (ParapetCrownHeight * 0.5f), wall.rotation);

            var crownSize = new Vector3(size.x, ParapetCrownHeight, size.z);
            GameObject crown = Block(parent, wall.name + "_Crown", crownSize, 2f, _brick,
                wall.position + Vector3.up * (baseHeight * 0.5f), wall.rotation);

            crown.AddComponent<Destructible>().Bind(crown.GetComponent<MeshRenderer>(), crownSize, ParapetCrownPieces);
        }

        private static void BuildColumns(Transform root, float radius)
        {
            var columns = new GameObject("Columns").transform;
            columns.SetParent(root, false);

            float ringRadius = radius + ColumnRingOffset;
            float step = 360f / ColumnCount;
            var random = new XorShiftRandomProvider(LayoutSeed);

            for (int i = 0; i < ColumnCount; i++)
            {
                float angle = step * 0.5f + step * i;
                Vector3 foot = OnRing(angle, ringRadius);
                Quaternion facing = Quaternion.Euler(0f, -angle, 0f);

                Block(columns, $"Plinth_{i}", new Vector3(2.6f, 1f, 2.6f), 1.5f, _block, foot + Vector3.up * 0.5f, facing);

                if (IsCollapsed(i))
                {
                    BuildFallenColumn(columns, i, foot, angle, random);
                    continue;
                }

                Block(columns, $"Shaft_{i}", new Vector3(ColumnWidth, ColumnHeight, ColumnWidth), 1.5f, _block,
                    foot + Vector3.up * (1f + ColumnHeight * 0.5f), facing);
                Block(columns, $"Capital_{i}", new Vector3(2.4f, 0.8f, 2.4f), 1.5f, _block,
                    foot + Vector3.up * (1.4f + ColumnHeight), facing);

                if (i % 2 == 0)
                {
                    BuildCandles(columns, i, foot, angle);
                }

                // A lintel to the next column, only while both still stand to carry it.
                int next = (i + 1) % ColumnCount;

                if (!IsCollapsed(next))
                {
                    float midAngle = angle + step * 0.5f;
                    float chord = 2f * ringRadius * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad);
                    Vector3 middle = OnRing(midAngle, ringRadius * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad));

                    Block(columns, $"Lintel_{i}", new Vector3(1.4f, 1.6f, chord), 1.5f, _block,
                        middle + Vector3.up * (2.6f + ColumnHeight), Quaternion.Euler(0f, -midAngle, 0f));
                }
            }
        }

        /// <summary>
        /// A broken stump, the shaft lying where it fell - outward, away from the fighting floor - and the
        /// lintels it carried in pieces beside it.
        /// </summary>
        private static void BuildFallenColumn(
            Transform parent, int index, Vector3 foot, float angle, XorShiftRandomProvider random)
        {
            const float Length = 9f;

            float stump = index == 2 ? 3.2f : 5.4f;
            Quaternion facing = Quaternion.Euler(0f, -angle, 0f);

            Block(parent, $"Stump_{index}", new Vector3(ColumnWidth, stump, ColumnWidth), 1.5f, _block,
                foot + Vector3.up * (1f + stump * 0.5f), facing * Quaternion.Euler(0f, 0f, 3f));

            Vector3 outward = OnRing(angle, 1f);
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            Vector3 centre = foot + outward * (Length * 0.5f + 1.2f) + across * 1.1f + Vector3.up * 1.1f;
            Quaternion lying = Quaternion.LookRotation(outward + across * 0.25f, Vector3.up) * Quaternion.Euler(6f, 0f, 0f);

            Block(parent, $"FallenShaft_{index}", new Vector3(ColumnWidth, ColumnWidth, Length), 1.5f, _block, centre, lying);

            for (int piece = 0; piece < 3; piece++)
            {
                float size = random.NextFloat(0.9f, 1.5f);
                Vector3 at = foot + outward * random.NextFloat(1.8f, 4f) - across * random.NextFloat(1f, 3f);

                Block(parent, $"LintelPiece_{index}_{piece}", new Vector3(1.4f, 1.6f, size * 2.4f), 1.5f, _block,
                    at + Vector3.up * 0.75f,
                    Quaternion.Euler(random.NextFloat(-12f, 12f), random.NextFloat(0f, 360f), random.NextFloat(-8f, 8f)));
            }
        }

        /// <summary>Candles at a column's foot: the only warm light left in the building.</summary>
        private static void BuildCandles(Transform parent, int index, Vector3 foot, float angle)
        {
            Material wax = MaterialLibrary.GetOrCreateSurface("CandleWax", new Color(0.86f, 0.82f, 0.70f), 0f, 0.3f);
            Material flame = MaterialLibrary.GetOrCreateSurface("CandleFlame", Color.black, 0f, 0f, CandleFlame);

            // On the side facing the fight, so the light falls on the floor rather than the column's back.
            Vector3 front = foot - OnRing(angle, 1.8f);
            float[] heights = { 0.45f, 0.3f, 0.6f };

            for (int c = 0; c < heights.Length; c++)
            {
                Vector3 at = front + Quaternion.Euler(0f, c * 120f + angle, 0f) * new Vector3(0.22f, 0f, 0f);

                Block(parent, $"Candle_{index}_{c}", new Vector3(0.09f, heights[c], 0.09f), 1f, wax,
                    at + Vector3.up * (heights[c] * 0.5f), Quaternion.identity);
                Block(parent, $"Flame_{index}_{c}", new Vector3(0.05f, 0.09f, 0.05f), 1f, flame,
                    at + Vector3.up * (heights[c] + 0.05f), Quaternion.identity);
            }

            var lightObject = new GameObject($"CandleLight_{index}");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = front + Vector3.up * 0.8f;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = CandleLight;
            light.intensity = 2.2f;
            light.range = 8f;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// The octagonal outer wall: two piers per face with a tall window between them, and an aisle roof
        /// bay reaching in to the column ring. Ruined bays keep lowered piers and lose the rest.
        /// </summary>
        private static void BuildOuterWalls(Transform root, float radius)
        {
            var walls = new GameObject("OuterWalls").transform;
            walls.SetParent(root, false);

            float apothem = radius + OuterWallApothemOffset;
            float faceWidth = 2f * apothem * Mathf.Tan(Mathf.PI / ColumnCount);
            float pier = faceWidth * 0.34f;
            float window = faceWidth - pier * 2f;
            float step = 360f / ColumnCount;

            for (int face = 0; face < ColumnCount; face++)
            {
                float angle = step * face;
                bool ruined = IsRuinedBay(face);
                Quaternion facing = Quaternion.Euler(0f, -angle, 0f);
                Vector3 centre = OnRing(angle, apothem + OuterWallThickness * 0.5f);
                Vector3 along = facing * Vector3.forward;
                float pierHeight = ruined ? OuterWallHeight * 0.62f : OuterWallHeight;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 at = centre + along * (side * (window + pier) * 0.5f);

                    Block(walls, $"Pier_{face}_{(side < 0 ? "L" : "R")}",
                        new Vector3(OuterWallThickness, pierHeight, pier + 0.05f), 2f, _brick,
                        at + Vector3.up * (pierHeight * 0.5f), facing);
                }

                Block(walls, $"Sill_{face}", new Vector3(OuterWallThickness, WindowSill, window), 2f, _brick,
                    centre + Vector3.up * (WindowSill * 0.5f), facing);

                if (ruined)
                {
                    continue;
                }

                float headHeight = OuterWallHeight - WindowHead;

                Block(walls, $"Head_{face}", new Vector3(OuterWallThickness, headHeight, window), 2f, _brick,
                    centre + Vector3.up * (WindowHead + headHeight * 0.5f), facing);

                float bayDepth = apothem - (radius + ColumnRingOffset) + 1f;
                Vector3 bayCentre = OnRing(angle, apothem - bayDepth * 0.5f);

                Block(walls, $"AisleRoof_{face}", new Vector3(bayDepth, 0.6f, faceWidth), 2f, _block,
                    bayCentre + Vector3.up * (OuterWallHeight + 0.3f), facing);
            }
        }

        /// <summary>Stones scattered between the column ring and the outer wall, never on the fighting floor.</summary>
        private static void BuildDebris(Transform root, float radius)
        {
            var debris = new GameObject("Debris").transform;
            debris.SetParent(root, false);

            var random = new XorShiftRandomProvider(LayoutSeed + 1u);

            for (int i = 0; i < DebrisCount; i++)
            {
                float angle = random.NextFloat(0f, 360f);
                float distance = radius + random.NextFloat(1.2f, OuterWallApothemOffset - 1f);
                float size = random.NextFloat(0.3f, 1.3f);
                var dimensions = new Vector3(size * random.NextFloat(0.7f, 1.6f), size, size * random.NextFloat(0.7f, 1.4f));

                Block(debris, $"Stone_{i:D2}", dimensions, 1.5f, i % 3 == 0 ? _brick : _block,
                    OnRing(angle, distance) + Vector3.up * (size * 0.35f),
                    Quaternion.Euler(random.NextFloat(-25f, 25f), random.NextFloat(0f, 360f), random.NextFloat(-25f, 25f)));
            }
        }

        /// <summary>
        /// Sunlight falling through the open crossing, faked with soft additive planes.
        /// </summary>
        /// <remarks>
        /// URP has no volumetric lighting, and the WebGL build could not afford it if it did. Three crossed
        /// planes with a soft-edged gradient read as a shaft from any angle the camera takes, cost a few
        /// dozen triangles, and never write depth, so they cannot hide a telegraph or a fighter.
        /// </remarks>
        private static void BuildShafts(Transform root)
        {
            const float Length = 26f;

            var shafts = new GameObject("LightShafts").transform;
            shafts.SetParent(root, false);

            Material material = GetOrCreateAdditive("LightShaft", ShaftGradient(), ShaftColour);
            Material webMaterial = GetOrCreateAdditive("LightShaftWeb", ShaftGradient(), WebShaftColour);
            var renderers = new Renderer[ShaftCount];
            Mesh mesh = GetOrCreateCrossedPlanes();
            Vector3 down = SunRotation * Vector3.forward;
            var random = new XorShiftRandomProvider(LayoutSeed + 2u);

            for (int i = 0; i < ShaftCount; i++)
            {
                var landing = new Vector3(random.NextFloat(-9f, 9f), 0f, random.NextFloat(-9f, 9f));
                float width = random.NextFloat(2.2f, 4f);

                var shaft = new GameObject($"Shaft_{i}");
                shaft.transform.SetParent(shafts, false);
                shaft.transform.position = landing - down * (Length * 0.5f);
                shaft.transform.rotation = Quaternion.FromToRotation(Vector3.up, -down) * Quaternion.Euler(0f, i * 37f, 0f);
                shaft.transform.localScale = new Vector3(width, Length, width);

                shaft.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = shaft.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderers[i] = renderer;
            }

            root.parent.gameObject.AddComponent<PlatformLighting>().BindShafts(renderers, webMaterial);
        }

        /// <summary>
        /// Hands the sun and the broken roof's shadow pattern to the platform lighting, once the light exists.
        /// </summary>
        /// <param name="sun">The arena's directional light.</param>
        public static void BindSun(Light sun)
        {
            PlatformLighting lighting = Object.FindAnyObjectByType<PlatformLighting>();

            if (lighting == null || sun == null)
            {
                return;
            }

            // The cookie's size lives on URP's light data, which a light created from code does not carry yet.
            if (!sun.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalLightData _))
            {
                sun.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            }

            lighting.BindSun(sun, RoofShadowCookie());
        }

        /// <summary>
        /// The shadow the ruined roof throws: four rafters across open sky, and ragged patches of roof still standing.
        /// </summary>
        /// <remarks>
        /// Tileable, because a directional cookie repeats across the whole floor. The patches come from value
        /// noise on a wrapping grid, seeded, so the pattern is identical in every regeneration.
        /// </remarks>
        private static Texture2D RoofShadowCookie()
        {
            const int Size = 256;
            const int Cells = 8;
            const string Name = "RoofShadowCookie";

            string path = EditorMenus.GeneratedMaterialFolder + "/" + Name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (existing != null)
            {
                return existing;
            }

            var random = new XorShiftRandomProvider(LayoutSeed + 3u);
            var lattice = new float[Cells, Cells];

            for (int y = 0; y < Cells; y++)
            {
                for (int x = 0; x < Cells; x++)
                {
                    lattice[x, y] = random.NextFloat01();
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = Name,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float u = (float)px / Size;
                    float v = (float)py / Size;

                    // Smoothly interpolated, wrapping lattice noise: the fallen and standing roof.
                    float gx = u * Cells, gy = v * Cells;
                    int x0 = Mathf.FloorToInt(gx) % Cells, y0 = Mathf.FloorToInt(gy) % Cells;
                    int x1 = (x0 + 1) % Cells, y1 = (y0 + 1) % Cells;
                    float fx = Mathf.SmoothStep(0f, 1f, gx - Mathf.Floor(gx));
                    float fy = Mathf.SmoothStep(0f, 1f, gy - Mathf.Floor(gy));
                    float noise = Mathf.Lerp(
                        Mathf.Lerp(lattice[x0, y0], lattice[x1, y0], fx),
                        Mathf.Lerp(lattice[x0, y1], lattice[x1, y1], fx), fy);

                    float roof = Mathf.SmoothStep(0.62f, 0.72f, noise);

                    // Four rafters, a whole number of them per tile so they meet across the seam.
                    float rafter = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4f));
                    float beam = Mathf.SmoothStep(0.06f, 0.14f, rafter);

                    float light = Mathf.Lerp(1f, 0.28f, roof) * Mathf.Lerp(0.35f, 1f, beam);
                    texture.SetPixel(px, py, new Color(light, light, light, 1f));
                }
            }

            texture.Apply(true);
            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(texture, path);

            return texture;
        }

        /// <summary>Motes drifting in the light. On unscaled time, so the air keeps moving through a hit-stop.</summary>
        private static void BuildDust(Transform root, float radius)
        {
            var dustObject = new GameObject("Dust");
            dustObject.transform.SetParent(root, false);
            dustObject.transform.position = new Vector3(0f, 4f, 0f);

            ParticleSystem system = dustObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.useUnscaledTime = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.04f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = DustColour;
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 18f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 1.6f, 8f, radius * 1.6f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.15f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f)
                });
            fade.color = gradient;

            var renderer = dustObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetOrCreateAdditive("DustMote", DotTexture(), Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static GameObject Block(
            Transform parent, string name, Vector3 size, float metresPerTile, Material material,
            Vector3 position, Quaternion rotation)
        {
            var block = new GameObject(name);
            block.transform.SetParent(parent, false);
            block.transform.SetPositionAndRotation(position, rotation);

            block.AddComponent<MeshFilter>().sharedMesh = TiledMeshes.Box(size, metresPerTile);
            block.AddComponent<MeshRenderer>().sharedMaterial = material;

            return block;
        }

        private static Vector3 OnRing(float angleDegrees, float distance)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians) * distance, 0f, Mathf.Sin(radians) * distance);
        }

        private static Material GetOrCreateAdditive(string materialName, Texture2D texture, Color colour)
        {
            string path = EditorMenus.GeneratedMaterialFolder + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

                if (shader == null)
                {
                    Debug.LogWarning("[Adaptive Boss Arena] The URP particle shader is missing; " + materialName + " will not render.");
                    return null;
                }

                material = new Material(shader) { name = materialName };
                AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>Soft across the shaft, brightest toward the sky and fading out before the floor.</summary>
        private static Texture2D ShaftGradient() => GetOrCreateTexture("LightShaftGradient", 32, 128, (u, v) =>
        {
            float across = Mathf.Sin(u * Mathf.PI);
            float along = Mathf.SmoothStep(0f, 1f, v / 0.35f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.15f);
            return across * across * along;
        });

        private static Texture2D DotTexture() => GetOrCreateTexture("DustMoteDot", 32, 32, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return Mathf.Clamp01(1f - d * d);
        });

        /// <summary>
        /// A white texture whose alpha comes from a function, saved as an asset.
        /// </summary>
        /// <remarks>
        /// Saved as a Unity texture asset rather than encoded to PNG, because image encoding lives in an
        /// engine module this project does not include.
        /// </remarks>
        private static Texture2D GetOrCreateTexture(string name, int width, int height, System.Func<float, float, float> alpha)
        {
            string path = EditorMenus.GeneratedMaterialFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = alpha((x + 0.5f) / width, (y + 0.5f) / height);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply(true);
            AssetAuthoring.EnsureFolderExists(EditorMenus.GeneratedMaterialFolder);
            AssetDatabase.CreateAsset(texture, path);

            return texture;
        }

        /// <summary>Three unit planes crossed at 60 degrees about the vertical, spanning y from -0.5 to 0.5.</summary>
        private static Mesh GetOrCreateCrossedPlanes()
        {
            string path = TiledMeshes.MeshFolder + "/CrossedPlanes.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                return existing;
            }

            var vertices = new Vector3[12];
            var uvs = new Vector2[12];
            var triangles = new int[18];

            for (int p = 0; p < 3; p++)
            {
                Vector3 across = Quaternion.Euler(0f, p * 60f, 0f) * new Vector3(0.5f, 0f, 0f);
                int v = p * 4;

                vertices[v] = -across + Vector3.down * 0.5f;
                vertices[v + 1] = across + Vector3.down * 0.5f;
                vertices[v + 2] = across + Vector3.up * 0.5f;
                vertices[v + 3] = -across + Vector3.up * 0.5f;

                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);

                int t = p * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "CrossedPlanes", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetAuthoring.EnsureFolderExists(TiledMeshes.MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);

            return mesh;
        }
    }
}
