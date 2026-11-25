using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// How fast the floor's light builds, on the config that actually ships.
    ///
    /// Read from the asset rather than from a fresh instance, because the asset is what
    /// the game plays with — and because this project has been bitten more than once by
    /// the asset, the bootstrap and the C# defaults each saying something different.
    /// </summary>
    public sealed class FloorGlowTuningTests
    {
        private const string JuicePath = "Assets/_Game/Data/JuiceConfig.asset";

        private static JuiceConfig Shipped()
        {
            JuiceConfig config = AssetDatabase.LoadAssetAtPath<JuiceConfig>(JuicePath);
            Assert.IsNotNull(config, $"No JuiceConfig at {JuicePath}.");
            return config;
        }

        /// <summary>
        /// "preciso que fique mais fácil de acumular o brilho, demora muito" and "deixe o
        /// brilho conforme os prints": the brightest frame the owner was shown, 0.67, now
        /// reached by the fourth perfect in a row rather than the sixth.
        /// </summary>
        [Test]
        public void The_light_peaks_at_the_approved_brightness_by_the_fourth_perfect()
        {
            JuiceConfig juice = Shipped();

            Assert.AreEqual(0.67f, juice.FloorGlowFor(4), 0.005f,
                "Four perfects in a row do not reach the peak the owner approved.");
            Assert.AreEqual(juice.FloorGlowFor(4), juice.FloorGlowFor(50), 1e-5f,
                "The light keeps climbing past the fourth perfect.");
            Assert.Less(juice.FloorGlowFor(3), juice.FloorGlowFor(4), "The fourth perfect adds nothing.");
        }

        /// <summary>
        /// A fresh config — what the play mode tests build, and what anything created in
        /// code gets — has to light the floor the way the shipped one does.
        /// </summary>
        [Test]
        public void A_fresh_config_glows_like_the_shipped_one()
        {
            JuiceConfig shipped = Shipped();
            JuiceConfig fresh = ScriptableObject.CreateInstance<JuiceConfig>();

            try
            {
                for (int streak = 0; streak <= 8; streak++)
                {
                    Assert.AreEqual(shipped.FloorGlowFor(streak), fresh.FloorGlowFor(streak), 1e-5f,
                        $"The shipped glow and the default disagree at {streak} in a row.");
                }

                Assert.AreEqual(shipped.FloorGlowRimBoost, fresh.FloorGlowRimBoost, 1e-5f, "Rim boost drifted.");
                Assert.AreEqual(shipped.FloorGlowSkirt, fresh.FloorGlowSkirt, 1e-5f, "Spill drifted.");
                Assert.AreEqual(shipped.FloorGlowRise, fresh.FloorGlowRise, 1e-5f, "Rise drifted.");
                Assert.AreEqual(shipped.FloorGlowFall, fresh.FloorGlowFall, 1e-5f, "Fall drifted.");
                Assert.AreEqual(shipped.FloorGlowFlash, fresh.FloorGlowFlash, 1e-5f, "Flash drifted.");
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }
    }
}
