using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>The character of an impact, which decides how it looks.</summary>
    /// <remarks>
    /// Kept deliberately small. Every flavour has to be distinguishable at a glance in the middle of
    /// an exchange, and past five or six variations they stop reading as different things and start
    /// reading as noise.
    /// </remarks>
    public enum ImpactFlavour
    {
        /// <summary>A light blow landing on a body. A short spray of blood, a few sparks off the mail.</summary>
        Light = 0,

        /// <summary>A heavy blow landing on a body. More blood thrown further, and a flash.</summary>
        Heavy = 1,

        /// <summary>A clean deflect. Bright steel sparks in a tight cone, and no blood.</summary>
        Deflect = 2,

        /// <summary>A late block. Dull and dispersed, so it reads as worse than a deflect.</summary>
        Block = 3,

        /// <summary>A broken guard. The largest burst in the game, because it is the biggest moment.</summary>
        PostureBreak = 4,

        /// <summary>A body or a blade meeting stone. Grit and dust, no blood.</summary>
        Stone = 5
    }

    /// <summary>
    /// One reusable impact: steel sparks, a spray of blood and a puff of dust, with a light flash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built entirely from code, like every other asset in the project. A particle system authored in
    /// the inspector would have to be committed as YAML, which is the one thing the generator
    /// approach exists to avoid.
    /// </para>
    /// <para>
    /// Three systems rather than one, because they need different blending. Sparks are light and add to
    /// the frame; blood and dust are matter and cover it. A single additive system drew every hit as a
    /// fan of glowing orange streaks, which is what made landing a blow on a body look like a toy.
    /// </para>
    /// <para>
    /// Emission is manual rather than rate- or burst-driven: the systems are left permanently playing
    /// with nothing to emit, and <see cref="Play"/> calls <c>Emit</c> directly. Restarting a particle
    /// system per hit is measurably more expensive and drops any particles still alive from the
    /// previous hit, which is exactly wrong during a combo.
    /// </para>
    /// <para>
    /// Runs on unscaled time so the burst still animates during the hit-stop it accompanies. A spark
    /// that freezes with the world would remove the very punctuation hit-stop is there to provide.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ImpactBurst : MonoBehaviour
    {
        /// <summary>How long the light flash lasts. Shorter than the particles, so it punctuates.</summary>
        private const float FlashSeconds = 0.08f;

        private ParticleSystem _sparks;
        private ParticleSystem _blood;
        private ParticleSystem _dust;
        private Light _flash;
        private IRandomProvider _random;
        private float _flashRemaining;
        private float _flashIntensity;

        /// <summary>True while nothing of this burst is still visible.</summary>
        public bool IsIdle =>
            _flashRemaining <= 0f && _sparks.particleCount == 0 && _blood.particleCount == 0 && _dust.particleCount == 0;

        /// <summary>Builds the particle systems and light. Called by the pool that owns this burst.</summary>
        /// <param name="sparkMaterial">Additive material the sparks are drawn with.</param>
        /// <param name="matterMaterial">Alpha-blended material blood and dust are drawn with; sparks' when null.</param>
        /// <param name="random">Scatter source, so no burst reaches for the global random generator.</param>
        public void Construct(Material sparkMaterial, Material matterMaterial, IRandomProvider random)
        {
            _random = random;
            matterMaterial = matterMaterial != null ? matterMaterial : sparkMaterial;

            // Thin, hot and quick, bouncing off the floor: sparks are steel shavings, not embers.
            _sparks = CreateSystem("Sparks", sparkMaterial, maxParticles: 48, gravity: 1.4f, lifetime: 0.35f);
            var sparkRenderer = _sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = 0.012f;
            sparkRenderer.lengthScale = 1f;

            ParticleSystem.CollisionModule collision = _sparks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = 0.35f;
            collision.dampen = 0.45f;
            collision.lifetimeLoss = 0.25f;

            // Heavy droplets that arc and fall fast; a little stretch along their flight reads as liquid.
            _blood = CreateSystem("Blood", matterMaterial, maxParticles: 64, gravity: 2.6f, lifetime: 0.55f);
            var bloodRenderer = _blood.GetComponent<ParticleSystemRenderer>();
            bloodRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            bloodRenderer.velocityScale = 0.02f;
            bloodRenderer.lengthScale = 1.2f;

            ParticleSystem.LimitVelocityOverLifetimeModule bloodDrag = _blood.limitVelocityOverLifetime;
            bloodDrag.enabled = true;
            bloodDrag.drag = 1.5f;

            // Slow, spreading and faint: dust hangs after the blow instead of flying with it.
            _dust = CreateSystem("Dust", matterMaterial, maxParticles: 24, gravity: -0.05f, lifetime: 1.4f);
            _dust.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;

            ParticleSystem.SizeOverLifetimeModule dustSize = _dust.sizeOverLifetime;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1f));

            ParticleSystem.LimitVelocityOverLifetimeModule dustDrag = _dust.limitVelocityOverLifetime;
            dustDrag.enabled = true;
            dustDrag.drag = 4f;

            ParticleSystem.RotationOverLifetimeModule dustSpin = _dust.rotationOverLifetime;
            dustSpin.enabled = true;
            dustSpin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            // No flash light in a browser: a per-pixel light on every hit is exactly the cost integrated graphics
            // cannot carry, and the bloom on the sparks still reads as a flash.
            if (!EffectBudget.IsWebPlayer)
            {
                _flash = gameObject.AddComponent<Light>();
                _flash.type = LightType.Point;
                _flash.range = 5f;
                _flash.shadows = LightShadows.None;
                _flash.enabled = false;
            }
        }

        /// <summary>Emits a burst at a point, thrown along a direction.</summary>
        /// <param name="position">Where the hit landed.</param>
        /// <param name="direction">Direction the burst is thrown along.</param>
        /// <param name="flavour">What kind of impact this was.</param>
        public void Play(Vector3 position, Vector3 direction, ImpactFlavour flavour)
        {
            transform.position = position;

            BurstProfile profile = ProfileFor(flavour);
            Vector3 axis = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.up;

            Emit(_sparks, position, axis, profile.Sparks, SparkColour, speedJitter: 0.5f);
            Emit(_blood, position, axis, profile.Blood, BloodColour, speedJitter: 0.45f);
            Emit(_dust, position, axis, profile.Dust, DustColour, speedJitter: 0.6f);

            if (_flash == null)
            {
                return;
            }

            _flashIntensity = profile.FlashIntensity;
            _flashRemaining = FlashSeconds;
            _flash.color = profile.FlashColour;
            _flash.enabled = _flashIntensity > 0f;
        }

        private void Update()
        {
            if (_flashRemaining <= 0f)
            {
                return;
            }

            // Unscaled, for the same reason the particles are: the flash exists to mark the frame the
            // hit landed, and that frame is usually frozen.
            _flashRemaining -= Time.unscaledDeltaTime;

            if (_flashRemaining <= 0f)
            {
                _flash.enabled = false;
                return;
            }

            _flash.intensity = _flashIntensity * (_flashRemaining / FlashSeconds);
        }

        private static readonly Color32 SparkColour = new Color32(255, 214, 160, 255);
        private static readonly Color32 BloodColour = new Color32(58, 4, 5, 255);
        private static readonly Color32 DustColour = new Color32(92, 84, 74, 70);

        private ParticleSystem CreateSystem(string systemName, Material material, int maxParticles, float gravity, float lifetime)
        {
            var child = new GameObject(systemName);
            child.transform.SetParent(transform, worldPositionStays: false);

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.gravityModifier = gravity;
            main.startLifetime = lifetime;

            // Emission and shape are both disabled: everything about a burst is decided at the moment
            // it is played, through the emit parameters rather than through configuration.
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // Particles shrink and fade as they die rather than vanishing, which is most of the
            // difference between sparks and confetti.
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();

            return system;
        }

        private void Emit(ParticleSystem system, Vector3 position, Vector3 axis, Spray spray, Color32 colour, float speedJitter)
        {
            var parameters = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };

            for (int i = 0; i < spray.Count; i++)
            {
                // Scattered around the impact direction rather than emitted in a sphere. A spray that
                // flies on along the blow is what makes a hit look like it came from somewhere.
                Vector3 scatter = new Vector3(
                    _random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f)) * spray.Spread;

                parameters.velocity = (axis + scatter).normalized * spray.Speed *
                                      _random.NextFloat(1f - speedJitter, 1f + speedJitter);
                parameters.startSize = spray.Size * _random.NextFloat(0.6f, 1.4f);
                parameters.startLifetime = system.main.startLifetime.constant * _random.NextFloat(0.6f, 1.3f);
                parameters.rotation = _random.NextFloat(0f, 360f);
                parameters.startColor = colour;

                system.Emit(parameters, 1);
            }
        }

        /// <summary>How much of one kind of matter a burst throws, and how.</summary>
        private readonly struct Spray
        {
            public Spray(int count, float speed, float size, float spread)
            {
                Count = count;
                Speed = speed;
                Size = size;
                Spread = spread;
            }

            public int Count { get; }
            public float Speed { get; }
            public float Size { get; }
            public float Spread { get; }
        }

        /// <summary>Everything that distinguishes one flavour of impact from another.</summary>
        private readonly struct BurstProfile
        {
            public BurstProfile(Spray sparks, Spray blood, Spray dust, float flashIntensity, Color flashColour)
            {
                Sparks = sparks;
                Blood = blood;
                Dust = dust;
                FlashIntensity = flashIntensity;
                FlashColour = flashColour;
            }

            public Spray Sparks { get; }
            public Spray Blood { get; }
            public Spray Dust { get; }
            public float FlashIntensity { get; }
            public Color FlashColour { get; }
        }

        private static readonly Spray None = new Spray(0, 0f, 0f, 0f);

        /// <summary>
        /// The look of each flavour.
        /// </summary>
        /// <remarks>
        /// Tuned so that the outcome is legible without reading a single number: blood means the blow landed
        /// on a body, sparks alone mean steel met steel, and a deflect throws more, brighter, tighter sparks
        /// than a block. The player learns which outcome they got from peripheral vision alone.
        /// </remarks>
        private static BurstProfile ProfileFor(ImpactFlavour flavour)
        {
            var warmFlash = new Color(1f, 0.72f, 0.45f);
            var steelFlash = new Color(0.85f, 0.9f, 1f);

            switch (flavour)
            {
                case ImpactFlavour.Heavy:
                    return new BurstProfile(
                        sparks: new Spray(6, 7f, 0.018f, 0.6f),
                        blood: new Spray(30, 4.5f, 0.09f, 0.55f),
                        dust: new Spray(3, 1.2f, 0.7f, 1f),
                        flashIntensity: 3f, flashColour: warmFlash);

                case ImpactFlavour.Deflect:
                    return new BurstProfile(
                        sparks: new Spray(36, 11f, 0.016f, 0.3f),
                        blood: None,
                        dust: None,
                        flashIntensity: 12f, flashColour: steelFlash);

                case ImpactFlavour.Block:
                    return new BurstProfile(
                        sparks: new Spray(10, 5f, 0.015f, 0.8f),
                        blood: None,
                        dust: new Spray(2, 0.8f, 0.5f, 1f),
                        flashIntensity: 2.5f, flashColour: steelFlash);

                case ImpactFlavour.PostureBreak:
                    return new BurstProfile(
                        sparks: new Spray(30, 12f, 0.02f, 0.9f),
                        blood: new Spray(24, 5f, 0.1f, 0.9f),
                        dust: new Spray(8, 2f, 1.1f, 1f),
                        flashIntensity: 14f, flashColour: warmFlash);

                case ImpactFlavour.Stone:
                    return new BurstProfile(
                        sparks: new Spray(8, 6f, 0.015f, 0.7f),
                        blood: None,
                        dust: new Spray(10, 2.2f, 1f, 1f),
                        flashIntensity: 0f, flashColour: warmFlash);

                default:
                    return new BurstProfile(
                        sparks: new Spray(3, 6f, 0.015f, 0.5f),
                        blood: new Spray(14, 3.5f, 0.07f, 0.45f),
                        dust: None,
                        flashIntensity: 1.2f, flashColour: warmFlash);
            }
        }

        /// <summary>Alpha ramp holding full opacity briefly before falling away.</summary>
        private static Gradient FadeOutGradient()
        {
            var gradient = new Gradient();

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }
    }
}
