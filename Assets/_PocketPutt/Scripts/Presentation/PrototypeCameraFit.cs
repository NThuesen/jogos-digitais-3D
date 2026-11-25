using UnityEngine;

namespace PocketPutt.Presentation
{
    /// <summary>
    /// Preserves the whole miniature course on narrow Android screens and
    /// desktop Game View, without altering gameplay or the aim raycast.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PrototypeCameraFit : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float stageWidth = 3.97f;
        [SerializeField, Min(0.1f)] private float stageLength = 8.29f;
        [SerializeField, Min(0f)] private float horizontalPadding = 0.70f;
        [SerializeField, Min(0f)] private float verticalPadding = 0.80f;

        private Camera viewCamera;
        private int lastWidth = -1;
        private int lastHeight = -1;

        public void Configure(float width, float length)
        {
            stageWidth = width;
            stageLength = length;
            Fit();
        }

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            Fit();
        }

        private void OnEnable() => Fit();

        private void LateUpdate()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height)
                Fit();
        }

        private void Fit()
        {
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>();

            if (viewCamera == null || Screen.height <= 0 || Screen.width <= 0)
                return;

            float aspect = (float)Screen.width / Screen.height;
            float heightRequired = (stageLength + verticalPadding) * 0.5f;
            float widthRequired = (stageWidth + horizontalPadding) / (2f * aspect);
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = Mathf.Max(heightRequired, widthRequired);

            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }
    }
}
