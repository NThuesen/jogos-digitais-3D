using SlipperyZlope.RingToss.Config;
using Unity.Cinemachine;
using UnityEngine;

namespace SlipperyZlope.RingToss.Mobile
{
    /// <summary>
    /// Opens the lens on a screen taller than the one the game was composed for, so
    /// the composed width is visible on every device.
    ///
    /// A camera's field of view is **vertical**. That single fact is behind the most
    /// expensive bug in this project's history: a taller screen shows *less* width,
    /// not more, and everything placed against a 9:16 screenshot — the booth's own
    /// posts, the lights they carried, and pegs the player was asked to hit — ended
    /// up off the edge of a real handset.
    ///
    /// The answer then was to compose against the narrowest phone anyone would
    /// plausibly use and accept losing scenery on anything taller. This is the
    /// better answer: solve the angle from the aspect the device actually has, so
    /// <see cref="CameraFraming.NarrowestAspect"/> becomes a floor that is met
    /// everywhere rather than a guess about the market.
    ///
    /// It only ever *widens*. A screen shorter than the design aspect already has the
    /// width and keeps the authored angle, showing a little extra at the sides.
    /// Nothing placed against the framing numbers can be pushed off a screen by this,
    /// which is the property that makes it safe to leave running.
    ///
    /// Written to the Cinemachine camera, not to the Camera. Cinemachine owns the
    /// lens and rewrites it every frame; setting the other one looks like it worked
    /// for exactly one frame.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class CameraFitter : MonoBehaviour
    {
        [Tooltip("The Cinemachine camera whose lens is driven. Falls back to the " +
                 "first one in the scene.")]
        [SerializeField] private CinemachineCamera virtualCamera;

        [Tooltip("Optional. Set when there is no Cinemachine camera to drive.")]
        [SerializeField] private Camera plainCamera;

        private Vector2Int resolution;

        private void Awake()
        {
            if (virtualCamera == null)
            {
                virtualCamera = FindFirstObjectByType<CinemachineCamera>();
            }

            if (plainCamera == null)
            {
                plainCamera = Camera.main;
            }

            Apply();
        }

        /// <summary>
        /// Re-read when the resolution changes rather than once at start.
        ///
        /// It changes on a foldable being opened, on a tablet being rotated, and in
        /// the editor whenever the game view is resized — which is also the only way
        /// anybody sees this working before a device build.
        /// </summary>
        private void Update()
        {
            if (Screen.width != resolution.x || Screen.height != resolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            resolution = new Vector2Int(Screen.width, Screen.height);

            if (resolution.x <= 0 || resolution.y <= 0)
            {
                return;
            }

            float aspect = (float)resolution.x / resolution.y;
            float fieldOfView = CameraFraming.FieldOfViewFor(aspect);

            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                lens.FieldOfView = fieldOfView;
                virtualCamera.Lens = lens;
            }

            if (plainCamera != null)
            {
                plainCamera.fieldOfView = fieldOfView;
            }
        }
    }
}
