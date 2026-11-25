using UnityEngine;

namespace SlipperyZlope.RingToss.Mobile
{
    /// <summary>
    /// Locks the frame rate, raising it on high refresh rate screens.
    ///
    /// The whole project is about smoothness, so the frame rate is set explicitly
    /// rather than left to the platform default, and vSync is disabled because on
    /// mobile it overrides targetFrameRate.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class FrameRateController : MonoBehaviour
    {
        [SerializeField] private int baseTargetFrameRate = 60;

        [Tooltip("Screens reporting at least this refresh rate get the higher target.")]
        [SerializeField] private int highRefreshRateThreshold = 90;

        [SerializeField] private int highTargetFrameRate = 120;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;

            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            bool isHighRefreshRate = refreshRate >= highRefreshRateThreshold;

            Application.targetFrameRate = isHighRefreshRate ? highTargetFrameRate : baseTargetFrameRate;
        }
    }
}
