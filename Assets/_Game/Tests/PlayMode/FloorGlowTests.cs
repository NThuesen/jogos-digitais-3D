using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Juice;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The border's glow: brighter with each perfect in a row, capped, and back to square
    /// one on a miss.
    ///
    /// Built from a cube rather than the booth, so the only thing the raised events reach
    /// is the component under test — the real scene's listeners would take a payload with
    /// no peg and no ring in it as the crash it would be.
    /// </summary>
    public sealed class FloorGlowTests
    {
        private GameObject host;
        private GameObject rim;
        private GameObject skirt;
        private FloorGlow glow;
        private JuiceConfig juice;

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(SceneManager.GetSceneByName("Main").isLoaded,
                "The game scene is still loaded; its listeners would hear these events too.");

            juice = ScriptableObject.CreateInstance<JuiceConfig>();
            rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            skirt = GameObject.CreatePrimitive(PrimitiveType.Cube);

            host = new GameObject("Floor Glow Under Test");
            glow = host.AddComponent<FloorGlow>();
            glow.Bind(rim.GetComponent<Renderer>(), skirt.GetComponent<Renderer>(), juice);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(host);
            Object.Destroy(rim);
            Object.Destroy(skirt);
            Object.Destroy(juice);
        }

        private float SkirtLight()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            skirt.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor("_BaseColor").maxColorComponent;
        }

        private static void Catch(bool perfect)
        {
            GameEvents.RaiseRinger(new RingerCaught(null, null, perfect, 0, 0));
            GameEvents.RaiseResolved(new ThrowResolved(
                perfect ? ThrowOutcome.PerfectRinger : ThrowOutcome.Ringer, null, Vector3.zero, 0, 0));
        }

        private static void Miss(ThrowOutcome outcome = ThrowOutcome.Miss)
        {
            GameEvents.RaiseResolved(new ThrowResolved(outcome, null, Vector3.zero, 0, 0));
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;

            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        private float RimEmission()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rim.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor("_EmissionColor").maxColorComponent;
        }

        [UnityTest]
        public IEnumerator Perfects_in_a_row_brighten_the_border_and_a_miss_puts_it_back()
        {
            yield return Wait(0.6f);
            float resting = glow.Intensity;
            Assert.AreEqual(juice.FloorGlowFor(0), resting, 0.01f, "The border is not at its base glow.");

            float restingOnScreen = RimEmission();
            Assert.Greater(restingOnScreen, 0f, "The border carries no glow at all.");

            for (int i = 0; i < 3; i++)
            {
                Catch(perfect: true);
                yield return Wait(0.1f);
            }

            yield return Wait(1.2f);

            Assert.AreEqual(3, glow.Streak);
            Assert.AreEqual(juice.FloorGlowFor(3), glow.Intensity, 0.01f);
            Assert.Greater(RimEmission(), restingOnScreen * 1.5f, "Three perfects did not visibly lift the border.");

            Miss();
            Assert.AreEqual(0, glow.Streak);

            yield return Wait(3.5f);
            Assert.AreEqual(resting, glow.Intensity, 0.01f, "The border did not go back to square one after a miss.");
        }

        [UnityTest]
        public IEnumerator A_bounce_off_the_peg_is_a_miss_too()
        {
            Catch(perfect: true);
            Catch(perfect: true);
            Assert.AreEqual(2, glow.Streak);

            Miss(ThrowOutcome.Bounce);
            Assert.AreEqual(0, glow.Streak);
            yield return null;
        }

        [UnityTest]
        public IEnumerator An_ordinary_catch_keeps_the_streak_without_adding_to_it()
        {
            Catch(perfect: true);
            Catch(perfect: false);
            Catch(perfect: false);

            Assert.AreEqual(1, glow.Streak, "A scrappy catch is not an error, and it is not a perfect either.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_long_streak_stops_brightening_at_the_ceiling()
        {
            for (int i = 0; i < 20; i++)
            {
                Catch(perfect: true);
            }

            yield return Wait(1.6f);

            Assert.AreEqual(juice.FloorGlowFor(20), juice.FloorGlowFor(6), 1e-5f,
                "The ceiling is not where the config says the border stops.");
            Assert.AreEqual(juice.FloorGlowFor(6), glow.Intensity, 0.01f);
        }

        /// <summary>Waits for a condition, on the real clock, or gives up.</summary>
        private static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator The_border_spills_light_rather_than_only_going_paler()
        {
            yield return null;
            yield return null;

            // At rest there is no spill at all: the border keeps a faint glow because it
            // is the edge of the landing area, but the *light* is the streak's and only
            // the streak's. So these are absolute thresholds — a relative one against a
            // resting value of zero says nothing.
            Assert.AreEqual(0f, SkirtLight(), 1e-3f, "There is light on the floor before anything was earned.");

            for (int i = 0; i < 6; i++)
            {
                Catch(true);
            }

            // Waited for on the clock the glow actually runs on rather than by counting
            // frames. FloorGlow rises and falls on unscaled time; a play mode frame takes
            // whatever real time it takes, and captureDeltaTime does not pin the unscaled
            // clock — so a frame count here is an assertion about how busy the machine is.
            yield return Until(() => SkirtLight() > 0.2f, 3f);

            // The spill is additive: what it writes is the light it adds. A run of
            // perfects has to put real light on the floor beside the border, which is the
            // difference between a glow and a line that has been tinted lighter.
            Assert.Greater(SkirtLight(), 0.2f,
                "Six perfects did not put any light on the floor beside the border.");

            Miss();

            yield return Until(() => SkirtLight() < 0.02f, 5f);

            Assert.Less(SkirtLight(), 0.02f, "The light did not go out on a miss.");
        }

        /// <summary>
        /// Every round of an endless or time attack run lays out a new board, and a new
        /// board used to put the light out — so in the two modes with the most throws in
        /// them, a streak could never outgrow the two or three pegs of a single board.
        /// That is most of the owner's "demora muito".
        /// </summary>
        [UnityTest]
        public IEnumerator The_next_round_of_a_run_keeps_the_streak()
        {
            Catch(perfect: true);
            Catch(perfect: true);

            // What the director raises for each new round of a generated run.
            GameEvents.RaiseLevelStarted(-1);
            GameEvents.RaiseRoundStarted(2);

            Assert.AreEqual(2, glow.Streak, "Moving on to the next board cost the player their streak.");
            yield return null;
        }

        /// <summary>
        /// "quando o usuário perde ou muda de modo, o brilho deve zerar". Going back to the
        /// menu raised nothing the glow could hear, so the board behind the menu stayed lit
        /// by the run the player had just walked away from.
        /// </summary>
        [UnityTest]
        public IEnumerator Leaving_for_the_menu_puts_the_light_out_at_once()
        {
            yield return BuildUpTheLight();

            GameEvents.RaiseSessionClosed();
            yield return null;

            Assert.AreEqual(0, glow.Streak);
            Assert.Less(SkirtLight(), 0.02f, "The light from the run that was left behind is still on.");
        }

        [UnityTest]
        public IEnumerator Starting_a_mode_puts_the_light_out_at_once()
        {
            yield return BuildUpTheLight();

            GameEvents.RaiseSessionStarted(GameMode.TimeAttack);
            yield return null;

            Assert.AreEqual(0, glow.Streak);
            Assert.Less(SkirtLight(), 0.02f, "A new session started under the last one's light.");
        }

        private IEnumerator BuildUpTheLight()
        {
            for (int i = 0; i < 4; i++)
            {
                Catch(perfect: true);
            }

            yield return Until(() => SkirtLight() > 0.2f, 3f);

            Assert.Greater(SkirtLight(), 0.2f, "The streak never lit the floor to begin with.");
        }
    }

    /// <summary>
    /// The same glow in the real booth: on the board's border, and on nothing else. The
    /// owner's third and plainest version of this note was "somente a borda dele, sem
    /// passar para o resto do chão".
    /// </summary>
    public sealed class FloorGlowSceneTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene main = SceneManager.GetSceneByName("Main");

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterFloorGlowTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        [UnityTest]
        public IEnumerator Only_the_border_carries_the_glow()
        {
            yield return null;

            Assert.IsNotNull(Object.FindFirstObjectByType<FloorGlow>(), "The booth has no floor glow.");

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            int lit = 0;

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                renderer.GetPropertyBlock(block);
                bool glowing = block.GetColor("_EmissionColor").maxColorComponent > 0f;

                string path = renderer.transform.parent == null
                    ? renderer.name
                    : renderer.transform.parent.name + "/" + renderer.name;

                if (path == "Board/Glow Frame")
                {
                    Assert.IsTrue(glowing, "The board's border is not lit.");
                    lit++;
                    continue;
                }

                // Board/Trim is in this list on purpose. It used to be the thing that
                // lit, and it is a plate 0.17 m larger than the deck on every side, so
                // the only part of it the camera sees is the strip *behind* the board —
                // which is where the owner kept seeing a glow they had twice asked not
                // to have. It is scenery now and must stay dark.
                if (path == "Board/Trim" || path == "Board/Surface" || path == "Ground"
                    || path == "Booth Floor/Floor" || path.StartsWith("Deck/"))
                {
                    Assert.IsFalse(glowing, $"{path} is lit, and only the border should be.");
                }
            }

            Assert.AreEqual(1, lit, "Expected the board's border and nothing else to glow.");
        }

        /// <summary>
        /// The director's half of the reset. What the player does to change mode has to
        /// reach the glow, and going back to the menu used to raise nothing at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Going_back_to_the_menu_puts_the_light_out()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            FloorGlow glow = Object.FindFirstObjectByType<FloorGlow>();

            director.StartTimeAttack();
            yield return null;

            glow.ShowStreak(4);
            director.ReturnToIdle();

            Assert.AreEqual(0, glow.Streak, "The menu is still lit by the run the player left.");
        }

        [UnityTest]
        public IEnumerator Switching_straight_to_another_mode_puts_the_light_out()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            FloorGlow glow = Object.FindFirstObjectByType<FloorGlow>();

            director.StartTimeAttack();
            yield return null;

            glow.ShowStreak(4);
            director.StartEndless();

            Assert.AreEqual(0, glow.Streak, "A new mode started under the last one's light.");
            director.ReturnToIdle();
        }
    }
}
