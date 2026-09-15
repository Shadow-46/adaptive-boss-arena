using System.Collections;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Game;
using AdaptiveBossArena.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the arena camera stays steady through a real fight.
    /// </summary>
    /// <remarks>
    /// Knockdowns, launches, wall shoves and charges move the fighters around the whole arena and through
    /// each other's space, which the framing was never exercised against. A camera that whips a large angle
    /// in a single frame reads as a glitch however good each individual frame looks.
    /// </remarks>
    [TestFixture]
    public sealed class CameraStabilityTests
    {
        /// <summary>The most the camera's heading may turn in one frame at 60 Hz before it reads as a cut.</summary>
        private const float MaximumTurnPerFrameDegrees = 12f;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            float waited = 0f;

            do
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            while (Time.timeScale <= 0f && waited < 15f);
        }

        [UnityTest]
        public IEnumerator TheCameraNeverWhipsDuringAFight()
        {
            var rig = Object.FindAnyObjectByType<ArenaCameraRig>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var boss = Object.FindAnyObjectByType<BossController>();

            Assert.IsNotNull(rig);

            Vector3 previous = Flat(rig.transform.forward);
            float worst = 0f, worstSeparation = 0f;
            int whips = 0;
            float elapsed = 0f;

            while (elapsed < 20f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;

                // Normalised to 60 Hz, so a slow frame is not mistaken for a fast turn.
                float frames = Mathf.Max(1f, Time.unscaledDeltaTime * 60f);
                Vector3 heading = Flat(rig.transform.forward);
                float turn = Vector3.Angle(previous, heading) / frames;
                previous = heading;

                if (turn > MaximumTurnPerFrameDegrees)
                {
                    whips++;
                }

                if (turn > worst)
                {
                    worst = turn;
                    worstSeparation = Vector3.Distance(player.transform.position, boss.transform.position);
                }
            }

            Debug.Log($"[CAMERA] worst turn {worst:F1} deg/frame at fighter separation {worstSeparation:F2} m, frames over limit {whips}");

            Assert.LessOrEqual(worst, MaximumTurnPerFrameDegrees,
                $"The camera whipped {worst:F1} degrees in one frame, with the fighters {worstSeparation:F2} m apart ({whips} frames over).");
        }

        [UnityTest]
        public IEnumerator TheCameraCanSeeThePlayerPinnedAgainstTheWall()
        {
            // The situation knockback and wall shoves now create constantly: the player against the ring,
            // the boss in the middle, and the camera placed behind the player - which is outside the wall.
            var rig = Object.FindAnyObjectByType<ArenaCameraRig>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var boss = Object.FindAnyObjectByType<BossController>();

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var context = (PlayerContext)typeof(PlayerController).GetField("_context", flags).GetValue(player);

            int occluded = 0, frames = 0;
            float worstRadius = 0f;

            foreach (Vector3 side in new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward })
            {
                context.Motor.Teleport(side * 15.2f);
                boss.transform.position = new Vector3(0f, boss.transform.position.y, 0f);

                float settle = 0f;

                while (settle < 1.5f)
                {
                    yield return null;
                    settle += Time.unscaledDeltaTime;
                    context.Motor.Teleport(side * 15.2f);
                }

                Camera camera = rig.GetComponentInChildren<Camera>();
                Vector3 eye = camera.transform.position;
                Vector3 chest = player.transform.position + Vector3.up * 1.2f;

                frames++;
                worstRadius = Mathf.Max(worstRadius, new Vector2(eye.x, eye.z).magnitude);

                if (Physics.Linecast(eye, chest, 1 << Core.Constants.Layers.Arena))
                {
                    occluded++;
                }
            }

            Debug.Log($"[CAMERA] pinned against the wall: occluded from {occluded} of {frames} sides, camera out to radius {worstRadius:F1} m");

            Assert.AreEqual(0, occluded, $"The wall hid the player from the camera on {occluded} of {frames} sides (camera at radius {worstRadius:F1} m).");
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}
