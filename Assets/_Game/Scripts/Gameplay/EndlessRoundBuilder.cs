using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Generates one endless round's board.
    ///
    /// The difficulty curve is written here as a schedule rather than emerging from a
    /// random roll, for the same reason the authored levels are written in code: it
    /// has to be readable top to bottom and reviewable in a diff. Randomness only
    /// decides *where* inside the round's budget the pegs go, never how hard the round
    /// is — two runs that reach round 20 face the same round 20, laid out differently.
    ///
    /// One rule outranks all of it: everything this produces must be winnable. Every
    /// anchor is placed inside the fan the throw can actually cover, moving pegs are
    /// given room for their whole travel inside it, peg thickness is capped against
    /// the round's ring size, and enough pegs are left standing that a board of
    /// retracting ones can never present nothing to aim at.
    /// </summary>
    public static class EndlessRoundBuilder
    {
        /// <summary>Board-local Z of the throw origin. Ranges are measured from here.</summary>
        public const float ThrowOriginZ = -4.5f;

        /// <summary>
        /// Height the ring leaves the hand at. Mirrors the scene's throw origin, and
        /// is only needed here because a raised peg's reachability depends on it.
        /// </summary>
        public const float LaunchHeight = 0.65f;

        /// <summary>
        /// Closest a peg is ever placed. Shorter than this and the peg stands off the
        /// front edge of the board mat, which reads as a mistake rather than as a
        /// challenge.
        /// </summary>
        public const float MinPlacementRange = 1.9f;

        /// <summary>Kept inside the throw's yaw limit, so the widest column is never the widest legal throw.</summary>
        public const float YawMarginDegrees = 2.5f;

        /// <summary>Kept inside the throw's range limit, at both ends, for the same reason.</summary>
        public const float RangeMargin = 0.35f;

        public const int MaxPegs = 5;

        /// <summary>Centre-to-centre metres two pegs must keep between them.</summary>
        private const float MinPegSpacing = 0.78f;

        /// <summary>
        /// Depth difference at which two pegs start standing in front of one another
        /// rather than beside one another.
        /// </summary>
        private const float StackedDepth = 0.55f;

        /// <summary>
        /// Degrees of bearing *from the camera* two stacked pegs must keep apart.
        ///
        /// Metres of separation is the wrong measure for this and was the only one
        /// being applied. Two pegs a metre and a half apart in depth on the same
        /// bearing are nowhere near each other on the board and are drawn on top of
        /// each other on the screen: the near one hides the far one's base, which is
        /// the part the player judges the throw against. Depth is a real difficulty
        /// lever and stays; a column that lines up with the lens is not a harder shot,
        /// it is a shot taken at something that cannot be seen.
        /// </summary>
        private const float MinStackedBearing = 2.4f;

        /// <summary>
        /// How much wider than the peg the ring's hole must stay. The capture rule
        /// needs daylight, not equality: a hole the exact width of the peg is a peg
        /// that can only be ringed by a throw with no error in it at all.
        /// </summary>
        private const float HoleToPegMargin = 1.55f;

        /// <summary>Rounds between stage changes. Purely cosmetic; the ramp is continuous.</summary>
        public const int RoundsPerStage = 5;

        /// <summary>Fills <paramref name="target"/> with the board for a round.</summary>
        public static void Build(
            LevelConfig target, int round, int seed, StageTheme theme, PhysicsConfig physics)
        {
            if (target == null || physics == null)
            {
                return;
            }

            round = Mathf.Max(1, round);

            // Deterministic per round within a run, different between runs. A run that
            // is identical every time stops being a survival mode after one evening.
            System.Random random = new System.Random(seed * 7919 + round * 104729);

            float ramp = Mathf.Clamp01((round - 1) / 24f);

            int pegCount = PegCountFor(round);

            // Two phases. The first is the real curve, over the first two dozen
            // rounds. The second is a slow grind that never quite stops, so a player
            // who reaches round forty is still being asked for something a little
            // harder rather than replaying round twenty-eight forever.
            float ringScale = Mathf.Lerp(1f, 0.74f, ramp) - Mathf.Lerp(0f, 0.06f, Ease(round, 25, 30));
            float wobble = Mathf.Lerp(0f, 0.9f, Ease(round, 8, 14));

            PegPlacement[] pegs = new PegPlacement[pegCount];

            PlacePegs(pegs, round, random, physics);
            ApplyThickness(pegs, round, random, ringScale, physics);
            ApplyElevation(pegs, round, physics);
            ApplyMotion(pegs, round, random, physics);
            ApplyRetraction(pegs, round);

            Vector3 wind = WindFor(round, random);
            float gust = Mathf.Lerp(0f, 1.8f, Ease(round, 18, 12));
            float gustHz = 0.24f + ramp * 0.1f;

            target.Configure(
                $"ROUND {round}",
                DescribeRound(round),
                theme,
                pegs,
                // Rings are not the resource in this mode; lives are. The budget is set
                // beyond anything a round can consume so the out-of-rings branch of the
                // shared loop can never fire here.
                rings: 12,
                scale: ringScale,
                wobble: wobble,
                windForce: wind,
                gust: gust,
                gustHz: gustHz,
                twoStars: 99,
                threeStars: 99);
        }

        /// <summary>
        /// The board fills out early — an empty board is the least interesting thing
        /// this mode can show — and then stops growing, because past five pegs the
        /// round stops being about aim and starts being about patience.
        /// </summary>
        public static int PegCountFor(int round)
        {
            if (round <= 1)
            {
                return 1;
            }

            if (round <= 3)
            {
                return 2;
            }

            if (round <= 6)
            {
                return 3;
            }

            if (round <= 10)
            {
                return 4;
            }

            return MaxPegs;
        }

        /// <summary>
        /// How deep the board is allowed to go this round. Early rounds are played
        /// entirely in the near half, so the player learns the short end of the drag
        /// before the long one is asked for.
        /// </summary>
        public static float MaxRangeFor(int round, PhysicsConfig physics)
        {
            float reach = physics.MaxThrowRange - RangeMargin;
            return Mathf.Lerp(3.8f, reach, Ease(round, 1, 9));
        }

        private static void PlacePegs(
            PegPlacement[] pegs, int round, System.Random random, PhysicsConfig physics)
        {
            float minRange = Mathf.Max(MinPlacementRange, physics.MinThrowRange + RangeMargin);
            float maxRange = Mathf.Max(minRange + 0.5f, MaxRangeFor(round, physics));
            float maxYaw = Mathf.Max(4f, physics.MaxYawDegrees - YawMarginDegrees);

            // One peg per depth band rather than a free roll across the whole board.
            // A uniform roll clusters, and a round whose pegs all landed at the same
            // distance asks one question five times.
            float band = (maxRange - minRange) / pegs.Length;

            for (int i = 0; i < pegs.Length; i++)
            {
                float nearEdge = minRange + band * i;
                Vector2 anchor = Vector2.zero;

                for (int attempt = 0; attempt < 24; attempt++)
                {
                    float range = nearEdge + band * (float)random.NextDouble();

                    // The screen is usually the tighter of the two limits: a bearing
                    // that is comfortably throwable at the back of the board is off the
                    // side of a tall phone's display.
                    float limit = Mathf.Min(maxYaw, CameraFraming.MaxYawAt(range));
                    float yaw = Mathf.Lerp(-limit, limit, (float)random.NextDouble());

                    anchor = AnchorFor(range, yaw);

                    if (IsClearOfPlaced(anchor, pegs, i))
                    {
                        break;
                    }
                }

                PegPlacement placement = PegPlacement.Default(anchor);
                placement.points = PointsFor(round, Mathf.Abs(anchor.y - ThrowOriginZ));
                // Shorter than standard, never taller. The throw aims at the height of
                // a standard peg, so a peg above that is one the ring arrives *below* —
                // it clips the side instead of dropping over, and nothing on screen
                // explains why. Below standard the ring still arrives descending onto
                // the top, which is a real difficulty lever and a fair one.
                placement.heightScale = round < 7
                    ? 1f
                    : Mathf.Lerp(0.82f, 1f, (float)random.NextDouble());

                pegs[i] = placement;
            }

            Shuffle(pegs, random);
        }

        /// <summary>Board-local anchor for a distance and a bearing from the throw origin.</summary>
        public static Vector2 AnchorFor(float range, float yawDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(yaw) * range, ThrowOriginZ + Mathf.Cos(yaw) * range);
        }

        /// <summary>Distance from the throw origin to a board-local anchor.</summary>
        public static float RangeOf(Vector2 anchor)
        {
            return new Vector2(anchor.x, anchor.y - ThrowOriginZ).magnitude;
        }

        /// <summary>Bearing from the throw origin to a board-local anchor, in degrees.</summary>
        public static float YawOf(Vector2 anchor)
        {
            return Mathf.Atan2(anchor.x, anchor.y - ThrowOriginZ) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Bearing from the camera to a board-local anchor, in degrees. Two pegs that
        /// share one are drawn at the same place across the screen, however far apart
        /// they are on the board.
        /// </summary>
        public static float CameraBearingOf(Vector2 anchor)
        {
            return Mathf.Atan2(anchor.x, anchor.y - CameraFraming.CameraZ) * Mathf.Rad2Deg;
        }

        private static bool IsClearOfPlaced(Vector2 candidate, PegPlacement[] pegs, int placed)
        {
            for (int i = 0; i < placed; i++)
            {
                Vector2 other = pegs[i].anchor;

                if ((other - candidate).sqrMagnitude < MinPegSpacing * MinPegSpacing)
                {
                    return false;
                }

                // Separated in depth is fine. Separated in depth on the same bearing is
                // one peg behind another; see MinStackedBearing.
                if (Mathf.Abs(other.y - candidate.y) > StackedDepth
                    && Mathf.Abs(CameraBearingOf(other) - CameraBearingOf(candidate))
                        < MinStackedBearing)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Fattens some pegs, but never past what the round's ring can still drop over.
        /// The cap is computed from the ring rather than authored, so shrinking the
        /// ring later cannot silently make a round unwinnable.
        /// </summary>
        private static void ApplyThickness(
            PegPlacement[] pegs, int round, System.Random random, float ringScale, PhysicsConfig physics)
        {
            int count = Mathf.Clamp((round - 11) / 5, 0, pegs.Length - 1);

            if (count <= 0)
            {
                return;
            }

            float hole = physics.RingHoleRadius * ringScale;
            float cap = hole / (HoleToPegMargin * Mathf.Max(0.001f, physics.PegRadius));

            if (cap <= 1f)
            {
                return;
            }

            float wanted = Mathf.Lerp(1.2f, 1.5f, Ease(round, 16, 14));
            float scale = Mathf.Min(wanted, cap);

            // Taken from the back of the array while motion takes from the front, so
            // the two rarely land on the same peg until the board is nearly full.
            for (int i = 0; i < count; i++)
            {
                pegs[pegs.Length - 1 - i].radiusScale = scale;
            }
        }

        /// <summary>
        /// Stands some pegs on plinths.
        ///
        /// A raised peg is met earlier on the arc than a peg on the board, so the
        /// player has to aim past it — which is a genuinely different problem from
        /// every other lever here, and the only one that changes *where* to aim rather
        /// than *when*.
        ///
        /// The height is verified rather than assumed. Reachability depends on the
        /// peg's distance as well as its height, so a plinth that is fine at three
        /// metres can put a peg at five metres out of the drag's range entirely; the
        /// height is shrunk until the throw can actually get there.
        /// </summary>
        private static void ApplyElevation(PegPlacement[] pegs, int round, PhysicsConfig physics)
        {
            int count = Mathf.Clamp((round - 4) / 6, 0, pegs.Length - 1);

            if (count <= 0)
            {
                return;
            }

            float wanted = Mathf.Lerp(0.18f, 0.42f, Ease(round, 10, 20));

            // Taken from the middle of the array: motion works from the front and
            // thickness from the back, so this keeps the three levers apart for as
            // long as the board has room for them.
            int start = Mathf.Max(0, (pegs.Length - count) / 2);

            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                float range = RangeOf(pegs[index].anchor);
                float height = wanted;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float top = physics.PegHeight * pegs[index].heightScale + height;

                    if (ThrowSolver.CanReach(range, top, LaunchHeight, physics))
                    {
                        pegs[index].baseHeight = height;
                        break;
                    }

                    height *= 0.65f;
                }
            }
        }

        private static void ApplyMotion(
            PegPlacement[] pegs, int round, System.Random random, PhysicsConfig physics)
        {
            // A static peg is left on the board until late, so there is always one
            // shot the player can take on their own terms while they read the rest.
            int ceiling = round < 14 ? pegs.Length - 1 : pegs.Length;
            int count = Mathf.Clamp((round - 4) / 3, 0, ceiling);

            if (count <= 0)
            {
                return;
            }

            float amplitude = Mathf.Lerp(0.32f, 0.95f, Ease(round, 5, 16));
            float speed = Mathf.Lerp(0.2f, 0.52f, Ease(round, 5, 30));

            for (int i = 0; i < count; i++)
            {
                PegPlacement placement = pegs[i];

                placement.motion = PickMotion(round, random);
                placement.motionSpeed = speed * Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());
                placement.motionPhase = (float)random.NextDouble();
                placement.motionAmplitude = FitAmplitude(placement, amplitude, physics);

                pegs[i] = placement;
            }
        }

        private static PegMotion PickMotion(int round, System.Random random)
        {
            // Sideways first, because it is the one the player can read at a glance.
            // Depth and orbit change the distance to the peg, which is a harder read,
            // and bobbing changes the height of the catch itself.
            if (round < 9)
            {
                return PegMotion.SlideX;
            }

            int roll = random.Next(round < 18 ? 3 : 4);

            switch (roll)
            {
                case 0:
                    return PegMotion.SlideX;
                case 1:
                    return PegMotion.Bob;
                case 2:
                    return PegMotion.SlideZ;
                default:
                    return PegMotion.Orbit;
            }
        }

        /// <summary>
        /// Trims a peg's travel so its whole path stays inside the reachable fan. A
        /// moving peg that spends part of its cycle out of range is not a harder peg,
        /// it is a peg the player is punished for aiming at, at a moment they had no
        /// way to predict.
        ///
        /// The fit is found by testing the path rather than by solving for it. The
        /// closed form is easy to get subtly wrong — a first attempt treated the
        /// sideways travel of a sliding peg as an arc length when it is a chord, which
        /// under-counted the bearing it swings through and let a peg drift a fraction
        /// of a degree outside the fan at the end of its swing. Sampling the actual
        /// motion and shrinking until it fits cannot disagree with the rule it is
        /// enforcing, because it is evaluating the same function the game will.
        /// </summary>
        private static float FitAmplitude(
            in PegPlacement placement, float wanted, PhysicsConfig physics)
        {
            if (placement.motion == PegMotion.Bob)
            {
                // Height, not reach, so the fan does not constrain it. Capped low for a
                // different reason: the throw arrives at the height of a standing peg,
                // so a peg at the top of its bob is briefly above the arc and can only
                // be clipped. That is a fair timing problem while the travel is small
                // enough to read, and an unexplained miss once it is not.
                return Mathf.Min(wanted, 0.26f);
            }

            PegPlacement probe = placement;
            float amplitude = wanted;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                probe.motionAmplitude = amplitude;

                if (StaysReachable(probe, physics))
                {
                    return amplitude;
                }

                amplitude *= 0.7f;
            }

            return 0f;
        }

        /// <summary>
        /// Walks a peg's whole cycle and asks whether every point of it is reachable.
        ///
        /// Height is part of the question, not a separate one. A peg that is raised
        /// *and* slides in depth has both of its limits moving at once: the plinth was
        /// checked as reachable at the anchor's distance, and sliding two thirds of a
        /// metre further back can take it past what any throw can climb to. Checking
        /// them together is the only way that case is ever caught.
        /// </summary>
        private static bool StaysReachable(in PegPlacement placement, PhysicsConfig physics)
        {
            const int samples = 48;

            float minRange = physics.MinThrowRange + RangeMargin * 0.5f;
            float maxRange = physics.MaxThrowRange - RangeMargin * 0.5f;
            float maxYaw = physics.MaxYawDegrees - YawMarginDegrees * 0.5f;
            float top = physics.PegHeight * Mathf.Max(0.01f, placement.heightScale) + placement.baseHeight;
            bool raised = placement.baseHeight > 0f;

            for (int i = 0; i < samples; i++)
            {
                // One full cycle, whatever the speed: the offset is periodic in
                // time * speed + phase, so sampling the phase covers all of it.
                float time = i / (float)samples / Mathf.Max(0.0001f, placement.motionSpeed);
                Vector3 offset = placement.OffsetAt(time);
                Vector2 moved = placement.anchor + new Vector2(offset.x, offset.z);

                float range = RangeOf(moved);

                if (range < minRange || range > maxRange)
                {
                    return false;
                }

                if (Mathf.Abs(YawOf(moved)) > maxYaw)
                {
                    return false;
                }

                // And on screen for the whole of its travel, not just in reach. With
                // room to spare, because the test that checks this samples different
                // points of the cycle than the fit does.
                if (!CameraFraming.FitsWithMargin(moved))
                {
                    return false;
                }

                if (raised && !ThrowSolver.CanReach(range, top, LaunchHeight, physics))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sinks some pegs on a cycle. Phases are spread evenly across the cycle rather
        /// than rolled, so two retracting pegs are never down at the same moment, and
        /// the count is capped so at least two pegs stay permanently standing — the
        /// board must never present nothing to aim at.
        /// </summary>
        private static void ApplyRetraction(PegPlacement[] pegs, int round)
        {
            int count = Mathf.Clamp((round - 7) / 5, 0, pegs.Length - 2);

            if (count <= 0)
            {
                return;
            }

            float period = Mathf.Lerp(4f, 2.8f, Ease(round, 12, 20));
            float duty = Mathf.Lerp(0.7f, 0.58f, Ease(round, 12, 20));

            // Index 0 is skipped so the peg the player can always fall back on is the
            // same one whether or not this round retracts anything.
            for (int i = 0; i < count; i++)
            {
                int index = 1 + i;
                PegPlacement placement = pegs[index];

                placement.retractPeriod = period;
                placement.retractDuty = duty;
                placement.retractPhase = count <= 1 ? 0f : (float)i / count;

                pegs[index] = placement;
            }
        }

        private static Vector3 WindFor(int round, System.Random random)
        {
            float strength = Mathf.Lerp(0f, 1.6f, Ease(round, 13, 12));

            if (strength <= 0.01f)
            {
                return Vector3.zero;
            }

            float direction = random.Next(2) == 0 ? -1f : 1f;
            return new Vector3(strength * direction, 0f, 0f);
        }

        private static int PointsFor(int round, float range)
        {
            // Deeper pegs pay more, and everything pays more as the run goes on, so a
            // long run's score is dominated by how far it got rather than by how many
            // throws it took to get there.
            return Mathf.RoundToInt(80f + round * 12f + (range - MinPlacementRange) * 26f);
        }

        private static string DescribeRound(int round)
        {
            if (round <= 1)
            {
                return "find your range";
            }

            if (round <= 4)
            {
                return "keep them all";
            }

            if (round <= 9)
            {
                return "lead the moving ones";
            }

            if (round <= 11)
            {
                return "some of them are up a step";
            }

            if (round <= 13)
            {
                return "they will not wait";
            }

            if (round <= 17)
            {
                return "mind the wind";
            }

            if (round <= 20)
            {
                return "thicker posts, smaller rings";
            }

            return "no mercy";
        }

        /// <summary>Stage index for a round, used to pick the palette.</summary>
        public static int StageFor(int round)
        {
            return Mathf.Max(0, round - 1) / RoundsPerStage;
        }

        /// <summary>
        /// 0 before <paramref name="from"/>, ramping to 1 over the next
        /// <paramref name="over"/> rounds. Every mechanic fades in rather than
        /// switching on, so no single round is where the mode suddenly got hard.
        /// </summary>
        private static float Ease(int round, int from, int over)
        {
            return Mathf.Clamp01((round - from) / (float)Mathf.Max(1, over));
        }

        private static void Shuffle(PegPlacement[] pegs, System.Random random)
        {
            for (int i = pegs.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pegs[i], pegs[j]) = (pegs[j], pegs[i]);
            }
        }
    }
}
