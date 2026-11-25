using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace PocketPutt.Prototype
{
    /// <summary>A single drag defines angle and power; no aim assist or second power bar.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PrototypePuttController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private LineRenderer aimLine;

        [Header("Prototype tuning (not final values)")]
        [SerializeField, Min(0.1f)] private float maximumDragDistance = 2.1f;
        [SerializeField, Min(0.1f)] private float maximumLaunchSpeed = 7.5f;
        [SerializeField, Min(0f)] private float rollingDeceleration = 4f;
        [SerializeField, Min(0f)] private float minimumDragDistance = 0.13f;
        [SerializeField, Range(0.01f, 1f)] private float aimSmoothing = 0.35f;
        [SerializeField, Min(0f)] private float stoppedSpeed = 0.09f;

        private Rigidbody ball;
        private Vector3 startPosition;
        private Vector3 smoothedAim;
        private bool aiming;
        private bool gameFinished;
        private bool touchOwned;
        private int activeTouchId = -1;
        private int shots;
        // Persist values configured by the editor builder when the scene is saved.
        [SerializeField] private string levelName;
        [SerializeField] private int targetThree = 1;
        [SerializeField] private int targetTwo = 2;
        private string resultText = "";
        private int starsEarned;
        private Material lineMaterial;

        public int Shots => shots;
        public bool Finished => gameFinished;

        public void Configure(Camera camera, string name, int threeStarTarget, int twoStarTarget)
        {
            gameplayCamera = camera;
            levelName = name;
            targetThree = threeStarTarget;
            targetTwo = twoStarTarget;
        }

        private void Awake()
        {
            ball = GetComponent<Rigidbody>();
            startPosition = transform.position;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (aimLine == null)
            {
                GameObject lineObject = new GameObject("Aim Line");
                aimLine = lineObject.AddComponent<LineRenderer>();
                aimLine.positionCount = 2;
                aimLine.startWidth = 0.07f;
                aimLine.endWidth = 0.025f;
                aimLine.useWorldSpace = true;
                aimLine.numCapVertices = 3;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    lineMaterial = new Material(shader);
                    aimLine.sharedMaterial = lineMaterial;
                }

                aimLine.startColor = new Color(1f, 0.88f, 0.4f);
                aimLine.endColor = new Color(1f, 0.98f, 0.72f);
            }

            aimLine.enabled = false;
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void OnDestroy()
        {
            if (lineMaterial != null)
                Destroy(lineMaterial);
        }

        private void Update()
        {
            if (gameFinished || gameplayCamera == null)
                return;

            if (Touch.activeTouches.Count > 0)
            {
                ReadTouches();
            }
            else if (Mouse.current != null && !touchOwned)
            {
                ReadMouse();
            }
        }

        private void ReadTouches()
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (!aiming && touch.phase == TouchPhase.Began)
                {
                    BeginAim(touch.screenPosition, true, touch.touchId);
                }
                else if (aiming && touchOwned && touch.touchId == activeTouchId)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                        UpdateAim(touch.screenPosition);
                    else if (touch.phase == TouchPhase.Ended)
                    {
                        UpdateAim(touch.screenPosition);
                        EndAim();
                    }
                    else if (touch.phase == TouchPhase.Canceled)
                        CancelAim();
                }
            }
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            Vector2 screen = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
                BeginAim(screen, false, -1);
            else if (aiming && !touchOwned && mouse.leftButton.isPressed)
                UpdateAim(screen);
            else if (aiming && !touchOwned && mouse.leftButton.wasReleasedThisFrame)
            {
                UpdateAim(screen);
                EndAim();
            }
        }

        private bool ScreenToField(Vector2 screen, out Vector3 fieldPoint)
        {
            Ray ray = gameplayCamera.ScreenPointToRay(screen);
            Plane fieldPlane = new Plane(Vector3.up, startPosition);
            if (fieldPlane.Raycast(ray, out float distance))
            {
                fieldPoint = ray.GetPoint(distance);
                return true;
            }

            fieldPoint = default;
            return false;
        }

        private void BeginAim(Vector2 screen, bool fromTouch, int touchId)
        {
            if (aiming || ball.linearVelocity.sqrMagnitude > stoppedSpeed * stoppedSpeed)
                return;
            if (!ScreenToField(screen, out Vector3 point))
                return;
            Vector3 fromBall = point - transform.position;
            fromBall.y = 0f;
            if (fromBall.sqrMagnitude > 0.55f * 0.55f)
                return;

            aiming = true;
            touchOwned = fromTouch;
            activeTouchId = touchId;
            smoothedAim = Vector3.zero;
            aimLine.enabled = true;
            UpdateLine();
        }

        private void UpdateAim(Vector2 screen)
        {
            if (!aiming || !ScreenToField(screen, out Vector3 point))
                return;

            Vector3 rawAim = transform.position - point;
            rawAim.y = 0f;
            rawAim = Vector3.ClampMagnitude(rawAim, maximumDragDistance);
            smoothedAim = Vector3.Lerp(smoothedAim, rawAim, aimSmoothing);
            UpdateLine();
        }

        private void UpdateLine()
        {
            float fraction = Mathf.Clamp01(smoothedAim.magnitude / maximumDragDistance);
            Vector3 origin = transform.position + Vector3.up * 0.17f;
            aimLine.SetPosition(0, origin);

            Vector3 direction = smoothedAim.sqrMagnitude > 0.0001f ? smoothedAim.normalized : Vector3.zero;
            aimLine.SetPosition(1, origin + direction * (fraction * 2.4f));
        }

        private void EndAim()
        {
            if (!aiming)
                return;
            float distance = smoothedAim.magnitude;
            if (distance >= minimumDragDistance)
            {
                float fraction = Mathf.Clamp01(distance / maximumDragDistance);
                Vector3 velocityChange = smoothedAim.normalized * (fraction * maximumLaunchSpeed);
                ball.AddForce(velocityChange, ForceMode.VelocityChange);
                shots++;
            }
            CancelAim();
        }

        private void CancelAim()
        {
            aiming = false;
            touchOwned = false;
            activeTouchId = -1;
            smoothedAim = Vector3.zero;
            aimLine.enabled = false;
        }

        private void FixedUpdate()
        {
            if (ball == null || gameFinished)
                return;

            Vector3 velocity = ball.linearVelocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            if (speed < stoppedSpeed)
            {
                ball.linearVelocity = Vector3.zero;
                return;
            }

            ball.linearVelocity = Vector3.MoveTowards(
                velocity, Vector3.zero, rollingDeceleration * Time.fixedDeltaTime);
        }

        public void FinishHole()
        {
            if (gameFinished || shots == 0)
                return;

            gameFinished = true;
            CancelAim();
            ball.linearVelocity = Vector3.zero;
            int stars = shots <= targetThree ? 3 : shots <= targetTwo ? 2 : 1;
            starsEarned = stars;
            resultText = $"Buraco! {shots} tacada(s) — {stars} estrela(s).";
            Debug.Log($"[Pocket Putt Prototype] {resultText}");
        }

        private void RestartAttempt()
        {
            CancelAim();
            ball.position = startPosition;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            shots = 0;
            gameFinished = false;
            resultText = "";
            starsEarned = 0;
        }

        private void OnGUI()
        {
            // Minimal, compact prototype HUD. Never place a full-width win panel
            // over the hole or middle of the golf course.
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(Screen.width / 720f, 0.9f, 1.15f);
            float margin = 10f * scale;
            float left = safe.xMin + margin;
            float top = Screen.height - safe.yMax + margin;
            float right = safe.xMax - margin;
            float available = right - left;
            float rowHeight = 32f * scale;
            float gap = 7f * scale;
            float restartWidth = 88f * scale;
            float menuWidth = 72f * scale;
            float labelWidth = Mathf.Min(204f * scale, available - restartWidth - menuWidth - 2f * gap);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = Color.white;

            Rect counter = new Rect(left, top, labelWidth, rowHeight);
            GUI.Box(counter, GUIContent.none);
            GUI.Label(counter, $"TACADAS  {shots}", labelStyle);

            Rect restart = new Rect(right - restartWidth, top, restartWidth, rowHeight);
            if (GUI.Button(restart, "Reiniciar"))
                RestartAttempt();
            // Game Hub: back to the menu that launched this game.
            Rect menu = new Rect(restart.xMin - gap - menuWidth, top, menuWidth, rowHeight);
            if (GUI.Button(menu, "Menu"))
                GameHub.HubNavigation.ReturnToMenu();

            if (gameFinished)
            {
                float resultWidth = Mathf.Min(230f * scale, available);
                Rect result = new Rect(left, top + rowHeight + 5f * scale,
                    resultWidth, 36f * scale);
                GUI.Box(result, GUIContent.none);
                string shotLabel = shots == 1 ? "TACADA" : "TACADAS";
                GUI.Label(result, $"{starsEarned} ESTRELAS  /  {shots} {shotLabel}", labelStyle);
            }
        }
    }
}
