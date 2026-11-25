using System.Collections.Generic;
using SlipperyZlope.RingToss.Config;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Authors the stage themes and the fifty levels as assets.
    ///
    /// Level design lives in code rather than being clicked into the inspector, so the
    /// difficulty curve can be read top to bottom, reviewed in a diff, and rebuilt
    /// identically on a clean clone. What it writes are still ordinary
    /// ScriptableObjects, so anything can be nudged in the inspector afterwards —
    /// re-running this restores the authored values.
    ///
    /// Fifty levels is too many to hand-place peg by peg and still be readable, so
    /// layouts are composed from named shapes and modifiers: <c>Fat(Raise(Row(...)))</c>
    /// says what a level *is* in one line, where two hundred literal coordinates would
    /// say nothing at all.
    ///
    /// **Every peg is clamped into the reachable fan on the way in.** The throw covers
    /// 1.1 m to 6.4 m of range at up to 20 degrees of yaw from the origin at
    /// (0, 1.15, -4.5), and a peg outside that is not difficult, it is impossible. The
    /// clamp makes an unreachable peg unauthorable rather than merely discouraged, and
    /// logs an error when it has to act so the level gets fixed rather than silently
    /// moved.
    /// </summary>
    public static class LevelLibraryBootstrap
    {
        private const string DataFolder = "Assets/_Game/Data";
        private const string ThemesFolder = DataFolder + "/Themes";
        private const string LevelsFolder = DataFolder + "/Levels";
        private const string LibraryPath = DataFolder + "/LevelLibrary.asset";

        public const int LevelCount = 50;
        public const int LevelsPerStage = 10;

        /// <summary>Board-local Z of the throw origin. Every range is measured from here.</summary>
        public const float ThrowOriginZ = -4.5f;

        // The legal fan, kept a little inside what the throw can actually do so no
        // level ever asks for a throw at the very limit of the drag.
        private const float MinRange = 2f;
        private const float MaxRange = 5.9f;

        /// <summary>
        /// The throw's own yaw limit. The *binding* limit is usually tighter than this
        /// and comes from <see cref="CameraFraming"/>: a peg can be well within reach
        /// and still be off the side of a tall phone's screen, which is worse than
        /// unreachable because the player cannot even see what they are missing.
        /// </summary>
        private const float MaxYaw = 17f;

        /// <summary>Deepest a peg on a plinth may stand, and how high the plinth may be.</summary>
        private const float MaxRaisedRange = 5.5f;
        private const float MaxPlinth = 0.45f;

        // The depth bands levels are built from, as ranges from the throw origin.
        private const float Near = 2.2f;
        private const float Close = 2.9f;
        private const float Mid = 3.8f;
        private const float Deep = 4.7f;
        private const float Far = 5.4f;
        private const float Back = 5.85f;

        /// <summary>
        /// A note on the opening, and on wind.
        ///
        /// The first four levels shipped with three spare rings each, which meant a
        /// player could miss three times in a row on a single static peg two metres
        /// away and still take three stars. Playtesters said the game was *"só isso"*,
        /// and a tutorial that cannot be failed is most of why. They are on two spare
        /// now — still forgiving, no longer free.
        ///
        /// That is a deliberately small change, because the bigger one was made
        /// elsewhere: the trajectory preview used to draw the whole arc and mark the
        /// landing spot, which answered the only question the game asks. It stops
        /// halfway now, and the opening got harder without a single level changing.
        ///
        /// A note on wind, because these numbers look timid and are not.
        ///
        /// The gusts here were between three and four times larger, and six levels
        /// were unwinnable because of it — level 44 asked the player to place a ring
        /// inside a hole 0.24 m across while an unreadable sine wave moved it 0.55 m.
        /// A person found that by playing; every test in the project passed it,
        /// because reachability is a question about geometry and wind is not.
        ///
        /// WindFairnessTests now bounds the *gust* alone, on the grounds that a steady
        /// wind is a skill and a gust is a dice roll. The constant wind was left where
        /// it was — that is the half worth having, and it is now drawn on screen.
        /// </summary>
        private static int clampedPegs;
        private static int trimmedTravel;

        [MenuItem("SlipperyZlope/Bootstrap Levels")]
        public static void Run()
        {
            EditorAssets.EnsureFolder(ThemesFolder);
            EditorAssets.EnsureFolder(LevelsFolder);

            clampedPegs = 0;
            trimmedTravel = 0;

            StageTheme[] themes =
            {
                CreateSunsetTheme(),
                CreateMidnightTheme(),
                CreateDawnTheme(),
                CreateGroveTheme(),
                CreateEmberTheme()
            };

            List<LevelConfig> levels = new List<LevelConfig>(LevelCount);

            BuildStageOne(levels, themes[0]);
            BuildStageTwo(levels, themes[1]);
            BuildStageThree(levels, themes[2]);
            BuildStageFour(levels, themes[3]);
            BuildStageFive(levels, themes[4]);

            if (levels.Count != LevelCount)
            {
                Debug.LogError($"[LevelLibraryBootstrap] Wrote {levels.Count} levels, expected {LevelCount}.");
            }

            if (clampedPegs > 0)
            {
                Debug.LogError(
                    $"[LevelLibraryBootstrap] {clampedPegs} peg(s) had to be moved into the reachable " +
                    "fan. The level data asking for them is wrong and should be corrected.");
            }

            if (trimmedTravel > 0)
            {
                // A warning, not an error: trimming is the system working. It is still
                // worth seeing, because a peg whose stroke was cut in half is not the
                // level the author thought they wrote.
                Debug.LogWarning(
                    $"[LevelLibraryBootstrap] {trimmedTravel} moving peg(s) had their travel trimmed " +
                    "to stay inside the reachable fan.");
            }

            LevelLibrary library = EditorAssets.LoadOrCreate<LevelLibrary>(LibraryPath);
            library.SetLevels(levels.ToArray());
            library.SetThemes(themes);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();

            Debug.Log($"[LevelLibraryBootstrap] Wrote {themes.Length} themes and {levels.Count} levels.");
        }

        // ---- Layout shapes -------------------------------------------------------
        //
        // Everything below takes ranges and bearings rather than board coordinates,
        // because those are the two things the throw is actually made of. A level
        // author thinks "three across, halfway back", not "x = 0.95, z = -0.5".

        /// <summary>One peg at a distance and a bearing.</summary>
        private static PegPlacement[] One(float range, float yaw = 0f)
        {
            return new[] { At(range, yaw) };
        }

        /// <summary>
        /// The widest bearing a shape may actually use at a given distance.
        ///
        /// The shapes ask for a spread and get whatever fits. A level saying "three
        /// across, wide" means exactly that — the number of degrees was never the
        /// point, and at the back of the board the screen allows fewer of them than
        /// the throw does.
        /// </summary>
        private static float FitSpread(float range, float wanted)
        {
            return Mathf.Min(wanted, Mathf.Min(MaxYaw, CameraFraming.MaxYawAt(range)));
        }

        /// <summary>Pegs evenly spread across the fan at one distance.</summary>
        private static PegPlacement[] Row(float range, int count, float yawSpread)
        {
            PegPlacement[] pegs = new PegPlacement[count];
            float spread = FitSpread(range, yawSpread);

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : (float)i / (count - 1);
                pegs[i] = At(range, Mathf.Lerp(-spread, spread, t));
            }

            return pegs;
        }

        /// <summary>
        /// Total degrees a column is fanned across, so no peg in it hides the next.
        ///
        /// Small on the board and large on the screen. A bearing at the near end of a
        /// column is worth about a third of itself seen from the lens and a bearing at
        /// the far end about half, so eleven degrees of fan buys roughly two degrees of
        /// separation between neighbours — which is a little more than a peg is wide
        /// from the camera, and therefore the least that guarantees daylight between
        /// them.
        /// </summary>
        private const float ColumnFan = 11f;

        /// <summary>
        /// Pegs one behind the other along a bearing, swept across a few degrees so
        /// each is seen beside the last rather than through it.
        ///
        /// A true column is a column on the board and a single peg on the screen. The
        /// camera looks down the line, so the nearest peg covers the base of every peg
        /// behind it — and the base is what a throw is judged against, so the player is
        /// asked to land a ring on something they cannot see. That reads as unfair
        /// rather than as hard, which is the wrong kind of difficult and the only kind
        /// this game must not be.
        ///
        /// The depth is the whole point of the shape and none of it is given up: the
        /// pegs still recede, they simply recede on a diagonal. Each is clamped to its
        /// own bearing limit, which tightens as the pegs come closer to the camera, so
        /// the fan can never push the near end of a column off the side of the frame.
        /// </summary>
        private static PegPlacement[] Column(float yaw, params float[] ranges)
        {
            PegPlacement[] pegs = new PegPlacement[ranges.Length];
            float half = ColumnFan * 0.5f;

            for (int i = 0; i < ranges.Length; i++)
            {
                float t = ranges.Length == 1 ? 0.5f : (float)i / (ranges.Length - 1);
                float wanted = yaw + Mathf.Lerp(-half, half, t);
                float limit = FitSpread(ranges[i], Mathf.Abs(wanted));

                pegs[i] = At(ranges[i], Mathf.Clamp(wanted, -limit, limit));
            }

            return pegs;
        }

        /// <summary>A V opening away from the player: one near peg, two flanking it deeper.</summary>
        private static PegPlacement[] Vee(float range, float depth, float yawSpread)
        {
            float spread = FitSpread(range + depth, yawSpread);

            return new[]
            {
                At(range, 0f),
                At(range + depth, -spread),
                At(range + depth, spread)
            };
        }

        /// <summary>The same V, closed towards the player.</summary>
        private static PegPlacement[] Wedge(float range, float depth, float yawSpread)
        {
            float spread = FitSpread(range, yawSpread);

            return new[]
            {
                At(range, -spread),
                At(range, spread),
                At(range + depth, 0f)
            };
        }

        /// <summary>A line running across and back at the same time.</summary>
        private static PegPlacement[] Diagonal(int count, float nearRange, float farRange, float yawSpread)
        {
            PegPlacement[] pegs = new PegPlacement[count];

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : (float)i / (count - 1);
                float range = Mathf.Lerp(nearRange, farRange, t);

                // Fitted per peg, because each one sits at its own distance and the
                // frame narrows the closer it comes.
                float spread = FitSpread(range, yawSpread);
                pegs[i] = At(range, Mathf.Lerp(-spread, spread, t));
            }

            return pegs;
        }

        /// <summary>Two ranks, the back one offset so nothing hides behind anything.</summary>
        private static PegPlacement[] Ranks(float nearRange, float farRange, int nearCount, int farCount, float yawSpread)
        {
            List<PegPlacement> pegs = new List<PegPlacement>(nearCount + farCount);
            pegs.AddRange(Row(nearRange, nearCount, yawSpread * 0.6f));
            pegs.AddRange(Row(farRange, farCount, yawSpread));
            return pegs.ToArray();
        }

        /// <summary>Everything at one distance except one peg pulled right in.</summary>
        private static PegPlacement[] Ambush(float range, int count, float yawSpread, float nearRange)
        {
            List<PegPlacement> pegs = new List<PegPlacement>(count + 1) { At(nearRange, 0f) };
            pegs.AddRange(Row(range, count, yawSpread));
            return pegs.ToArray();
        }

        private static PegPlacement[] Join(params PegPlacement[][] groups)
        {
            List<PegPlacement> pegs = new List<PegPlacement>();

            for (int i = 0; i < groups.Length; i++)
            {
                pegs.AddRange(groups[i]);
            }

            return pegs.ToArray();
        }

        /// <summary>
        /// One peg, placed by range and bearing and clamped into the reachable fan.
        ///
        /// The clamp is the point: it makes an unreachable peg something the author
        /// cannot write rather than something they are asked to remember.
        /// </summary>
        private static PegPlacement At(float range, float yawDegrees)
        {
            float clampedRange = Mathf.Clamp(range, MinRange, MaxRange);

            // Two limits, and the screen's is usually the tighter one. At the back of
            // the board a 17 degree bearing is comfortably throwable and a centimetre
            // off the edge of a 9:20 display.
            float limit = Mathf.Min(MaxYaw, CameraFraming.MaxYawAt(clampedRange));
            float clampedYaw = Mathf.Clamp(yawDegrees, -limit, limit);

            if (!Mathf.Approximately(clampedRange, range) || !Mathf.Approximately(clampedYaw, yawDegrees))
            {
                clampedPegs++;
            }

            float yaw = clampedYaw * Mathf.Deg2Rad;

            PegPlacement placement = PegPlacement.Default(new Vector2(
                Mathf.Sin(yaw) * clampedRange,
                ThrowOriginZ + Mathf.Cos(yaw) * clampedRange));

            placement.points = Mathf.RoundToInt(80f + (clampedRange - MinRange) * 34f);
            return placement;
        }

        /// <summary>Distance from the throw origin to a placed peg.</summary>
        private static float RangeOf(in PegPlacement peg)
        {
            return new Vector2(peg.anchor.x, peg.anchor.y - ThrowOriginZ).magnitude;
        }

        // ---- Modifiers -----------------------------------------------------------
        //
        // Each returns the array it was given so they compose inline. An empty
        // "which" means every peg in the layout.

        /// <summary>
        /// Sets a peg moving, and trims its travel so the whole cycle stays reachable.
        ///
        /// Clamping the anchor is not enough. A peg placed well inside the fan can
        /// still swing out of it at the end of its stroke, and the player is then
        /// punished for aiming at it at a moment they had no way to predict. The
        /// amplitude the author asks for is a wish; what survives is whatever fits.
        ///
        /// Found by sampling rather than by solving, for the same reason the endless
        /// generator does it: the closed form is easy to get subtly wrong, and walking
        /// the same OffsetAt the game will call cannot disagree with it.
        /// </summary>
        private static PegPlacement[] Move(
            PegPlacement[] pegs, PegMotion motion, float amplitude, float speed, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                pegs[i].motion = motion;
                pegs[i].motionSpeed = speed;
                pegs[i].motionAmplitude = FitAmplitude(pegs[i], amplitude);
                pegs[i].points += 40;
            }

            return pegs;
        }

        private static float FitAmplitude(PegPlacement placement, float wanted)
        {
            // Height, not reach, so the fan does not constrain it. Capped low for a
            // different reason: the throw arrives at the height of a standing peg, so
            // a peg at the top of its bob is briefly above the arc.
            if (placement.motion == PegMotion.Bob)
            {
                return Mathf.Min(wanted, 0.26f);
            }

            float amplitude = wanted;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                placement.motionAmplitude = amplitude;

                if (StaysInFan(placement))
                {
                    if (attempt > 0)
                    {
                        trimmedTravel++;
                    }

                    return amplitude;
                }

                amplitude *= 0.8f;
            }

            trimmedTravel++;
            return 0f;
        }

        private static bool StaysInFan(in PegPlacement placement)
        {
            const int samples = 48;

            for (int i = 0; i < samples; i++)
            {
                // One full cycle whatever the speed: the offset is periodic in
                // time * speed + phase, so sampling the phase covers all of it.
                float time = i / (float)samples / Mathf.Max(0.0001f, placement.motionSpeed);
                Vector3 offset = placement.OffsetAt(time);
                Vector2 moved = placement.anchor + new Vector2(offset.x, offset.z);

                float range = new Vector2(moved.x, moved.y - ThrowOriginZ).magnitude;
                float yaw = Mathf.Abs(Mathf.Atan2(moved.x, moved.y - ThrowOriginZ) * Mathf.Rad2Deg);

                if (range < MinRange || range > MaxRange || yaw > MaxYaw)
                {
                    return false;
                }

                // And it has to stay on screen for the whole of its travel, not just
                // inside the throw's reach — with room to spare, because the test that
                // checks this samples different points of the cycle than the fit does.
                if (!CameraFraming.FitsWithMargin(moved))
                {
                    return false;
                }
            }

            return true;
        }

        private static PegPlacement[] Phase(PegPlacement[] pegs, float phase, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                pegs[i].motionPhase = phase;
            }

            return pegs;
        }

        private static PegPlacement[] Fat(PegPlacement[] pegs, float scale, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                pegs[i].radiusScale = scale;
                pegs[i].points += 30;
            }

            return pegs;
        }

        private static PegPlacement[] Stubby(PegPlacement[] pegs, float scale, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                // Shorter only. The throw arrives at the height of a standard peg, so a
                // taller one is met below its top and can only be clipped; a shorter one
                // is still met from above, which is a fair problem.
                pegs[i].heightScale = Mathf.Clamp(scale, 0.6f, 1f);
                pegs[i].points += 25;
            }

            return pegs;
        }

        /// <summary>Stands pegs on a plinth, which has to be aimed past rather than at.</summary>
        private static PegPlacement[] Raise(PegPlacement[] pegs, float height, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                if (RangeOf(pegs[i]) > MaxRaisedRange)
                {
                    Debug.LogError(
                        $"[LevelLibraryBootstrap] A peg at {RangeOf(pegs[i]):F2} m is too deep to raise: " +
                        "the aim has to reach past it and would leave the drag's range.");
                    continue;
                }

                pegs[i].baseHeight = Mathf.Clamp(height, 0f, MaxPlinth);
                pegs[i].points += Mathf.RoundToInt(height * 220f);
            }

            return pegs;
        }

        private static PegPlacement[] Blink(
            PegPlacement[] pegs, float period, float duty, float phase, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                pegs[i].retractPeriod = period;
                pegs[i].retractDuty = duty;
                pegs[i].retractPhase = phase;
                pegs[i].points += 60;
            }

            return pegs;
        }

        private static PegPlacement[] Worth(PegPlacement[] pegs, int points, params int[] which)
        {
            foreach (int i in Indices(pegs, which))
            {
                pegs[i].points = points;
            }

            return pegs;
        }

        private static IEnumerable<int> Indices(PegPlacement[] pegs, int[] which)
        {
            if (which == null || which.Length == 0)
            {
                for (int i = 0; i < pegs.Length; i++)
                {
                    yield return i;
                }

                yield break;
            }

            for (int i = 0; i < which.Length; i++)
            {
                if (which[i] >= 0 && which[i] < pegs.Length)
                {
                    yield return which[i];
                }
            }
        }
        private static StageTheme CreateSunsetTheme()
        {
            StageTheme theme = EditorAssets.LoadOrCreate<StageTheme>(ThemesFolder + "/Theme_Sunset.asset");

            theme.SetPalette(
                top: Hex("#241B4A"),
                middle: Hex("#7A2E63"),
                horizon: Hex("#F2764B"),
                horizonAt: 0.42f,
                sun: Hex("#FFD79A"),
                sunAt: new Vector2(0.26f, 0.10f),
                sunRadius: 0.06f,
                silhouette: Hex("#241733"),
                ground: Hex("#3B2547"),
                board: Hex("#5A3260"),
                trim: Hex("#F7C948"),
                peg: Hex("#E23E57"),
                stripe: Hex("#FDF3E3"),
                glow: Hex("#FFC46B"),
                // Every one of these is at least fifty degrees of hue away from the
                // peg. A ring that shares the posts' colour cannot be picked out of the
                // board in flight, which is the one thing the ring has to do.
                rings: new[] { Hex("#FFD166"), Hex("#4ECDC4"), Hex("#C9A7FF"), Hex("#8FB8FF") },
                key: Hex("#FFD9B0"),
                keyIntensity: 1.2f,
                ambSky: Hex("#6B4C7A"),
                ambEquator: Hex("#4A3057"),
                ambGround: Hex("#241733"),
                perfect: Hex("#4DFFC3"),
                success: Hex("#FFD166"),
                fail: Hex("#B9AEC7"),
                text: Hex("#FBF6F0"),
                label: "Sunset Fair");

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static StageTheme CreateMidnightTheme()
        {
            StageTheme theme = EditorAssets.LoadOrCreate<StageTheme>(ThemesFolder + "/Theme_Midnight.asset");

            theme.SetPalette(
                top: Hex("#080B2E"),
                middle: Hex("#1B2A63"),
                horizon: Hex("#3E6FA8"),
                horizonAt: 0.38f,
                sun: Hex("#DDE9FF"),
                sunAt: new Vector2(0.74f, 0.40f),
                sunRadius: 0.035f,
                silhouette: Hex("#0B1030"),
                ground: Hex("#1A2247"),
                board: Hex("#26305C"),
                trim: Hex("#5AD2E8"),
                peg: Hex("#FF6B8A"),
                stripe: Hex("#FFF2F5"),
                glow: Hex("#6FE8FF"),
                rings: new[] { Hex("#6FE8FF"), Hex("#FFB86B"), Hex("#FFE066"), Hex("#A0FFB8") },
                key: Hex("#BFD4FF"),
                keyIntensity: 0.95f,
                ambSky: Hex("#2E3D77"),
                ambEquator: Hex("#1E2752"),
                ambGround: Hex("#0D1230"),
                perfect: Hex("#8CFFE0"),
                success: Hex("#6FE8FF"),
                fail: Hex("#8E97B8"),
                text: Hex("#EFF5FF"),
                label: "Midnight Carnival");

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static StageTheme CreateDawnTheme()
        {
            StageTheme theme = EditorAssets.LoadOrCreate<StageTheme>(ThemesFolder + "/Theme_Dawn.asset");

            theme.SetPalette(
                top: Hex("#16394F"),
                middle: Hex("#2E8C97"),
                horizon: Hex("#FFB27A"),
                horizonAt: 0.46f,
                sun: Hex("#FFF0C2"),
                sunAt: new Vector2(0.66f, 0.08f),
                sunRadius: 0.075f,
                silhouette: Hex("#14313F"),
                ground: Hex("#2A5560"),
                board: Hex("#37727A"),
                trim: Hex("#FFE3A3"),
                peg: Hex("#FF7A5C"),
                stripe: Hex("#FFF6EA"),
                glow: Hex("#FFD98A"),
                rings: new[] { Hex("#7FB4FF"), Hex("#FFE066"), Hex("#7FE7D2"), Hex("#C69BFF") },
                key: Hex("#FFECC9"),
                keyIntensity: 1.2f,
                ambSky: Hex("#4E93A0"),
                ambEquator: Hex("#356E79"),
                ambGround: Hex("#1B4049"),
                perfect: Hex("#3BFFD1"),
                success: Hex("#FFE066"),
                fail: Hex("#A7BCC0"),
                text: Hex("#FFFAF2"),
                label: "Neon Dawn");

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static StageTheme CreateGroveTheme()
        {
            StageTheme theme = EditorAssets.LoadOrCreate<StageTheme>(ThemesFolder + "/Theme_Grove.asset");

            // Green everywhere except the pegs, which go coral. A board this cool needs
            // its targets on the opposite side of the wheel or they sink into it.
            theme.SetPalette(
                top: Hex("#123A2E"),
                middle: Hex("#2E6B4F"),
                horizon: Hex("#A8D8A0"),
                horizonAt: 0.4f,
                sun: Hex("#FFF4C8"),
                sunAt: new Vector2(0.62f, 0.26f),
                sunRadius: 0.055f,
                silhouette: Hex("#10281F"),
                ground: Hex("#2B4A38"),
                board: Hex("#3A6049"),
                trim: Hex("#F2C14E"),
                peg: Hex("#FF6B4A"),
                stripe: Hex("#FFF6E8"),
                glow: Hex("#FFD166"),
                // Pulled away from the peg's coral: the two warm rings that used to be
                // here were close enough to read as a peg in flight.
                rings: new[] { Hex("#FFE066"), Hex("#7FD8FF"), Hex("#C8A2FF"), Hex("#FFF3C4") },
                key: Hex("#FFF0C8"),
                keyIntensity: 1.15f,
                ambSky: Hex("#4E7A63"),
                ambEquator: Hex("#35573F"),
                ambGround: Hex("#16281E"),
                perfect: Hex("#7CFFB2"),
                success: Hex("#FFD166"),
                fail: Hex("#A9B8A6"),
                text: Hex("#F4FBF2"),
                label: "Emerald Fete");

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static StageTheme CreateEmberTheme()
        {
            StageTheme theme = EditorAssets.LoadOrCreate<StageTheme>(ThemesFolder + "/Theme_Ember.asset");

            // The last stage, and the darkest. The sun is almost gone, the horizon is
            // the only warm thing left in the sky, and the pegs are the coldest colour
            // in the game so they cut straight out of it.
            theme.SetPalette(
                top: Hex("#1A0812"),
                middle: Hex("#5E1420"),
                horizon: Hex("#FF6B35"),
                horizonAt: 0.3f,
                sun: Hex("#FFB067"),
                sunAt: new Vector2(0.38f, 0.05f),
                sunRadius: 0.075f,
                silhouette: Hex("#16060E"),
                ground: Hex("#2E1220"),
                board: Hex("#451A28"),
                trim: Hex("#FFB347"),
                peg: Hex("#4ECDC4"),
                stripe: Hex("#F0FFFD"),
                glow: Hex("#FFD48A"),
                // None of these is the peg's teal. A ring the same colour as the posts
                // is a ring the player cannot pick out of the board mid-throw.
                rings: new[] { Hex("#FFE066"), Hex("#FF8FD0"), Hex("#C8A2FF"), Hex("#A0FFB8") },
                key: Hex("#FFC9A0"),
                keyIntensity: 1.1f,
                ambSky: Hex("#6B2233"),
                ambEquator: Hex("#47182A"),
                ambGround: Hex("#1E0A14"),
                perfect: Hex("#7CFFE0"),
                success: Hex("#FFB347"),
                fail: Hex("#B49AA4"),
                text: Hex("#FFF3EA"),
                label: "Ember Night");

            EditorUtility.SetDirty(theme);
            return theme;
        }

        // ---- Stage 1: Sunset Fair. Learn what the drag does. ---------------------

        private static void BuildStageOne(List<LevelConfig> levels, StageTheme theme)
        {
            levels.Add(Write("Level_01", theme, "Warm Up", "one peg, no tricks",
                One(Mid), spare: 2));

            levels.Add(Write("Level_02", theme, "Range Finder", "near and far",
                Column(0f, Near, Far), spare: 2));

            levels.Add(Write("Level_03", theme, "Side by Side", "same throw, different aim",
                Row(Close, 2, 12f), spare: 2));

            levels.Add(Write("Level_04", theme, "Three Across", "sweep the row",
                Row(Mid, 3, 14f), spare: 2));

            levels.Add(Write("Level_05", theme, "The Wedge", "two in front, one behind",
                Wedge(Close, 1.5f, 13f), spare: 2));

            levels.Add(Write("Level_06", theme, "Back and Forth", "all force, no aim",
                Column(0f, Near, Mid, Back), spare: 2));

            levels.Add(Write("Level_07", theme, "First Mover", "lead the moving one",
                Move(Row(Mid, 3, 14f), PegMotion.SlideX, 0.55f, 0.3f, 1),
                spare: 2));

            levels.Add(Write("Level_08", theme, "Slim Pickings", "a smaller ring",
                Row(Deep, 3, 15f), spare: 2, ringScale: 0.86f));

            levels.Add(Write("Level_09", theme, "The Diagonal", "across and away at once",
                Diagonal(4, Near, Far, 15f), spare: 2));

            levels.Add(Write("Level_10", theme, "Full House", "the whole board",
                Ranks(Close, Deep, 2, 3, 15f), spare: 2, ringScale: 0.94f));
        }

        // ---- Stage 2: Midnight Carnival. The margin narrows, and the board gains
        // a third dimension: pegs that stand above it. ------------------------------

        private static void BuildStageTwo(List<LevelConfig> levels, StageTheme theme)
        {
            levels.Add(Write("Level_11", theme, "Tight Fit", "barely wider than the post",
                Row(Mid, 2, 12f), spare: 2, ringScale: 0.82f));

            levels.Add(Write("Level_12", theme, "Fat Posts", "thicker, so less room",
                Fat(Row(Mid, 3, 14f), 1.45f), spare: 2));

            levels.Add(Write("Level_13", theme, "The Step", "one of them is up a step",
                Raise(Row(Close, 2, 12f), 0.28f, 1), spare: 2));

            levels.Add(Write("Level_14", theme, "High and Low", "two heights, two throws",
                Raise(Ranks(Close, Deep, 2, 2, 14f), 0.34f, 0, 1),
                spare: 2));

            levels.Add(Write("Level_15", theme, "Short Stack", "less post to catch on",
                Stubby(Row(Deep, 3, 15f), 0.7f), spare: 2));

            levels.Add(Write("Level_16", theme, "Pillars", "three steps, three ranges",
                Raise(Raise(Raise(Column(0f, Near, Mid, Far), 0.42f, 0), 0.28f, 1), 0.14f, 2),
                spare: 2));

            levels.Add(Write("Level_17", theme, "Needle", "the smallest ring yet",
                Row(Far, 2, 13f), spare: 2, ringScale: 0.78f));

            levels.Add(Write("Level_18", theme, "Crosswalk", "one will not stand still",
                Move(Row(Deep, 4, 16f), PegMotion.SlideX, 0.7f, 0.32f, 2),
                spare: 2, ringScale: 0.92f));

            // The back post pays four times the front one. Nothing stops the player
            // taking the easy ones first, but the score says which throw mattered.
            levels.Add(Write("Level_19", theme, "The Gauntlet", "the back post is the prize",
                Worth(Fat(Column(0f, Near, Close, Mid, Far), 1.4f, 1, 3), 600, 3),
                spare: 2));

            levels.Add(Write("Level_20", theme, "Midnight Run", "everything this stage taught",
                Stubby(Raise(Fat(Ranks(Close, Deep, 2, 3, 15f), 1.35f, 4), 0.3f, 0), 0.78f, 2),
                spare: 2, ringScale: 0.88f));
        }

        // ---- Stage 3: Neon Dawn. Nothing stays where you left it. -----------------

        private static void BuildStageThree(List<LevelConfig> levels, StageTheme theme)
        {
            levels.Add(Write("Level_21", theme, "Sidestep", "both of them, sliding",
                Move(Row(Mid, 2, 7f), PegMotion.SlideX, 0.6f, 0.3f),
                spare: 2));

            levels.Add(Write("Level_22", theme, "Bobbing", "up and down, so time it low",
                Move(Row(Deep, 2, 13f), PegMotion.Bob, 0.24f, 0.42f),
                spare: 2));

            levels.Add(Write("Level_23", theme, "Push and Pull", "the range keeps changing",
                Move(Column(0f, Close, Deep), PegMotion.SlideZ, 0.55f, 0.26f),
                spare: 2));

            levels.Add(Write("Level_24", theme, "Orbit", "one of them is going around",
                Move(Vee(Close, 1.6f, 13f), PegMotion.Orbit, 0.45f, 0.28f, 0),
                spare: 2, ringScale: 0.92f));

            levels.Add(Write("Level_25", theme, "Pendulum", "they cross in the middle",
                Phase(Move(Row(Mid, 2, 4f), PegMotion.SlideX, 0.8f, 0.34f), 0.5f, 1),
                spare: 2, ringScale: 0.9f));

            levels.Add(Write("Level_26", theme, "Carousel", "three, all turning",
                Phase(Phase(Move(Row(Deep, 3, 9f), PegMotion.Orbit, 0.4f, 0.26f), 0.33f, 1), 0.66f, 2),
                spare: 2));

            levels.Add(Write("Level_27", theme, "Crossfire", "two lanes, opposite ways",
                Phase(Move(Ranks(Close, Far, 2, 2, 10f), PegMotion.SlideX, 0.5f, 0.32f), 0.5f, 2, 3),
                spare: 2, ringScale: 0.9f));

            levels.Add(Write("Level_28", theme, "Tidal", "in and out, and one that waits",
                Move(Wedge(Close, 1.8f, 14f), PegMotion.SlideZ, 0.45f, 0.24f, 0, 1),
                spare: 2));

            levels.Add(Write("Level_29", theme, "Kinetic", "four ideas at once",
                Move(Move(Move(Row(Deep, 4, 10f), PegMotion.SlideX, 0.5f, 0.32f, 0, 3),
                        PegMotion.Bob, 0.24f, 0.44f, 1),
                    PegMotion.Orbit, 0.4f, 0.3f, 2),
                spare: 2, ringScale: 0.88f));

            levels.Add(Write("Level_30", theme, "Dance Floor", "nothing is standing still",
                Phase(Move(Ranks(Close, Far, 2, 3, 11f), PegMotion.SlideX, 0.45f, 0.33f), 0.5f, 2, 4),
                spare: 2, ringScale: 0.86f));
        }

        // ---- Stage 4: Emerald Fete. The air moves, and so do the pegs — sideways
        // out of existence. ---------------------------------------------------------

        private static void BuildStageFour(List<LevelConfig> levels, StageTheme theme)
        {
            levels.Add(Write("Level_31", theme, "First Breeze", "aim off the target",
                Row(Mid, 3, 14f), spare: 2, wind: 1.1f));

            levels.Add(Write("Level_32", theme, "Crosswind", "and now it means it",
                Ranks(Close, Far, 2, 2, 15f), spare: 2, wind: 1.9f,
                ringScale: 0.92f));

            levels.Add(Write("Level_33", theme, "Gusts", "it will not hold still",
                Row(Deep, 3, 15f), spare: 3, wind: 0.7f, gust: 0.6f, gustHz: 0.28f,
                wobble: 0.5f));

            levels.Add(Write("Level_34", theme, "Now You See It", "wait for it",
                Blink(Row(Mid, 3, 14f), 3.4f, 0.62f, 0f, 1),
                spare: 2));

            levels.Add(Write("Level_35", theme, "Blink", "never both at once",
                Blink(Blink(Row(Deep, 3, 15f), 3.2f, 0.6f, 0f, 0), 3.2f, 0.6f, 0.5f, 2),
                spare: 2));

            levels.Add(Write("Level_36", theme, "Wind and Wait", "two clocks to read",
                Blink(Ranks(Close, Far, 2, 2, 15f), 3.6f, 0.62f, 0.25f, 3),
                spare: 2, wind: 1.5f, ringScale: 0.9f));

            levels.Add(Write("Level_37", theme, "Ghost Row", "three of them, taking turns",
                Blink(Blink(Blink(Row(Deep, 4, 16f), 3.6f, 0.6f, 0f, 0),
                        3.6f, 0.6f, 0.33f, 2), 3.6f, 0.6f, 0.66f, 3),
                spare: 2));

            levels.Add(Write("Level_38", theme, "Storm Front", "moving in bad air",
                Move(Row(Mid, 4, 14f), PegMotion.SlideX, 0.55f, 0.3f, 1, 2),
                spare: 3, wind: 1.2f, gust: 0.6f, gustHz: 0.3f, wobble: 0.7f,
                ringScale: 0.88f));

            levels.Add(Write("Level_39", theme, "Vanishing Act", "up a step and gone",
                Blink(Raise(Ranks(Close, Deep, 2, 2, 14f), 0.36f, 0, 1), 3.4f, 0.6f, 0.5f, 3),
                spare: 2));

            levels.Add(Write("Level_40", theme, "Emerald Finale", "all of it, at once",
                Blink(Move(Raise(Ranks(Close, Far, 2, 3, 12f), 0.3f, 1), PegMotion.SlideX, 0.45f, 0.32f, 3),
                    3.4f, 0.6f, 0.4f, 4),
                spare: 3, wind: 1.1f, gust: 0.5f, gustHz: 0.29f, wobble: 0.6f,
                ringScale: 0.86f));
        }

        // ---- Stage 5: Ember Night. No new ideas, only less room for error. --------

        private static void BuildStageFive(List<LevelConfig> levels, StageTheme theme)
        {
            levels.Add(Write("Level_41", theme, "Fine Line", "the ring barely clears",
                Row(Mid, 3, 14f), spare: 2, ringScale: 0.76f));

            levels.Add(Write("Level_42", theme, "Tightrope", "the far corners",
                Row(Back, 3, 16f), spare: 2, ringScale: 0.82f));

            levels.Add(Write("Level_43", theme, "Split Second", "they are barely there",
                Blink(Blink(Row(Deep, 3, 15f), 2.6f, 0.55f, 0f, 0), 2.6f, 0.55f, 0.5f, 2),
                spare: 2, ringScale: 0.86f));

            levels.Add(Write("Level_44", theme, "Heavy Weather", "the far three, and one at your feet",
                Ambush(Deep, 3, 15f, Near), spare: 3, wind: 2.1f, gust: 0.55f, gustHz: 0.32f,
                wobble: 0.9f, ringScale: 0.88f));

            levels.Add(Write("Level_45", theme, "Steeplechase", "raised and running",
                Move(Raise(Diagonal(4, Near, Far, 12f), 0.34f, 1, 2), PegMotion.SlideX, 0.4f, 0.3f, 2, 3),
                spare: 2, ringScale: 0.86f));

            levels.Add(Write("Level_46", theme, "Needle Point", "thick posts, thin margin",
                Fat(Row(Mid, 3, 14f), 1.3f), spare: 2, ringScale: 0.78f));

            levels.Add(Write("Level_47", theme, "Chaos Theory", "read it before you throw",
                Blink(Move(Move(Join(Column(-13f, Close, Deep), Row(Back, 3, 10f)),
                            PegMotion.SlideX, 0.55f, 0.34f, 2, 4),
                        PegMotion.Bob, 0.24f, 0.46f, 1), 3.2f, 0.6f, 0.5f, 3),
                spare: 3, wind: 1.3f, ringScale: 0.84f));

            levels.Add(Write("Level_48", theme, "Last Light", "five posts, two spare rings",
                Stubby(Fat(Ranks(Near, Deep, 2, 3, 15f), 1.3f, 3), 0.74f, 0),
                spare: 2, ringScale: 0.84f));

            levels.Add(Write("Level_49", theme, "The Long Night", "the back of the board, moving",
                Phase(Move(Row(Back, 4, 9f), PegMotion.SlideX, 0.55f, 0.36f), 0.5f, 1, 3),
                spare: 2, wind: 1.4f, gust: 0.45f, gustHz: 0.3f, ringScale: 0.82f));

            levels.Add(Write("Level_50", theme, "Grand Finale", "everything the game has",
                Blink(Move(Move(Raise(Fat(Ranks(Close, Far, 2, 3, 12f), 1.28f, 4), 0.38f, 1),
                            PegMotion.SlideX, 0.45f, 0.35f, 0, 3),
                        PegMotion.Orbit, 0.35f, 0.3f, 2), 3f, 0.58f, 0.4f, 4),
                spare: 3, wind: 1.5f, gust: 0.45f, gustHz: 0.31f, wobble: 0.8f,
                ringScale: 0.8f));
        }

        /// <summary>
        /// Writes a level.
        ///
        /// The ring budget is stated as <paramref name="spare"/> — how many throws the
        /// player may waste — rather than as a total. That is the number that actually
        /// sets the difficulty, and stating the total instead hid it: a level with five
        /// pegs and nine rings looks generous next to one with two pegs and five, and
        /// it is in fact tighter. Every budget in this file used to be three or four
        /// spare, which is why the mode played soft.
        ///
        /// Stars come from the same number, so the scale is the same in every level:
        /// a flawless run is three stars, one wasted ring is two, and anything more is
        /// one.
        /// </summary>
        private static LevelConfig Write(
            string assetName,
            StageTheme theme,
            string displayName,
            string subtitle,
            PegPlacement[] pegs,
            int spare,
            float ringScale = 1f,
            float wobble = 0f,
            float wind = 0f,
            float gust = 0f,
            float gustHz = 0.3f)
        {
            LevelConfig level = EditorAssets.LoadOrCreate<LevelConfig>($"{LevelsFolder}/{assetName}.asset");

            int rings = pegs.Length + Mathf.Max(0, spare);

            level.Configure(
                displayName, subtitle, theme, pegs,
                rings, ringScale, wobble,
                new Vector3(wind, 0f, 0f), gust, gustHz,
                twoStars: Mathf.Max(1, spare - 1),
                threeStars: Mathf.Max(1, spare));

            EditorUtility.SetDirty(level);
            return level;
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
            {
                Debug.LogError($"[LevelLibraryBootstrap] Bad colour '{value}'.");
                return Color.magenta;
            }

            return color;
        }

    }
}
