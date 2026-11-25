using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using UnityEngine;

namespace SlipperyZlope.RingToss.Audio
{
    /// <summary>
    /// Turns gameplay events into sound.
    ///
    /// Every clip is synthesised at load by <see cref="ProceduralAudio"/>, and played
    /// through a small round-robin of AudioSources so overlapping hits do not cut each
    /// other off.
    ///
    /// The combo raises the catch chime's pitch by a fixed step per consecutive hit,
    /// capped so a long streak never turns shrill. That rising pitch is doing as much
    /// work as anything on screen: it is the part of a streak the player hears without
    /// having to look at a counter.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        [SerializeField] private JuiceConfig juiceConfig;

        [Tooltip("A catch now plays two clips at once — the chime and the rattle — on " +
                 "top of whatever impacts are still ringing out, so six voices is no " +
                 "longer enough to avoid cutting one of them off.")]
        [Range(2, 16)]
        [SerializeField] private int voiceCount = 9;

        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.75f;

        [Tooltip("The bed of fairground air under everything. Meant to be felt rather " +
                 "than heard: at this level it registers as the booth having a space " +
                 "around it, and turning it off makes the game sound like it is " +
                 "happening inside the phone.")]
        [Range(0f, 1f)]
        [SerializeField] private float ambienceVolume = 0.16f;

        private AudioSource[] voices;
        private int voiceCursor;

        // Lives arrive as a level rather than as a delta, so the last one is kept to
        // tell a loss from a gain.
        private int previousLives = int.MaxValue;

        private AudioClip whoosh;
        private AudioClip catchChime;
        private AudioClip perfectChime;
        private AudioClip knock;
        private AudioClip thud;
        private AudioClip fanfare;
        private AudioClip blip;
        private AudioClip lifeGained;
        private AudioClip lifeLost;
        private AudioClip rattle;
        private AudioClip sparkle;
        private AudioClip confirm;
        private AudioClip clockWind;
        private AudioClip clockUnwind;
        private AudioClip clockTick;
        private AudioClip clockTock;
        private AudioClip ticketTick;

        // Tickets landing on the balance. A source of its own, like the clock's tick: a
        // jackpot lands two dozen in about a second, and on the shared round-robin voices
        // those ticks would cut off the fanfare they are meant to sit under.
        private AudioSource ticketSource;

        // The time attack clock's running tick. A source of its own, because it is
        // pitched, filtered and distorted by how much time is left and the round-robin
        // voices are shared with every other sound in the game.
        private AudioSource tickSource;
        private AudioLowPassFilter tickFilter;
        private AudioDistortionFilter tickDistortion;
        private float clockRemaining;
        private float lastClockHeard = float.NegativeInfinity;
        private float tickTimer;
        private float tension;
        private bool tickIsTock;

        /// <summary>
        /// How long after the clock last moved the tick keeps going. Longer than any
        /// frame hitch, far shorter than a round's intro — during which the clock does
        /// not run, and so neither does its tick.
        /// </summary>
        private const float ClockGrace = 0.3f;

        /// <summary>Perfects banked when the streak was last heard, so only gains sound.</summary>
        private int previousStreak;
        private AudioClip ambience;

        private AudioSource ambienceSource;

        public bool Muted { get; private set; }

        private void Awake()
        {
            BuildVoices();
            BuildClips();
            BuildTick();
            BuildAmbience();

            GameObject tickets = new GameObject("Tickets");
            tickets.transform.SetParent(transform, false);
            ticketSource = tickets.AddComponent<AudioSource>();
            ticketSource.playOnAwake = false;
            ticketSource.spatialBlend = 0f;
        }

        /// <summary>
        /// Holds the fairground's bed of air while something else owns the sound.
        ///
        /// The splash has a soundtrack of its own — three throws, a catch and a fanfare —
        /// and the booth's ambience underneath it turns both into mud. The owner asked for
        /// the game's idle noise to start when the animation ends, which is also the
        /// better reading: the fairground should arrive with the fairground.
        /// </summary>
        public void SuspendAmbience()
        {
            ambienceHeld = true;

            if (ambienceSource != null)
            {
                ambienceSource.Pause();
            }
        }

        /// <summary>Lets it back in. Ignored if nothing was holding it.</summary>
        public void ResumeAmbience()
        {
            if (!ambienceHeld)
            {
                return;
            }

            ambienceHeld = false;

            if (ambienceSource != null && !Muted)
            {
                ambienceSource.UnPause();
            }
        }

        private bool ambienceHeld;

        private void BuildAmbience()
        {
            GameObject host = new GameObject("Ambience");
            host.transform.SetParent(transform, false);

            ambienceSource = host.AddComponent<AudioSource>();
            ambienceSource.clip = ambience;
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.volume = masterVolume * ambienceVolume;
            ambienceSource.Play();
        }

        private void BuildVoices()
        {
            voices = new AudioSource[voiceCount];

            for (int i = 0; i < voiceCount; i++)
            {
                GameObject host = new GameObject($"Voice_{i:D2}");
                host.transform.SetParent(transform, false);

                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;

                // 2D: the board is small and always centred, so panning by world
                // position would only add inconsistency between throws.
                source.spatialBlend = 0f;
                source.volume = masterVolume;

                voices[i] = source;
            }
        }

        private void BuildClips()
        {
            whoosh = ProceduralAudio.Whoosh("sfx_whoosh");

            // A perfect fifth above the ordinary catch, so the two results are
            // distinguishable with the phone in a pocket.
            //
            // Both dropped a fourth from where they started. D5 and A5 sat in the band
            // the ear is most sensitive to, which is why they read as sharp however
            // gently they were synthesised; G4 and D5 carry the same interval somewhere
            // the ear can relax.
            catchChime = ProceduralAudio.Chime("sfx_catch", 392f, 1.6f);
            perfectChime = ProceduralAudio.Chime("sfx_perfect", 587.33f, 1.9f, bright: true);

            knock = ProceduralAudio.Knock("sfx_knock");
            thud = ProceduralAudio.Thud("sfx_thud");
            fanfare = ProceduralAudio.Fanfare("sfx_fanfare");
            blip = ProceduralAudio.Blip("sfx_blip");

            // A life is the best thing that happens in endless mode, so it gets its
            // own arrival: a fanfare rooted a fourth above the level-clear one, which
            // makes it recognisable without being the same event.
            lifeGained = ProceduralAudio.Fanfare("sfx_life_gained", 698.46f);

            // And the worst gets a low, short chime rather than another thud. The
            // throw that lost the life has already played a thud; a second one says
            // nothing, and the player needs to hear that this miss cost something the
            // last one did not.
            lifeLost = ProceduralAudio.Chime("sfx_life_lost", 174.61f, 1.1f);

            // The sound the game is named after. A catch used to be a chime and
            // nothing else, which left the most physical moment in the game as the
            // least physical thing in the mix.
            rattle = ProceduralAudio.Rattle("sfx_rattle");

            sparkle = ProceduralAudio.Sparkle("sfx_tickets");
            confirm = ProceduralAudio.Confirm("sfx_confirm");

            clockWind = ProceduralAudio.ClockWind("sfx_clock_wind");
            clockUnwind = ProceduralAudio.ClockUnwind("sfx_clock_unwind");
            clockTick = ProceduralAudio.ClockTick("sfx_clock_tick", false);
            clockTock = ProceduralAudio.ClockTick("sfx_clock_tock", true);

            // E6 and seventy milliseconds: a coin's click rather than a note, so two
            // dozen of them in a second read as a pile landing and not as a melody.
            ticketTick = ProceduralAudio.Blip("sfx_ticket_tick", 1318.51f, 0.07f);
            ambience = ProceduralAudio.Ambience("amb_fairground");
        }

        private void OnEnable()
        {
            GameEvents.Thrown += OnThrown;
            GameEvents.Impact += OnImpact;
            GameEvents.Resolved += OnResolved;
            GameEvents.LevelEnded += OnLevelEnded;
            GameEvents.RoundCleared += OnRoundCleared;
            GameEvents.TicketShowerStarted += OnTicketShowerStarted;
            GameEvents.TicketLanded += OnTicketLanded;
            GameEvents.StreakRewarded += OnStreakRewarded;
            GameEvents.PerfectStreakChanged += OnPerfectStreakChanged;
            GameEvents.LivesChanged += OnLivesChanged;
            GameEvents.TimeAwarded += OnTimeAwarded;
            GameEvents.TimeChanged += OnTimeChanged;
            GameEvents.RunEnded += OnRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.Thrown -= OnThrown;
            GameEvents.Impact -= OnImpact;
            GameEvents.Resolved -= OnResolved;
            GameEvents.LevelEnded -= OnLevelEnded;
            GameEvents.RoundCleared -= OnRoundCleared;
            GameEvents.TicketShowerStarted -= OnTicketShowerStarted;
            GameEvents.TicketLanded -= OnTicketLanded;
            GameEvents.StreakRewarded -= OnStreakRewarded;
            GameEvents.PerfectStreakChanged -= OnPerfectStreakChanged;
            GameEvents.LivesChanged -= OnLivesChanged;
            GameEvents.TimeAwarded -= OnTimeAwarded;
            GameEvents.TimeChanged -= OnTimeChanged;
            GameEvents.RunEnded -= OnRunEnded;
        }

        public void SetConfig(JuiceConfig value)
        {
            juiceConfig = value;
        }

        public void SetMuted(bool value)
        {
            Muted = value;

            if (ambienceSource != null)
            {
                ambienceSource.volume = value ? 0f : masterVolume * ambienceVolume;
            }
        }

        /// <summary>Tickets landing. Raised by the economy rather than by a throw.</summary>
        public void PlayTickets()
        {
            Play(sparkle, Random.Range(0.98f, 1.04f), 0.8f);
        }

        public void PlayPurchase()
        {
            Play(confirm, 1f, 0.9f);
        }

        public void PlayDenied()
        {
            // The confirm, pitched down and short. A separate sound for "you cannot
            // afford this" is more vocabulary than the moment needs.
            Play(blip, 0.62f, 0.6f);
        }

        public void PlayUiTap()
        {
            Play(blip, Random.Range(0.97f, 1.06f), 0.7f);
        }

        /// <summary>
        /// Time won or lost, in the mode where that is the whole resource.
        ///
        /// Its own pair of sounds rather than the life chimes borrowed. The first
        /// version borrowed them and it was defensible — they already mean *the
        /// resource moved* — but they are chimes, and a chime says "points". What is
        /// actually happening here is a clock being wound or letting go, and the mode
        /// went to the trouble of drawing a dial instead of printing a number
        /// precisely so that the mechanism would be the thing the player perceives.
        /// The sound had to make the same move.
        ///
        /// Both are quiet. This fires on nearly every throw rather than a few times a
        /// run, so it sits under the catch it is commenting on.
        /// </summary>
        private void OnTimeAwarded(float seconds)
        {
            if (Mathf.Abs(seconds) < 0.01f)
            {
                return;
            }

            if (seconds > 0f)
            {
                // A bigger award winds a little further up. Small enough that it reads
                // as the same sound with more behind it rather than as a second sound.
                float pitch = Mathf.Lerp(1f, 1.1f, Mathf.InverseLerp(1f, 4f, seconds));
                Play(clockWind, pitch, 0.7f);
                return;
            }

            Play(clockUnwind, Random.Range(0.97f, 1.03f), 0.7f);
        }

        private void BuildTick()
        {
            GameObject host = new GameObject("Clock Tick");
            host.transform.SetParent(transform, false);

            tickSource = host.AddComponent<AudioSource>();
            tickSource.playOnAwake = false;
            tickSource.loop = false;
            tickSource.spatialBlend = 0f;
            tickSource.volume = 1f;

            // Distortion before the low pass, so the filter rounds off whatever edge the
            // distortion adds. The other way round, "slightly distorted" came out harsh.
            tickDistortion = host.AddComponent<AudioDistortionFilter>();
            tickDistortion.distortionLevel = 0f;

            tickFilter = host.AddComponent<AudioLowPassFilter>();
            tickFilter.cutoffFrequency = 1600f;
        }

        private void OnTimeChanged(float remaining, float max)
        {
            clockRemaining = remaining;
            lastClockHeard = Time.unscaledTime;
        }

        /// <summary>
        /// The time attack clock ticking, for exactly as long as it runs.
        ///
        /// Driven by the clock's own event rather than by the mode: TimeChanged is
        /// raised on every frame the clock drains and on no other, so the tick stops by
        /// itself for a round's intro, for the results, for an advert, and the moment
        /// the player walks away with BACK. There is no state here to forget to reset.
        ///
        /// Unscaled time, so a hit stop does not slow the clock down.
        /// </summary>
        private void Update()
        {
            bool running = Time.unscaledTime - lastClockHeard < ClockGrace && Time.timeScale > 0f;

            if (!running || Muted || tickSource == null)
            {
                // Ready to tick the moment the clock starts again, rather than a second
                // into it.
                tickTimer = 0f;
                return;
            }

            // Eased, not snapped. Time is paid back in lumps, and a voice that jumped half
            // an octave on every catch would be a second sound rather than the same clock.
            tension = Mathf.MoveTowards(
                tension, ClockTension.TensionFor(clockRemaining), Time.unscaledDeltaTime * 0.8f);

            tickTimer -= Time.unscaledDeltaTime;

            if (tickTimer > 0f)
            {
                return;
            }

            // After a long hitch, one tick rather than a burst of the ones it missed.
            tickTimer = tickTimer < -ClockTension.TickInterval
                ? ClockTension.TickInterval
                : tickTimer + ClockTension.TickInterval;

            ClockVoice voice = ClockTension.VoiceFor(tension);

            tickSource.pitch = voice.Pitch;
            tickFilter.cutoffFrequency = voice.Cutoff;
            tickDistortion.distortionLevel = voice.Distortion;
            tickSource.PlayOneShot(tickIsTock ? clockTock : clockTick, masterVolume * voice.Volume);

            tickIsTock = !tickIsTock;
        }

        private void OnThrown(ThrowLaunched payload)
        {
            Play(whoosh, Mathf.Lerp(1.15f, 0.86f, payload.NormalisedForce), 0.55f);
        }

        private void OnImpact(RingImpact payload)
        {
            float strength = Mathf.Clamp01(payload.Speed / 6f);

            if (strength < 0.08f)
            {
                return;
            }

            if (payload.HitPeg)
            {
                Play(knock, Random.Range(0.92f, 1.12f), 0.3f + strength * 0.7f);
                return;
            }

            Play(thud, Random.Range(0.9f, 1.1f), 0.2f + strength * 0.5f);
        }

        private void OnResolved(ThrowResolved payload)
        {
            float pitch = juiceConfig == null ? 1f : juiceConfig.ComboPitch(payload.Combo);

            switch (payload.Outcome)
            {
                case ThrowOutcome.PerfectRinger:
                    Play(perfectChime, pitch, 0.95f);
                    Play(rattle, Random.Range(0.98f, 1.06f), 0.8f);
                    break;
                case ThrowOutcome.Ringer:
                    Play(catchChime, pitch, 0.85f);

                    // Lower and quieter than a perfect's: the ring came in off the peg
                    // rather than dropping cleanly down it.
                    Play(rattle, Random.Range(0.86f, 0.94f), 0.62f);
                    break;
                case ThrowOutcome.Bounce:
                    Play(thud, Random.Range(0.95f, 1.05f), 0.45f);
                    break;
                case ThrowOutcome.Miss:
                    Play(thud, Random.Range(0.82f, 0.92f), 0.35f);
                    break;
            }
        }

        private void OnLevelEnded(LevelResult result)
        {
            if (result.Completed)
            {
                Play(fanfare, 1f, 0.9f);
                return;
            }

            Play(thud, 0.72f, 0.5f);
        }

        /// <summary>
        /// An award starting to arrive on screen, sounded by how far the player went out
        /// of their way for it. Play is left to the landing ticks unless it is a proper
        /// payout; an advert the player chose gets the fanfare; a boosted share gets the
        /// life fanfare, a fourth higher, with the sparkle over it — the loudest thing the
        /// title screen ever does, because it is the best thing that happens there.
        ///
        /// Driven by the interface's picture rather than by the award itself, because a
        /// share pays while Android's share sheet is covering the game.
        /// </summary>
        private void OnTicketShowerStarted(TicketSource source, int amount)
        {
            switch (source)
            {
                case TicketSource.BoostedShare:
                    Play(lifeGained, 1f, 0.95f);
                    Play(sparkle, 1.06f, 0.8f);
                    break;

                case TicketSource.RewardedAd:
                    Play(fanfare, 1f, 0.9f);
                    Play(sparkle, 1f, 0.7f);
                    break;

                case TicketSource.Purchase:
                    // Paying is not being paid. The purchase already has its own sound,
                    // and the tickets leaving still tick one by one as they fly, so a
                    // payout fanfare here would celebrate the player's money going away.
                    break;

                default:
                    // Only the big payouts. A perfect ringer pays three tickets and already
                    // has a chime and a rattle of its own; a third sound on the same frame
                    // is clutter, not reward.
                    if (amount >= 10)
                    {
                        PlayTickets();
                    }

                    break;
            }
        }

        /// <summary>Each ticket reaching the balance, climbing in pitch so a pile of them resolves.</summary>
        private void OnTicketLanded(int index, int count)
        {
            if (Muted || ticketSource == null || ticketTick == null)
            {
                return;
            }

            float along = count <= 1 ? 1f : (float)index / (count - 1);
            ticketSource.pitch = Mathf.Lerp(1f, 1.5f, along);
            ticketSource.PlayOneShot(ticketTick, masterVolume * 0.34f);
        }

        private void OnRoundCleared(int round, int bonus)
        {
            // A semitone higher every few rounds, so a long run audibly climbs. Capped
            // before it turns shrill, the same way the combo chime is.
            Play(fanfare, Mathf.Min(1f + round * 0.012f, 1.3f), 0.85f);
        }

        /// <summary>
        /// One perfect banked towards the next life.
        ///
        /// The pitch climbs with the streak, so five perfects in a row are a rising
        /// figure that resolves on the fifth rather than the same blip five times.
        /// That is the whole trick: the sound tells the player how close they are
        /// without anybody reading the pips, and the last one *wants* to be finished.
        ///
        /// Quiet, and only on a gain. A miss resets the streak too, and a sound there
        /// would be the game pinging at somebody for losing.
        /// </summary>
        private void OnPerfectStreakChanged(int streak, int needed)
        {
            bool banked = streak > previousStreak && streak > 0;
            previousStreak = streak;

            if (!banked)
            {
                return;
            }

            float climb = needed <= 1 ? 1f : (streak - 1) / (float)(needed - 1);

            Play(blip, Mathf.Lerp(1.1f, 1.72f, climb), 0.5f);
        }

        private void OnStreakRewarded(bool granted)
        {
            previousStreak = 0;
            Play(granted ? lifeGained : perfectChime, granted ? 1f : 1.35f, 0.9f);
        }

        private void OnLivesChanged(int lives, int max)
        {
            // Only losses. A gain has already announced itself through the streak, and
            // playing both stacks two cues on the same frame. A full bar is the start
            // of a run, which must not sound like losing one.
            if (lives >= previousLives || lives >= max)
            {
                previousLives = lives;
                return;
            }

            previousLives = lives;

            // The last life gets a lower, louder read, so the player hears the run
            // getting dangerous rather than having to watch the corner of the screen.
            bool critical = lives <= 1;
            Play(lifeLost, critical ? 0.82f : 1f, critical ? 0.85f : 0.65f);
        }

        private void OnRunEnded(EndlessResult result)
        {
            // Forgotten with the run. A run that ended three perfects into a streak
            // would otherwise leave the next run's first four banked perfects silent,
            // because each is compared against a number from a game that is over.
            previousStreak = 0;

            Play(thud, 0.62f, 0.6f);
        }

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (clip == null || Muted || voices == null)
            {
                return;
            }

            AudioSource source = voices[voiceCursor];
            voiceCursor = (voiceCursor + 1) % voices.Length;

            source.pitch = pitch;
            source.volume = masterVolume * Mathf.Clamp01(volume);
            source.clip = clip;
            source.Play();
        }
    }
}
