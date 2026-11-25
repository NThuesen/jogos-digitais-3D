using System.Collections;
using System.IO;
using AgentTools;
using NUnit.Framework;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// A development tool rather than a test: walks every screen and dumps the canvas
    /// so `ui_audit` can measure it. Marked Explicit so it never runs in a suite.
    ///
    /// This exists because a screenshot cannot answer the question that matters. A
    /// label that fits today fits because today's string is short — the score has
    /// three digits, the level name is "FIRST TOSS", the payout is "+9 TICKETS" and
    /// the ticket count is under four figures. The dump carries the boxes and the
    /// glyph bounds, so the tool can say a label is 40 units wider than the box it
    /// was given, which is true whatever the string happens to be.
    ///
    /// Set AGENT_UI_DIR to collect the dumps. The screen size is taken from the
    /// player window, so run it at the handset resolution being checked:
    ///   RINGTOSS_UI_WIDTH / RINGTOSS_UI_HEIGHT are honoured by the same env vars
    ///   the capture tool uses, since both want the same thing.
    /// </summary>
    [Explicit("Audit tool, run on demand.")]
    public sealed class UiAuditTests
    {
        // A Samsung S24 FE, which is what this game is being checked against. Any
        // handset can be dropped in here; what must not happen is auditing whatever
        // window the editor has open, because a portrait layout that fits one aspect
        // ratio routinely does not fit another and nothing on screen says which one
        // you are looking at.
        private static readonly int Width = Size("RINGTOSS_UI_WIDTH", 1080);
        private static readonly int Height = Size("RINGTOSS_UI_HEIGHT", 2340);

        private static int Size(string variable, int fallback)
        {
            string value = System.Environment.GetEnvironmentVariable(variable);

            return int.TryParse(value, out int parsed) && parsed >= 64 && parsed <= 4096
                ? parsed
                : fallback;
        }

        private RenderTexture view;
        private Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            for (int i = 0; i < 12; i++)
            {
                yield return null;
            }

            // Pointing the camera at a render texture of the handset's size is what
            // makes the canvas lay itself out for that handset: a ScreenSpaceCamera
            // canvas takes its pixel rect from its camera, not from Screen.
            camera = Camera.main;
            Assert.IsNotNull(camera, "No main camera to size the canvas against.");

            view = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = view;

            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (view != null)
            {
                view.Release();
                Object.Destroy(view);
            }
        }

        [UnityTest]
        public IEnumerator Dump_every_screen()
        {
            Assert.IsTrue(AgentUi.Enabled, "Set AGENT_UI_DIR to collect the dumps.");

            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "No GameDirector in the scene.");

            yield return Settle();
            AgentUi.Capture("01_title");

            // The title once an advert has bought the 2x: the tag moves from WATCH to
            // SHARE, and MODO DEV is on — the three new labels, each where a real
            // player's screen would carry them.
            if (director.Progress != null)
            {
                director.Progress.GrantShareBoost();
                director.Progress.ModoDev = true;
            }

            yield return Press("Settings");
            yield return Settle();
            yield return Press("Home Settings");
            yield return Settle(80);
            AgentUi.Capture("01b_title_share_tagged");

            if (director.Progress != null)
            {
                director.Progress.ConsumeShareBoost();
                director.Progress.ModoDev = false;
            }

            yield return Press("Settings");
            yield return Settle();
            yield return Press("Home Settings");
            yield return Settle();

            // The screens reached from the title, each on the way back out, so the
            // navigation itself is exercised rather than the panels being poked.
            // Every screen leaves by the same control now: a BACK in the top left
            // that goes home. The audit walks out through it rather than through a
            // per-screen button, which is also the cheapest check that each one is
            // actually there and actually wired.
            yield return Press("Levels");
            yield return Settle();
            AgentUi.Capture("02_level_select");

            yield return Press("Home Levels");
            yield return Settle();

            // With a four-figure balance rather than an empty one. The shop dumped clean
            // for several rounds because the audit always walked in with no tickets: the
            // balance label is only as wide as the number in it, and it takes a real
            // balance to reach the button beside it. A capture found what this missed.
            if (director.Progress != null)
            {
                director.Progress.AddTickets(3590);
            }

            yield return Press("Shop");
            yield return Settle();
            AgentUi.Capture("03_shop");

            yield return Press("Trails Tab");
            yield return Settle();
            AgentUi.Capture("04_shop_trails");

            yield return Press("Home Shop");
            yield return Settle();

            yield return Press("Settings");
            yield return Settle();
            AgentUi.Capture("05_settings");

            // Settings in the state the MODO DEV switch exists in, which is the only
            // state worth measuring: it is built only where the build carries the flag.
            // It sits sixteen units under the two link buttons — the same minimum that
            // caught WATCH NOW against SHOP in phase 18 — and a gap that small is
            // exactly what this fixture is for rather than something to eyeball.
            if (director.Progress != null)
            {
                director.Progress.ModoDev = true;
            }

            yield return Press("Home Settings");
            yield return Settle();
            yield return Press("Settings");
            yield return Settle(60);
            AgentUi.Capture("05b_settings_modo_dev");

            if (director.Progress != null)
            {
                director.Progress.ModoDev = false;
            }

            // Every tab of the legal documents, because they are four different
            // bodies of text in one box and only one of them can be measured at a time.
            yield return Press("Legal");
            yield return Settle();
            AgentUi.Capture("06_legal_data");

            yield return Press("Legal Tab 1");
            yield return Settle();
            AgentUi.Capture("07_legal_privacy");

            yield return Press("Legal Tab 2");
            yield return Settle();
            AgentUi.Capture("08_legal_terms");

            yield return Press("Legal Tab 3");
            yield return Settle();
            AgentUi.Capture("09_legal_notices");

            yield return Press("Legal Back");
            yield return Settle();

            yield return Press("Privacy Choices");
            yield return Settle();
            AgentUi.Capture("10_consent");

            // Answering the dialog is itself the way out of it — both buttons return
            // to the title — so there is nothing left to press here.
            yield return Press("Decline Personalised");
            yield return Settle();

            // A level in play, with the numbers a mid-game HUD actually carries.
            director.StartLevel(0);
            yield return Settle(90);
            AgentUi.Capture("07_hud_levels");

            // The endless HUD is a different set of widgets in the same corners.
            director.StartEndless();
            yield return Settle(90);
            AgentUi.Capture("08_hud_endless");

            // And time attack is a third: the clock replaces the hearts, and it is a
            // dial rather than a row, so it occupies that corner differently from
            // either of the other two.
            director.StartTimeAttack();
            yield return Settle(90);
            AgentUi.Capture("09_hud_time_attack");

            // The results screen, with a payout on it — the line that used to be
            // sliced in half by the NEXT button, and the reason this fixture exists.
            director.StartLevel(0);
            yield return Settle(60);
            yield return ClearTheBoard(director);
            yield return Settle(150);
            AgentUi.Capture("10_result");

            string directory = System.Environment.GetEnvironmentVariable("AGENT_UI_DIR");
            Debug.Log($"[UiAuditTests] Canvas dumps written to {directory}");

            if (director.Progress != null)
            {
                director.Progress.ResetAll(director.LevelCount);
            }
        }

        /// <summary>
        /// Drops a ring straight onto every peg until the board is cleared, so the
        /// results screen arrives with a real score and a real payout rather than with
        /// zeroes, which is the state the layout has to survive.
        /// </summary>
        private static IEnumerator ClearTheBoard(GameDirector director)
        {
            for (int attempt = 0; attempt < 24 && director.State != SessionState.Finished; attempt++)
            {
                yield return WaitForReady(director);

                RingInstance ring = director.ActiveRing;

                if (ring == null || director.Board == null || director.Board.ActivePegs.Count == 0)
                {
                    yield break;
                }

                PegTarget peg = director.Board.ActivePegs[0];

                ring.Arm(peg.TopPosition + Vector3.up * 0.6f, Quaternion.identity);
                director.Throwing.LaunchArmedRing(new Vector3(0f, -0.6f, 0f), new Vector3(0f, 9f, 0f), 0.6f);

                yield return Settle(70);
            }
        }

        private static IEnumerator WaitForReady(GameDirector director)
        {
            float deadline = Time.time + 10f;

            while (director.State != SessionState.Ready && Time.time < deadline)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Waits, then forces a canvas rebuild.
        ///
        /// TMP lays a label out when it is next rendered, and this run renders
        /// nothing. Without the explicit rebuild every glyph bound in the dump is the
        /// previous string's, which produces an audit full of confident nonsense.
        /// </summary>
        private static IEnumerator Settle(int frames = 26)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }

            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator Press(string name)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);

            foreach (Button button in buttons)
            {
                if (button.name != name || !button.isActiveAndEnabled)
                {
                    continue;
                }

                button.onClick.Invoke();
                yield return null;
                yield break;
            }

            Debug.LogWarning($"[UiAuditTests] No active button named '{name}'.");
        }
    }
}
