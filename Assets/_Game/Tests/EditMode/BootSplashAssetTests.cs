using System.IO;
using NUnit.Framework;
using SlipperyZlope.RingToss.UI;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The splash the game opens with is a rendered video, and a video is a file: it can
    /// be missing from a checkout, missed by a build, or renamed in the animations repo
    /// without anybody here noticing. The splash itself is written to shrug that off —
    /// a missing file just means no splash — which is exactly why something has to fail
    /// loudly when it goes, or the studio's opening would quietly stop happening.
    /// </summary>
    public sealed class BootSplashAssetTests
    {
        private const string Directory = "Assets/StreamingAssets";

        [Test]
        public void The_splash_video_ships_in_streaming_assets()
        {
            string path = Path.Combine(Directory, BootSplash.FileName);

            Assert.IsTrue(File.Exists(path),
                $"No splash video at {path}. It is rendered from ../animations: " +
                "renders/ringtoss-splash/ringtoss-splash.mp4.");

            long bytes = new FileInfo(path).Length;

            // A megabyte-ish. Far smaller means a truncated copy; far larger means
            // somebody dropped the master render into the APK.
            Assert.Greater(bytes, 200_000, "The splash video is too small to be the render.");
            Assert.Less(bytes, 8_000_000, "The splash video is too big to ship in the APK.");
        }

        /// <summary>
        /// The splash is cropped to fit the phone, never stretched (BootSplash.CoverSize),
        /// which only works if the video is taller than the phones it plays on: then what
        /// is cropped is spare stage above and below. A 9:16 render would lose the sides of
        /// the composition on every modern phone instead.
        /// </summary>
        [Test]
        public void The_splash_video_is_rendered_taller_than_the_phones_it_plays_on()
        {
            Vector2 size = VideoSize(Path.Combine(Directory, BootSplash.FileName));

            Assert.Greater(size.x, 0f, "No video track with a size was found in the splash.");
            Assert.GreaterOrEqual(size.y / size.x, 21f / 9f - 0.001f,
                $"The splash is {size.x}x{size.y}; render it at 9:21 (1080x2520) or taller.");
        }

        /// <summary>
        /// Width and height of the first track that has one, from its 'tkhd' box — the
        /// audio track's is zero. Version 0 and 1 boxes differ only in the widths of their
        /// time fields.
        /// </summary>
        private static Vector2 VideoSize(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            for (int i = 4; i + 4 < bytes.Length; i++)
            {
                if (bytes[i] != 't' || bytes[i + 1] != 'k' || bytes[i + 2] != 'h' || bytes[i + 3] != 'd')
                {
                    continue;
                }

                int version = bytes[i + 4];
                int offset = i + 4 + (version == 1 ? 88 : 76);

                if (offset + 8 > bytes.Length)
                {
                    break;
                }

                float width = ReadFixed(bytes, offset);
                float height = ReadFixed(bytes, offset + 4);

                if (width > 0f && height > 0f)
                {
                    return new Vector2(width, height);
                }
            }

            return Vector2.zero;
        }

        private static float ReadFixed(byte[] bytes, int at) =>
            ((bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3]) / 65536f;

        [Test]
        public void The_video_module_is_in_the_manifest()
        {
            string manifest = File.ReadAllText("Packages/manifest.json");

            // Unity trims unused modules, and this one is only used by the splash. Without
            // it, VideoPlayer does not resolve and nothing compiles.
            StringAssert.Contains("com.unity.modules.video", manifest,
                "The video module is missing; BootSplash cannot compile without it.");
        }
    }
}
