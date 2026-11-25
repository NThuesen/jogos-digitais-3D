using System.IO;
using SlipperyZlope.RingToss.Audio;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Writes the game's own sound effects out as WAV files.
    ///
    /// The game synthesises every clip at load and ships no audio assets at all,
    /// which is the right trade for the build and an awkward one for review: there
    /// is nothing to listen to without running it. This dumps exactly what
    /// <c>AudioDirector</c> builds, at the same frequencies and durations, so the
    /// mix can be heard, measured and kept as a reference.
    ///
    /// The output is a *record*, not a source. Editing a WAV here changes nothing —
    /// the sound lives in <c>ProceduralAudio</c>.
    /// </summary>
    public static class AudioExport
    {
        [MenuItem("SlipperyZlope/Export Audio To WAV")]
        public static void Export()
        {
            string directory = EditorUtility.SaveFolderPanel(
                "Export the game's synthesised audio", string.Empty, "audio");

            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            ExportTo(directory);
        }

        /// <summary>The same thing, for a headless run.</summary>
        public static void ExportFromCommandLine()
        {
            string directory = System.Environment.GetEnvironmentVariable("RINGTOSS_AUDIO_DIR")
                               ?? Path.Combine(Application.dataPath, "..", "audio-export");

            ExportTo(directory);
        }

        private static void ExportTo(string directory)
        {
            Directory.CreateDirectory(directory);

            // Mirrors AudioDirector.Build exactly. Kept as a list rather than read off
            // the component because the component builds them in Awake and this has to
            // run without a scene.
            (string Name, AudioClip Clip)[] clips =
            {
                ("sfx_whoosh", ProceduralAudio.Whoosh("sfx_whoosh")),
                ("sfx_catch", ProceduralAudio.Chime("sfx_catch", 392f, 1.6f)),
                ("sfx_perfect", ProceduralAudio.Chime("sfx_perfect", 587.33f, 1.9f, bright: true)),
                ("sfx_knock", ProceduralAudio.Knock("sfx_knock")),
                ("sfx_thud", ProceduralAudio.Thud("sfx_thud")),
                ("sfx_fanfare", ProceduralAudio.Fanfare("sfx_fanfare")),
                ("sfx_blip", ProceduralAudio.Blip("sfx_blip")),
                ("sfx_life_gained", ProceduralAudio.Fanfare("sfx_life_gained", 698.46f)),
                ("sfx_life_lost", ProceduralAudio.Chime("sfx_life_lost", 174.61f, 1.1f)),
                ("sfx_rattle", ProceduralAudio.Rattle("sfx_rattle")),
                ("sfx_tickets", ProceduralAudio.Sparkle("sfx_tickets")),
                ("sfx_confirm", ProceduralAudio.Confirm("sfx_confirm")),
                ("sfx_clock_wind", ProceduralAudio.ClockWind("sfx_clock_wind")),
                ("sfx_clock_unwind", ProceduralAudio.ClockUnwind("sfx_clock_unwind")),
                ("sfx_clock_tick", ProceduralAudio.ClockTick("sfx_clock_tick", false)),
                ("sfx_clock_tock", ProceduralAudio.ClockTick("sfx_clock_tock", true)),
                ("amb_fairground", ProceduralAudio.Ambience("amb_fairground")),
            };

            int written = 0;

            foreach ((string name, AudioClip clip) in clips)
            {
                if (clip == null)
                {
                    Debug.LogWarning($"[Audio] {name} came back null and was skipped.");
                    continue;
                }

                File.WriteAllBytes(Path.Combine(directory, name + ".wav"), Encode(clip));
                written++;
            }

            Debug.Log($"[Audio] Wrote {written} clips to {directory}.");
        }

        /// <summary>
        /// A 16-bit PCM WAV. Written by hand because Unity has no encoder, and the
        /// alternative — adding an audio import package so a review tool can run — is
        /// the tail wagging the dog.
        /// </summary>
        private static byte[] Encode(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            int dataBytes = samples.Length * 2;
            byte[] file = new byte[44 + dataBytes];

            void Ascii(int at, string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    file[at + i] = (byte)text[i];
                }
            }

            Ascii(0, "RIFF");
            System.BitConverter.GetBytes(36 + dataBytes).CopyTo(file, 4);
            Ascii(8, "WAVEfmt ");
            System.BitConverter.GetBytes(16).CopyTo(file, 16);
            System.BitConverter.GetBytes((short)1).CopyTo(file, 20);
            System.BitConverter.GetBytes((short)clip.channels).CopyTo(file, 22);
            System.BitConverter.GetBytes(clip.frequency).CopyTo(file, 24);
            System.BitConverter.GetBytes(clip.frequency * clip.channels * 2).CopyTo(file, 28);
            System.BitConverter.GetBytes((short)(clip.channels * 2)).CopyTo(file, 32);
            System.BitConverter.GetBytes((short)16).CopyTo(file, 34);
            Ascii(36, "data");
            System.BitConverter.GetBytes(dataBytes).CopyTo(file, 40);

            for (int i = 0; i < samples.Length; i++)
            {
                short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                System.BitConverter.GetBytes(value).CopyTo(file, 44 + i * 2);
            }

            return file;
        }
    }
}
