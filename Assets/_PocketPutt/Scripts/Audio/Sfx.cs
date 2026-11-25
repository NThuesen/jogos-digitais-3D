using System;
using UnityEngine;

namespace PocketPutt.Audio
{
    /// <summary>Fully procedural sound effects and music: no audio assets, works offline, tiny build size.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }

        const int Rate = 24000;
        public bool SoundOn = true;
        public bool MusicOn = true;
        public bool VibrationOn = true;

        AudioSource[] pool;
        int next;
        AudioSource music;
        AudioClip putt, wall, drop, click, whoosh, fanfare, trophy, unlock, sand, splash;
        AudioClip[] stars;

        public static Sfx Create(Transform parent)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Sfx>();
            s.Init();
            return s;
        }

        void Init()
        {
            I = this;
            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = 0.32f;
            Synthesize();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (music != null)
            {
                music.Stop();
                Destroy(music.clip);
            }
            foreach (AudioClip clip in new[] { putt, wall, drop, click, whoosh, fanfare, trophy, unlock, sand, splash })
                Destroy(clip);
            if (stars != null)
                foreach (AudioClip clip in stars) Destroy(clip);
        }

        void Synthesize()
        {
            var rng = new System.Random(7);
            putt = Make("putt", 0.24f, t =>
            {
                float f = Mathf.Lerp(240f, 70f, Mathf.Min(1f, t / 0.12f));
                float body = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 20f);
                float click = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 300f) * 0.7f;
                return (body + click) * 0.95f;
            });
            wall = Make("wall", 0.14f, t =>
            {
                float tone = Mathf.Sin(2 * Mathf.PI * 560f * t) * Mathf.Exp(-t * 45f) + Mathf.Sin(2 * Mathf.PI * 1190f * t) * Mathf.Exp(-t * 70f) * 0.4f;
                float nz = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 220f) * 0.5f;
                return (tone + nz) * 0.8f;
            });
            drop = Make("drop", 0.9f, t =>
            {
                float v = 0f;
                // rattle: three decreasing bounces
                float[] times = { 0f, 0.09f, 0.16f, 0.21f };
                for (int i = 0; i < times.Length; i++)
                {
                    float tt = t - times[i];
                    if (tt < 0) continue;
                    v += Mathf.Sin(2 * Mathf.PI * (880f - i * 120f) * tt) * Mathf.Exp(-tt * 55f) * (0.6f - i * 0.12f);
                }
                float tp = t - 0.27f;
                if (tp > 0)
                {
                    float f = Mathf.Lerp(420f, 240f, Mathf.Min(1f, tp / 0.3f));
                    v += Mathf.Sin(2 * Mathf.PI * f * tp) * Mathf.Exp(-tp * 7f) * 0.8f;
                    v += Mathf.Sin(2 * Mathf.PI * f * 2.01f * tp) * Mathf.Exp(-tp * 11f) * 0.25f;
                }
                return v * 0.7f;
            });
            click = Make("click", 0.06f, t => Mathf.Sin(2 * Mathf.PI * 1500f * t) * Mathf.Exp(-t * 90f) * 0.6f);
            whoosh = Make("whoosh", 0.35f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f));
                float nz = ((float)rng.NextDouble() * 2f - 1f);
                float tone = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(300f, 900f, t / 0.35f) * t);
                return (nz * 0.25f + tone * 0.35f) * env * 0.6f;
            });
            stars = new AudioClip[3];
            float[] notes = { 523.25f, 659.25f, 783.99f };
            for (int i = 0; i < 3; i++)
            {
                float f0 = notes[i];
                stars[i] = Make("star" + i, 1.0f, t => Bell(f0, t) * 0.7f);
            }
            fanfare = Make("fanfare", 1.5f, t =>
            {
                float[] seq = { 523.25f, 659.25f, 783.99f, 1046.5f };
                float v = 0f;
                for (int i = 0; i < seq.Length; i++)
                {
                    float tt = t - i * 0.13f;
                    if (tt < 0) continue;
                    v += Bell(seq[i], tt) * 0.45f;
                }
                if (t > 0.5f) { float tt = t - 0.5f; v += (Bell(523.25f, tt) + Bell(659.25f, tt) + Bell(783.99f, tt) + Bell(1046.5f, tt)) * 0.22f; }
                return v;
            });
            trophy = Make("trophy", 3.2f, t =>
            {
                float[] seq = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                float v = 0f;
                for (int i = 0; i < seq.Length; i++)
                {
                    float tt = t - i * 0.16f;
                    if (tt < 0) continue;
                    v += Bell(seq[i], tt) * 0.4f;
                }
                if (t > 1.1f)
                {
                    float tt = t - 1.1f;
                    float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f, 1568f };
                    foreach (float f in chord) v += Bell(f, tt) * 0.22f;
                }
                return v * 0.9f;
            });
            unlock = Make("unlock", 0.7f, t =>
            {
                float v = Bell(659.25f, t) * 0.5f;
                if (t > 0.12f) v += Bell(987.77f, t - 0.12f) * 0.5f;
                return v;
            });
            sand = Make("sand", 0.1f, t => ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 30f) * 0.25f);
            // splash: a soft noise burst over a falling "bloop"; deliberately quieter and lower than the cup drop
            splash = Make("splash", 0.45f, t =>
            {
                float nz = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.35f * Mathf.Min(1f, t / 0.01f);
                float f = Mathf.Lerp(420f, 170f, Mathf.Min(1f, t / 0.16f));
                float bloop = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 11f) * 0.55f;
                return (nz + bloop) * 0.8f;
            });
            music.clip = MakeMusic();
            if (MusicOn) music.Play();
        }

        static float Bell(float f, float t)
        {
            if (t < 0) return 0f;
            return (Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 4.2f) +
                    Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 9f) * 0.35f +
                    Mathf.Sin(2 * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 16f) * 0.12f);
        }

        static AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float fade = Mathf.Clamp01((seconds - t) * 40f);
                data[i] = Mathf.Clamp(f(t) * fade, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Gentle generative loop: kalimba-like pentatonic arpeggios over C - Am - F - G.</summary>
        static AudioClip MakeMusic()
        {
            const float bpm = 92f;
            float beat = 60f / bpm;
            int bars = 8;
            int n = Mathf.RoundToInt(bars * 4 * beat * Rate);
            var data = new float[n];
            var rng = new System.Random(21);
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f, 523.25f }, // C
                new[] { 220.00f, 261.63f, 329.63f, 440.00f }, // Am
                new[] { 174.61f, 261.63f, 349.23f, 440.00f }, // F
                new[] { 196.00f, 293.66f, 392.00f, 493.88f }, // G
            };
            float[] bass = { 65.41f, 55.00f, 43.65f, 49.00f };
            for (int bar = 0; bar < bars; bar++)
            {
                int ch = bar % 4;
                float barT = bar * 4 * beat;
                for (int step = 0; step < 8; step++)
                {
                    float t0 = barT + step * beat * 0.5f;
                    float f = chords[ch][(step * 3 + (bar >= 4 ? 1 : 0)) % 4];
                    if (rng.NextDouble() < 0.18) continue;
                    if (step % 4 == 3 && rng.NextDouble() < 0.5) f *= 2f;
                    AddPluck(data, t0, f, 0.16f, 1.2f);
                }
                AddPluck(data, barT, bass[ch], 0.22f, 1.6f);
                AddPluck(data, barT + 2 * beat, bass[ch] * 1.5f, 0.12f, 1.2f);
                // soft pad
                for (int k = 0; k < 3; k++) AddPad(data, barT, 4 * beat, chords[ch][k] * 0.5f, 0.035f);
            }
            var clip = AudioClip.Create("music", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddPluck(float[] data, float time, float f, float amp, float dur)
        {
            int start = Mathf.RoundToInt(time * Rate);
            int len = Mathf.RoundToInt(dur * Rate);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float v = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 3f * t) * Mathf.Exp(-t * 14f)) * Mathf.Exp(-t * 3.4f);
                v *= Mathf.Min(1f, t * 400f);
                int idx = (start + i) % data.Length;
                data[idx] += v * amp;
            }
        }

        static void AddPad(float[] data, float time, float dur, float f, float amp)
        {
            int start = Mathf.RoundToInt(time * Rate);
            int len = Mathf.RoundToInt(dur * Rate);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 3f) * Mathf.Min(1f, (dur - t) * 3f);
                float v = Mathf.Sin(2 * Mathf.PI * f * t) * env;
                data[(start + i) % data.Length] += v * amp;
            }
        }

        void Play(AudioClip clip, float vol = 1f, float pitch = 1f)
        {
            if (!SoundOn || clip == null) return;
            AudioSource s = pool[next++ % pool.Length];
            s.pitch = pitch;
            s.volume = vol;
            s.clip = clip;
            s.Play();
        }

        public void Putt(float power) { Play(putt, 0.55f + power * 0.45f, Mathf.Lerp(1.15f, 0.85f, power)); Tick(18, power); }
        public void Wall(float impact) { if (impact > 0.4f) Play(wall, Mathf.Clamp01(impact / 6f) * 0.8f + 0.12f, UnityEngine.Random.Range(0.92f, 1.1f)); }
        public void Drop() { Play(drop, 1f); Tick(40, 1f); }
        public void Splash() { Play(splash, 0.85f, UnityEngine.Random.Range(0.95f, 1.05f)); Tick(25, 0.6f); }
        public void Click() => Play(click, 0.7f, UnityEngine.Random.Range(0.96f, 1.04f));
        public void Whoosh() => Play(whoosh, 0.6f);
        public void Star(int i) => Play(stars[Mathf.Clamp(i, 0, 2)], 0.9f);
        public void Fanfare() => Play(fanfare, 0.9f);
        public void Trophy() => Play(trophy, 1f);
        public void Unlock() => Play(unlock, 0.9f);

        public void SetMusic(bool on)
        {
            MusicOn = on;
            if (music == null) return;
            if (on && !music.isPlaying) music.Play();
            else if (!on) music.Pause();
        }

        /// <summary>Short Android haptic pulse; silently ignored elsewhere.</summary>
        public void Tick(int ms, float strength = 1f)
        {
            if (!VibrationOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vib != null) vib.Call("vibrate", (long)Mathf.Max(8, Mathf.RoundToInt(ms * Mathf.Lerp(0.5f, 1f, strength))));
                }
            }
            catch (Exception) { }
#endif
        }

        // Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the Android manifest.
        #if UNITY_ANDROID
        static void KeepVibratePermission() { if (Time.frameCount < 0) Handheld.Vibrate(); }
#endif
    }
}
