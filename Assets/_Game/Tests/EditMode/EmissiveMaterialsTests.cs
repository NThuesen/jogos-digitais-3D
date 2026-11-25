using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Every material whose emission the game drives at runtime has to carry the emission
    /// keyword in the asset itself.
    ///
    /// The game switches emission on per renderer from code — the peg caps pulse, the
    /// ring glows, and the deck lights with a streak of perfects. None of that shows on
    /// a phone unless the shader variant with emission is in the build, and a build
    /// only keeps the variants some shipped material asset uses. Turning the keyword on
    /// at runtime does nothing when the variant was stripped: the editor draws the glow
    /// and the handset silently does not.
    ///
    /// The keyword kept vanishing from the assets because URP recomputes it whenever it
    /// validates a material, and keeps it only while the GI flags carry an emissive bit
    /// (BaseShaderGUI.SetMaterialKeywords, `flags &amp; AnyEmissive`). Flagged
    /// EmissiveIsBlack it was dropped; flagged None — the first fix, reasoned from the
    /// wrong rule — it was dropped too, and this test caught it on the next import. The
    /// assets are flagged RealtimeEmissive with a black colour: the keyword stays, and
    /// they add no light of their own.
    /// </summary>
    public sealed class EmissiveMaterialsTests
    {
        [TestCase("Assets/_Game/Art/Materials/SceneryLit.mat")]
        [TestCase("Assets/_Game/Art/Materials/PegCapMaterial.mat")]
        [TestCase("Assets/_Game/Art/Materials/RingMaterial.mat")]
        public void Ships_the_emission_variant_without_emitting_anything_itself(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, $"No material at {path}.");

            Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"),
                $"{path} has lost the emission keyword. Anything that glows through it at " +
                "runtime will glow in the editor and not on a phone.");

            Assert.AreNotEqual(MaterialGlobalIlluminationFlags.None,
                material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive,
                $"{path} carries no emissive GI flag, which is what makes URP drop the keyword.");

            Color emission = material.GetColor("_EmissionColor");
            Assert.AreEqual(0f, emission.maxColorComponent, 1e-4f,
                $"{path} emits light of its own; the glow is meant to come from the renderers.");
        }
    }
}
