using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Tickets the player can watch arrive. The owner's rule: every award is seen landing
    /// in the number, and a share on the 2x tag is the biggest thing the title does.
    ///
    /// What is checked is what the rule means rather than what it looks like: the number
    /// climbs as tickets land instead of jumping, every ticket lands, the sound is told
    /// once, and nothing the celebration draws can take a tap.
    /// </summary>
    public sealed class TicketShowerTests
    {
        private TicketShower shower;
        private TextMeshProUGUI counter;
        private int announcements;
        private TicketSource announcedSource;
        private int announcedAmount;
        private readonly List<string> countedAtLanding = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }

            shower = Object.FindFirstObjectByType<TicketShower>();
            Assert.IsNotNull(shower, "The interface built no ticket shower.");

            GameObject label = new GameObject("Test Counter", typeof(RectTransform));
            label.transform.SetParent(shower.Overlay, false);
            counter = label.AddComponent<TextMeshProUGUI>();
            ((RectTransform)label.transform).sizeDelta = new Vector2(400f, 60f);

            announcements = 0;
            countedAtLanding.Clear();
            GameEvents.TicketShowerStarted += OnStarted;
            GameEvents.TicketLanded += OnLanded;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.TicketShowerStarted -= OnStarted;
            GameEvents.TicketLanded -= OnLanded;

            Scene main = SceneManager.GetSceneByName("Main");

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterTicketShowerTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        private void OnStarted(TicketSource source, int amount)
        {
            announcements++;
            announcedSource = source;
            announcedAmount = amount;
        }

        private void OnLanded(int index, int count)
        {
            counter.ForceMeshUpdate();
            countedAtLanding.Add(counter.text);
        }

        private IEnumerator UntilDone(float seconds = 20f)
        {
            float until = Time.realtimeSinceStartup + seconds;

            while (shower.IsCounting(counter) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.IsFalse(shower.IsCounting(counter), "The celebration never finished.");
        }

        [UnityTest]
        public IEnumerator A_boosted_share_rains_every_ticket_into_the_number_one_at_a_time()
        {
            shower.Play(
                TicketCelebration.Jackpot, TicketSource.BoostedShare, Vector2.zero,
                counter, 100, 140, "{0} tickets", "2× SHARE BONUS", "+40 TICKETS");

            yield return UntilDone();

            Assert.AreEqual(1, announcements, "The start of the celebration was announced more or less than once.");
            Assert.AreEqual(TicketSource.BoostedShare, announcedSource);
            Assert.AreEqual(40, announcedAmount);

            Assert.AreEqual(24, countedAtLanding.Count, "Not every ticket reached the number.");
            Assert.AreEqual("140 tickets", countedAtLanding[countedAtLanding.Count - 1]);

            // The number climbs with the tickets rather than jumping at the end.
            int previous = 100;
            int distinct = 0;

            foreach (string shown in countedAtLanding)
            {
                int value = int.Parse(shown.Split(' ')[0]);
                Assert.GreaterOrEqual(value, previous, "The count went backwards.");

                if (value > previous)
                {
                    distinct++;
                }

                previous = value;
            }

            Assert.Greater(distinct, 15, "The number jumped instead of counting up.");
        }

        [UnityTest]
        public IEnumerator Nothing_a_celebration_draws_can_take_a_tap()
        {
            shower.Play(
                TicketCelebration.Jackpot, TicketSource.BoostedShare, Vector2.zero,
                counter, 0, 40, "{0}", "2× SHARE BONUS", "+40 TICKETS");

            yield return null;
            yield return null;

            foreach (Graphic graphic in shower.Overlay.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == counter)
                {
                    continue;
                }

                Assert.IsFalse(graphic.raycastTarget,
                    $"{graphic.name} catches raycasts: a celebration would swallow the next tap.");
            }

            shower.Finish();
        }

        [UnityTest]
        public IEnumerator Buying_something_takes_the_tickets_out_of_the_balance_as_they_leave()
        {
            // The spend runs backwards: the number falls while the tickets are still in
            // the air, because that is the moment the player paid.
            shower.Play(
                TicketCelebration.Spend, TicketSource.Play, Vector2.zero,
                counter, 500, 350, "{0} TICKETS", toward: new Vector2(240f, -300f));

            yield return null;
            yield return null;
            yield return null;

            counter.ForceMeshUpdate();
            int early = int.Parse(counter.text.Split(' ')[0]);
            Assert.Less(early, 500, "The balance had not started falling while tickets were flying.");

            yield return UntilDone();

            counter.ForceMeshUpdate();
            Assert.AreEqual("350 TICKETS", counter.text);

            int previous = 500;

            foreach (string shown in countedAtLanding)
            {
                int value = int.Parse(shown.Split(' ')[0]);
                Assert.LessOrEqual(value, previous, "The balance went up during a purchase.");
                previous = value;
            }

            Assert.GreaterOrEqual(countedAtLanding.Count, 5);
        }

        [UnityTest]
        public IEnumerator A_burst_can_play_on_its_own_and_clears_itself_up()
        {
            // A purchase's burst has no award behind it, so it has to run — and stop —
            // without one.
            shower.Burst(new Vector2(0f, -200f), Color.magenta, 20);

            yield return null;
            yield return null;

            Assert.IsFalse(shower.IsCounting(counter), "A burst is not an award.");

            float until = Time.realtimeSinceStartup + 8f;
            int live = 0;

            while (Time.realtimeSinceStartup < until)
            {
                live = 0;

                foreach (Transform child in shower.Overlay)
                {
                    if (child.name.StartsWith("Confetti") && child.gameObject.activeSelf)
                    {
                        live++;
                    }
                }

                if (live == 0)
                {
                    break;
                }

                yield return null;
            }

            Assert.AreEqual(0, live, "The burst left confetti on the screen.");
        }

        [UnityTest]
        public IEnumerator A_second_award_finishes_the_first_instead_of_fighting_over_the_number()
        {
            shower.Play(TicketCelebration.Payout, TicketSource.Play, Vector2.zero, counter, 0, 20, "+{0}");

            yield return null;
            yield return null;

            shower.Play(TicketCelebration.Trickle, TicketSource.Play, Vector2.zero, counter, 20, 23, "+{0}");

            yield return UntilDone();

            counter.ForceMeshUpdate();
            Assert.AreEqual("+23", counter.text);
        }
    }
}
