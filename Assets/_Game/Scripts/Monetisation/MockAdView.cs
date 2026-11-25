using System;
using System.Collections;
using SlipperyZlope.RingToss.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// A full-screen placeholder advert, so the shape of the interruption can be
    /// judged before any SDK exists.
    ///
    /// The thing worth looking at here is not the artwork — it is deliberately
    /// nothing. It is the *shape of the interruption*: how long the game is gone
    /// for, where the close button is and when it appears, what the player is
    /// looking at while they wait, and what happens on the way back. All of that is
    /// fixed by the mediator later, but the game's own behaviour around it — the
    /// pause, the audio, the resume, the reward, the failure — is ours, is a real
    /// sequence with real timing, and cannot be judged against a stub that returns
    /// instantly.
    ///
    /// It is also, on purpose, visually alien: a hard-edged card in colours the game
    /// never uses, with a system-looking bar across the top. That is what a real ad
    /// is, and pretending otherwise while designing around it is how a game ends up
    /// with an interruption that feels worse than it needed to.
    ///
    /// Everything runs on unscaled time. A real ad plays while the game is paused,
    /// so a simulation that runs on scaled time never finishes.
    /// </summary>
    public sealed class MockAdView : MonoBehaviour
    {
        /// <summary>Reference height the canvas scales against, matching the game's own.</summary>
        private static readonly Vector2 Reference = new Vector2(1080f, 1920f);

        // Opaque. At 0.94 the game's own score and life icons still read through it,
        // which is not what an interstitial does and makes the interruption look
        // milder than it is — the thing this exists to let someone judge.
        private static readonly Color Scrim = new Color(0.02f, 0.02f, 0.04f, 1f);
        private static readonly Color Chrome = new Color(0.11f, 0.12f, 0.15f, 1f);
        private static readonly Color Creative = new Color(0.13f, 0.42f, 0.85f, 1f);
        private static readonly Color CreativeDeep = new Color(0.08f, 0.24f, 0.6f, 1f);
        private static readonly Color Accent = new Color(0.99f, 0.78f, 0.15f, 1f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.55f);

        private Canvas canvas;
        private CanvasGroup group;
        private TextMeshProUGUI counter;
        private TextMeshProUGUI headline;
        private TextMeshProUGUI footer;
        private TextMeshProUGUI brand;
        private Button close;
        private Image icon;
        private Image call;

        private bool closedEarly;

        /// <summary>
        /// Shows the placeholder and calls back exactly once.
        ///
        /// <paramref name="rewarded"/> changes the contract rather than the look: a
        /// rewarded ad cannot be closed until it has been watched, and closing early
        /// returns <see cref="AdResult.Skipped"/> with no reward. That distinction is
        /// the whole reason the two placements exist and it has to be felt.
        /// </summary>
        public IEnumerator Run(AdPlacement placement, float seconds, float skipAfter, Action<AdResult> onComplete)
        {
            bool rewarded = placement != AdPlacement.Interstitial;
            Build(placement, rewarded);

            closedEarly = false;
            float started = Time.unscaledTime;

            // Fade in rather than cut. A cut to a full-screen advert is jarring in a
            // way that has nothing to do with the advert, and every mediator fades.
            while (Time.unscaledTime - started < 0.18f)
            {
                group.alpha = (Time.unscaledTime - started) / 0.18f;
                yield return null;
            }

            group.alpha = 1f;

            while (!closedEarly)
            {
                float elapsed = Time.unscaledTime - started;
                float remaining = seconds - elapsed;

                if (remaining <= 0f)
                {
                    break;
                }

                counter.text = rewarded
                    ? $"Reward in {Mathf.CeilToInt(remaining)}"
                    : elapsed >= skipAfter ? "Skip  ✕" : $"{Mathf.CeilToInt(skipAfter - elapsed)}";

                // Interstitials become closable partway through; rewarded ones do not.
                close.interactable = rewarded ? false : elapsed >= skipAfter;

                // The icon turns, so the screen is never completely static. A frozen
                // placeholder reads as the game having crashed, which is the one
                // reaction that would make this test useless.
                icon.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 1.6f) * 9f);

                yield return null;
            }

            AdResult result = closedEarly
                ? AdResult.Skipped
                : rewarded ? AdResult.Watched : AdResult.Watched;

            started = Time.unscaledTime;

            while (Time.unscaledTime - started < 0.12f)
            {
                group.alpha = 1f - (Time.unscaledTime - started) / 0.12f;
                yield return null;
            }

            Destroy(canvas.gameObject);
            canvas = null;

            onComplete?.Invoke(result);
        }

        /// <summary>
        /// The "no fill" case: a real network fails often, and the game must not
        /// charge the player for it or hang waiting.
        /// </summary>
        public IEnumerator RunFailure(AdPlacement placement, Action<AdResult> onComplete)
        {
            Build(placement, false);

            // Strip the creative rather than leaving it up with an error over it. A
            // filled advert that says "no advert available" is not what a no-fill
            // looks like, and the first version of this did exactly that: star, brand
            // and an INSTALL button under the words.
            icon.gameObject.SetActive(false);
            brand.gameObject.SetActive(false);
            call.gameObject.SetActive(false);

            headline.text = "No advert available";
            footer.text = "The game continues. This is the network failing, not you.";
            counter.text = string.Empty;
            close.gameObject.SetActive(false);
            group.alpha = 1f;

            float started = Time.unscaledTime;

            while (Time.unscaledTime - started < 0.7f)
            {
                yield return null;
            }

            Destroy(canvas.gameObject);
            canvas = null;

            onComplete?.Invoke(AdResult.Failed);
        }

        private void Build(AdPlacement placement, bool rewarded)
        {
            GameObject host = new GameObject("Mock Ad", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            host.layer = LayerMask.NameToLayer("UI");

            canvas = host.AddComponent<Canvas>();

            // Camera space, and above everything. Overlay is drawn after the pipeline
            // finishes, so a render request never sees it and the placeholder could
            // not be reviewed in a headless capture at all — which is most of the
            // point of building it.
            Camera ui = FindUiCamera();

            if (ui != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = ui;
                canvas.planeDistance = 0.9f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            canvas.sortingOrder = 500;

            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            host.AddComponent<GraphicRaycaster>();

            group = host.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform root = (RectTransform)host.transform;
            Sprite card = UiSprites.RoundedRect(96, 26);

            Image scrim = UiFactory.Image("Scrim", root, null, Scrim);
            UiFactory.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;

            // The creative. A flat card in colours the game never uses.
            Image panel = UiFactory.Image("Creative", root, card, Creative);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(880f, 1180f));

            Image lower = UiFactory.Image("Creative Lower", panel.rectTransform, card, CreativeDeep);
            UiFactory.Band(lower.rectTransform, 0f, 0f, 0f, 420f, 0f);

            icon = UiFactory.Image("Icon", panel.rectTransform, UiSprites.Star(128), Accent);
            UiFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(280f, 280f));

            brand = UiFactory.Text(
                "Brand", panel.rectTransform, "SOME OTHER GAME", 34f, Faint);
            UiFactory.Band(brand.rectTransform, 0.62f, 1f, -190f, 46f);

            headline = UiFactory.Text(
                "Headline", panel.rectTransform,
                rewarded ? "Watch to earn your reward" : "Your advert here", 54f,
                Color.white, TMPro.TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Band(headline.rectTransform, 0.28f, 0.5f, 20f, 80f, 40f);

            call = UiFactory.Image("CTA", panel.rectTransform, card, Accent);
            UiFactory.Place(call.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(520f, 120f));

            TextMeshProUGUI ctaLabel = UiFactory.Text(
                "CTA Label", call.rectTransform, "INSTALL", 46f,
                new Color(0.12f, 0.1f, 0.06f), TMPro.TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(ctaLabel.rectTransform);

            // The chrome. Every mediator puts a bar like this across the top, and the
            // game has to be designed around it existing.
            Image bar = UiFactory.Image("Chrome", root, null, Chrome);
            UiFactory.Band(bar.rectTransform, 1f, 1f, 0f, 150f);

            TextMeshProUGUI label = UiFactory.Text(
                "Label", bar.rectTransform,
                $"Ad · placeholder · {Short(placement)}", 32f, Faint,
                TMPro.TextAlignmentOptions.Left);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(48f, 0f), new Vector2(540f, 60f));

            close = UiFactory.Button(
                "Close", bar.rectTransform, card, new Color(1f, 1f, 1f, 0.14f),
                string.Empty, 0f, Color.white);
            UiFactory.Place(close.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-40f, 0f), new Vector2(210f, 84f));
            close.onClick.AddListener(() => closedEarly = true);

            counter = UiFactory.Text("Counter", close.transform, string.Empty, 34f, Color.white);
            UiFactory.Stretch(counter.rectTransform);

            footer = UiFactory.Text(
                "Footer", root,
                rewarded
                    ? "Closing early forfeits the reward."
                    : "A real advert would be here. Nothing is being requested.",
                28f, new Color(1f, 1f, 1f, 0.4f));
            UiFactory.Band(footer.rectTransform, 0f, 0f, 60f, 48f);
        }

        /// <summary>
        /// A placement name that fits beside the close button.
        ///
        /// The enum's own names run into it, and a label that collides with the
        /// control the player is looking for is exactly the kind of thing this
        /// placeholder exists to expose — including in itself.
        /// </summary>
        private static string Short(AdPlacement placement)
        {
            return placement switch
            {
                AdPlacement.Interstitial => "interstitial",
                AdPlacement.RewardedContinue => "rewarded, continue",
                _ => "rewarded, tickets",
            };
        }

        /// <summary>
        /// The camera the game's own interface renders through, so the placeholder
        /// lands in the same render request a capture makes.
        /// </summary>
        private static Camera FindUiCamera()
        {
            foreach (Canvas existing in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (existing.renderMode == RenderMode.ScreenSpaceCamera && existing.worldCamera != null)
                {
                    return existing.worldCamera;
                }
            }

            return Camera.main;
        }
    }
}
