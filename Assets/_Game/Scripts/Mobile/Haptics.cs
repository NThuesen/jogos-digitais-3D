using UnityEngine;

namespace SlipperyZlope.RingToss.Mobile
{
    public enum HapticStrength
    {
        Light,
        Medium,
        Heavy
    }

    /// <summary>
    /// Short vibration pulses on impact.
    ///
    /// Handheld.Vibrate is a fixed half-second buzz on Android, which is far too long
    /// to be feedback for a ring landing. This goes to the platform Vibrator directly
    /// so the pulse can be a few tens of milliseconds, which is what reads as a tap
    /// rather than as an alarm.
    ///
    /// Everything here is a no-op off device, so calling it from shared code is safe.
    ///
    /// Nothing here may ever throw. These pulses are raised from inside the throw
    /// resolution, so an exception escaping a pulse unwinds the caller mid-resolution
    /// and leaves the session state machine stranded — which is a hung game, caused by
    /// a buzz that failed. Acquiring the vibrator was already guarded; using it was
    /// not, and using it is the half that needs a runtime permission and so the half
    /// that actually fails.
    /// </summary>
    public static class Haptics
    {
        private static bool enabled = true;
        private static bool initialised;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass vibrationEffectClass;
        private static int androidApiLevel;
#endif

        public static bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public static void Pulse(HapticStrength strength)
        {
            if (!enabled)
            {
                return;
            }

            Initialise();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (vibrator == null)
            {
                return;
            }

            long milliseconds = strength switch
            {
                HapticStrength.Light => 12L,
                HapticStrength.Medium => 26L,
                _ => 42L
            };

            try
            {
                // Amplitude control arrived in API 26. Below that only a duration can
                // be asked for, so the strengths collapse into different lengths.
                if (androidApiLevel >= 26 && vibrationEffectClass != null)
                {
                    int amplitude = strength switch
                    {
                        HapticStrength.Light => 70,
                        HapticStrength.Medium => 150,
                        _ => 255
                    };

                    using AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", milliseconds, amplitude);
                    vibrator.Call("vibrate", effect);
                    return;
                }

                vibrator.Call("vibrate", milliseconds);
            }
            catch (System.Exception exception)
            {
                // Dropped for the rest of the session rather than retried. The usual
                // cause is a missing VIBRATE permission, which will not resolve itself,
                // and retrying would throw on every impact for the whole run.
                Debug.LogWarning($"[Haptics] Pulse failed, haptics disabled: {exception.Message}");
                vibrator = null;
            }
#endif
        }

        private static void Initialise()
        {
            if (initialised)
            {
                return;
            }

            initialised = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION");
                androidApiLevel = version.GetStatic<int>("SDK_INT");

                using AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (androidApiLevel >= 26)
                {
                    vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Haptics] Unavailable: {exception.Message}");
                vibrator = null;
            }
#endif
        }
    }
}
