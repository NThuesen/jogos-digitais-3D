using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// The dotted arc shown while aiming.
    ///
    /// Drawn as a row of billboarded discs rather than a LineRenderer: dots need to
    /// flow towards the target and shrink into the distance, and a line can only do
    /// that with a scrolling texture, which this project has no textures for.
    ///
    /// The arc is sampled with the same gravity the ring actually flies under, so the
    /// preview cannot drift out of sync with the real throw.
    /// </summary>
    public sealed class TrajectoryPreview : MonoBehaviour
    {
        [SerializeField] private JuiceConfig juiceConfig;
        [SerializeField] private Mesh dotMesh;
        [SerializeField] private Material dotMaterial;

        [Tooltip("How many dots the arc is drawn with.")]
        [Range(6, 48)]
        [SerializeField] private int dotCount = 22;

        [Tooltip("Simulated seconds between consecutive dots.")]
        [SerializeField] private float sampleInterval = 0.052f;

        [Tooltip("Height at which the arc stops being drawn.")]
        [SerializeField] private float groundHeight = 0.03f;

        [Tooltip("How much of the arc is drawn, as a fraction of the flight. Half " +
                 "shows the launch and hides the landing, which is the Angry Birds " +
                 "reading: enough to aim with, not enough to solve with.")]
        [Range(0.15f, 1f)]
        [SerializeField] private float arcFraction = 0.5f;

        [Tooltip("Draw the ring marking where the throw lands. Off: the marker is the " +
                 "answer, and showing it makes the arc decorative.")]
        [SerializeField] private bool showLandingMarker;

        [Tooltip("Height the landing marker is drawn at. Has to clear the board's own " +
                 "surface: the board sits proud of the ground, so a marker left at the " +
                 "arc's cutoff height is buried inside it and never seen at all.")]
        [SerializeField] private float markerHeight = 0.055f;

        [SerializeField] private float dotSize = 0.055f;

        [Tooltip("Distance the dot size is authored against. Dots are scaled by how far " +
                 "they are from the camera relative to this, so every dot is the same " +
                 "size on screen and the taper below is the only thing that shrinks " +
                 "them. Without it the first dot leaves the hand three metres away and " +
                 "the last lands twelve metres away, and the arc reads as a few large " +
                 "blobs trailing off into nothing rather than as a path.")]
        [SerializeField] private float dotReferenceDistance = 7f;

        [Tooltip("Radius of the ring drawn flat on the board where the throw is aimed " +
                 "to land. This is the only part of the preview that answers the " +
                 "question the player is actually asking.")]
        [SerializeField] private float markerRadius = 0.34f;

        [SerializeField] private float markerPulseSpeed = 2.6f;

        [Tooltip("Dots shrink along the arc so the far end reads as further away.")]
        [SerializeField] private float farDotScale = 0.45f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");


        private Transform[] dots;
        private MeshRenderer[] dotRenderers;
        private Transform marker;
        private MeshRenderer markerRenderer;
        private Mesh markerMesh;
        [SerializeField] private Camera billboardCamera;

        private MaterialPropertyBlock block;
        private Color tint = Color.white;
        private float flow;
        private bool visible;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            BuildDots();
            BuildMarker();
            Hide();
        }

        private void BuildDots()
        {
            dots = new Transform[dotCount];
            dotRenderers = new MeshRenderer[dotCount];

            for (int i = 0; i < dotCount; i++)
            {
                GameObject dot = GeometryFactory.CreateMeshObject(
                    $"Dot_{i:D2}", transform, dotMesh, dotMaterial);
                dots[i] = dot.transform;
                dotRenderers[i] = dot.GetComponent<MeshRenderer>();
                dot.SetActive(false);
            }
        }

        /// <summary>
        /// The landing marker lies flat on the board rather than facing the camera.
        /// A billboarded marker reads as a dot floating in space; a flat one reads as
        /// a target painted on the board, which is what the player has to judge depth
        /// against.
        /// </summary>
        private void BuildMarker()
        {
            // An outline rather than the dot mesh: a filled disc this size covers the
            // board exactly where the player is trying to read it.
            markerMesh = MeshFactory.Annulus(0.74f, 1f, 40);

            GameObject host = GeometryFactory.CreateMeshObject(
                "Landing Marker", transform, markerMesh, dotMaterial);

            marker = host.transform;
            markerRenderer = host.GetComponent<MeshRenderer>();
            host.SetActive(false);
        }

        private void OnDestroy()
        {
            if (markerMesh != null)
            {
                Destroy(markerMesh);
            }
        }

        public void Bind(JuiceConfig juice, Mesh mesh, Material material, Camera camera)
        {
            juiceConfig = juice;
            dotMesh = mesh;
            dotMaterial = material;
            billboardCamera = camera;
        }

        public void SetTint(Color color)
        {
            tint = color;
        }

        public void SetCamera(Camera camera)
        {
            billboardCamera = camera;
        }

        /// <summary>Draws the arc the ring would follow if released now.</summary>
        public void Show(
            Vector3 origin,
            Vector3 velocity,
            Vector3 gravity,
            float normalisedForce,
            Vector3 landingPoint)
        {
            visible = true;

            // The preview used to draw the whole arc and put a ring on the spot the
            // throw would land. Between them that is not a hint, it is the answer, and
            // playtesters said what you would expect them to say about a game that
            // answers itself: *"só isso?"*.
            //
            // So the arc stops halfway and the marker is gone. What is left is the
            // launch — direction, and how hard — which is exactly what a slingshot
            // shows you and what Angry Birds shows you. The rest is the player's
            // judgement, which is the thing that was missing.
            //
            // Both are serialized rather than deleted. This is a difficulty decision
            // and difficulty decisions get revisited; the code should not have to be.
            if (showLandingMarker)
            {
                ShowMarker(landingPoint, normalisedForce);
            }
            else if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }

            // Where to stop, in seconds of flight. Solved from the arc rather than
            // taken as a dot count, because the arc gets longer with force and a fixed
            // count would show a hard throw proportionally less than a soft one.
            float cutoff = arcFraction * TimeToGround(origin, velocity, gravity);

            float speed = juiceConfig == null ? 2.4f : juiceConfig.PreviewDotSpeed;
            float pulse = juiceConfig == null ? 0.35f : juiceConfig.PreviewPulseAmount;

            // The flow offset slides the sample times, which makes the dots travel
            // towards the target without any of them actually moving along the arc.
            flow = Mathf.Repeat(flow + Time.deltaTime * speed, 1f);

            Quaternion facing = billboardCamera == null
                ? Quaternion.identity
                : Quaternion.FromToRotation(Vector3.up, -billboardCamera.transform.forward);

            bool ended = false;

            for (int i = 0; i < dots.Length; i++)
            {
                if (ended)
                {
                    dots[i].gameObject.SetActive(false);
                    continue;
                }

                // The first dot never moves. It sits on the ring's centre, so the arc
                // visibly leaves from the middle of the ring and the rest flow out of it.
                // Every dot used to slide, so the nearest was usually already out at the
                // far rim — which is the arc "starting at the edge" the owner saw twice.
                float t = i == 0 ? 0f : (i - 1 + flow) * sampleInterval;
                Vector3 position = origin + velocity * t + 0.5f * gravity * (t * t);

                if (t > cutoff && i > 0)
                {
                    dots[i].gameObject.SetActive(false);
                    ended = true;
                    continue;
                }

                if (position.y <= groundHeight && i > 0)
                {
                    dots[i].gameObject.SetActive(false);
                    ended = true;
                    continue;
                }

                float along = dots.Length <= 1 ? 0f : (float)i / (dots.Length - 1);
                float sizeFade = Mathf.Lerp(1f, farDotScale, along);

                float distance = billboardCamera == null
                    ? dotReferenceDistance
                    : Vector3.Distance(position, billboardCamera.transform.position);

                float perspective = distance / Mathf.Max(0.01f, dotReferenceDistance);

                // Each dot breathes slightly out of phase with its neighbours, so the
                // arc shimmers along its length instead of blinking as one block.
                float breathe = 1f + pulse * 0.5f * Mathf.Sin((flow + along) * Mathf.PI * 4f);

                dots[i].gameObject.SetActive(true);
                dots[i].SetPositionAndRotation(position, facing);
                // The hub a little larger than the dots leaving it: the anchor of the arc.
                float hub = i == 0 ? 1.3f : 1f;
                dots[i].localScale = Vector3.one * (dotSize * perspective * sizeFade * breathe * hub);

                float alpha = Mathf.Lerp(0.95f, 0.18f, along) * Mathf.Lerp(0.45f, 1f, normalisedForce);
                dotRenderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, alpha));
                dotRenderers[i].SetPropertyBlock(block);
            }

        }

        /// <summary>
        /// When the arc comes back down to the board, from the ballistics rather than
        /// by walking it. The positive root of the usual quadratic.
        /// </summary>
        private float TimeToGround(Vector3 origin, Vector3 velocity, Vector3 gravity)
        {
            float g = -gravity.y;

            if (g <= 0.001f)
            {
                return sampleInterval * dots.Length;
            }

            float drop = Mathf.Max(0f, origin.y - groundHeight);
            float rise = velocity.y;

            return (rise + Mathf.Sqrt(Mathf.Max(0f, rise * rise + 2f * g * drop))) / g;
        }

        private void ShowMarker(Vector3 landingPoint, float normalisedForce)
        {
            if (marker == null)
            {
                return;
            }

            marker.gameObject.SetActive(true);
            marker.position = new Vector3(landingPoint.x, markerHeight, landingPoint.z);

            float pulse = 1f + 0.09f * Mathf.Sin(Time.time * markerPulseSpeed * Mathf.PI);
            marker.localScale = Vector3.one * (markerRadius * pulse);

            markerRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(
                tint.r, tint.g, tint.b, Mathf.Lerp(0.35f, 0.8f, normalisedForce)));
            markerRenderer.SetPropertyBlock(block);
        }

        public void Hide()
        {
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }

            if (dots == null)
            {
                return;
            }

            visible = false;

            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].gameObject.SetActive(false);
            }
        }

        public bool IsVisible => visible;
    }
}
