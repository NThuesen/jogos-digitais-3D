using System.Collections;
using System.IO;
using NUnit.Framework;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using SlipperyZlope.RingToss.Juice;
using SlipperyZlope.RingToss.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// A development tool rather than a test: renders the running game to disk so
    /// framing, colour and readability can be reviewed without opening the editor.
    /// Marked Explicit so it never runs as part of a normal suite.
    ///
    /// Constraints found the hard way and worth keeping written down:
    /// - WaitForEndOfFrame never completes in batch mode and hangs the run forever.
    ///   Always put a timeout around a batch-mode Unity.
    /// - Render through RenderPipeline.SubmitRenderRequest, not Camera.Render, which
    ///   bypasses the scriptable pipeline and produces an image lit by ambient only.
    /// - The pipeline needs a few frames to warm up; the first ones come out without
    ///   material albedo. Discard them.
    /// - Unity must not be run with -nographics here, or there is no device to render
    ///   with and SupportsRenderRequest returns false.
    /// </summary>
    [Explicit("Capture tool, run on demand.")]
    public sealed class SceneCaptureTests
    {
        // 9:20, matching CameraFraming.NarrowestAspect.
        //
        // These used to be 540x960 — 9:16 — and that single wrong number cost two
        // rounds of scenery work. The camera's field of view is vertical, so a taller
        // screen shows *less* width: at 9:16 the captures showed 25% more of the board
        // than a real phone does, and everything placed against them — the booth's own
        // frame posts included — was off the edge of a real screen. A capture tool has
        // to frame what the player frames or it is not verification, it is decoration.
        // Overridable, so a specific handset can be checked rather than only the
        // narrowest supported one. A Samsung S24 FE is 1080x2340, which is 9:19.5 —
        // *wider* than the 9:20 these default to, so anything framed here fits there
        // with a little room at the sides. That is the correct direction: design for
        // the narrowest, and a wider screen shows a little more scenery.
        private static readonly int Width =
            ReadSize("RINGTOSS_CAPTURE_WIDTH", 540);

        private static readonly int Height =
            ReadSize("RINGTOSS_CAPTURE_HEIGHT", 1200);

        private static int ReadSize(string variable, int fallback)
        {
            string value = System.Environment.GetEnvironmentVariable(variable);

            return int.TryParse(value, out int parsed) && parsed >= 64 && parsed <= 4096
                ? parsed
                : fallback;
        }

        private string outputDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            outputDirectory = System.Environment.GetEnvironmentVariable("RINGTOSS_CAPTURE_DIR")
                              ?? Application.temporaryCachePath;

            Directory.CreateDirectory(outputDirectory);

            SceneManager.LoadScene("Main");

            for (int i = 0; i < 12; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Capture_the_whole_loop()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "No GameDirector in the scene.");

            yield return Capture("01_title");

            director.StartLevel(0);
            yield return WaitFrames(70);
            yield return Capture("02_level_one_ready");

            // A mid-flight frame: a real ballistic arc, so the trail and the ground
            // shadow are doing what they would in play.
            yield return LaunchAtPeg(director);
            yield return WaitFrames(11);
            yield return Capture("03_in_flight");

            // Wait for that throw to finish, then guarantee the catch rather than
            // hoping the scripted arc lands. The point of this frame is the feedback
            // package, not the aim.
            yield return WaitForReady(director);
            yield return DropOnPeg(director);
            yield return WaitFrames(9);
            yield return Capture("04_catch");

            yield return WaitFrames(110);
            yield return Capture("05_result");

            // A busier board, a different palette, and pegs that move.
            director.StartLevel(11);
            yield return WaitFrames(80);
            yield return Capture("06_final_level");

            director.StartLevel(5);
            yield return WaitFrames(80);
            yield return Capture("07_midnight_stage");

            // Aiming. The preview is driven directly rather than through synthesised
            // touch: this is a capture tool, and what needs looking at is the arc and
            // the landing ring, not the input layer that would have produced them.
            yield return WaitForReady(director);
            yield return ShowAim(director, 0.62f, 0.45f);
            yield return Capture("08_aiming");

            // A soft throw, for the same aiming question at the other end of the power
            // range: the arc is cut by *time of flight*, so a gentle lob has to show
            // proportionally as much of itself as a hard one.
            yield return WaitForReady(director);
            yield return ShowAim(director, 0.33f, -0.2f);
            yield return Capture("08c_aiming_soft");

            // A windy level, mid-aim. Two things only exist here: the drifting air —
            // which had to be drawn because a crosswind nobody can see is a trap, not
            // a difficulty — and the trajectory arc, which now stops halfway and must
            // be checked for still being enough to aim with.
            director.StartLevel(43);
            yield return WaitForReady(director);
            yield return ShowAim(director, 0.7f, 0.3f);
            yield return Capture("08b_wind_and_half_arc");

            director.StartEndless();
            yield return WaitFrames(90);
            yield return Capture("09_endless_round_one");

            // Time attack, for the alarm clock in the corner. It is the one HUD widget
            // that is a drawn object rather than a row of icons, so it is the one that
            // has to be looked at rather than measured.
            director.StartTimeAttack();
            yield return WaitFrames(90);
            yield return Capture("09b_time_attack_clock");

            // A late board, staged rather than played to. Reaching round eighteen
            // honestly would be a few hundred scripted throws, and the frame is here
            // to show what a crowded board looks like, not to prove the run works —
            // EndlessLoopTests does that.
            yield return StageEndlessBoard(director, 18);
            yield return WaitFrames(60);
            yield return Capture("10_endless_late_board");

            // A late authored level, for the plinths and the fifth stage's palette.
            director.StartLevel(44);
            yield return WaitFrames(80);
            yield return Capture("11_stage_five");

            director.StartLevel(15);
            yield return WaitFrames(80);
            yield return Capture("12_raised_pegs");

            // The shop, with enough tickets to show both the affordable and the
            // out-of-reach states side by side.
            if (director.Progress != null)
            {
                director.Progress.AddTickets(420);
            }

            director.ReturnToIdle();
            yield return WaitFrames(30);
            yield return ShowShop();
            yield return WaitFrames(40);
            yield return Capture("13_shop_rings");

            yield return ShowShopTrails();
            yield return WaitFrames(40);
            yield return Capture("14_shop_trails");

            // A patterned skin and a braided trail, in flight, which is the only way
            // to see either of them doing their job.
            yield return WearAndThrow(director, skin: 1, trail: 2, level: 3, name: "15_candy_in_flight");
            yield return WearAndThrow(director, skin: 8, trail: 8, level: 25, name: "16_nebula_in_flight");
            yield return WearAndThrow(director, skin: 9, trail: 9, level: 44, name: "17_prism_in_flight");

            if (director.Progress != null)
            {
                director.Progress.ResetAll(director.LevelCount);
            }

            Debug.Log($"[SceneCaptureTests] Captures written to {outputDirectory}");
        }

        /// <summary>
        /// This round's changes, each in the state it exists to be seen in: the title's
        /// buttons as plaques, the lit border on the deck the ring can land in, the arc's
        /// new shadow track on the floor, a catch's shockwave now that it is drawn above
        /// the boards rather than inside them, and a trail at the size that made it worth
        /// buying again.
        /// </summary>
        [UnityTest]
        public IEnumerator Capture_this_round()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "No GameDirector in the scene.");

            Save.ProgressStore progress = director.Progress;
            Assert.IsNotNull(progress, "No ProgressStore.");
            progress.ResetAll(director.LevelCount);

            // The title, whose buttons are the note: they were flat rectangles over a
            // booth made of canvas and brass.
            yield return WaitRealtime(1.2f);
            yield return Capture("r21_01_title_buttons");

            // The border. Only the deck's own outline lights, and it climbs with perfects
            // in a row — the strip behind the board, which is what used to light, must be
            // the same in all three.
            director.StartLevel(0);
            yield return WaitForReady(director);

            FloorGlow glow = Object.FindFirstObjectByType<FloorGlow>();
            Assert.IsNotNull(glow, "The booth has no floor glow.");

            yield return WaitRealtime(1f);
            yield return Capture("r21_02_border_streak_0");

            glow.ShowStreak(3);
            yield return WaitRealtime(1.2f);
            yield return Capture("r21_03_border_streak_3");

            glow.ShowStreak(6);
            yield return WaitRealtime(1.2f);
            yield return Capture("r21_04_border_streak_6");
            glow.ShowStreak(0);

            // The same three streaks in endless, which is what PLAY starts and what the
            // owner was looking at when they said the border did not match these frames.
            // Endless generates its rounds and cycles the stage themes, and the glow
            // takes its colour from the theme — so if the two modes disagree, this is
            // where it shows.
            director.StartEndless();
            yield return WaitFrames(90);

            glow = Object.FindFirstObjectByType<FloorGlow>();
            yield return WaitRealtime(0.6f);
            yield return Capture("r21_05a_endless_streak_0");

            glow.ShowStreak(3);
            yield return WaitRealtime(1.2f);
            yield return Capture("r21_05b_endless_streak_3");

            glow.ShowStreak(6);
            yield return WaitRealtime(1.2f);
            yield return Capture("r21_05c_endless_streak_6");
            glow.ShowStreak(0);

            yield return WaitRealtime(1.6f);
            yield return Capture("r21_05d_endless_after_reset");

            director.StartLevel(0);
            yield return WaitForReady(director);
            yield return ShowAim(director, 0.62f, 0.12f);
            yield return Capture("r21_05_aim_no_track");

            // A catch, a few frames in, so the shockwave is mid-expansion. It has been
            // drawn under the deck since it was written; this is the first picture of it.
            yield return WaitForReady(director);
            yield return DropOnPeg(director);
            yield return WaitFrames(6);
            yield return Capture("r21_06_catch_shockwave");
            yield return WaitFrames(10);
            yield return Capture("r21_07_catch_shockwave_wide");

            // And a trail, at the width that put it back on the shop's side.
            yield return WearAndThrow(director, 6, 6, 2, "r21_08_trail_neon");
            yield return WearAndThrow(director, 7, 7, 2, "r21_09_trail_aurora");

            progress.ResetAll(director.LevelCount);
            Debug.Log($"[SceneCaptureTests] Captures written to {outputDirectory}");
        }

        private static TMPro.TextMeshProUGUI FindTitleTickets()
        {
            foreach (TMPro.TextMeshProUGUI label in
                     Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (label.name == "Tickets" && label.text.EndsWith(" tickets"))
                {
                    return label;
                }
            }

            return null;
        }

        private static UnityEngine.UI.Button FindButton(string name)
        {
            foreach (UnityEngine.UI.Button button in
                     Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            return null;
        }

        /// <summary>Waits real seconds. The celebrations run on unscaled time, not on frames.</summary>
        private static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Equips a skin and a trail, starts a level and catches the ring mid-flight.
        ///
        /// The items are bought rather than forced, so the capture walks the same path
        /// a player's purchase does and would break if that path did.
        /// </summary>
        private IEnumerator WearAndThrow(
            GameDirector director, int skin, int trail, int level, string name)
        {
            Save.ProgressStore progress = director.Progress;

            if (progress == null)
            {
                yield break;
            }

            progress.AddTickets(60000);
            progress.BuySkin(skin, 0);
            progress.BuyTrail(trail, 0);
            progress.EquipSkin(skin);
            progress.EquipTrail(trail);
            GameEvents.RaiseCosmeticsChanged(skin, trail);

            director.StartLevel(level);
            yield return WaitFrames(70);

            yield return LaunchAtPeg(director);
            yield return WaitFrames(13);
            yield return Capture(name);
        }

        /// <summary>
        /// Opens the shop by pressing its button, so the capture goes through the same
        /// path a player would rather than through a back door the game does not have.
        /// </summary>
        private static IEnumerator ShowShop()
        {
            yield return PressButton("Shop");
        }

        private static IEnumerator ShowShopTrails()
        {
            yield return PressButton("Trails Tab");
        }

        private static IEnumerator PressButton(string name)
        {
            UnityEngine.UI.Button[] buttons =
                Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name != name)
                {
                    continue;
                }

                buttons[i].onClick.Invoke();
                yield return null;
                yield break;
            }

            Debug.LogWarning($"[SceneCaptureTests] No button named '{name}'.");
            yield return null;
        }

        /// <summary>Draws the aim preview for a given drag, without an input device.</summary>
        private static IEnumerator ShowAim(GameDirector director, float forward, float lateral)
        {
            TrajectoryPreview preview = Object.FindFirstObjectByType<TrajectoryPreview>();
            Config.PhysicsConfig physics = LoadPhysicsConfig();

            if (preview == null || physics == null || director.Throwing == null)
            {
                yield break;
            }

            // Pre-inverted where the config asks for a slingshot, because the solver
            // un-inverts it — this has to hand over the *gesture*, not the intention.
            //
            // It did not, and the throw became a backwards drag the moment the game
            // switched to pulling back: the solver rejected it, this returned early,
            // and every aiming capture since has quietly been a picture of no preview
            // at all. Nothing failed; the frames just stopped containing the thing
            // they were taken to show.
            Vector2 drag = new Vector2(
                Screen.height * physics.LateralDragScreenFraction * lateral,
                Screen.height * physics.MaxDragScreenFraction * forward);

            if (physics.InvertDragDirection)
            {
                drag = -drag;
            }

            ThrowSolution solution = ThrowSolver.Solve(
                drag, Screen.height, Quaternion.identity,
                director.Throwing.Origin.position.y, physics);

            if (!solution.IsValid)
            {
                yield break;
            }

            Vector3 origin = director.Throwing.Origin.position;
            Vector3 flat = new Vector3(solution.Velocity.x, 0f, solution.Velocity.z).normalized;
            Vector3 landing = new Vector3(origin.x, 0f, origin.z) + flat * solution.Range;

            GameEvents.RaiseAimChanged(new AimState(solution.NormalisedForce, lateral, solution.Range));

            // From the ring as drawn, the way ThrowController draws it in play. This used
            // to hand over the throw origin, so captures kept showing an arc the game no
            // longer draws.
            for (int i = 0; i < 24; i++)
            {
                RingInstance armed = director.Throwing.ArmedRing;
                Vector3 hub = armed != null ? armed.VisualCentre : origin;

                preview.Show(hub, solution.Velocity, physics.Gravity, solution.NormalisedForce, landing);
                yield return null;
            }
        }

        /// <summary>
        /// The border's glow as a run actually builds it: real perfects, carried from one
        /// round's board to the next, then what losing and leaving do to it. The owner's
        /// note was that it took far too long to build, never looked like the approved
        /// frames in play, and did not go out on a loss or a change of mode.
        /// </summary>
        [UnityTest]
        public IEnumerator Capture_the_glow_ramp()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            FloorGlow glow = Object.FindFirstObjectByType<FloorGlow>();
            Assert.IsNotNull(director, "No GameDirector in the scene.");
            Assert.IsNotNull(glow, "The booth has no floor glow.");
            director.Progress.ResetAll(director.LevelCount);

            director.StartEndless();
            yield return WaitForReady(director);
            yield return WaitRealtime(0.8f);
            yield return Capture("glow_ramp_0_start");

            for (int attempt = 0; attempt < 10 && glow.Streak < 4; attempt++)
            {
                yield return WaitForReady(director);
                yield return DropOnAvailablePeg(director);
                yield return WaitWhileFlying(director);

                // Long enough for the 0.12 s rise to settle and the flare to fade, so the
                // frame shows the step itself — what the approved prints showed.
                yield return WaitRealtime(0.6f);
                yield return Capture($"glow_ramp_{glow.Streak}_round{director.CurrentRound}");
            }

            Assert.AreEqual(4, glow.Streak, "Four drops straight down did not make four perfects in a row.");

            // The player's way out — the HUD's BACK — not the director's, so the frame shows
            // the title the player would be looking at.
            yield return PressButton("Home Hud");
            yield return WaitRealtime(0.8f);
            yield return Capture("glow_ramp_5_back_at_the_menu");

            // Time attack, lost on the clock: the light has to be out on the results screen.
            director.StartTimeAttack();
            yield return WaitForReady(director);
            yield return DropOnAvailablePeg(director);
            yield return WaitWhileFlying(director);
            yield return WaitForReady(director);
            yield return DropOnAvailablePeg(director);
            yield return WaitWhileFlying(director);
            yield return WaitRealtime(0.6f);
            yield return Capture($"glow_ramp_6_time_attack_streak{glow.Streak}");

            while (!director.TimeAttack.IsOver)
            {
                director.TimeAttack.Drain(1f);
            }

            float deadline = Time.realtimeSinceStartup + 6f;
            while (director.State != SessionState.Finished && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            yield return WaitRealtime(1.2f);
            yield return Capture("glow_ramp_7_after_losing_on_time");
            director.ReturnToIdle();
        }

        /// <summary>
        /// Whether these frames include post-processing at all — the question phase 21
        /// left open after proving that SubmitRenderRequest skips it.
        ///
        /// The same lit board, captured with the shipped Bloom and again with Bloom's
        /// intensity at an absurd eight. If post-processing is in the capture the two must
        /// differ; byte-identical frames mean every glow screenshot is pre-bloom.
        /// </summary>
        [UnityTest]
        public IEnumerator Capture_proves_post_processing_is_in_the_frame()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            FloorGlow glow = Object.FindFirstObjectByType<FloorGlow>();
            director.StartEndless();
            yield return WaitForReady(director);
            glow.ShowStreak(4);
            yield return WaitRealtime(1.2f);

            yield return Capture("post_check_bloom_shipped");

            // The control: the same board three frames later with nothing changed, which
            // is how much the scene's own animation — bulbs, the idle ring — moves pixels.
            yield return WaitFrames(3);
            yield return Capture("post_check_control");

            // The bloom the game ships comes from URP's default volume profile — the
            // scene's own profile has no effects in it at all — so rather than edit an
            // asset, a temporary global volume outranks it for one frame.
            GameObject loud = new GameObject("Bloom x8 (capture check)");
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(8f);
            bloom.threshold.Override(0.82f);
            Volume volume = loud.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = profile;

            yield return WaitFrames(3);
            yield return Capture("post_check_bloom_x8");

            Object.Destroy(loud);
            Object.Destroy(profile);

            long noise = Difference("post_check_bloom_shipped", "post_check_control");
            long bloomed = Difference("post_check_control", "post_check_bloom_x8");

            Debug.Log($"[SceneCaptureTests] Frame-to-frame noise {noise}, shipped bloom vs eight {bloomed}.");
            Assert.Greater(bloomed, noise * 4L,
                $"Bloom at eight moved the frame by {bloomed}, against {noise} from the scene's own " +
                "animation: these captures do not include post-processing.");
        }

        private long Difference(string first, string second)
        {
            byte[] a = File.ReadAllBytes(Path.Combine(outputDirectory, first + ".raw"));
            byte[] b = File.ReadAllBytes(Path.Combine(outputDirectory, second + ".raw"));
            long total = 0;

            // The first eight bytes are the width and height the dump starts with.
            for (int i = 8; i < a.Length && i < b.Length; i++)
            {
                total += Mathf.Abs(a[i] - b[i]);
            }

            return total;
        }

        private static IEnumerator DropOnAvailablePeg(GameDirector director)
        {
            RingInstance ring = director.ActiveRing;
            PegTarget peg = null;

            foreach (PegTarget candidate in director.Board.ActivePegs)
            {
                if (candidate.IsAvailable)
                {
                    peg = candidate;
                    break;
                }
            }

            if (ring == null || peg == null)
            {
                yield break;
            }

            ring.Arm(peg.TopPosition + Vector3.up * 0.55f, Quaternion.identity);
            director.Throwing.LaunchArmedRing(new Vector3(0f, -0.5f, 0f), Vector3.zero, 0.5f);
            yield return null;
        }

        private static IEnumerator WaitWhileFlying(GameDirector director)
        {
            float deadline = Time.realtimeSinceStartup + 8f;

            while (director.State == SessionState.Flying && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        /// <summary>
        /// The floor band behind the pegs, taken apart.
        ///
        /// A playtest recording showed the strip between the back of the board and the
        /// booth turning bright gold and then light cyan as an endless run moved through
        /// the themes, and nothing in the code said which surface that was. These frames
        /// settle it: two rounds as they play, and the later one again with each
        /// candidate switched off in turn.
        /// </summary>
        [UnityTest]
        public IEnumerator Capture_the_floor_band()
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            World.WorldBuilder world = Object.FindFirstObjectByType<World.WorldBuilder>();
            Assert.IsNotNull(director, "No GameDirector.");
            Assert.IsNotNull(world, "No WorldBuilder.");

            // The title panel sits exactly over the band, so with the interface up
            // switching the ground off appears to change nothing at all. The first run
            // of this capture reported exactly that.
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                canvas.enabled = false;
            }

            foreach (int round in new[] { 1, 6 })
            {
                yield return StageEndlessBoard(director, round);
                world.ApplyTheme(director.Library.GetTheme(EndlessRoundBuilder.StageFor(round)));
                yield return WaitFrames(8);
                yield return Capture($"floor_round{round}");
            }

            // An inventory of the band before anything else: every renderer standing in
            // the strip between the back of the board and the booth, and what it is made
            // of. The recording is unambiguous about *where* the band is — it starts at the
            // board's back edge and the barrels stand on it — so the question is only
            // which surfaces occupy that box.
            Bounds strip = new Bounds(new Vector3(0f, 0.15f, 2.92f), new Vector3(3.2f, 0.4f, 2.05f));
            System.Text.StringBuilder inventory = new System.Text.StringBuilder();

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.bounds.Intersects(strip))
                {
                    continue;
                }

                string path = renderer.name;
                for (Transform t = renderer.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;

                Material material = renderer.sharedMaterial;
                Color colour = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor") : Color.clear;

                inventory.AppendLine(
                    $"{path,-70} {(material == null ? "-" : material.name),-28} " +
                    $"rgb {colour.r:0.00} {colour.g:0.00} {colour.b:0.00}  " +
                    $"y {renderer.bounds.min.y:0.000}..{renderer.bounds.max.y:0.000}  " +
                    $"z {renderer.bounds.min.z:0.00}..{renderer.bounds.max.z:0.00}  " +
                    $"shadows {(renderer.receiveShadows ? "recv" : "-")}");
            }

            string inventoryLogs = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(inventoryLogs);
            File.WriteAllText(Path.Combine(inventoryLogs, "floor_band_objects.txt"), inventory.ToString());

            // Then find it rather than guess it. Every object under the world is switched
            // off in turn and the band is measured; whatever the band most depends on is
            // the band. A first pass at this named five suspects and all five were wrong.
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            Vector3 band = new Vector3(0f, 0.05f, 2.9f);

            float baseline = BandLuma(camera, band);
            System.Collections.Generic.List<(string name, float luma)> results =
                new System.Collections.Generic.List<(string, float)>();

            yield return Probe(world.transform, string.Empty, camera, band, results);

            results.Sort((a, b) => Mathf.Abs(b.luma - baseline).CompareTo(Mathf.Abs(a.luma - baseline)));

            // And one level down inside the three it depends on most, so a whole booth
            // switched off does not hide which plank of it was the problem.
            int top = Mathf.Min(3, results.Count);
            string[] leaders = new string[top];
            for (int i = 0; i < top; i++) leaders[i] = results[i].name;

            foreach (string leader in leaders)
            {
                Transform parent = world.transform.Find(leader);
                if (parent != null)
                {
                    yield return Probe(parent, leader + "/", camera, band, results);
                }
            }

            results.Sort((a, b) => Mathf.Abs(b.luma - baseline).CompareTo(Mathf.Abs(a.luma - baseline)));

            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine($"band luma with everything on: {baseline:0.000}");
            report.AppendLine("switched off                         band luma   change");

            for (int i = 0; i < Mathf.Min(16, results.Count); i++)
            {
                report.AppendLine($"{results[i].name,-36} {results[i].luma:0.000}      {results[i].luma - baseline:+0.000;-0.000}");
            }

            File.WriteAllText(Path.Combine(outputDirectory, "floor_band_report.txt"), report.ToString());

            // Also somewhere that outlives the run. The capture tool keeps only the frames
            // it converts, and the first run of this probe lost its answer that way.
            string logs = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "floor_band_report.txt"), report.ToString());
            Debug.Log("[Capture] " + report);

            if (results.Count > 0)
            {
                Transform culprit = world.transform.Find(results[0].name);
                culprit.gameObject.SetActive(false);
                yield return WaitFrames(2);
                yield return Capture("floor_round6_without_the_band");
                culprit.gameObject.SetActive(true);
            }
        }

        private IEnumerator Probe(
            Transform parent, string prefix, Camera camera, Vector3 band,
            System.Collections.Generic.List<(string name, float luma)> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                yield return null;
                results.Add((prefix + child.name, BandLuma(camera, band)));
                child.gameObject.SetActive(true);
            }
        }

        /// <summary>Mean luminance of a strip of the frame around a world point, 0 to 1.</summary>
        private static float BandLuma(Camera camera, Vector3 worldPoint)
        {
            RenderTexture target = new RenderTexture(
                Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest { destination = target };

            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;

            // Asked while the camera is pointed at the target, so the projection uses
            // the capture's aspect and not the batch-mode window's.
            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);

            RenderPipeline.SubmitRenderRequest(camera, request);
            camera.targetTexture = previousTarget;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();

            RenderTexture.active = previousActive;

            Color32[] pixels = image.GetPixels32();
            int row = Mathf.Clamp(Mathf.RoundToInt(viewport.y * Height), 0, Height - 1);

            double sum = 0d;
            int count = 0;

            for (int y = row - 12; y <= row + 12; y++)
            {
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                for (int x = Mathf.RoundToInt(Width * 0.2f); x < Mathf.RoundToInt(Width * 0.8f); x++)
                {
                    Color32 c = pixels[y * Width + x];
                    sum += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
                    count++;
                }
            }

            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);

            return (float)(sum / Mathf.Max(1, count) / 255d);
        }

        /// <summary>Lays out a generated round's board without playing up to it.</summary>
        private static IEnumerator StageEndlessBoard(GameDirector director, int round)
        {
            Config.PhysicsConfig physics = LoadPhysicsConfig();

            if (physics == null || director.Library == null)
            {
                yield break;
            }

            Config.LevelConfig staged = ScriptableObject.CreateInstance<Config.LevelConfig>();
            staged.hideFlags = HideFlags.DontSave;

            EndlessRoundBuilder.Build(
                staged, round, seed: 7, theme: director.Library.GetTheme(
                    EndlessRoundBuilder.StageFor(round)), physics: physics);

            director.Board.StartLevel(staged);

            yield return null;
        }

        private static IEnumerator LaunchAtPeg(GameDirector director)
        {
            float deadline = Time.realtimeSinceStartup + 5f;

            while (director.ActiveRing == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            RingInstance ring = director.ActiveRing;

            if (ring == null || director.Board.ActivePegs.Count == 0)
            {
                yield break;
            }

            // A real ballistic arc towards the peg, so the captured frame shows the
            // flight the player would actually see rather than a straight drop.
            Vector3 origin = ring.Centre;
            Vector3 target = director.Board.ActivePegs[0].transform.position + Vector3.up * 0.7f;

            director.Throwing.LaunchArmedRing(
                BallisticVelocity(origin, target, 42f, Physics.gravity.magnitude * 1.9f),
                new Vector3(0f, 14f, 0f),
                0.7f);
        }

        /// <summary>
        /// Launch velocity that lands on <paramref name="target"/> at the given pitch.
        /// Falls back to a flat lob if the target is out of range at that angle.
        /// </summary>
        private static Vector3 BallisticVelocity(Vector3 origin, Vector3 target, float pitchDegrees, float gravity)
        {
            Vector3 delta = target - origin;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;

            if (distance < 1e-3f)
            {
                return Vector3.up;
            }

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float tangent = Mathf.Tan(pitch);
            float denominator = 2f * Mathf.Cos(pitch) * Mathf.Cos(pitch) * (distance * tangent - delta.y);

            if (denominator <= 0f)
            {
                return flat.normalized * 8f + Vector3.up * 4f;
            }

            float speed = Mathf.Sqrt(gravity * distance * distance / denominator);
            Vector3 direction = flat.normalized;

            return direction * (speed * Mathf.Cos(pitch)) + Vector3.up * (speed * Mathf.Sin(pitch));
        }

        /// <summary>Waits until the player could throw again, or gives up.</summary>
        private static IEnumerator WaitForReady(GameDirector director)
        {
            float deadline = Time.realtimeSinceStartup + 10f;

            while (director.State != SessionState.Ready && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        /// <summary>Puts the ring straight over the peg so the catch is certain.</summary>
        private static IEnumerator DropOnPeg(GameDirector director)
        {
            RingInstance ring = director.ActiveRing;

            if (ring == null || director.Board.ActivePegs.Count == 0)
            {
                yield break;
            }

            PegTarget peg = director.Board.ActivePegs[0];

            ring.Arm(peg.TopPosition + Vector3.up * 0.6f, Quaternion.identity);
            director.Throwing.LaunchArmedRing(new Vector3(0f, -0.6f, 0f), new Vector3(0f, 9f, 0f), 0.6f);

            yield return null;
        }

        /// <summary>
        /// The physics config the scene is using. Nothing exposes it publicly — the
        /// game has no reason to hand it out — so this reaches for the loaded asset,
        /// which is fine for a tool that only ever runs on demand.
        /// </summary>
        private static Config.PhysicsConfig LoadPhysicsConfig()
        {
            Config.PhysicsConfig[] loaded = Resources.FindObjectsOfTypeAll<Config.PhysicsConfig>();
            return loaded.Length == 0 ? null : loaded[0];
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator Capture(string name)
        {
            // Deliberately not WaitForEndOfFrame: it never completes in batch mode and
            // hangs the run. SubmitRenderRequest renders on demand, so the frame
            // boundary does not matter here.
            yield return null;

            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(camera, "No camera in the scene.");

            RenderTexture target = new RenderTexture(
                Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4
            };

            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest { destination = target };

            // Still asked, as the cheapest proof that there is a graphics device at all:
            // without one this returns false and every capture would be a black frame.
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request),
                "No graphics device. Unity must not be run with -nographics for captures.");

            // The canvas is screen-space-camera, so it lays itself out against the
            // camera's pixel rect. Without pointing the camera at the target texture
            // first, that rect is whatever tiny landscape window batch mode invented,
            // and the HUD is laid out for the wrong aspect and cropped out of frame —
            // which looks exactly like a HUD that is not being drawn at all.
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;

            Canvas.ForceUpdateCanvases();

            // Rendered by the player loop, not by a render request.
            //
            // Phase 21 proved that SubmitRenderRequest showed no post-processing — Bloom
            // at eight came back byte-identical — and moved captures onto the player loop
            // on the assumption that that path would show it. It does not either, and
            // for a reason that has nothing to do with either path: the renderer asset
            // has no PostProcessData, so URP never creates its post-processing pass and
            // *nothing* in this project is post-processed — not these frames and not the
            // game on a phone. `Capture_proves_post_processing_is_in_the_frame` measures
            // it against the scene's own frame-to-frame noise (D92). Until that changes,
            // these frames are exactly what a player sees, glow included, because the
            // glow is emission and additive geometry rather than bloom.
            yield return null;
            yield return null;

            camera.targetTexture = previousTarget;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();

            RenderTexture.active = previousActive;

            // Raw RGB24 rather than PNG: EncodeToPNG lives in the imageconversion
            // module, which this project has no reason to ship. A companion script
            // turns these dumps into images.
            byte[] pixels = image.GetRawTextureData();

            using (BinaryWriter writer = new BinaryWriter(File.Create(
                       Path.Combine(outputDirectory, name + ".raw"))))
            {
                writer.Write(Width);
                writer.Write(Height);
                writer.Write(pixels);
            }

            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }
}
