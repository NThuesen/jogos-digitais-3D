using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Monetisation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// How far the advertising actually gets, on this machine, right now.
    ///
    /// It cannot answer the question anybody really wants — *does an advert appear on
    /// the phone* — because that needs the phone. What it can do is walk the chain
    /// that ends there and say which link is the last one that works: the game ID is
    /// accepted, the SDK starts, consent opens the gate, a placement fills. If all
    /// four hold here then the only thing left untested is the handset drawing it,
    /// and if one of them fails then the handset was never going to help.
    ///
    /// Deliberately <see cref="ExplicitAttribute"/> and never part of a suite. It
    /// talks to Unity's servers, so it needs a network, it takes real seconds, and a
    /// build machine with no route to the internet would fail it for a reason that
    /// has nothing to do with the game.
    ///
    /// It also writes a consent answer into PlayerPrefs, because that is the gate
    /// being tested. Teardown puts it back.
    /// </summary>
    [Explicit]
    public sealed class AdFillTests
    {
        private AdService service;
        private UnityAds mediation;
        private ConsentService consent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            for (int i = 0; i < 6; i++)
            {
                yield return null;
            }

            service = Object.FindFirstObjectByType<AdService>();
            mediation = Object.FindFirstObjectByType<UnityAds>();
            consent = Object.FindFirstObjectByType<ConsentService>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            consent?.Reset();

            Scene main = SceneManager.GetSceneByName("Main");

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterAdFill"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        [UnityTest]
        public IEnumerator Report_how_far_the_advertising_gets()
        {
            Assert.IsNotNull(service, "No AdService in the scene.");
            Assert.IsNotNull(mediation, "No UnityAds in the scene.");
            Assert.IsNotNull(consent, "No ConsentService in the scene.");

            Debug.Log($"[AdFill] backend={mediation.Name} adsAvailable={service.AdsAvailable} " +
                      $"rewardedOffered={service.RewardedOffered}");

            Assert.IsTrue(service.AdsAvailable,
                "AdConfig.adsEnabled is off, so nothing would ever be requested.");

            // The gate. Answering as an adult who allows personalisation is the only
            // path that reaches a personalised request, and it is the path a player
            // most often takes.
            consent.Record(personalisedAllowed: true, isOfAge: true);
            Debug.Log($"[AdFill] consent recorded -> personalisation={service.Personalisation}");

            Assert.AreNotEqual(AdPersonalisation.None, service.Personalisation,
                "Consent was recorded but the policy still refuses every request.");

            bool ready = false;
            bool answered = false;

            // The placeholder fallback makes IsReady always true, which is the right
            // behaviour for a player and useless for a test: this measures whether a
            // real advert arrived, so the stand-in is switched off for the duration.
            // D44 — a tool that produces evidence must fail loudly when it has none.
            bool fallback = mediation.PlaceholderWhenUnfilled;
            mediation.PlaceholderWhenUnfilled = false;

            service.InitialiseAfterConsent();

            // The SDK is asked once and then watched. Initialise is fire-and-forget
            // from the service's side, so this waits on the state rather than on a
            // callback it has no way to see.
            float deadline = Time.realtimeSinceStartup + 45f;

            while (!mediation.IsInitialised && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            answered = true;
            Debug.Log($"[AdFill] initialised={mediation.IsInitialised} " +
                      $"after {45f - (deadline - Time.realtimeSinceStartup):0.0}s");

            Assert.IsTrue(answered);
            Assert.IsTrue(mediation.IsInitialised,
                "The SDK never initialised. Check the game ID, and that this machine " +
                "can reach unityads.unity3d.com.");

            // Filling is a separate question from starting, and the one that decides
            // whether a player ever sees anything.
            deadline = Time.realtimeSinceStartup + 45f;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (mediation.IsFilled(AdPlacement.Interstitial) ||
                    mediation.IsFilled(AdPlacement.RewardedFreeTickets))
                {
                    ready = true;
                    break;
                }

                yield return null;
            }

            Debug.Log(
                $"[AdFill] interstitial filled={mediation.IsFilled(AdPlacement.Interstitial)} " +
                $"rewarded filled={mediation.IsFilled(AdPlacement.RewardedFreeTickets)}");

            Debug.Log($"[AdFill] the game would offer a rewarded advert now: " +
                      $"{service.CanOffer(AdPlacement.RewardedFreeTickets)}");

            mediation.PlaceholderWhenUnfilled = fallback;

            Assert.IsTrue(ready,
                "The SDK started but no placement filled within 45s. With test mode ON " +
                "this normally fills in a second or two on a real handset. With test " +
                "mode OFF and no live store listing, no fill is the EXPECTED answer — " +
                "the game shows the placeholder instead, and this test is only " +
                "meaningful again once the listing is live.");
        }
    }
}
