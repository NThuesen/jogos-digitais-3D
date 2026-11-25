using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// The freeze on the moment of a catch.
    ///
    /// Hit stop is the single cheapest way to make an impact feel like it landed. It
    /// works because it briefly removes the player's ability to look away from the
    /// thing that just happened.
    ///
    /// Time.fixedDeltaTime is scaled along with the time scale. Without that, physics
    /// keeps stepping at its normal rate through the freeze and the ring visibly
    /// stutters out of it instead of snapping.
    /// </summary>
    public sealed class HitStop : MonoBehaviour
    {
        [SerializeField] private JuiceConfig juiceConfig;

        private float baseFixedDeltaTime;
        private float remaining;
        private bool active;
        private float appliedTimeScale;

        private void Awake()
        {
            baseFixedDeltaTime = Time.fixedDeltaTime;
        }

        public void SetConfig(JuiceConfig value)
        {
            juiceConfig = value;
        }

        /// <summary>
        /// Freezes for <paramref name="duration"/> seconds of real time. Calling again
        /// while a freeze is running extends it rather than restarting, so a burst of
        /// impacts cannot stack into a hang.
        /// </summary>
        public void Freeze(float duration)
        {
            if (duration <= 0f || juiceConfig == null)
            {
                return;
            }

            remaining = Mathf.Max(remaining, duration);

            if (active)
            {
                return;
            }

            active = true;
            appliedTimeScale = juiceConfig.HitStopTimeScale;
            Time.timeScale = appliedTimeScale;
            Time.fixedDeltaTime = baseFixedDeltaTime * Time.timeScale;
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            // Unscaled: the freeze has to end even though the game's clock is stopped.
            remaining -= Time.unscaledDeltaTime;

            if (remaining > 0f)
            {
                return;
            }

            Release();
        }

        /// <summary>
        /// Hands the clock back, but only if this freeze is still the thing holding it.
        ///
        /// An advert stops the clock outright while it is on screen, and the freeze
        /// runs on unscaled time so it expires underneath one. Restoring "normal" there
        /// would set the game running full speed behind a full-screen ad.
        /// </summary>
        private void Release()
        {
            active = false;
            remaining = 0f;

            if (!Mathf.Approximately(Time.timeScale, appliedTimeScale))
            {
                return;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
        }

        private void OnDisable()
        {
            // Leaving the time scale stopped because an object was disabled mid-freeze
            // would hang the whole game.
            if (!active)
            {
                return;
            }

            Release();
        }
    }
}
