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
        /// <summary>A connecting light attack. Small, quick, warm.</summary>
        Light = 0,

        /// <summary>A connecting heavy attack. Fewer, larger, slower particles.</summary>
        Heavy = 1,

        /// <summary>A clean deflect. Bright metallic sparks in a tight cone.</summary>
        Deflect = 2,

        /// <summary>A late block. Dull and dispersed, so it reads as worse than a deflect.</summary>
        Block = 3,

        /// <summary>A broken guard. The largest burst in the game, because it is the biggest moment.</summary>
        PostureBreak = 4
    }

    /// <summary>
    /// One reusable particle burst with an accompanying light flash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built entirely from code, like every other asset in the project. A particle system authored in
    /// the inspector would have to be committed as YAML, which is the one thing the generator
    /// approach exists to avoid.
    /// </para>
    /// <para>
    /// Emission is manual rather than rate- or burst-driven: the system is left permanently playing
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

        private ParticleSystem _particles;
        private Light _flash;
        private float _flashRemaining;
        private float _flashIntensity;

        /// <summary>True while nothing of this burst is still visible.</summary>
        public bool IsIdle => _flashRemaining <= 0f && _particles.particleCount == 0;

        /// <summary>Builds the particle system and light. Called by the pool that owns this burst.</summary>
        /// <param name="material">Shared material for every burst's particles.</param>
        public void Construct(Material material)
        {
            _particles = gameObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.gravityModifier = 1.1f;
            main.startLifetime = 0.4f;
            main.startSpeed = 6f;
            main.startSize = 0.1f;

            // Emission and shape are both disabled: everything about a burst is decided at the moment
            // it is played, through the emit parameters rather than through configuration.
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            // Particles shrink and fade as they die rather than vanishing, which is most of the
            // difference between sparks and confetti.
            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.6f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _flash = gameObject.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.range = 6f;
            _flash.shadows = LightShadows.None;
            _flash.enabled = false;

            _particles.Play();
        }

        /// <summary>Emits a burst at a point, thrown along a direction.</summary>
        /// <param name="position">Where the hit landed.</param>
        /// <param name="direction">Direction the sparks are thrown along.</param>
        /// <param name="flavour">What kind of impact this was.</param>
        public void Play(Vector3 position, Vector3 direction, ImpactFlavour flavour)
        {
            transform.position = position;

            BurstProfile profile = ProfileFor(flavour);
            Vector3 axis = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.up;

            var parameters = new ParticleSystem.EmitParams
            {
                startColor = profile.Color,
                startSize = profile.Size,
                position = position,
                applyShapeToPosition = false
            };

            for (int i = 0; i < profile.Count; i++)
            {
                // Scattered around the impact direction rather than emitted in a sphere. Sparks that
                // fly back along the blade are what makes a hit look like it came from somewhere.
                Vector3 scatter = Random.insideUnitSphere * profile.Spread;
                parameters.velocity = (axis + scatter).normalized *
                                      profile.Speed * Random.Range(0.6f, 1.4f);

                _particles.Emit(parameters, 1);
            }

            _flashIntensity = profile.FlashIntensity;
            _flashRemaining = FlashSeconds;
            _flash.color = profile.Color;
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

        /// <summary>Everything that distinguishes one flavour of impact from another.</summary>
        private readonly struct BurstProfile
        {
            public BurstProfile(
                int count, float speed, float size, float spread, float flashIntensity, Color color)
            {
                Count = count;
                Speed = speed;
                Size = size;
                Spread = spread;
                FlashIntensity = flashIntensity;
                Color = color;
            }

            public int Count { get; }
            public float Speed { get; }
            public float Size { get; }
            public float Spread { get; }
            public float FlashIntensity { get; }
            public Color Color { get; }
        }

        /// <summary>
        /// The look of each flavour.
        /// </summary>
        /// <remarks>
        /// Tuned so that the reward hierarchy is legible without reading a single number: a deflect
        /// throws more, brighter, tighter sparks than a block, and a posture break dwarfs both. The
        /// player learns which outcome they got from peripheral vision alone.
        /// </remarks>
        private static BurstProfile ProfileFor(ImpactFlavour flavour)
        {
            switch (flavour)
            {
                case ImpactFlavour.Heavy:
                    return new BurstProfile(
                        count: 22, speed: 7f, size: 0.16f, spread: 0.55f, flashIntensity: 7f,
                        color: new Color(1f, 0.62f, 0.28f));

                case ImpactFlavour.Deflect:
                    return new BurstProfile(
                        count: 34, speed: 11f, size: 0.09f, spread: 0.3f, flashIntensity: 12f,
                        color: new Color(0.75f, 0.92f, 1f));

                case ImpactFlavour.Block:
                    return new BurstProfile(
                        count: 10, speed: 4f, size: 0.1f, spread: 0.8f, flashIntensity: 2.5f,
                        color: new Color(0.6f, 0.63f, 0.7f));

                case ImpactFlavour.PostureBreak:
                    return new BurstProfile(
                        count: 48, speed: 13f, size: 0.2f, spread: 0.9f, flashIntensity: 16f,
                        color: new Color(1f, 0.85f, 0.4f));

                default:
                    return new BurstProfile(
                        count: 14, speed: 6f, size: 0.11f, spread: 0.45f, flashIntensity: 4f,
                        color: new Color(1f, 0.78f, 0.5f));
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
