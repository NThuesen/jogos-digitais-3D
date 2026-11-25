using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.UI
{
    /// <summary>How big an arrival an award gets. Chosen by where it came from and how much.</summary>
    public enum TicketCelebration
    {
        /// <summary>
        /// Tickets leaving the balance for something bought. The one that runs backwards:
        /// they fly out of the number, which counts down as they go, and land on the thing
        /// they paid for.
        /// </summary>
        Spend,

        /// <summary>A few tickets from play, into the HUD's badge.</summary>
        Trickle,

        /// <summary>A proper payout: a cleared level, a finished run, a share.</summary>
        Payout,

        /// <summary>An advert the player chose to watch: a banner, a burst, a fanfare.</summary>
        Reward,

        /// <summary>A share on the 2x tag: the whole screen, tickets raining into the balance.</summary>
        Jackpot
    }

    /// <summary>
    /// Tickets the player can watch arrive.
    ///
    /// The owner's note was that earning tickets has to look like earning tickets —
    /// "nem que seja ver os tickets literalmente caindo para o número dele" — and most
    /// of all when the player went out of their way for them: an advert they chose to
    /// watch, a friend they told. A number that changes is bookkeeping. A pile of
    /// tickets flying into it is a reward.
    ///
    /// So every award is a flight of ticket stubs from where it was earned into the
    /// number that holds it, and the number counts up as each one lands — punched,
    /// flashed and ticked, so the count and the tickets are visibly the same thing. How
    /// many fly, and what comes with them, is a tier: a trickle from play, a payout, a
    /// banner and a burst for a chosen advert, and for a boosted share the whole screen —
    /// a flash, confetti, and two dozen tickets raining in from the top.
    ///
    /// Everything is pooled and driven by hand on unscaled time, allocation-free, because
    /// the trickle plays during play and adverts stop the clock.
    /// </summary>
    public sealed class TicketShower : MonoBehaviour
    {
        private const int TicketCapacity = 28;
        private const int ConfettiCapacity = 44;

        /// <summary>Longest frame the animation will integrate, so a hitch cannot teleport a ticket.</summary>
        private const float MaxStep = 1f / 20f;

        private struct Flight
        {
            public RectTransform Rect;
            public bool Active;
            public bool Launched;
            public float Delay;
            public float Age;
            public Vector2 Start;
            public Vector2 Burst;
            public Vector2 Control;
            public float BurstTime;
            public float HomeTime;
            public float Spin;
            public float Flutter;
            public float Fall;
            public bool Rain;
        }

        private struct Bit
        {
            public RectTransform Rect;
            public Image Image;
            public bool Active;
            public float Age;
            public float Life;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Spin;
            public float Angle;
            public float FlipSpeed;
        }

        private RectTransform overlay;
        private readonly Flight[] flights = new Flight[TicketCapacity];
        private readonly Bit[] bits = new Bit[ConfettiCapacity];
        private Color[] confettiColours;

        private RectTransform banner;
        private Image bannerBackground;
        private TextMeshProUGUI bannerHeadline;
        private TextMeshProUGUI bannerSubline;
        private Image flash;
        private TextMeshProUGUI floater;

        private Color rewardBannerColour;
        private Color jackpotBannerColour;
        private Color rewardInk;
        private Color jackpotInk;

        // The award in progress.
        private bool running;
        private TicketCelebration kind;
        private TicketSource source;
        private float elapsed;
        private float startDelay;
        private bool announced;
        private int count;
        private int landed;
        private int startValue;
        private int endValue;
        private string format;
        private TextMeshProUGUI counter;
        private Vector3 counterScale;
        private Color counterColour;
        private Vector2 target;
        private float punchAt = float.NegativeInfinity;
        private float punchAmount;

        // A spend counts as the tickets leave rather than as they arrive, and its "-N"
        // floats over the balance they left rather than over what they were spent on.
        private bool countOnLaunch;
        private Vector2 counterPoint;
        private Vector2 floaterHome;
        private float bannerAt = float.PositiveInfinity;
        private float bannerHold;
        private float flashAt = float.PositiveInfinity;
        private float floaterAt = float.PositiveInfinity;
        private int confettiLive;

        private uint seed = 0x2545F491u;

        /// <summary>Builds the overlay and the pools. Called once by <see cref="UiRoot"/>.</summary>
        public void Build(
            Transform canvas,
            Sprite ticket,
            Sprite rounded,
            Color ticketColour,
            Color rewardColour,
            Color jackpotColour,
            Color darkInk,
            Color lightInk,
            Color[] confetti)
        {
            overlay = UiFactory.Rect("Ticket Shower", canvas);
            UiFactory.Stretch(overlay);

            rewardBannerColour = rewardColour;
            jackpotBannerColour = jackpotColour;
            rewardInk = darkInk;
            jackpotInk = lightInk;
            confettiColours = confetti;

            // Under everything else in the overlay, so the tickets fly over the flash.
            flash = UiFactory.Image("Flash", overlay, null, new Color(1f, 1f, 1f, 0f));
            UiFactory.Stretch(flash.rectTransform);
            flash.raycastTarget = false;
            flash.gameObject.SetActive(false);

            banner = UiFactory.Rect("Banner", overlay);
            UiFactory.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 240f), new Vector2(660f, 210f));

            bannerBackground = UiFactory.Image("Background", banner, rounded, rewardColour);
            UiFactory.Stretch(bannerBackground.rectTransform);
            bannerBackground.raycastTarget = false;

            bannerHeadline = UiFactory.Text(
                "Headline", banner, string.Empty, 72f, darkInk,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(bannerHeadline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(600f, 96f));

            bannerSubline = UiFactory.Text(
                "Subline", banner, string.Empty, 40f, darkInk,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(bannerSubline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(600f, 60f));

            banner.gameObject.SetActive(false);

            for (int i = 0; i < ConfettiCapacity; i++)
            {
                Image piece = UiFactory.Image($"Confetti_{i:D2}", overlay, rounded, Color.white);
                piece.raycastTarget = false;
                piece.rectTransform.sizeDelta = new Vector2(14f, 26f);
                piece.gameObject.SetActive(false);
                bits[i] = new Bit { Rect = piece.rectTransform, Image = piece };
            }

            for (int i = 0; i < TicketCapacity; i++)
            {
                RectTransform host = UiFactory.Rect($"Ticket_{i:D2}", overlay);
                host.sizeDelta = new Vector2(72f, 42f);

                // A shadow under each stub, so a gold ticket still reads over the gold
                // counter it is landing on and over the bright sky of the HUD.
                Image shadow = UiFactory.Image("Shadow", host, ticket, new Color(0f, 0f, 0f, 0.38f));
                UiFactory.Stretch(shadow.rectTransform);
                shadow.rectTransform.anchoredPosition = new Vector2(3f, -5f);
                shadow.raycastTarget = false;

                Image stub = UiFactory.Image("Stub", host, ticket, ticketColour);
                UiFactory.Stretch(stub.rectTransform);
                stub.raycastTarget = false;

                host.gameObject.SetActive(false);
                flights[i] = new Flight { Rect = host };
            }

            floater = UiFactory.Legible(UiFactory.Text(
                "Floater", overlay, string.Empty, 54f, ticketColour,
                TextAlignmentOptions.Center, FontStyles.Bold));
            floater.rectTransform.sizeDelta = new Vector2(360f, 80f);
            floater.raycastTarget = false;
            floater.gameObject.SetActive(false);
        }

        /// <summary>The full-screen layer everything flies across.</summary>
        public RectTransform Overlay => overlay;

        /// <summary>True while an award is counting into this label, which nothing else should write then.</summary>
        public bool IsCounting(TextMeshProUGUI label) => running && label != null && counter == label;

        /// <summary>A rect's centre in the overlay's own units, which is where flights are planned.</summary>
        public Vector2 LocalOf(RectTransform rect)
        {
            if (overlay == null || rect == null)
            {
                return Vector2.zero;
            }

            return overlay.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        }

        /// <summary>
        /// Flies an award into <paramref name="label"/>, counting it from
        /// <paramref name="from"/> to <paramref name="to"/> with <paramref name="numberFormat"/>
        /// as each ticket lands. A second award arriving mid-flight finishes the first at
        /// once rather than queueing behind it: a count that is still climbing when the
        /// next one starts is two numbers fighting over one label.
        /// </summary>
        public void Play(
            TicketCelebration celebration,
            TicketSource awardSource,
            Vector2 origin,
            TextMeshProUGUI label,
            int from,
            int to,
            string numberFormat,
            string headline = null,
            string subline = null,
            float delay = 0f,
            Vector2? toward = null)
        {
            if (overlay == null || label == null)
            {
                return;
            }

            Finish();

            running = true;
            kind = celebration;
            source = awardSource;
            elapsed = 0f;
            startDelay = Mathf.Max(0f, delay);
            announced = false;
            landed = 0;
            startValue = from;
            endValue = to;
            format = numberFormat;
            counter = label;
            counterScale = label.rectTransform.localScale;
            counterColour = label.color;
            target = LocalOf(label.rectTransform);
            punchAt = float.NegativeInfinity;
            bannerAt = float.PositiveInfinity;
            flashAt = float.PositiveInfinity;
            floaterAt = float.PositiveInfinity;

            label.SetText(numberFormat, from);
            counterPoint = target;

            int amount = Mathf.Abs(to - from);

            float stagger;
            float lead = 0f;

            switch (celebration)
            {
                case TicketCelebration.Spend:
                    // One ticket per twenty-five spent, so a cheap trail and the most
                    // expensive ring do not cost the same handful of stubs.
                    count = Mathf.Clamp(amount / 25, 5, 10);
                    stagger = 0.05f;
                    punchAmount = 0.18f;
                    break;

                case TicketCelebration.Trickle:
                    count = Mathf.Clamp(amount, 1, 5);
                    stagger = 0.07f;
                    punchAmount = 0.16f;
                    break;

                case TicketCelebration.Payout:
                    count = Mathf.Clamp(amount / 2, 6, 10);
                    stagger = 0.05f;
                    punchAmount = 0.2f;
                    break;

                case TicketCelebration.Reward:
                    count = 16;
                    stagger = 0.045f;
                    punchAmount = 0.26f;
                    lead = 0.3f;
                    ShowBanner(headline, subline, rewardBannerColour, rewardInk, 1.25f);
                    break;

                default:
                    count = 24;
                    stagger = 0.04f;
                    punchAmount = 0.32f;
                    lead = 0.42f;
                    flashAt = startDelay;
                    flash.gameObject.SetActive(true);
                    ShowBanner(headline, subline, jackpotBannerColour, jackpotInk, 1.7f);
                    break;
            }

            count = Mathf.Min(count, TicketCapacity);

            // A spend runs the other way: out of the number, onto the thing it bought.
            countOnLaunch = celebration == TicketCelebration.Spend;

            if (countOnLaunch)
            {
                target = toward ?? origin;
            }

            // Rewards come out of the banner that announced them, which is what makes the
            // banner the thing that paid. Everything else comes from where it was earned.
            Vector2 launch = celebration == TicketCelebration.Reward
                ? banner.anchoredPosition
                : countOnLaunch ? counterPoint : origin;

            float halfWidth = overlay.rect.width * 0.5f;
            float halfHeight = overlay.rect.height * 0.5f;

            for (int i = 0; i < count; i++)
            {
                ref Flight flight = ref flights[i];

                flight.Active = true;
                flight.Launched = false;
                flight.Age = 0f;
                flight.Delay = startDelay + lead + i * stagger;
                flight.Spin = Range(-540f, 540f);
                flight.Rain = celebration == TicketCelebration.Jackpot;

                if (flight.Rain)
                {
                    // Raining in from above the top edge, spread across the width, then
                    // pulled into the number: the literal reading of the owner's note.
                    flight.Start = new Vector2(
                        Range(-halfWidth + 70f, halfWidth - 70f),
                        halfHeight + Range(40f, 220f));
                    flight.Delay += Range(0f, 0.18f);
                    flight.BurstTime = Range(0.42f, 0.62f);
                    flight.HomeTime = Range(0.42f, 0.56f);
                    flight.Fall = Range(1500f, 2100f);
                    flight.Flutter = Range(7f, 12f);

                    float t = flight.BurstTime;
                    flight.Burst = flight.Start + new Vector2(
                        Mathf.Sin(t * flight.Flutter) * 28f, -0.5f * flight.Fall * t * t);

                    Vector2 velocity = new Vector2(
                        Mathf.Cos(t * flight.Flutter) * 28f * flight.Flutter, -flight.Fall * t);
                    flight.Control = flight.Burst + velocity * (flight.HomeTime * 0.3f);
                }
                else
                {
                    // Bursts up and out of the source, then arcs over and drops in.
                    float spread = celebration == TicketCelebration.Trickle ? 45f : 70f;
                    float angle = (90f + Range(-spread, spread)) * Mathf.Deg2Rad;
                    float reach = celebration switch
                    {
                        TicketCelebration.Trickle => Range(50f, 110f),
                        TicketCelebration.Payout => Range(90f, 190f),
                        _ => Range(150f, 280f)
                    };

                    flight.Start = launch + new Vector2(Range(-14f, 14f), Range(-10f, 10f));
                    flight.Burst = flight.Start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                    flight.BurstTime = Range(0.2f, 0.3f);
                    flight.HomeTime = Range(0.46f, 0.62f);

                    float lift = Mathf.Max(flight.Burst.y, target.y) + Range(140f, 260f);
                    flight.Control = new Vector2(Mathf.Lerp(flight.Burst.x, target.x, 0.35f), lift);
                }

                flight.Rect.gameObject.SetActive(false);
            }

            for (int i = count; i < TicketCapacity; i++)
            {
                flights[i].Active = false;
                flights[i].Rect.gameObject.SetActive(false);
            }

            if (celebration == TicketCelebration.Reward || celebration == TicketCelebration.Jackpot)
            {
                LaunchConfetti(
                    celebration == TicketCelebration.Jackpot ? ConfettiCapacity : 18,
                    banner.anchoredPosition, startDelay + 0.08f, null);
            }
        }

        /// <summary>
        /// A burst of colour at a point, with no award behind it: what buying something
        /// gets, in the colour of the thing that was bought.
        /// </summary>
        public void Burst(Vector2 at, Color colour, int pieces = 18)
        {
            if (overlay == null)
            {
                return;
            }

            LaunchConfetti(Mathf.Clamp(pieces, 1, ConfettiCapacity), at, 0f, colour);
        }

        /// <summary>Ends whatever is playing now, with the label on its final number.</summary>
        public void Finish()
        {
            if (counter != null)
            {
                counter.SetText(format, endValue);
                counter.rectTransform.localScale = counterScale;
                counter.color = counterColour;
            }

            for (int i = 0; i < TicketCapacity; i++)
            {
                flights[i].Active = false;

                if (flights[i].Rect != null)
                {
                    flights[i].Rect.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < ConfettiCapacity; i++)
            {
                bits[i].Active = false;

                if (bits[i].Rect != null)
                {
                    bits[i].Rect.gameObject.SetActive(false);
                }
            }

            if (banner != null) banner.gameObject.SetActive(false);
            if (flash != null) flash.gameObject.SetActive(false);
            if (floater != null) floater.gameObject.SetActive(false);

            confettiLive = 0;
            counter = null;
            running = false;
        }

        private void ShowBanner(string headline, string subline, Color background, Color ink, float hold)
        {
            bannerHeadline.SetText(headline ?? string.Empty);
            bannerSubline.SetText(subline ?? string.Empty);
            bannerSubline.gameObject.SetActive(!string.IsNullOrEmpty(subline));
            bannerHeadline.color = ink;
            bannerSubline.color = ink;
            bannerBackground.color = background;

            bannerAt = startDelay;
            bannerHold = hold;
            banner.localScale = Vector3.zero;
            banner.gameObject.SetActive(true);
        }

        private void LaunchConfetti(int pieces, Vector2 at, float delay, Color? tint)
        {
            confettiLive = 0;

            for (int i = 0; i < ConfettiCapacity; i++)
            {
                ref Bit bit = ref bits[i];

                if (i >= pieces)
                {
                    bit.Active = false;
                    bit.Rect.gameObject.SetActive(false);
                    continue;
                }

                float angle = Range(25f, 155f) * Mathf.Deg2Rad;
                float speed = Range(700f, 1500f);

                bit.Active = true;
                bit.Age = -delay;
                bit.Life = Range(1.1f, 1.7f);
                bit.Position = at + new Vector2(Range(-120f, 120f), Range(-30f, 30f));
                bit.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                bit.Spin = Range(-720f, 720f);
                bit.Angle = Range(0f, 360f);
                bit.FlipSpeed = Range(8f, 16f);

                // One colour for a burst that belongs to something — a skin, a trail —
                // and the studio's set for a celebration that belongs to the game.
                bit.Image.color = tint ?? confettiColours[i % confettiColours.Length];
                bit.Rect.gameObject.SetActive(false);
                confettiLive++;
            }
        }

        private void Update()
        {
            // A burst can be the only thing playing: a purchase has no award behind it.
            if (!running && confettiLive <= 0)
            {
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            elapsed += dt;

            if (running && !announced && elapsed >= startDelay)
            {
                announced = true;
                GameEvents.RaiseTicketShowerStarted(source, Mathf.Abs(endValue - startValue));

                if (kind == TicketCelebration.Jackpot)
                {
                    Haptics.Pulse(HapticStrength.Heavy);
                }
                else if (kind == TicketCelebration.Reward)
                {
                    Haptics.Pulse(HapticStrength.Medium);
                }
            }

            bool flying = running && TickFlights(dt);
            bool falling = TickConfetti(dt);
            bool bannered = TickBanner();
            bool flashing = TickFlash();
            bool floating = TickFloater();
            TickCounter();

            if (running && !flying && !falling && !bannered && !flashing && !floating && landed >= count)
            {
                Finish();
            }
        }

        private bool TickFlights(float dt)
        {
            bool any = false;

            for (int i = 0; i < count; i++)
            {
                ref Flight flight = ref flights[i];

                if (!flight.Active)
                {
                    continue;
                }

                any = true;
                flight.Age += dt;
                float t = flight.Age - flight.Delay;

                if (t < 0f)
                {
                    continue;
                }

                if (!flight.Launched)
                {
                    flight.Launched = true;
                    flight.Rect.gameObject.SetActive(true);

                    // Spending is counted here: the balance has to fall as the tickets
                    // leave it, not when they reach what they paid for.
                    if (countOnLaunch)
                    {
                        Step();
                    }
                }

                Vector2 position;
                float scale;
                float angle;

                if (t < flight.BurstTime)
                {
                    float u = t / flight.BurstTime;

                    if (flight.Rain)
                    {
                        position = flight.Start + new Vector2(
                            Mathf.Sin(t * flight.Flutter) * 28f, -0.5f * flight.Fall * t * t);
                        scale = Mathf.Lerp(0.7f, 1.1f, OutCubic(u));
                        angle = Mathf.Sin(t * flight.Flutter) * 35f;
                    }
                    else
                    {
                        position = Vector2.LerpUnclamped(flight.Start, flight.Burst, OutCubic(u));
                        scale = OutBack(Mathf.Clamp01(u * 1.3f)) * 1.1f;
                        angle = flight.Spin * t;
                    }
                }
                else
                {
                    float u = Mathf.Clamp01((t - flight.BurstTime) / flight.HomeTime);

                    // Accelerating into the number: it falls in rather than drifting over.
                    float e = u * u;
                    position = Bezier(flight.Burst, flight.Control, target, e);
                    scale = Mathf.Lerp(1.1f, 0.55f, e);

                    float from = flight.Rain
                        ? Mathf.Sin(flight.BurstTime * flight.Flutter) * 35f
                        : flight.Spin * flight.BurstTime;
                    angle = from * (1f - OutCubic(u));

                    if (u >= 1f)
                    {
                        flight.Active = false;
                        flight.Rect.gameObject.SetActive(false);

                        if (!countOnLaunch)
                        {
                            Step();
                        }

                        continue;
                    }
                }

                flight.Rect.anchoredPosition = position;
                flight.Rect.localScale = new Vector3(scale, scale, 1f);
                flight.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            return any;
        }

        /// <summary>One ticket accounted for: arriving on an award, leaving on a spend.</summary>
        private void Step()
        {
            landed++;

            int value = startValue + Mathf.RoundToInt((endValue - startValue) * (float)landed / count);
            counter.SetText(format, value);
            punchAt = elapsed;

            GameEvents.RaiseTicketLanded(landed - 1, count);

            // A tap on every few, so a pile of tickets is felt arriving and not just seen.
            int every = kind == TicketCelebration.Jackpot ? 3 : 4;

            if (kind >= TicketCelebration.Reward && landed % every == 0)
            {
                Haptics.Pulse(HapticStrength.Light);
            }

            // No floater on a spend, and this was arrived at the hard way. The shop's top
            // strip is the busiest in the game: the heading sits above the balance and the
            // two tabs sit below it, so a "-150" rising lands on PRIZE BOOTH and the same
            // label falling lands on the RINGS tab. Both were captured and both looked
            // like a bug.
            //
            // It is also redundant, which is the better reason. For an award the floater
            // *is* the payoff; for a purchase the payoff is the card, the price was on the
            // button the player just pressed, and the balance counting down from 4000 to
            // 3850 already says what was charged.
            if (landed >= count && kind != TicketCelebration.Trickle && !countOnLaunch)
            {
                floaterAt = elapsed;
                floaterHome = target;
                floater.SetText("+{0}", endValue - startValue);
                floater.rectTransform.anchoredPosition = floaterHome + new Vector2(0f, 50f);
                floater.gameObject.SetActive(true);
            }
        }

        private void TickCounter()
        {
            if (counter == null)
            {
                return;
            }

            float k = Mathf.Clamp01((elapsed - punchAt) / 0.22f);
            float kick = (1f - k) * (1f - k);

            float scale = 1f + punchAmount * kick;
            counter.rectTransform.localScale = counterScale * scale;
            counter.color = Color.Lerp(counterColour, Color.white, kick * 0.85f);
        }

        private bool TickConfetti(float dt)
        {
            if (confettiLive <= 0)
            {
                return false;
            }

            const float gravity = 2300f;
            bool any = false;

            for (int i = 0; i < ConfettiCapacity; i++)
            {
                ref Bit bit = ref bits[i];

                if (!bit.Active)
                {
                    continue;
                }

                any = true;
                bit.Age += dt;

                if (bit.Age < 0f)
                {
                    continue;
                }

                if (bit.Age >= bit.Life)
                {
                    bit.Active = false;
                    bit.Rect.gameObject.SetActive(false);
                    confettiLive--;
                    continue;
                }

                if (!bit.Rect.gameObject.activeSelf)
                {
                    bit.Rect.gameObject.SetActive(true);
                }

                bit.Velocity.y -= gravity * dt;
                bit.Velocity *= 1f - Mathf.Min(0.9f, 1.4f * dt);
                bit.Position += bit.Velocity * dt;
                bit.Angle += bit.Spin * dt;

                // Paper turning over: the width folds through zero as it tumbles.
                float flip = Mathf.Abs(Mathf.Cos(bit.Age * bit.FlipSpeed));
                float fade = Mathf.Clamp01((bit.Life - bit.Age) / 0.35f);

                bit.Rect.anchoredPosition = bit.Position;
                bit.Rect.localRotation = Quaternion.Euler(0f, 0f, bit.Angle);
                bit.Rect.localScale = new Vector3(Mathf.Max(0.15f, flip), 1f, 1f);

                Color colour = bit.Image.color;
                colour.a = fade;
                bit.Image.color = colour;
            }

            return any;
        }

        private bool TickBanner()
        {
            if (!banner.gameObject.activeSelf)
            {
                return false;
            }

            float t = elapsed - bannerAt;

            if (t < 0f)
            {
                banner.localScale = Vector3.zero;
                return true;
            }

            const float appear = 0.36f;
            const float leave = 0.28f;

            if (t < appear)
            {
                float u = t / appear;
                float scale = OutBack(u);
                banner.localScale = new Vector3(scale, scale, 1f);
                banner.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 0f, OutCubic(u)));
                banner.anchoredPosition = new Vector2(0f, 240f);
                return true;
            }

            if (t < appear + bannerHold)
            {
                // A slow breath while it holds, so a banner that is still on screen is
                // still alive and does not read as a frozen frame.
                float breathe = 1f + 0.025f * Mathf.Sin((t - appear) * 7f);
                banner.localScale = new Vector3(breathe, breathe, 1f);
                banner.localRotation = Quaternion.identity;
                return true;
            }

            float v = Mathf.Clamp01((t - appear - bannerHold) / leave);

            if (v >= 1f)
            {
                banner.gameObject.SetActive(false);
                return false;
            }

            float away = v * v;
            banner.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, away);
            banner.anchoredPosition = new Vector2(0f, 240f + 150f * away);
            banner.localRotation = Quaternion.Euler(0f, 0f, 8f * away);
            return true;
        }

        private bool TickFlash()
        {
            if (!flash.gameObject.activeSelf)
            {
                return false;
            }

            float t = elapsed - flashAt;

            if (t < 0f)
            {
                return true;
            }

            const float rise = 0.06f;
            const float fall = 0.4f;

            float alpha = t < rise ? t / rise : 1f - (t - rise) / fall;

            if (alpha <= 0f && t > rise)
            {
                flash.gameObject.SetActive(false);
                return false;
            }

            flash.color = new Color(1f, 1f, 1f, 0.34f * Mathf.Clamp01(alpha));
            return true;
        }

        private bool TickFloater()
        {
            if (!floater.gameObject.activeSelf)
            {
                return false;
            }

            float t = elapsed - floaterAt;
            const float life = 0.9f;

            if (t >= life)
            {
                floater.gameObject.SetActive(false);
                return false;
            }

            float u = t / life;
            float pop = OutBack(Mathf.Clamp01(u * 3f));

            floater.rectTransform.anchoredPosition = floaterHome + new Vector2(0f, 50f + 80f * OutCubic(u));
            floater.rectTransform.localScale = new Vector3(pop, pop, 1f);

            Color colour = floater.color;
            colour.a = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;
            floater.color = colour;
            return true;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * a + 2f * inverse * t * control + t * t * b;
        }

        private static float OutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;
            float shifted = t - 1f;
            return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
        }

        /// <summary>
        /// A small xorshift, so the layout of a shower costs no allocation and no call into
        /// the shared <see cref="Random"/> that gameplay's wind and generator also draw from.
        /// </summary>
        private float Range(float min, float max)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return min + (max - min) * (seed / (float)uint.MaxValue);
        }
    }
}
