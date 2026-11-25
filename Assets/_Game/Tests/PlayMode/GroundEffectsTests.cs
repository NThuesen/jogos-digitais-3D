using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Juice;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Flat effects have to be drawn on the board, not inside it.
    ///
    /// The catch shockwave — an expanding ring at the foot of the peg, and one of the
    /// better things in the feedback layer — was drawn 0.02 above a board root that sits
    /// at zero, while the deck's own surface is at 0.026 and its plank seams at 0.042. It
    /// was therefore underneath an opaque floor in every game that has ever been played
    /// and in every capture this project has taken. Nobody noticed for six phases,
    /// because a hidden flat mesh does not look broken: it looks like nothing happened.
    ///
    /// The heights now live in <see cref="BoardMetrics"/>, and this is the check that
    /// they are actually used. It fails if anybody picks a clearance by hand again.
    /// </summary>
    public sealed class GroundEffectsTests
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

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterGroundEffectsTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        [Test]
        public void The_shared_clearance_is_above_everything_the_board_draws()
        {
            Assert.Greater(BoardMetrics.GroundFxY, BoardMetrics.DeckSurfaceY,
                "A ground effect at or below the deck is inside it.");

            Assert.Greater(BoardMetrics.GroundFxY, BoardMetrics.DeckSeamY,
                "The deck's seams are proud of its surface, so clearing the surface is not enough.");
        }

        [UnityTest]
        public IEnumerator A_catch_shockwave_is_drawn_on_the_board_rather_than_under_it()
        {
            FxPool fx = Object.FindFirstObjectByType<FxPool>();
            Assert.IsNotNull(fx, "The scene built no effects pool.");

            // At the foot of a peg, which is where a catch plays one: the board root is
            // at zero, so this is the exact case that was buried.
            fx.PlayShockwave(Vector3.zero, Color.white, 1.2f, 0.5f);

            yield return null;

            Transform wave = null;

            foreach (Transform child in fx.transform)
            {
                if (child.name.StartsWith("Shockwave") && child.gameObject.activeSelf)
                {
                    wave = child;
                    break;
                }
            }

            Assert.IsNotNull(wave, "No shockwave was activated.");
            Assert.Greater(wave.position.y, BoardMetrics.DeckSeamY,
                $"The shockwave is at y={wave.position.y}, which is inside a board whose " +
                $"seams reach {BoardMetrics.DeckSeamY}. It would never be seen.");
        }
    }
}
