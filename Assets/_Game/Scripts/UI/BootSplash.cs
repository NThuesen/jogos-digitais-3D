using System;
using System.IO;
using SlipperyZlope.RingToss.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SlipperyZlope.RingToss.UI
{
    /// <summary>
    /// The studio's splash, played over the game as it opens.
    ///
    /// The animation is a rendered video rather than anything built here: it was made in
    /// Motion Canvas, in `../animations`, and lives in `StreamingAssets/splash.mp4` —
    /// 1080x1920, six and a bit seconds, about three quarters of a megabyte, **with its
    /// own soundtrack**. A splash that has to be recreated in the engine to be shown is a
    /// splash that drifts from the one the studio signed off.
    ///
    /// What plays is the ring toss one: three throws at a peg, two near misses and a
    /// catch that spins down it, then the wordmark. The studio's first splash — a bar of
    /// soap in bath water — is still in the animations repo, and was replaced here
    /// because it shared nothing with the game it opened.
    ///
    /// It covers everything on its own canvas, above the interface's, and takes the taps
    /// while it does, so nothing behind it can be pressed by accident. **Any tap skips
    /// it**, from the first frame: an animation the player cannot get out of is the
    /// fastest way to make them resent it.
    ///
    /// Three things it refuses to do:
    ///
    /// - **Play in batch mode.** Captures, layout dumps and play mode tests all load this
    ///   scene, and a video over the first frames would put a splash in front of every
    ///   one of them.
    /// - **Play twice.** Once per process, so reloading the scene — which tests do
    ///   constantly — does not replay it.
    /// - **Fail loudly.** A missing file, an unsupported codec or a device that cannot
    ///   decode gets the same answer as a finished video: get out of the way. The splash
    ///   is the least important thing in the build and must never be what stops it.
    /// </summary>
    public sealed class BootSplash : MonoBehaviour
    {
        /// <summary>The file in StreamingAssets, as the studio renders it.</summary>
        public const string FileName = "splash.mp4";

        /// <summary>
        /// How long to wait for the video to be ready before giving up on it. Generous:
        /// a cold start on a mid-range handset has a lot else to do.
        /// </summary>
        private const float PrepareTimeout = 3f;

        /// <summary>The size ../animations renders the splash at: 9:21, see CoverSize.</summary>
        private const int RenderedWidth = 1080;

        private const int RenderedHeight = 2520;

        /// <summary>Shown once per launch, not once per scene load.</summary>
        private static bool playedThisSession;

        private Canvas canvas;
        private RawImage screen;
        private Image cover;
        private VideoPlayer player;
        private RenderTexture target;
        private AudioSource speaker;
        private AudioDirector audioDirector;

        private float startedAt;
        private bool prepared;
        private bool finished;

        /// <summary>True while the splash is covering the game.</summary>
        public bool IsShowing => !finished && canvas != null && canvas.enabled;

        public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, FileName);

        /// <summary>
        /// Builds the splash and starts it, or returns null when there is nothing to
        /// show: batch mode, a second scene load, or no video on disk.
        /// </summary>
        public static BootSplash Play(Transform parent, int sortingOrder, AudioDirector audio = null)
        {
            if (Application.isBatchMode || playedThisSession)
            {
                return null;
            }

            // Android serves StreamingAssets from inside the APK, where File.Exists
            // cannot see it; everywhere else a missing file is worth checking for.
            bool packed = Application.platform == RuntimePlatform.Android;

            if (!packed && !File.Exists(Path))
            {
                Debug.LogWarning($"[BootSplash] No splash video at {Path}; skipping it.");
                return null;
            }

            playedThisSession = true;

            GameObject host = new GameObject("Boot Splash");
            host.transform.SetParent(parent, false);

            BootSplash splash = host.AddComponent<BootSplash>();
            splash.audioDirector = audio;
            splash.Build(sortingOrder);
            return splash;
        }

        private void Build(int sortingOrder)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            // Shown until the first frame arrives, in the video's own background — the
            // dark stage, #2B1536 as encoded — so the splash does not open on a flash of
            // some other colour.
            cover = new GameObject("Cover").AddComponent<Image>();
            cover.transform.SetParent(transform, false);
            cover.color = new Color32(43, 21, 54, 255);
            cover.raycastTarget = true;
            Stretch(cover.rectTransform);

            // Laid out once the video says how big it is: see FitToScreen.
            screen = new GameObject("Video").AddComponent<RawImage>();
            screen.transform.SetParent(transform, false);
            screen.raycastTarget = false;
            screen.color = new Color(1f, 1f, 1f, 0f);
            Stretch(screen.rectTransform);

            target = CreateTarget(RenderedWidth, RenderedHeight);

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = Path;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            // The splash has a soundtrack: three throws, a graze, the catch and a
            // fanfare, mixed against the animation's own timings. Playing it silently
            // would be showing half of it.
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
            speaker.volume = audioDirector != null && audioDirector.Muted ? 0f : 1f;

            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, speaker);

            // And the fairground's bed of air waits its turn. Two soundtracks at once is
            // mud, and the owner asked for the game's idle noise to begin when this ends.
            audioDirector?.SuspendAmbience();
            player.isLooping = false;
            player.skipOnDrop = true;
            player.loopPointReached += OnReachedEnd;
            player.errorReceived += OnError;
            player.prepareCompleted += OnPrepared;

            screen.texture = target;
            startedAt = Time.unscaledTime;

            try
            {
                player.Prepare();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[BootSplash] The video could not be prepared: {error.Message}");
                Finish();
            }
        }

        /// <summary>
        /// The size to draw the video at so that it covers the screen without being
        /// stretched: scaled uniformly until both sides reach the screen's, the overflow
        /// cropped evenly.
        ///
        /// The first version stretched the video to the screen, and on a 9:19.5 phone a
        /// 9:16 video came out 22% taller than it was drawn — the peg's ball became an
        /// egg. The video is now rendered at 9:21, taller than the phones it plays on, so
        /// what this crops is spare dark stage above and below the composition.
        /// </summary>
        public static Vector2 CoverSize(Vector2 screenSize, Vector2 videoSize)
        {
            if (videoSize.x <= 0f || videoSize.y <= 0f)
            {
                return screenSize;
            }

            float scale = Mathf.Max(screenSize.x / videoSize.x, screenSize.y / videoSize.y);
            return videoSize * scale;
        }

        /// <summary>
        /// Renders the video at its own size and draws it centred at <see cref="CoverSize"/>.
        /// Done once the video is prepared because that is when its size is known.
        /// </summary>
        private void FitToScreen(VideoPlayer source)
        {
            int width = (int)source.width;
            int height = (int)source.height;

            // Only a video that is not the size the splash was rendered at needs a new
            // texture; one drawn into a texture of another shape would be squashed.
            if (width > 0 && height > 0 && (target.width != width || target.height != height))
            {
                target.Release();
                Destroy(target);
                target = CreateTarget(width, height);
                source.targetTexture = target;
                screen.texture = target;
            }

            RectTransform rect = screen.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = CoverSize(((RectTransform)canvas.transform).rect.size, new Vector2(width, height));
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RenderTexture CreateTarget(int width, int height) =>
            new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "Boot Splash" };

        private void OnPrepared(VideoPlayer source)
        {
            prepared = true;
            FitToScreen(source);
            screen.color = Color.white;
            source.Play();
        }

        private void OnReachedEnd(VideoPlayer source) => Finish();

        private void OnError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[BootSplash] {message}");
            Finish();
        }

        private void Update()
        {
            if (finished)
            {
                return;
            }

            // Any touch or click, from the first frame.
            if (Skipped())
            {
                Finish();
                return;
            }

            if (!prepared && Time.unscaledTime - startedAt > PrepareTimeout)
            {
                Debug.LogWarning("[BootSplash] The video was not ready in time; skipping it.");
                Finish();
            }
        }

        /// <summary>
        /// Whether the player has asked for this to be over.
        ///
        /// Through the Input System, like the rest of the game, and that is not a style
        /// preference: this project ships with active input handling set to the new system
        /// alone, where every call on the legacy class throws the first time it runs. The
        /// first version of this read the legacy class, which compiles, passes every test,
        /// and would have thrown on each frame of the first thing a player ever sees — a
        /// batch run cannot catch it, because the splash deliberately never plays in one.
        ///
        /// <see cref="Pointer"/> covers a finger and a mouse together, which is the same
        /// choice the throw controller makes.
        /// </summary>
        private static bool Skipped()
        {
            Pointer pointer = Pointer.current;

            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        /// <summary>Gets out of the way, whatever the reason, and never comes back.</summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;

            // Whatever ended this — the end of the video, a tap, a codec that could not
            // decode it, or a prepare that never came back — the game's own sound starts
            // here. A splash that failed must not take the ambience down with it.
            audioDirector?.ResumeAmbience();

            if (player != null)
            {
                player.loopPointReached -= OnReachedEnd;
                player.errorReceived -= OnError;
                player.prepareCompleted -= OnPrepared;
                player.Stop();
            }

            if (canvas != null)
            {
                canvas.enabled = false;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (target != null)
            {
                target.Release();
                Destroy(target);
                target = null;
            }
        }
    }
}
