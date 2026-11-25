using NUnit.Framework;
using SlipperyZlope.RingToss.UI;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// How the splash video is laid over a phone's screen.
    ///
    /// The first version stretched it to the screen, and on the owner's 9:19.5 phone a
    /// 9:16 video came out 22% taller than it was drawn: the peg's ball turned into an egg.
    /// Phones are not one shape, so the video is rendered taller than any of them and
    /// scaled *uniformly* until it covers the screen, the overflow cropped.
    /// </summary>
    public sealed class BootSplashLayoutTests
    {
        private static readonly Vector2 Video = new Vector2(1080f, 2520f);

        [Test]
        public void On_the_owners_phone_the_video_is_drawn_at_its_own_size_and_cropped_top_and_bottom()
        {
            // A Samsung S24 FE is 1080x2340: the width fits exactly, and 90 px of dark
            // stage are cropped above and below.
            Assert.AreEqual(new Vector2(1080f, 2520f), BootSplash.CoverSize(new Vector2(1080f, 2340f), Video));
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1440f, 3200f)]
        [TestCase(1080f, 2640f)]
        [TestCase(720f, 1280f)]
        public void The_video_keeps_its_proportions_and_covers_the_screen(float width, float height)
        {
            Vector2 drawn = BootSplash.CoverSize(new Vector2(width, height), Video);

            Assert.AreEqual(Video.x / Video.y, drawn.x / drawn.y, 1e-4f, "The video was stretched.");
            Assert.GreaterOrEqual(drawn.x, width - 0.01f, "Bare screen shows at the sides.");
            Assert.GreaterOrEqual(drawn.y, height - 0.01f, "Bare screen shows above or below.");
            Assert.IsTrue(Mathf.Abs(drawn.x - width) < 0.01f || Mathf.Abs(drawn.y - height) < 0.01f,
                "Scaled further than covering the screen needs, which crops more than it has to.");
        }
    }
}
