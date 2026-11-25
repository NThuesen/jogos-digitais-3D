using System.Collections.Generic;
using SlipperyZlope.RingToss.Audio;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using SlipperyZlope.RingToss.Juice;
using SlipperyZlope.RingToss.Monetisation;
using SlipperyZlope.RingToss.Mobile;
using SlipperyZlope.RingToss.Save;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.UI
{
    /// <summary>
    /// The whole interface: title, level select, the in-game HUD and the results
    /// screen, built at load and driven by game events.
    ///
    /// Every screen is a CanvasGroup that fades and scales rather than a GameObject
    /// that blinks on. Every number that changes counts up rather than swapping. Those
    /// two rules are most of the difference between an interface that feels like part
    /// of the game and one that feels like a debug overlay laid on top of it.
    ///
    /// UI tweens run on unscaled time, so the results screen still animates while a
    /// hit stop has the game clock stopped.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class UiRoot : MonoBehaviour
    {
        private enum Screen
        {
            None,
            Title,
            LevelSelect,
            Shop,
            Hud,
            Result,
            Settings,
            Consent,
            Legal
        }

        /// <summary>Which half of the shop is showing.</summary>
        private enum ShopTab
        {
            Skins,
            Trails
        }

        /// <summary>One card in the shop grid, reused across both tabs.</summary>
        private struct ShopCard
        {
            public Button Button;
            public Image Background;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Status;
            public Image[] Swatch;
        }

        [Header("Systems")]
        [SerializeField] private GameDirector director;
        [SerializeField] private AudioDirector audioDirector;
        [SerializeField] private JuiceConfig juiceConfig;
        [SerializeField] private Camera uiCamera;
        [SerializeField] private CosmeticsLibrary cosmetics;
        [SerializeField] private CosmeticsDirector cosmeticsDirector;

        [Tooltip("Optional. Absent in a build with no mediator, in which case the " +
                 "privacy screen still works and the consent dialog never appears.")]
        [SerializeField] private AdService adService;

        [SerializeField] private ConsentService consent;

        [Header("Palette")]
        [SerializeField] private Color panelColor = new Color(0.11f, 0.09f, 0.19f, 0.94f);
        [SerializeField] private Color accentColor = new Color(1f, 0.79f, 0.33f);
        [SerializeField] private Color textColor = new Color(0.97f, 0.96f, 0.94f);
        [Tooltip("Secondary text. Brightened from 0.68 to 0.80: on the shop's cards " +
                 "it measured 4.08:1, under the 4.5:1 a small label needs, and every " +
                 "price in the shop is that label. It is 5.7:1 now.")]
        [SerializeField] private Color mutedColor = new Color(0.80f, 0.78f, 0.86f);
        [Tooltip("Darkened 12%: white on the old coral was 2.95:1, just under the " +
                 "3:1 a large label needs. 3.7:1 now, and the same colour to look at.")]
        [SerializeField] private Color primaryButtonColor = new Color(0.836f, 0.317f, 0.370f);
        [SerializeField] private Color secondaryButtonColor = new Color(0.28f, 0.26f, 0.4f);

        [Tooltip("Endless is its own mode, not a variation on Levels, so it gets its " +
                 "own colour rather than the secondary button's.")]
        [SerializeField] private Color endlessButtonColor = new Color(0.206f, 0.533f, 0.533f);

        /// <summary>
        /// Time attack's amber. Deep enough that white on it measures 4.1:1, which
        /// clears the 3:1 floor for large text with room to spare — the two buttons
        /// above it were both darkened for the same reason.
        /// </summary>
        [SerializeField] private Color timeButtonColor = new Color(0.72f, 0.42f, 0.16f);

        [Tooltip("What the time attack clock bleeds towards as it empties.")]
        [SerializeField] private Color warningColor = new Color(1f, 0.33f, 0.28f);

        [Tooltip("The flash when the clock is paid time back.")]
        [SerializeField] private Color timeGainColor = new Color(0.44f, 1f, 0.6f);

        [Tooltip("The alarm clock's brass: its case, bells and feet. The booth's own " +
                 "metal colour, so the prop belongs to the stall rather than to the HUD.")]
        [SerializeField] private Color clockCaseColor = new Color(0.87f, 0.65f, 0.31f);

        [Tooltip("Lives left. Also the colour a heart pops to when one is earned back.")]
        [SerializeField] private Color lifeColor = new Color(1f, 0.36f, 0.45f);

        [SerializeField] private Color ticketColor = new Color(1f, 0.84f, 0.42f);

        [Tooltip("The 2x tag and the share-bonus banner. The logo sheet's deep pink: white on " +
                 "it clears 3:1 for large text, which the lighter brand #FF7BA4 does not.")]
        [SerializeField] private Color saleTagColor = new Color(0.878f, 0.314f, 0.498f);

        [Tooltip("Text on the gold reward banner.")]
        [SerializeField] private Color bannerInkColor = new Color(0.17f, 0.13f, 0.25f);

        [SerializeField] private Color ownedColor = new Color(0.32f, 0.68f, 0.52f);

        private const int LevelsPerRow = 3;

        /// <summary>Levels per stage page. Matches the level library's stage size.</summary>
        private const int LevelsPerPage = 10;

        private Sprite roundedSprite;
        private Sprite plaqueSprite;
        private Sprite circleSprite;
        private Sprite ringSprite;
        private Sprite starSprite;
        private Sprite heartSprite;
        private Sprite fadeSprite;

        private Canvas canvas;
        private Screen current = Screen.None;

        private CanvasGroup titleGroup;
        private CanvasGroup settingsGroup;
        private CanvasGroup consentGroup;
        private RectTransform settingsPanel;
        private RectTransform consentPanel;
        private TextMeshProUGUI soundLabel;
        private TextMeshProUGUI consentAgeLabel;
        private Image consentAgeTick;
        private bool consentAgeConfirmed;
        private CanvasGroup levelSelectGroup;
        private CanvasGroup hudGroup;
        private CanvasGroup resultGroup;

        private RectTransform titlePanel;
        private RectTransform levelSelectPanel;
        private RectTransform resultPanel;

        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI levelNameLabel;
        private TextMeshProUGUI levelSubtitleLabel;
        private TextMeshProUGUI pegsLabel;
        private TextMeshProUGUI comboLabel;
        private RectTransform comboBadge;

        private readonly List<Image> ringIcons = new List<Image>();
        private RectTransform ringRow;

        private readonly List<Image> lifeIcons = new List<Image>();
        private readonly List<Image> streakPips = new List<Image>();
        private RectTransform livesRow;
        private TextMeshProUGUI streakLabel;
        private TextMeshProUGUI endlessBestLabel;
        private TextMeshProUGUI timeAttackBestLabel;
        private Image livesGlow;
        private Sequence livesFlash;
        private int shownLives = -1;
        private int shownStreak = -1;
        private RectTransform pullHint;
        private readonly List<Image> pullChevrons = new List<Image>();
        private Image pullFinger;
        private RectTransform timeRow;
        private RectTransform clockRoot;
        private Image clockFill;
        private Image clockGlow;
        private RectTransform clockHand;
        private Sequence clockPop;

        private RectTransform roundBanner;
        private TextMeshProUGUI roundBannerLabel;
        private TextMeshProUGUI roundBannerBonus;

        private CanvasGroup shopGroup;
        private RectTransform shopPanel;
        private TextMeshProUGUI shopTicketLabel;
        private CanvasGroup legalGroup;
        private RectTransform legalPanel;
        private RectTransform legalContent;
        private ScrollRect legalScroll;
        private TextMeshProUGUI legalHeading;
        private TextMeshProUGUI legalBody;
        private readonly List<Button> legalTabs = new List<Button>();
        private int legalSection;
        private Button deleteDataButton;
        private TextMeshProUGUI deleteDataLabel;
        private bool deleteArmed;
        private Button modoDevButton;
        private TextMeshProUGUI modoDevLabel;
        private Button freeTicketsButton;
        private TextMeshProUGUI freeTicketsLabel;
        private Button titleAdButton;
        private TextMeshProUGUI titleAdLabel;
        private TextMeshProUGUI titleTicketLabel;
        private Button shareButton;
        private TextMeshProUGUI shareLabel;

        // The 2x offer: a tag on SHARE once a chosen advert has bought it, and one on each
        // WATCH button saying so before the advert is watched. See RefreshShare.
        private RectTransform shareTag;
        private RectTransform titleAdTag;
        private float shareTagShownAt;

        private TextMeshProUGUI modoDevTitleLabel;
        private TextMeshProUGUI modoDevShopLabel;

        private TicketShower ticketShower;
        private Sprite ticketSprite;
        private Sprite tagSprite;

        // A share pays on the tap, while Android's share sheet is covering the game, so
        // its celebration waits here until the player is back to see it.
        private bool shareCelebrationPending;
        private int pendingShareAmount;
        private int pendingShareTotal;
        private TicketSource pendingShareSource;
        private float pendingShareSince;
        private float pendingShareFallback;
        private bool pendingShareLeftApp;
        private float returnedToAppAt;

        // Next time the title's two offers are redrawn. See Update.
        private float nextTitleRefresh;
        private TextMeshProUGUI hudTicketLabel;
        private RectTransform hudTicketBadge;
        private Button skinsTab;
        private Button trailsTab;
        private ShopTab currentTab = ShopTab.Skins;
        private readonly List<ShopCard> shopCards = new List<ShopCard>();
        private Sprite[] patternSprites;

        private TextMeshProUGUI resultTitleLabel;
        private TextMeshProUGUI resultScoreLabel;
        private TextMeshProUGUI resultBestLabel;
        private TextMeshProUGUI resultDepthLabel;
        private TextMeshProUGUI resultTicketLabel;
        private int ticketsThisRun;
        private readonly List<Image> resultStars = new List<Image>();
        private Button nextButton;
        private TextMeshProUGUI retryLabel;
        private Button continueButton;
        private TextMeshProUGUI continueLabel;

        private readonly List<Button> levelButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> levelButtonLabels = new List<TextMeshProUGUI>();
        private readonly List<Image[]> levelButtonStars = new List<Image[]>();
        private readonly List<Button> stageTabs = new List<Button>();
        private readonly List<TextMeshProUGUI> stageTabLabels = new List<TextMeshProUGUI>();
        private TextMeshProUGUI totalStarsLabel;
        private int levelPage;

        /// <summary>How many stage pages the library needs, rounded up.</summary>
        private int StageCount =>
            director == null ? 1 : Mathf.Max(1, (director.LevelCount + LevelsPerPage - 1) / LevelsPerPage);

        // Held so a restart can stop the parent Sequence. Tween.StopAll on the target
        // is an error once its tweens have been adopted by a Sequence, because the
        // Sequence owns them from that point on.
        private Sequence scorePop;
        private Sequence comboPop;
        private Sequence pegsPop;
        private Sequence[] ringPops;
        private Sequence livesPop;
        private Sequence roundBannerPop;
        private Sequence ticketPop;
        private Sequence purchasePop;

        private int displayedScore;
        private int targetScore;
        private int pegsClaimed;
        private int pegsTotal;

        private bool IsEndless => director != null && director.Mode == GameMode.Endless;

        private bool IsTimeAttack => director != null && director.Mode == GameMode.TimeAttack;

        /// <summary>Seconds left at which the clock has gone fully to its warning colour.</summary>
        private const float TimeWarningSeconds = 5f;

        /// <summary>Either generated mode. Neither of them counts rings.</summary>
        private bool IsGeneratedRun => director != null && director.IsGeneratedRun;

        private void Awake()
        {
            CreateSprites();
            BuildCanvas();
            BuildEventSystem();

            BuildTitle();
            BuildSettings();
            BuildLegal();
            BuildConsent();
            BuildLevelSelect();
            BuildShop();
            BuildHud();
            BuildResult();

            // Last, so it draws over every screen it celebrates on.
            BuildTicketShower();

            // And the studio's splash over all of it, on a canvas of its own. It takes
            // itself away when it is done, when it is tapped, or when there is nothing to
            // show — batch mode, a reloaded scene, a missing file.
            BootSplash.Play(transform, canvas.sortingOrder + 100, audioDirector);
        }

        /// <summary>
        /// The overlay tickets fly across. It catches no raycasts, so a celebration never
        /// swallows the tap that comes after it.
        /// </summary>
        private void BuildTicketShower()
        {
            GameObject host = new GameObject("Ticket Shower");
            host.transform.SetParent(transform, false);

            ticketShower = host.AddComponent<TicketShower>();
            ticketShower.Build(
                canvas.transform, ticketSprite, roundedSprite, ticketColor,
                accentColor, saleTagColor, bannerInkColor, textColor,
                new[] { saleTagColor, ticketColor, endlessButtonColor, textColor, accentColor });
        }

        /// <summary>
        /// A price tag hung on a button's corner. Not a button itself — the button under it
        /// takes the tap — and it catches no raycasts, so it can never steal one.
        /// </summary>
        private RectTransform BuildOfferTag(
            Transform button, string name, string text, Vector2 anchor, Vector2 position,
            Vector2 size, float fontSize, float tilt)
        {
            // Centred on the point given, measured from the button's corner. Place's
            // second argument is the pivot, not a second anchor: passing the anchor twice
            // hung the tag from its own corner, over the button's label.
            RectTransform tag = UiFactory.Rect(name, button);
            UiFactory.Place(tag, anchor, new Vector2(0.5f, 0.5f), position, size);
            tag.localRotation = Quaternion.Euler(0f, 0f, tilt);

            Image shape = UiFactory.Image("Shape", tag, tagSprite, saleTagColor);
            UiFactory.Stretch(shape.rectTransform);
            shape.raycastTarget = false;

            // Clear of the pointed end and the string hole on the left.
            TextMeshProUGUI label = UiFactory.Text(
                "Label", tag, text, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            RectTransform box = label.rectTransform;
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.one;
            box.pivot = new Vector2(0.5f, 0.5f);
            box.offsetMin = new Vector2(size.y * 0.46f, 3f);
            box.offsetMax = new Vector2(-8f, -3f);
            label.raycastTarget = false;

            tag.gameObject.SetActive(false);
            return tag;
        }

        private void Start()
        {
            // The consent dialog comes before anything else, and only when there is
            // actually an advertising SDK for it to govern. Asking a player to make a
            // decision about advertising in a build that shows none is a dialog with
            // no purpose, and it trains people to dismiss the one that matters.
            if (adService != null && adService.AdsAvailable && consent != null && consent.MustAsk)
            {
                Show(Screen.Consent);
                return;
            }

            Show(Screen.Title);
        }

        private void OnEnable()
        {
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.ComboChanged += OnComboChanged;
            GameEvents.RingsRemainingChanged += OnRingsRemainingChanged;
            GameEvents.PegClaimed += OnPegClaimed;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelEnded += OnLevelEnded;
            GameEvents.LivesChanged += OnLivesChanged;
            GameEvents.PerfectStreakChanged += OnPerfectStreakChanged;
            GameEvents.TimeChanged += OnTimeChanged;
            GameEvents.TimeAwarded += OnTimeAwarded;
            GameEvents.StreakRewarded += OnStreakRewarded;
            GameEvents.RoundCleared += OnRoundCleared;
            GameEvents.RunEnded += OnRunEnded;
            GameEvents.TicketsAwarded += OnTicketsAwarded;
        }

        private void OnDisable()
        {
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.ComboChanged -= OnComboChanged;
            GameEvents.RingsRemainingChanged -= OnRingsRemainingChanged;
            GameEvents.PegClaimed -= OnPegClaimed;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelEnded -= OnLevelEnded;
            GameEvents.LivesChanged -= OnLivesChanged;
            GameEvents.PerfectStreakChanged -= OnPerfectStreakChanged;
            GameEvents.TimeChanged -= OnTimeChanged;
            GameEvents.TimeAwarded -= OnTimeAwarded;
            GameEvents.StreakRewarded -= OnStreakRewarded;
            GameEvents.RoundCleared -= OnRoundCleared;
            GameEvents.RunEnded -= OnRunEnded;
            GameEvents.TicketsAwarded -= OnTicketsAwarded;
        }

        /// <summary>
        /// Wires the advertising and consent services in.
        ///
        /// Separate from <see cref="Bind"/> rather than more arguments on it: a build
        /// with no mediator does not call this at all, and the privacy screen has to
        /// keep working in that build regardless.
        /// </summary>
        public void BindMonetisation(AdService ads, ConsentService consentService)
        {
            adService = ads;
            consent = consentService;
        }

        public void Bind(
            GameDirector gameDirector,
            AudioDirector audio,
            JuiceConfig juice,
            Camera camera,
            CosmeticsLibrary cosmeticsLibrary,
            CosmeticsDirector cosmeticsRuntime)
        {
            director = gameDirector;
            audioDirector = audio;
            juiceConfig = juice;
            uiCamera = camera;
            cosmetics = cosmeticsLibrary;
            cosmeticsDirector = cosmeticsRuntime;
        }

        private void CreateSprites()
        {
            roundedSprite = UiSprites.RoundedRect(64, 22);

            // Buttons get a plaque rather than the same flat rectangle the panels use.
            // Bigger and rounder than the panel sprite because it is nine-sliced across
            // controls from a 290-wide RETRY to a 600-wide NEXT, and the rim has to stay
            // the same weight on both.
            plaqueSprite = UiSprites.Plaque(96, 26, 5);
            circleSprite = UiSprites.Circle(64);
            ticketSprite = UiSprites.Ticket(96, 56);
            tagSprite = UiSprites.Tag(128, 64);
            ringSprite = UiSprites.Ring(64, 0.2f);
            starSprite = UiSprites.Star(64);
            heartSprite = UiSprites.Heart(64);
            fadeSprite = UiSprites.VerticalFade();
        }

        private void BuildCanvas()
        {
            GameObject host = new GameObject("Canvas", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            host.layer = LayerMask.NameToLayer("UI");

            canvas = host.AddComponent<Canvas>();

            // Camera space rather than overlay. Overlay is drawn after the pipeline
            // finishes, so a camera render request never sees it and the interface
            // cannot be reviewed in a headless capture at all.
            if (uiCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);

            // Portrait is locked, so matching height alone keeps the layout identical
            // on every phone and only the width of the safe margins changes.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            host.AddComponent<GraphicRaycaster>();
        }

        private void BuildEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject host = new GameObject("Event System");
            host.transform.SetParent(transform, false);
            host.AddComponent<EventSystem>();

            // The default actions have to be asked for explicitly when the module is
            // added from code; without them nothing on the canvas is clickable.
            InputSystemUIInputModule module = host.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>
        /// The title. Endless is the front door.
        ///
        /// It used to be Levels: PLAY continued the campaign and Endless sat under it
        /// in its own colour. That is the right shape for a game whose content is the
        /// levels, and the wrong one for this game — endless is where the run lives,
        /// it needs no unlock state to make sense, and a player who has never opened
        /// the app is one tap from throwing a ring instead of one tap from a grid of
        /// fifty numbered buttons. Levels moved to the row below, beside the shop,
        /// which is where a campaign belongs once it is something you choose rather
        /// than something you are given.
        ///
        /// Two plates carry everything. Text over the rendered fairway has whatever
        /// contrast the camera is pointing at, which changes with the theme and is not
        /// a decision anybody made.
        /// </summary>
        private void BuildTitle()
        {
            RectTransform screen = UiFactory.Screen("Title", canvas.transform);
            titleGroup = UiFactory.Group(screen);

            RectTransform safe = UiFactory.SafeScreen("Safe", screen);

            titlePanel = UiFactory.Rect("Panel", safe);
            UiFactory.Place(titlePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1180f));

            // No plate behind any of this. The fairground is the title screen's best
            // asset and two dark rectangles across it were hiding the thing they were
            // there to make readable. Every label carries its own outline and shadow
            // instead, which is contrast that follows the glyphs rather than a box
            // sized for the longest string.
            TextMeshProUGUI title = UiFactory.Legible(UiFactory.Text(
                "Title", titlePanel, "RING TOSS", 126f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold), 0.2f);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 336f), new Vector2(690f, 150f));

            TextMeshProUGUI subtitle = UiFactory.Legible(UiFactory.Text(
                "Subtitle", titlePanel, "SLIPPERYZLOPE GAMES", 34f, textColor));
            UiFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 236f), new Vector2(660f, 56f));

            Button play = UiFactory.Button(
                "Play", titlePanel, plaqueSprite, primaryButtonColor, "PLAY", 68f, textColor);
            UiFactory.Place(play.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 30f), new Vector2(600f, 150f));
            play.onClick.AddListener(OnPlayPressed);

            // Doubles as the caption for the button above it: with no record yet it
            // says what the mode is, and after one run it says how far you got.
            endlessBestLabel = UiFactory.Legible(UiFactory.Text(
                "Endless Best", titlePanel, string.Empty, 30f, textColor));
            UiFactory.Place(endlessBestLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -76f), new Vector2(660f, 46f));

            // Below endless and visibly smaller, because it is the variant rather than
            // the front door: same board, one different rule. Putting it beside LEVELS
            // would have said it was a third kind of game.
            Button timeAttack = UiFactory.Button(
                "Time Attack", titlePanel, plaqueSprite, timeButtonColor,
                "TIME ATTACK", 46f, textColor);
            UiFactory.Place(timeAttack.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -166f), new Vector2(600f, 120f));
            timeAttack.onClick.AddListener(OnTimeAttackPressed);

            timeAttackBestLabel = UiFactory.Legible(UiFactory.Text(
                "Time Attack Best", titlePanel, string.Empty, 30f, textColor));
            UiFactory.Place(timeAttackBestLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -252f), new Vector2(660f, 44f));

            Button levels = UiFactory.Button(
                "Levels", titlePanel, plaqueSprite, endlessButtonColor, "LEVELS", 46f, textColor);
            UiFactory.Place(levels.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-186f, -344f), new Vector2(348f, 110f));
            levels.onClick.AddListener(() => { Click(); Show(Screen.LevelSelect); });

            Button shop = UiFactory.Button(
                "Shop", titlePanel, plaqueSprite, secondaryButtonColor, "SHOP", 46f, textColor);
            UiFactory.Place(shop.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(186f, -344f), new Vector2(348f, 110f));
            shop.onClick.AddListener(() => { Click(); Show(Screen.Shop); });

            titleTicketLabel = UiFactory.Legible(UiFactory.Text(
                "Tickets", titlePanel, string.Empty, 34f, ticketColor,
                TextAlignmentOptions.Center, FontStyles.Bold));
            // Down from where it was first put. The layout audit measured WATCH NOW
            // overlapping SHOP by eleven pixels across its whole width — in an overlap
            // whichever button is drawn later takes the taps — and, once they no longer
            // overlapped, still eleven pixels apart, under the sixteen that keeps two
            // targets from being hit by accident. Sixteen units of gap is about twenty.
            UiFactory.Place(titleTicketLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-170f, -463f), new Vector2(380f, 46f));

            // The same offer as the one in the shop, on the screen the player is
            // actually on. The shop's copy is contextual — it sits beside the balance
            // at the moment somebody is short of tickets — but a control nobody finds
            // is not an option, and the shop is two taps away from the front door.
            titleAdButton = UiFactory.Button(
                "Watch Ad", titlePanel, plaqueSprite, endlessButtonColor,
                string.Empty, 0f, textColor);
            UiFactory.Place(titleAdButton.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(185f, -459f), new Vector2(310f, 88f));
            titleAdButton.onClick.AddListener(OnFreeTicketsPressed);

            titleAdLabel = UiFactory.Text(
                "Label", titleAdButton.transform, string.Empty, 32f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(titleAdLabel.rectTransform);

            // Two lines rather than one long one. The single-line version was 38
            // characters at 32pt, which is wider than the canvas is on any phone.
            TextMeshProUGUI hint = UiFactory.Legible(UiFactory.Text(
                "Hint", titlePanel, "swipe up to throw\nsideways to aim", 30f, textColor));
            hint.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -548f), new Vector2(660f, 84f));

            // Anchored to the bottom of the screen rather than to the title panel.
            // Privacy choices have to be reachable, and a control that scrolls off a
            // panel on a short screen is not reachable.
            Button settings = UiFactory.Button(
                "Settings", safe, plaqueSprite, secondaryButtonColor, "SETTINGS", 36f, mutedColor);
            UiFactory.Place(settings.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-176f, 120f), new Vector2(320f, 96f));
            settings.onClick.AddListener(() => { Click(); Show(Screen.Settings); });

            // Share with a friend, beside settings rather than in the panel: the panel
            // is full, and this row is anchored to the bottom of the safe area so it is
            // reachable on every phone. Always pressable — the link can be sent as often
            // as the player likes — and the label says whether this one pays.
            shareButton = UiFactory.Button(
                "Share", safe, plaqueSprite, endlessButtonColor, string.Empty, 0f, textColor);
            UiFactory.Place(shareButton.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(176f, 120f), new Vector2(320f, 96f));
            shareButton.onClick.AddListener(OnSharePressed);

            shareLabel = UiFactory.Text(
                "Label", shareButton.transform, string.Empty, 32f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(shareLabel.rectTransform);

            // The owner's "sale tag", on the share button once a chosen advert has bought
            // the 2x. It says 2x and not SALE: nothing is being sold here, and a tag should
            // say exactly what the next share pays.
            // Hung on the top right corner, above the label: the label's glyphs start a
            // good thirty units below the button's top edge, and the tag stops at twenty.
            shareTag = BuildOfferTag(
                shareButton.transform, "Share Tag", "2×", new Vector2(1f, 1f),
                new Vector2(-34f, 12f), new Vector2(124f, 62f), 40f, -10f);

            // And the same tag on the offer that buys it, before it is taken. A rewarded
            // advert has to say what it pays before the player opts in, and "+30" alone
            // would leave the better half of the deal unsaid. Hung below the button's
            // corner: above it is SHOP, sixteen units away.
            titleAdTag = BuildOfferTag(
                titleAdButton.transform, "Watch Tag", "+2× SHARE", new Vector2(1f, 0f),
                new Vector2(-56f, -26f), new Vector2(196f, 56f), 34f, -6f);

            // modo_dev says so on the front door, so a test build is never mistaken for
            // the real one — and never shipped as one.
            modoDevTitleLabel = UiFactory.Legible(UiFactory.Text(
                "Modo Dev", safe, "MODO DEV", 34f, saleTagColor,
                TextAlignmentOptions.Center, FontStyles.Bold));
            UiFactory.Place(modoDevTitleLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-150f, -60f), new Vector2(240f, 56f));
            modoDevTitleLabel.gameObject.SetActive(false);

            // Game Hub: back to the menu that launched this game. Top left of the safe
            // area, the same corner and size as the in-game BACK button, and clear of
            // MODO DEV in the opposite corner.
            Button hubMenu = UiFactory.Button(
                "Hub Menu", safe, plaqueSprite, new Color(0f, 0f, 0f, 0.42f), "MENU", 34f, textColor);
            UiFactory.Place(hubMenu.image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -36f), new Vector2(152f, 88f));
            hubMenu.onClick.AddListener(() => { Click(); GameHub.HubNavigation.ReturnToMenu(); });
        }

        /// <summary>
        /// The shop: two tabs of ten items each, sharing one grid.
        ///
        /// The grid is built once and repopulated when the tab changes, rather than
        /// built twice and hidden. Twenty cards of live text on a canvas is twenty
        /// layouts to rebuild on every screen change, and the two halves of the shop
        /// are never on screen together.
        /// </summary>
        private void BuildShop()
        {
            RectTransform screen = UiFactory.Screen("Shop", canvas.transform);
            shopGroup = UiFactory.Group(screen);

            Image dim = UiFactory.Image("Dim", screen, null, new Color(0.04f, 0.03f, 0.09f, 0.95f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            shopPanel = UiFactory.Rect("Panel", screen);
            UiFactory.Place(shopPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1560f));

            TextMeshProUGUI heading = UiFactory.Text(
                "Heading", shopPanel, "PRIZE BOOTH", 72f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(UiFactory.ContentWidth, 100f));

            shopTicketLabel = UiFactory.Text(
                "Tickets", shopPanel, string.Empty, 44f, ticketColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            // 328 wide at -196, not 380 at -170. The old box ran to +20 while the button
            // beside it starts at -15, so the two overlapped by 35 units — invisible for
            // rounds, because a label is only as wide as the number in it and the audit
            // always walked in with an empty balance. At four figures the glyphs reach
            // the end of the box and "3590 TICKETS" lost its S behind the button. This
            // box stops 17 units short of it, which is over the minimum gap the audit
            // enforces everywhere else.
            UiFactory.Place(shopTicketLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-196f, -124f), new Vector2(328f, 56f));

            // Beside the balance, which is the one place in the game where a player is
            // looking at a number and wishing it were bigger. Offered rather than
            // pushed: it is the only advert the player can choose, and it is the only
            // one that pays them.
            freeTicketsButton = UiFactory.Button(
                "Free Tickets", shopPanel, plaqueSprite, endlessButtonColor,
                string.Empty, 0f, textColor);
            // 88 rather than 70. The framing fixture measures a panel at its hidden
            // scale of 0.94, so a 74-unit thumb target has to be authored at 79 to
            // still be one when it is measured.
            //
            // 370 wide rather than 310, for the 2x: the shop has no room around this
            // button for a tag — the tabs are seventeen pixels below it — so the offer is
            // said on the button itself, and the whole sentence has to fit at full size.
            UiFactory.Place(freeTicketsButton.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(170f, -120f), new Vector2(370f, 88f));
            freeTicketsButton.onClick.AddListener(OnFreeTicketsPressed);

            freeTicketsLabel = UiFactory.Text(
                "Label", freeTicketsButton.transform, string.Empty, 32f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(freeTicketsLabel.rectTransform);

            RectTransform shopSafe = UiFactory.SafeScreen("Modo Dev Area", screen);
            modoDevShopLabel = UiFactory.Legible(UiFactory.Text(
                "Modo Dev", shopSafe, "MODO DEV", 34f, saleTagColor,
                TextAlignmentOptions.Center, FontStyles.Bold));
            UiFactory.Place(modoDevShopLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-150f, -60f), new Vector2(240f, 56f));
            modoDevShopLabel.gameObject.SetActive(false);

            skinsTab = UiFactory.Button(
                "Skins Tab", shopPanel, plaqueSprite, secondaryButtonColor, "RINGS", 44f, textColor);
            UiFactory.Place(skinsTab.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-190f, -222f), new Vector2(356f, 100f));
            skinsTab.onClick.AddListener(() => { Click(); SetTab(ShopTab.Skins); });

            trailsTab = UiFactory.Button(
                "Trails Tab", shopPanel, plaqueSprite, secondaryButtonColor, "TRAILS", 44f, textColor);
            UiFactory.Place(trailsTab.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(190f, -222f), new Vector2(356f, 100f));
            trailsTab.onClick.AddListener(() => { Click(); SetTab(ShopTab.Trails); });

            BuildShopGrid();

            BuildHomeButton(screen, "Home Shop");
        }

        private void BuildShopGrid()
        {
            int count = cosmetics == null
                ? 0
                : Mathf.Max(cosmetics.SkinCount, cosmetics.TrailCount);

            // Two columns inside PanelWidth, not inside the reference resolution.
            // The old 448 put 918 units of card into an 823-unit canvas and sliced
            // both outer columns down the middle on every phone taller than 9:16.
            const float cellWidth = 364f;
            const float cellHeight = 196f;
            const float gap = 20f;

            for (int i = 0; i < count; i++)
            {
                int index = i;
                int row = i / 2;
                int column = i % 2;

                float x = (column - 0.5f) * (cellWidth + gap);
                float y = -344f - row * (cellHeight + gap);

                Button button = UiFactory.Button(
                    $"Item_{i:D2}", shopPanel, plaqueSprite, secondaryButtonColor,
                    string.Empty, 0f, textColor);
                UiFactory.Place(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, y), new Vector2(cellWidth, cellHeight));
                button.onClick.AddListener(() => OnShopItemPressed(index));

                TextMeshProUGUI name = UiFactory.Text(
                    "Name", button.transform, string.Empty, 40f, textColor,
                    TextAlignmentOptions.TopLeft, FontStyles.Bold);
                UiFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(24f, -20f), new Vector2(208f, 48f));

                TextMeshProUGUI status = UiFactory.Text(
                    "Status", button.transform, string.Empty, 32f, mutedColor,
                    TextAlignmentOptions.BottomLeft);
                UiFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(24f, 20f), new Vector2(196f, 44f));

                // Five segments. A skin lights only the middle one and reads as a
                // single swatch; a trail lights all five and reads as a streak, which
                // is the only way a rainbow trail can preview at this size.
                Image[] swatch = new Image[5];

                for (int s = 0; s < swatch.Length; s++)
                {
                    Image segment = UiFactory.Image(
                        $"Swatch_{s}", button.transform, roundedSprite, Color.white);
                    UiFactory.Place(segment.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-24f - (swatch.Length - 1 - s) * 24f, 0f), new Vector2(22f, 84f));
                    swatch[s] = segment;
                }

                shopCards.Add(new ShopCard
                {
                    Button = button,
                    Background = button.image,
                    Name = name,
                    Status = status,
                    Swatch = swatch
                });
            }
        }

        private void BuildLevelSelect()
        {
            RectTransform screen = UiFactory.Screen("Level Select", canvas.transform);
            levelSelectGroup = UiFactory.Group(screen);

            Image dim = UiFactory.Image("Dim", screen, null, new Color(0.04f, 0.03f, 0.09f, 0.95f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            levelSelectPanel = UiFactory.Rect("Panel", screen);
            UiFactory.Place(levelSelectPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1400f));

            TextMeshProUGUI heading = UiFactory.Text(
                "Heading", levelSelectPanel, "LEVELS", 78f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(UiFactory.ContentWidth, 110f));

            totalStarsLabel = UiFactory.Text(
                "Total Stars", levelSelectPanel, string.Empty, 38f, mutedColor);
            UiFactory.Place(totalStarsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(UiFactory.ContentWidth, 56f));

            BuildStageTabs();
            BuildLevelGrid();

            BuildHomeButton(screen, "Home Levels");

        }

        /// <summary>
        /// One tab per stage, because fifty level buttons do not fit on a phone.
        ///
        /// A page per stage rather than a scroll view: the stages are the game's actual
        /// structure, ten is exactly what fits, and a grid that does not move is easier
        /// to hit with a thumb than one that does.
        /// </summary>
        private void BuildStageTabs()
        {
            int stages = StageCount;

            const float width = 138f;
            const float gap = 16f;

            for (int i = 0; i < stages; i++)
            {
                int page = i;
                float x = (i - (stages - 1) * 0.5f) * (width + gap);

                Button tab = UiFactory.Button(
                    $"Stage_{i}", levelSelectPanel, plaqueSprite, secondaryButtonColor,
                    (i + 1).ToString(), 44f, textColor);
                UiFactory.Place(tab.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -200f), new Vector2(width, 88f));
                tab.onClick.AddListener(() => { Click(); SetLevelPage(page); });

                stageTabs.Add(tab);
                stageTabLabels.Add(tab.GetComponentInChildren<TextMeshProUGUI>());
            }
        }

        private void BuildLevelGrid()
        {
            const float cell = 212f;
            const float gap = 28f;

            for (int i = 0; i < LevelsPerPage; i++)
            {
                int slot = i;
                int row = i / LevelsPerRow;
                int column = i % LevelsPerRow;

                float x = (column - (LevelsPerRow - 1) * 0.5f) * (cell + gap);
                float y = -330f - row * (cell + gap);

                Button button = UiFactory.Button(
                    $"Level_{i:D2}", levelSelectPanel, plaqueSprite, secondaryButtonColor,
                    string.Empty, 0f, textColor);
                UiFactory.Place(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, y), new Vector2(cell, cell));

                // The slot's level changes with the page, so the handler asks what it
                // is showing rather than capturing an index that goes stale.
                button.onClick.AddListener(() => OnLevelPressed(levelPage * LevelsPerPage + slot));

                TextMeshProUGUI label = UiFactory.Text(
                    "Number", button.transform, string.Empty, 74f, textColor,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 16f), new Vector2(cell, 100f));

                Image[] stars = new Image[3];

                for (int s = 0; s < 3; s++)
                {
                    Image star = UiFactory.Image($"Star_{s}", button.transform, starSprite, accentColor);
                    UiFactory.Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2((s - 1) * 46f, 26f), new Vector2(38f, 38f));
                    stars[s] = star;
                }

                levelButtons.Add(button);
                levelButtonLabels.Add(label);
                levelButtonStars.Add(stars);
            }
        }

        private void SetLevelPage(int page)
        {
            levelPage = Mathf.Clamp(page, 0, Mathf.Max(0, StageCount - 1));
            RefreshLevelGrid();
        }

        private void BuildHud()
        {
            RectTransform full = UiFactory.Screen("HUD", canvas.transform);
            hudGroup = UiFactory.Group(full);

            // A soft gradient behind the top row instead of a solid bar, so the sky
            // still reads through but the numbers stay legible over a bright horizon.
            Image topFade = UiFactory.Image("Top Fade", full, fadeSprite, new Color(0f, 0f, 0f, 0.94f));
            UiFactory.Band(topFade.rectTransform, 1f, 1f, 0f, 500f);

            // And the same at the bottom, where the lives, the ring budget and the
            // word PERFECTS sit directly on the booth's floor. That floor is a warm
            // mid-brown in three of the five stages, and grey text on it had nothing
            // behind it at all — the contrast was whatever the camera was pointing at.
            Image bottomFade = UiFactory.Image("Bottom Fade", full, fadeSprite, new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Band(bottomFade.rectTransform, 0f, 0.5f, 150f, 300f);
            bottomFade.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            // The gradient is allowed under the cutout — insetting it would leave a
            // bright band across the top of the display. Everything with a number on
            // it is not: the level name sat 2.5% from the top, which is underneath the
            // punch-hole on most handsets and underneath the notch on the rest.
            RectTransform screen = UiFactory.SafeScreen("Safe", full);

            // Moved right to clear the home button that now owns this corner, and
            // narrowed to match. The score is anchored to the other edge and starts at
            // 466 on the narrowest canvas, so 270 is what is going spare here.
            levelNameLabel = UiFactory.Text(
                "Level Name", screen, string.Empty, 44f, textColor,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Place(levelNameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(204f, -44f), new Vector2(258f, 56f));

            levelSubtitleLabel = UiFactory.Text(
                "Level Subtitle", screen, string.Empty, 30f, mutedColor, TextAlignmentOptions.TopLeft);
            UiFactory.Place(levelSubtitleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(204f, -96f), new Vector2(258f, 46f));

            scoreLabel = UiFactory.Text(
                "Score", screen, "0", 96f, accentColor,
                TextAlignmentOptions.TopRight, FontStyles.Bold);
            UiFactory.Place(scoreLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-52f, -116f), new Vector2(305f, 120f));

            pegsLabel = UiFactory.Text(
                "Pegs", screen, string.Empty, 38f, mutedColor, TextAlignmentOptions.TopRight);
            UiFactory.Place(pegsLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-52f, -238f), new Vector2(305f, 50f));

            BuildHomeButton(full, "Home Hud");

            hudTicketBadge = UiFactory.Rect("Tickets", screen);
            UiFactory.Place(hudTicketBadge, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(160f, -172f), new Vector2(240f, 68f));
            hudTicketBadge.localScale = Vector3.zero;

            Image ticketBackground = UiFactory.Image(
                "Background", hudTicketBadge, roundedSprite, new Color(0f, 0f, 0f, 0.42f));
            UiFactory.Stretch(ticketBackground.rectTransform);

            hudTicketLabel = UiFactory.Text(
                "Label", hudTicketBadge, string.Empty, 40f, ticketColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(hudTicketLabel.rectTransform);

            BuildRingRow(screen);
            BuildLivesRow(screen);
            BuildTimeRow(screen);
            BuildComboBadge(screen);
            BuildPullHint(screen);
            BuildRoundBanner(screen);
        }

        /// <summary>
        /// The one moment endless mode hands out. A cleared round has no results
        /// screen to celebrate on — the next board is already coming — so the
        /// acknowledgement has to happen in place, over the game, and get out of the
        /// way on its own.
        /// </summary>
        private void BuildRoundBanner(RectTransform screen)
        {
            roundBanner = UiFactory.Rect("Round Banner", screen);
            UiFactory.Place(roundBanner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 240f), new Vector2(UiFactory.ContentWidth, 260f));
            roundBanner.localScale = Vector3.zero;

            roundBannerLabel = UiFactory.Text(
                "Label", roundBanner, string.Empty, 92f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(roundBannerLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(UiFactory.ContentWidth, 120f));

            roundBannerBonus = UiFactory.Text(
                "Bonus", roundBanner, string.Empty, 54f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(roundBannerBonus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -48f), new Vector2(UiFactory.ContentWidth, 80f));
        }

        /// <summary>
        /// Lives and the run of perfects buying the next one back, in the same corner
        /// the ring budget occupies in Levels mode.
        ///
        /// Same corner on purpose: it is the same question in both modes — how much
        /// more can I get wrong — and the player should not have to look somewhere
        /// else to ask it.
        /// </summary>
        private void BuildLivesRow(RectTransform screen)
        {
            livesRow = UiFactory.Rect("Lives", screen);
            UiFactory.Place(livesRow, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 56f), new Vector2(UiFactory.CanvasWidth - 96f, 170f));

            const float spacing = 84f;

            for (int i = 0; i < EndlessRules.MaxLives; i++)
            {
                Image heart = UiFactory.Image($"Life_{i}", livesRow, heartSprite, lifeColor);
                UiFactory.Place(heart.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(38f + i * spacing, -38f), new Vector2(70f, 70f));
                lifeIcons.Add(heart);
            }

            // The streak reads as progress towards the next heart, so it sits directly
            // under them and uses the same colour.
            for (int i = 0; i < EndlessRules.PerfectsPerLife; i++)
            {
                Image pip = UiFactory.Image($"Pip_{i}", livesRow, circleSprite, lifeColor);
                UiFactory.Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(26f + i * 40f, -102f), new Vector2(24f, 24f));
                streakPips.Add(pip);
            }

            streakLabel = UiFactory.Text(
                "Streak Label", livesRow, "PERFECTS", 26f, mutedColor, TextAlignmentOptions.Left);
            UiFactory.Place(streakLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(226f, -102f), new Vector2(320f, 34f));

            // The same trick the time attack clock uses, for the same reason: a row of
            // icons that only ever changes colour is a readout, and the player is
            // watching a ring rather than the corner. A flash behind them is what makes
            // banking a perfect *land*.
            //
            // Drawn last so it sits over the hearts, and transparent until something
            // happens. Not raycast — it covers the whole corner.
            livesGlow = UiFactory.Image("Glow", livesRow, roundedSprite, new Color(1f, 1f, 1f, 0f));
            UiFactory.Place(livesGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(128f, -68f), new Vector2(320f, 172f));
            livesGlow.raycastTarget = false;

            livesRow.gameObject.SetActive(false);
        }

        /// <summary>
        /// The time attack clock, in the corner the lives and the ring budget share.
        ///
        /// Same corner on purpose, and for the same reason those two share it: it is
        /// one question in all three modes — how much more can I get wrong — and the
        /// player should not have to look somewhere else to ask it depending on which
        /// mode they picked.
        ///
        /// A bar and a number, not just a number. The bar is what is readable in
        /// peripheral vision while the eye is on a ring in flight, which is where the
        /// eye is for most of this mode; the number is what is readable when the bar is
        /// nearly empty and the difference between two and four seconds matters.
        /// </summary>
        private void BuildTimeRow(RectTransform screen)
        {
            timeRow = UiFactory.Rect("Time", screen);
            UiFactory.Place(timeRow, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 56f), new Vector2(UiFactory.CanvasWidth - 96f, 170f));

            // The dial's diameter. Everything else on the clock is a fraction of it,
            // so the whole prop resizes from one number.
            const float diameter = 118f;

            clockRoot = UiFactory.Rect("Clock", timeRow);
            UiFactory.Place(clockRoot, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(94f, -86f), new Vector2(diameter, diameter));

            // -- The prop, drawn before the dial so the case laps over the joins ----
            //
            // A wind-up alarm clock rather than a plain dial. It costs six primitives
            // and it buys the one thing the plain version could not: the clock reads
            // as a *thing on the stall* rather than as a widget the interface has put
            // on top of the game. The rest of this HUD is hearts, rings and tickets —
            // fairground objects — and a bare progress dial was the only element in
            // the frame that looked like software.
            //
            // Brass, because the booth is brass: the peg caps, the ticket colour and
            // the string lights are all in this hue, so the clock joins the scene
            // rather than sitting in front of it.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -1f : 1f;

                Image stem = UiFactory.Image("Bell Stem", clockRoot, roundedSprite, clockCaseColor);
                UiFactory.Place(stem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x * 30f, 40f), new Vector2(14f, 26f));

                Image bell = UiFactory.Image("Bell", clockRoot, circleSprite, clockCaseColor);
                UiFactory.Place(bell.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x * 40f, 52f), new Vector2(46f, 46f));

                // A highlight on each bell. Two flat discs read as spectacles; two
                // discs with a light on the same side read as metal.
                Image shine = UiFactory.Image("Shine", bell.transform, circleSprite,
                    new Color(1f, 1f, 1f, 0.4f));
                UiFactory.Place(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-7f, 7f), new Vector2(15f, 15f));

                Image foot = UiFactory.Image("Foot", clockRoot, roundedSprite, clockCaseColor);
                UiFactory.Place(foot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x * 38f, -56f), new Vector2(30f, 20f));
                foot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, x * -18f);
            }

            Image hammer = UiFactory.Image("Hammer", clockRoot, roundedSprite, clockCaseColor);
            UiFactory.Place(hammer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 56f), new Vector2(11f, 20f));

            Image caseRing = UiFactory.Image("Case", clockRoot, circleSprite, clockCaseColor);
            UiFactory.Stretch(caseRing.rectTransform);

            // -- The dial ----------------------------------------------------------

            Image face = UiFactory.Image("Face", clockRoot, circleSprite, new Color(0.06f, 0.04f, 0.1f, 0.92f));
            UiFactory.Stretch(face.rectTransform, 9f);

            // The remaining time is the wedge. Radial from twelve o'clock, filled
            // clockwise, so it empties the way a dial empties rather than the way a
            // progress bar does — the whole point of dropping the number is that the
            // shape has to be legible without being read.
            clockFill = UiFactory.Image("Fill", clockRoot, circleSprite, lifeColor);
            UiFactory.Stretch(clockFill.rectTransform, 15f);
            clockFill.type = Image.Type.Filled;
            clockFill.fillMethod = Image.FillMethod.Radial360;
            clockFill.fillOrigin = (int)Image.Origin360.Top;
            clockFill.fillClockwise = true;
            clockFill.fillAmount = 1f;

            Image innerRim = UiFactory.Image("Rim", clockRoot, ringSprite, new Color(0f, 0f, 0f, 0.35f));
            UiFactory.Stretch(innerRim.rectTransform, 9f);

            // The hand rides the leading edge of the wedge. Redundant with the wedge
            // on purpose: the wedge says how much is left and the hand says that it is
            // still moving, which is the part a player catches out of the corner of an
            // eye while watching a ring.
            clockHand = UiFactory.Rect("Hand", clockRoot);
            UiFactory.Place(clockHand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(7f, diameter * 0.38f));

            Image handImage = UiFactory.Image("Shaft", clockHand, roundedSprite, textColor);
            UiFactory.Stretch(handImage.rectTransform);

            Image pin = UiFactory.Image("Pin", clockRoot, circleSprite, clockCaseColor);
            UiFactory.Place(pin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(16f, 16f));

            // Sits over everything and is transparent until something happens to the
            // clock. Drawn last so the flash is on top of the dial rather than under it.
            clockGlow = UiFactory.Image("Glow", clockRoot, circleSprite, new Color(1f, 1f, 1f, 0f));
            UiFactory.Stretch(clockGlow.rectTransform, -16f);
            clockGlow.raycastTarget = false;

            timeRow.gameObject.SetActive(false);
        }

        /// <summary>
        /// The first-throw hint: a finger, and three chevrons pointing the way it goes.
        ///
        /// There was already a line of text on the title screen saying "pull back to
        /// throw" and it did not work — people opened the game, saw a ring, and tried
        /// to tap or flick it forwards. That is not a reading problem, it is that a
        /// caption on a *different screen* is not instruction, it is trivia. The
        /// gesture has to be shown where the gesture happens, at the moment it is
        /// wanted, and it has to be shown as a movement rather than described as one.
        ///
        /// So: a dot that slides up towards the ring, chevrons lighting in sequence as
        /// it passes, sitting directly under the ring. (It slid down while the throw was
        /// a slingshot. The owner's own testing reversed the gesture — the forward push
        /// is what players reach for — and the hint follows the gesture.) No words at all — this is the one piece of the
        /// interface that has to work before the player has read anything.
        ///
        /// It goes away for good once somebody has thrown a few times. A tutorial that
        /// is still there on the tenth throw has stopped being help and started being
        /// clutter.
        /// </summary>
        private void BuildPullHint(RectTransform screen)
        {
            pullHint = UiFactory.Rect("Pull Hint", screen);

            // Just under where the ring is held, in the middle. Anchored to the bottom
            // so it tracks the ring rather than the top of a phone of unknown height.
            UiFactory.Place(pullHint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 330f), new Vector2(260f, 300f));

            pullFinger = UiFactory.Image("Finger", pullHint, circleSprite, textColor);
            UiFactory.Place(pullFinger.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(54f, 54f));
            pullFinger.raycastTarget = false;

            for (int i = 0; i < 3; i++)
            {
                // Rounded rects rotated into a chevron rather than a glyph, because a
                // font's arrow is a different weight in every atlas and this one has to
                // read at a glance on a busy fairground.
                RectTransform chevron = UiFactory.Rect($"Chevron_{i}", pullHint);
                UiFactory.Place(chevron, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -78f - i * 62f), new Vector2(92f, 92f));

                Image left = UiFactory.Image("Left", chevron, roundedSprite, textColor);
                UiFactory.Place(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-21f, 0f), new Vector2(14f, 62f));
                left.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -38f);
                left.raycastTarget = false;

                Image right = UiFactory.Image("Right", chevron, roundedSprite, textColor);
                UiFactory.Place(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(21f, 0f), new Vector2(14f, 62f));
                right.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 38f);
                right.raycastTarget = false;

                pullChevrons.Add(left);
                pullChevrons.Add(right);
            }

            pullHint.gameObject.SetActive(false);
        }

        /// <summary>
        /// Whether the game should still be showing somebody how to throw.
        ///
        /// Three throws. One is an accident, two is a coincidence, three is the
        /// gesture — and a player who has thrown three times and still does not have
        /// it is not going to be rescued by an arrow.
        /// </summary>
        private bool NeedsPullHint =>
            director != null && director.Progress != null && director.Progress.ThrowsEver < 3;

        /// <summary>
        /// Runs the hint. Unscaled, because it has to keep moving through the hit stop
        /// and through the round intro, which is exactly when a new player is looking
        /// at it and wondering what to do.
        /// </summary>
        private void Update()
        {
            TickShareCelebration();
            TickOfferTags();

            // The title's two offers count down in real time. Both used to be drawn once,
            // when the title was shown, so "AGAIN IN 120s" sat frozen at 120 until the
            // player left the screen and came back.
            if (current == Screen.Title && Time.unscaledTime >= nextTitleRefresh)
            {
                nextTitleRefresh = Time.unscaledTime + 1f;
                RefreshFreeTickets();
                RefreshShare();
            }

            if (pullHint == null)
            {
                return;
            }

            bool wanted = current == Screen.Hud
                          && director != null
                          && director.State == SessionState.Ready
                          && !director.Throwing.IsAiming
                          && NeedsPullHint;

            if (pullHint.gameObject.activeSelf != wanted)
            {
                pullHint.gameObject.SetActive(wanted);
            }

            if (!wanted)
            {
                return;
            }

            const float period = 1.5f;
            float phase = Mathf.Repeat(Time.unscaledTime, period) / period;

            // The finger travels up the length of the chevrons — the way the ring is
            // about to go — and starts again from the bottom.
            pullFinger.rectTransform.anchoredPosition = new Vector2(0f, -260f + Mathf.SmoothStep(0f, 210f, phase));
            pullFinger.color = new Color(textColor.r, textColor.g, textColor.b,
                Mathf.Sin(phase * Mathf.PI) * 0.9f);

            for (int i = 0; i < pullChevrons.Count; i++)
            {
                // Two images per chevron, so the pair lights together.
                // Bottom chevron first, so the light runs upwards with the finger.
                float step = (2 - i / 2) / 3f;
                float lit = Mathf.Clamp01(1f - Mathf.Abs(phase - step) * 4f);

                pullChevrons[i].color = new Color(
                    textColor.r, textColor.g, textColor.b, 0.18f + lit * 0.72f);
            }
        }

        private void BuildRingRow(RectTransform screen)
        {
            ringRow = UiFactory.Rect("Rings", screen);
            UiFactory.Place(ringRow, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 56f), new Vector2(UiFactory.CanvasWidth - 96f, 90f));

            const int maxRings = 12;

            // Spaced to fit rather than spaced by eye. At 72 units apart the twelfth
            // icon sat 82 units off the right of the screen and the eleventh was
            // clipped, so the two highest-budget levels in the game showed the player
            // ten and a half rings — at the one moment they are counting them.
            float usable = UiFactory.CanvasWidth - 96f;
            float spacing = Mathf.Min(72f, (usable - 8f) / maxRings);
            float size = spacing * 0.86f;

            for (int i = 0; i < maxRings; i++)
            {
                Image icon = UiFactory.Image($"Ring_{i:D2}", ringRow, ringSprite, textColor);
                UiFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(spacing * 0.5f + 4f + i * spacing, 0f), new Vector2(size, size));
                icon.gameObject.SetActive(false);
                ringIcons.Add(icon);
            }

            ringPops = new Sequence[ringIcons.Count];
        }

        /// <summary>
        /// The one way out of a screen: top left, and it goes home.
        ///
        /// Every screen gets the same control in the same place, doing the same thing.
        /// The version this replaced had a BACK at the bottom of three panels, a pause
        /// glyph at the *top right* of the HUD that went to the level select, and a
        /// LEVELS button on the results screen — four affordances, three positions and
        /// two destinations for one idea. A player who has learned where the way out
        /// is should not have to learn it again per screen.
        ///
        /// Anchored inside the safe area, because the top left corner is exactly where
        /// a punch-hole camera lives on most handsets.
        /// </summary>
        private Button BuildHomeButton(RectTransform screen, string name)
        {
            RectTransform safe = UiFactory.SafeScreen(name + " Area", screen);

            Button home = UiFactory.Button(
                name, safe, plaqueSprite, new Color(0f, 0f, 0f, 0.42f), "BACK", 34f, textColor);
            UiFactory.Place(home.image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -36f), new Vector2(152f, 88f));
            home.onClick.AddListener(() => { Click(); GoHome(); });

            return home;
        }

        /// <summary>
        /// Back to the title, from anywhere.
        ///
        /// Whatever was running is stopped on the way out. Without that a run
        /// abandoned mid-throw keeps its board ticking and its clock draining behind
        /// the title screen, and the next PLAY starts on top of it.
        /// </summary>
        private void GoHome()
        {
            if (director != null && director.State != SessionState.Idle)
            {
                director.ReturnToIdle();
            }

            Show(Screen.Title);
        }

        private static void StopIfAlive(ref Sequence sequence)
        {
            if (sequence.isAlive)
            {
                sequence.Stop();
            }
        }

        private static void Restart(ref Sequence slot, Sequence replacement)
        {
            StopIfAlive(ref slot);
            slot = replacement;
        }

        private void BuildComboBadge(RectTransform screen)
        {
            comboBadge = UiFactory.Rect("Combo", screen);
            UiFactory.Place(comboBadge, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-48f, 60f), new Vector2(220f, 100f));
            comboBadge.localScale = Vector3.zero;

            Image background = UiFactory.Image("Background", comboBadge, roundedSprite,
                new Color(0f, 0f, 0f, 0.4f));
            UiFactory.Stretch(background.rectTransform);

            comboLabel = UiFactory.Text(
                "Label", comboBadge, string.Empty, 58f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(comboLabel.rectTransform);
        }

        private void BuildResult()
        {
            RectTransform screen = UiFactory.Screen("Result", canvas.transform);
            resultGroup = UiFactory.Group(screen);

            Image dim = UiFactory.Image("Dim", screen, null, new Color(0.04f, 0.03f, 0.09f, 0.72f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            // Fifty units taller than it was, which is what it takes for the payout
            // line to clear the NEXT button. It did not: the ticket label sat 30 units
            // inside the button's box and every results screen in the game showed
            // "+9 TICKETS" sliced through the middle. Nothing in the code says these
            // two are related — one is measured down from the top of the panel and the
            // other up from the bottom — which is why it survived every screenshot.
            resultPanel = UiFactory.Rect("Panel", screen);
            UiFactory.Place(resultPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1100f));

            Image background = UiFactory.Image("Background", resultPanel, roundedSprite, panelColor);
            UiFactory.Stretch(background.rectTransform);

            resultTitleLabel = UiFactory.Text(
                "Title", resultPanel, string.Empty, 84f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(resultTitleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(UiFactory.ContentWidth, 120f));

            for (int i = 0; i < 3; i++)
            {
                Image star = UiFactory.Image($"Star_{i}", resultPanel, starSprite, accentColor);

                // The middle star sits higher, which reads as a podium rather than a row.
                float lift = i == 1 ? 34f : 0f;
                UiFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((i - 1) * 190f, -280f + lift), new Vector2(160f, 160f));
                star.rectTransform.localScale = Vector3.zero;

                resultStars.Add(star);
            }

            resultScoreLabel = UiFactory.Text(
                "Score", resultPanel, string.Empty, 92f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(resultScoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -450f), new Vector2(UiFactory.ContentWidth, 120f));

            resultBestLabel = UiFactory.Text(
                "Best", resultPanel, string.Empty, 40f, mutedColor);
            UiFactory.Place(resultBestLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -584f), new Vector2(UiFactory.ContentWidth, 56f));

            // Endless has no stars to award, and depth is the number the mode is
            // actually about, so it takes the space the podium would have used.
            resultDepthLabel = UiFactory.Text(
                "Depth", resultPanel, string.Empty, 110f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(resultDepthLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -290f), new Vector2(UiFactory.ContentWidth, 170f));
            resultDepthLabel.gameObject.SetActive(false);

            // The payout, on the screen where the player is looking for it. The HUD
            // badge covers the throw-by-throw trickle; this is the lump sum.
            resultTicketLabel = UiFactory.Text(
                "Tickets", resultPanel, string.Empty, 42f, ticketColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(resultTicketLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -648f), new Vector2(UiFactory.ContentWidth, 56f));

            nextButton = UiFactory.Button(
                "Next", resultPanel, plaqueSprite, primaryButtonColor, "NEXT", 56f, textColor);
            UiFactory.Place(nextButton.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(600f, 140f));
            nextButton.onClick.AddListener(OnNextPressed);

            // A run always ends by losing, which is exactly when an interstitial used to
            // appear. It does not any more: the same advert is offered here instead, as
            // something the player asks for and is paid for. It takes the slot NEXT
            // leaves empty on this screen, because it is the one thing on it worth
            // pressing.
            continueButton = UiFactory.Button(
                "Continue", resultPanel, plaqueSprite, endlessButtonColor,
                string.Empty, 0f, textColor);
            UiFactory.Place(continueButton.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(600f, 140f));
            continueButton.onClick.AddListener(OnContinuePressed);

            continueLabel = UiFactory.Text(
                "Label", continueButton.transform, string.Empty, 44f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(continueLabel.rectTransform, 12f);
            continueButton.gameObject.SetActive(false);

            Button retry = UiFactory.Button(
                "Retry", resultPanel, plaqueSprite, secondaryButtonColor, "RETRY", 48f, textColor);
            retryLabel = retry.GetComponentInChildren<TextMeshProUGUI>();
            UiFactory.Place(retry.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-160f, 80f), new Vector2(290f, 110f));
            retry.onClick.AddListener(OnRetryPressed);

            BuildHomeButton(screen, "Home Result");

            Button levels = UiFactory.Button(
                "Result Levels", resultPanel, plaqueSprite, secondaryButtonColor,
                "LEVELS", 44f, textColor);
            UiFactory.Place(levels.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(160f, 80f), new Vector2(290f, 110f));
            levels.onClick.AddListener(() => { Click(); OpenLevelSelect(); });
        }

        /// <summary>
        /// The front door. Endless, because that is the mode the title screen offers
        /// first — see <see cref="BuildTitle"/>.
        /// </summary>
        private void OnPlayPressed()
        {
            OnEndlessPressed();
        }

        /// <summary>
        /// A sprite wrapping the skin's generated pattern, made once per skin.
        ///
        /// The texture itself comes from the runtime cosmetics director rather than
        /// being generated again here, so the shop card and the ring in the player's
        /// hand are looking at exactly the same bytes.
        /// </summary>
        private Sprite PatternSprite(int index)
        {
            if (cosmeticsDirector == null || cosmetics == null)
            {
                return null;
            }

            RingSkin skin = cosmetics.GetSkin(index);

            if (skin == null || !skin.IsPatterned)
            {
                return null;
            }

            patternSprites ??= new Sprite[cosmetics.SkinCount];

            if (patternSprites[index] == null)
            {
                Texture2D texture = cosmeticsDirector.PatternFor(index);

                if (texture == null)
                {
                    return null;
                }

                patternSprites[index] = Sprite.Create(
                    texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }

            return patternSprites[index];
        }

        private void SetTab(ShopTab tab)
        {
            currentTab = tab;
            RefreshShop();
        }

        /// <summary>
        /// Buys an item, or equips it if it is already owned. One tap does both
        /// because the two are never ambiguous: an item is owned or it is not.
        /// </summary>
        private void OnShopItemPressed(int index)
        {
            ProgressStore progress = director == null ? null : director.Progress;

            if (progress == null || cosmetics == null)
            {
                return;
            }

            bool skins = currentTab == ShopTab.Skins;
            bool owned = skins ? progress.IsSkinOwned(index) : progress.IsTrailOwned(index);

            if (owned)
            {
                bool equipped = skins ? progress.EquipSkin(index) : progress.EquipTrail(index);

                if (equipped)
                {
                    audioDirector?.PlayUiTap();
                    GameEvents.RaiseCosmeticsChanged(progress.EquippedSkin, progress.EquippedTrail);
                }

                RefreshShop();
                return;
            }

            // Explicit null checks rather than ?. — these are UnityEngine.Objects, and
            // the null-conditional operator bypasses Unity's lifetime check.
            RingSkin skin = skins ? cosmetics.GetSkin(index) : null;
            TrailStyle style = skins ? null : cosmetics.GetTrail(index);

            if (skins ? skin == null : style == null)
            {
                return;
            }

            int price = skins ? skin.Price : style.Price;
            bool bought = skins ? progress.BuySkin(index, price) : progress.BuyTrail(index, price);

            if (!bought)
            {
                audioDirector?.PlayDenied();
                return;
            }

            audioDirector?.PlayPurchase();

            // Equipping straight after buying is the only thing anybody wants to do
            // next, and making them tap twice for it would read as the purchase not
            // having worked.
            if (skins)
            {
                progress.EquipSkin(index);
            }
            else
            {
                progress.EquipTrail(index);
            }

            GameEvents.RaiseCosmeticsChanged(progress.EquippedSkin, progress.EquippedTrail);
            RefreshShop();

            // After the refresh, so the card is already showing what it became.
            CelebratePurchase(index, price, skins);
        }

        /// <summary>
        /// Buying something, made to feel like buying something. The owner's note was that
        /// the one moment the shop exists for had almost no feedback at all: tickets now
        /// leave the balance and land on the card, the card punches and flashes in the
        /// item's own colour, its status line stamps in, and there is a burst in that
        /// colour where the tickets arrive.
        /// </summary>
        private void CelebratePurchase(int index, int price, bool skins)
        {
            if (index < 0 || index >= shopCards.Count || director == null || director.Progress == null)
            {
                return;
            }

            ShopCard card = shopCards[index];

            if (card.Background == null || card.Status == null)
            {
                return;
            }

            RectTransform cardRect = card.Background.rectTransform;
            Color colour = PurchaseColour(index, skins);
            Color settled = card.Background.color;

            if (ticketShower != null && shopTicketLabel != null)
            {
                Vector2 onto = ticketShower.LocalOf(cardRect);

                ticketShower.Play(
                    TicketCelebration.Spend, TicketSource.Purchase, Vector2.zero, shopTicketLabel,
                    director.Progress.Tickets + price, director.Progress.Tickets, "{0} TICKETS",
                    toward: onto);

                ticketShower.Burst(onto, colour, 20);
            }

            Haptics.Pulse(HapticStrength.Medium);

            StopIfAlive(ref purchasePop);

            purchasePop = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(cardRect, 1f, 1.14f, 0.16f, Ease.OutBack))
                .Group(Tween.Color(card.Background, settled, colour, 0.16f, Ease.OutQuad))
                .Chain(Tween.Scale(cardRect, 1.14f, 1f, 0.34f, Ease.OutElastic))
                .Group(Tween.Color(card.Background, colour, settled, 0.34f, Ease.OutQuad))
                .Group(Tween.Scale(card.Status.rectTransform, 0f, 1f, 0.32f, Ease.OutBack));
        }

        /// <summary>The item's own colour: a skin's tint, a trail's head, the accent as a fallback.</summary>
        private Color PurchaseColour(int index, bool skins)
        {
            if (cosmetics == null)
            {
                return accentColor;
            }

            if (skins)
            {
                RingSkin skin = cosmetics.GetSkin(index);
                return skin == null ? accentColor : skin.TintFor(accentColor, Time.unscaledTime);
            }

            TrailStyle trail = cosmetics.GetTrail(index);

            if (trail == null)
            {
                return accentColor;
            }

            return trail.ColorMode == TrailColorMode.Rainbow
                ? Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.2f, 1f), 0.8f, 1f)
                : trail.Color;
        }

        private void RefreshShop()
        {
            ProgressStore progress = director == null ? null : director.Progress;

            if (progress == null || cosmetics == null)
            {
                return;
            }

            bool skins = currentTab == ShopTab.Skins;
            int count = skins ? cosmetics.SkinCount : cosmetics.TrailCount;
            int equipped = skins ? progress.EquippedSkin : progress.EquippedTrail;

            if (ticketShower == null || !ticketShower.IsCounting(shopTicketLabel))
            {
                shopTicketLabel.SetText("{0} TICKETS", progress.Tickets);
            }

            RefreshFreeTickets();

            skinsTab.image.color = skins ? primaryButtonColor : secondaryButtonColor;
            trailsTab.image.color = skins ? secondaryButtonColor : primaryButtonColor;

            for (int i = 0; i < shopCards.Count; i++)
            {
                ShopCard card = shopCards[i];
                bool exists = i < count;

                card.Button.gameObject.SetActive(exists);

                if (!exists)
                {
                    continue;
                }

                bool owned = skins ? progress.IsSkinOwned(i) : progress.IsTrailOwned(i);
                bool isEquipped = owned && i == equipped;
                int price = skins ? cosmetics.GetSkin(i).Price : cosmetics.GetTrail(i).Price;

                card.Name.SetText(skins
                    ? cosmetics.GetSkin(i).DisplayName
                    : cosmetics.GetTrail(i).DisplayName);

                if (isEquipped)
                {
                    card.Status.SetText("EQUIPPED");
                    card.Status.color = Color.Lerp(ownedColor, Color.white, 0.55f);
                }
                else if (owned)
                {
                    card.Status.SetText("TAP TO WEAR");
                    card.Status.color = mutedColor;
                }
                else
                {
                    card.Status.SetText("{0} TICKETS", price);
                    card.Status.color = price <= progress.Tickets ? ticketColor : mutedColor;
                }

                card.Background.color = isEquipped
                    ? new Color(ownedColor.r * 0.5f, ownedColor.g * 0.5f, ownedColor.b * 0.5f, 1f)
                    : secondaryButtonColor;

                card.Name.color = owned ? textColor : mutedColor;

                PaintSwatch(card, i, skins, owned);
            }
        }

        /// <summary>
        /// Draws the item preview. A skin gets one lit segment; a trail gets all five,
        /// tapering, which is the only way its length, width and colour sweep can be
        /// told apart from another trail's at this size.
        /// </summary>
        private void PaintSwatch(in ShopCard card, int index, bool skins, bool owned)
        {
            float dim = owned ? 1f : 0.42f;

            if (skins)
            {
                RingSkin skin = cosmetics.GetSkin(index);

                // The real generated pattern, not an approximation of it. The whole
                // point of the paid skins is that they are printed rather than tinted,
                // and a card that shows a flat colour sells the wrong thing.
                for (int s = 0; s < card.Swatch.Length; s++)
                {
                    card.Swatch[s].gameObject.SetActive(s == 0);
                }

                Image swatch = card.Swatch[0];
                Sprite sprite = PatternSprite(index);

                swatch.sprite = sprite;
                swatch.rectTransform.sizeDelta = new Vector2(126f, 86f);
                swatch.rectTransform.anchoredPosition = new Vector2(-26f, 0f);

                Color color = sprite == null
                    ? skin.TintFor(accentColor, Time.unscaledTime)
                    : Color.white;

                swatch.color = new Color(color.r, color.g, color.b, dim);
                return;
            }

            TrailStyle trail = cosmetics.GetTrail(index);

            for (int s = 0; s < card.Swatch.Length; s++)
            {
                card.Swatch[s].sprite = roundedSprite;
                card.Swatch[s].rectTransform.anchoredPosition =
                    new Vector2(-26f - (card.Swatch.Length - 1 - s) * 26f, 0f);
                // s = 0 is the far end of the trail, s = last is the ring end.
                float along = 1f - s / (float)(card.Swatch.Length - 1);

                Color color = trail.ColorMode == TrailColorMode.Rainbow
                    ? Color.HSVToRGB(Mathf.Repeat(along * trail.RainbowSpan, 1f), 0.8f, 1f)
                    : trail.Color;

                float alpha = Mathf.Lerp(trail.StartAlpha, trail.EndAlpha, along);
                float width = Mathf.Lerp(trail.StartWidth, trail.EndWidth, along);

                card.Swatch[s].gameObject.SetActive(true);
                card.Swatch[s].color = new Color(color.r, color.g, color.b, alpha * dim);
                card.Swatch[s].rectTransform.sizeDelta =
                    new Vector2(20f, Mathf.Clamp(width * 300f, 8f, 96f));
            }
        }

        /// <summary>
        /// Awards from play. The title's own awards — a chosen advert, a share — are
        /// celebrated where they are made, because only there is it known which button
        /// paid and whether a share sheet is covering the game.
        /// </summary>
        private void OnTicketsAwarded(int amount, int total, TicketSource source)
        {
            if (source != TicketSource.Play)
            {
                return;
            }

            ticketsThisRun += amount;

            if (hudTicketLabel == null)
            {
                return;
            }

            StopIfAlive(ref ticketPop);

            // Shown as the amount just earned rather than the balance. The balance is
            // in the shop; what the player needs to see mid-level is that the throw
            // they just made was worth something. Held long enough now for the tickets
            // to land in it — it used to be gone before anything could have arrived.
            ticketPop = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(hudTicketBadge, 0f, 1.16f, 0.18f, Ease.OutBack))
                .Chain(Tween.Scale(hudTicketBadge, 1.16f, 1f, 0.16f, Ease.OutCubic))
                .ChainDelay(1.15f)
                .Chain(Tween.Scale(hudTicketBadge, 1f, 0f, 0.2f, Ease.InBack));

            if (ticketShower != null && current == Screen.Hud)
            {
                // From the board, where it was earned, up into the badge.
                ticketShower.Play(
                    amount >= 10 ? TicketCelebration.Payout : TicketCelebration.Trickle,
                    TicketSource.Play, new Vector2(0f, -240f),
                    hudTicketLabel, 0, amount, "+{0}");
                return;
            }

            hudTicketLabel.SetText("+{0}", amount);
        }

        private void OnEndlessPressed()
        {
            Click();
            Show(Screen.Hud);
            director.StartEndless();
        }

        private void OnTimeAttackPressed()
        {
            Click();
            Show(Screen.Hud);
            director.StartTimeAttack();
        }

        private void OnLevelPressed(int index)
        {
            Click();

            if (director?.Progress != null && !director.Progress.IsUnlocked(index))
            {
                // Locked buttons are already non-interactive; this is the belt to that
                // brace, so a stray call can never skip the progression.
                return;
            }

            StartLevel(index);
        }

        private void OnRetryPressed()
        {
            Click();
            Show(Screen.Hud);
            director.RestartLevel();
        }

        private void OnNextPressed()
        {
            Click();
            Show(Screen.Hud);

            if (!director.StartNextLevel())
            {
                Show(Screen.Title);
            }
        }

        private void OnLivesChanged(int lives, int max)
        {
            RefreshLives(lives);

            bool lost = shownLives >= 0 && lives < shownLives;
            int died = lives;

            shownLives = lives;

            if (!lost)
            {
                return;
            }

            // The heart that went out is punched *in* rather than popped out. A loss
            // that animates the same way as a gain is a loss the player has to read
            // the colour of to understand, and by then the moment is over.
            if (died >= 0 && died < lifeIcons.Count)
            {
                RectTransform heart = lifeIcons[died].rectTransform;

                Tween.StopAll(heart);
                Tween.Scale(heart, 1.5f, 0.78f, 0.44f, Ease.OutElastic, useUnscaledTime: true);
            }

            FlashLives(warningColor, 0.85f);
        }

        private void RefreshLives(int lives)
        {
            for (int i = 0; i < lifeIcons.Count; i++)
            {
                bool held = i < lives;

                // A spent life dims in place rather than disappearing, so the row keeps
                // its width and the player reads how far from the end they are, not
                // just how much they have.
                lifeIcons[i].color = held ? lifeColor : new Color(1f, 1f, 1f, 0.13f);
                lifeIcons[i].rectTransform.localScale = Vector3.one * (held ? 1f : 0.78f);
            }
        }

        private void OnPerfectStreakChanged(int streak, int needed)
        {
            for (int i = 0; i < streakPips.Count; i++)
            {
                bool lit = i < streak;
                streakPips[i].color = lit ? lifeColor : new Color(1f, 1f, 1f, 0.16f);
                streakPips[i].rectTransform.localScale = Vector3.one * (lit ? 1.15f : 0.85f);
            }

            streakLabel.SetText(streak <= 0 ? "PERFECTS" : "{0} / {1}", streak, needed);

            // Only a *gain* celebrates. The streak is also reset to zero by every miss,
            // and flashing green on the frame a player loses a life would be the game
            // congratulating them for it.
            bool banked = streak > shownStreak && streak > 0;
            shownStreak = streak;

            if (!banked)
            {
                return;
            }

            // The pip that just lit, popped on its own. The row-wide flash is kept for
            // the life itself — a perfect is a step towards something, and it should
            // feel smaller than arriving.
            int index = Mathf.Clamp(streak - 1, 0, streakPips.Count - 1);

            if (streakPips.Count > 0)
            {
                RectTransform pip = streakPips[index].rectTransform;

                Tween.StopAll(pip);
                Tween.Scale(pip, 2.1f, 1.15f, 0.42f, Ease.OutElastic, useUnscaledTime: true);
            }

            FlashLives(timeGainColor, 0.45f);
        }

        private void OnStreakRewarded(bool lifeGranted)
        {
            if (!lifeGranted)
            {
                return;
            }

            StopIfAlive(ref livesPop);

            livesPop = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(livesRow, 1f, 1.24f, 0.12f, Ease.OutCubic))
                .Chain(Tween.Scale(livesRow, 1.24f, 1f, 0.42f, Ease.OutElastic));

            // Twice the perfect's flash, because arriving is worth more than a step
            // towards arriving, and the two have to be told apart at a glance.
            FlashLives(timeGainColor, 0.95f);
        }

        /// <summary>
        /// Washes the lives corner in a colour and lets it fade.
        ///
        /// One routine for all three events — perfect banked, life won, life lost —
        /// differing only in colour and strength, so the three read as the same
        /// language rather than as three unrelated effects. Unscaled, so it survives
        /// the hit stop that fires on the very catch it is reporting.
        /// </summary>
        private void FlashLives(Color colour, float strength)
        {
            if (livesGlow == null)
            {
                return;
            }

            // Its own handle rather than Tween.StopAll(this): this object is also the
            // target of the score count-up and both of the clock's tweens, and stopping
            // everything aimed at it would freeze the score mid-climb every time a
            // perfect landed.
            Restart(ref livesFlash, Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Custom(this, strength, 0f, 0.5f,
                    (root, alpha) => root.livesGlow.color =
                        new Color(colour.r, colour.g, colour.b, alpha),
                    Ease.OutQuad)));
        }

        private void OnRoundCleared(int round, int bonus)
        {
            roundBannerLabel.SetText("ROUND {0} CLEAR", round);
            roundBannerBonus.SetText("+{0}", bonus);

            StopIfAlive(ref roundBannerPop);

            // In, held, out — the whole thing has to fit inside the pause before the
            // next board arrives, or it is still on screen while the player is aiming.
            roundBannerPop = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(roundBanner, 0f, 1.14f, 0.26f, Ease.OutBack))
                .Chain(Tween.Scale(roundBanner, 1.14f, 1f, 0.22f, Ease.OutElastic))
                .ChainDelay(0.72f)
                .Chain(Tween.Scale(roundBanner, 1f, 0f, 0.24f, Ease.InBack));
        }

        private void HideRoundBanner()
        {
            StopIfAlive(ref roundBannerPop);
            roundBanner.localScale = Vector3.zero;
        }

        /// <summary>
        /// Ends a run: the advert first, if the pacing allows one, then the score.
        ///
        /// That order is the whole of it. An advert on top of a results screen makes
        /// the player read their own score twice, once through something they did not
        /// ask for; an advert in front of it is a beat between two parts of the game,
        /// which is what an interstitial is supposed to be.
        ///
        /// The callback always fires — suppressed, unfilled and watched all arrive
        /// here — so there is no path where a run ends and the results never appear.
        /// </summary>
        private void OnRunEnded(EndlessResult result)
        {
            HideRoundBanner();

            if (adService == null)
            {
                ShowRunResult(result);
                return;
            }

            // No advert here. Every run ends by losing, so an interstitial on this
            // screen is an advert for failing — which is exactly when a player is most
            // likely to put the game down. What this screen offers instead is a
            // rewarded continue: the same advert, asked for rather than served.
            ShowRunResult(result);
        }

        private void StartLevel(int index)
        {
            Show(Screen.Hud);
            director.StartLevel(index);
        }

        private void OpenLevelSelect()
        {
            director.ReturnToIdle();
            Show(Screen.LevelSelect);
        }

        private void OnLevelStarted(int levelIndex)
        {
            // Driven by the event rather than by the button that happened to be
            // pressed, so a level started from anywhere — a retry, a next-level jump,
            // a test — always brings the HUD up with it.
            Show(Screen.Hud);

            LevelConfig level = director.CurrentLevel;

            levelNameLabel.SetText(level == null ? string.Empty : level.DisplayName);
            levelSubtitleLabel.SetText(level == null ? string.Empty : level.Subtitle);

            // Endless carries its score across rounds, so the HUD reads the director
            // rather than assuming a new board means a new run.
            // Endless raises this for every round, and its payout is the whole run's,
            // so the counter only resets when a run actually begins.
            if (!IsGeneratedRun || director.CurrentRound <= 1)
            {
                ticketsThisRun = 0;
            }

            displayedScore = director.Score;
            targetScore = displayedScore;
            scoreLabel.SetText("{0}", displayedScore);

            pegsTotal = level == null ? 0 : level.PegCount;
            pegsClaimed = 0;

            ApplyModeToHud();

            SetComboVisible(0);
            RefreshPegs();
            HideRoundBanner();
        }

        /// <summary>
        /// Puts the running mode's resource in the bottom-left corner: the ring budget,
        /// the lives, or the clock. Exactly one of the three is ever up.
        /// </summary>
        private void ApplyModeToHud()
        {
            bool endless = IsEndless;
            bool timed = IsTimeAttack;

            ringRow.gameObject.SetActive(!endless && !timed);
            livesRow.gameObject.SetActive(endless);
            timeRow.gameObject.SetActive(timed);

            if (timed)
            {
                timeRow.localScale = Vector3.one;

                // Cleared of whatever the last run left on it: a dial still mid-flash
                // or mid-wobble when a new run opens reads as the game reacting to a
                // throw that has not happened.
                StopIfAlive(ref clockPop);
                clockRoot.localScale = Vector3.one;
                clockRoot.localRotation = Quaternion.identity;
                clockGlow.color = new Color(1f, 1f, 1f, 0f);

                OnTimeChanged(director.TimeAttack.Remaining, TimeAttackRules.MaxSeconds);
                return;
            }

            if (!endless)
            {
                return;
            }

            livesRow.localScale = Vector3.one;

            // Cleared so the first draw of a run is not read as a change: without this
            // a fresh board flashes red for the lives it "lost" coming back from three
            // to three, and green for the streak it "gained" going to zero.
            shownLives = -1;
            shownStreak = -1;
            livesGlow.color = new Color(1f, 1f, 1f, 0f);

            RefreshLives(director.Endless.Lives);
            OnPerfectStreakChanged(director.Endless.PerfectStreak, EndlessRules.PerfectsPerLife);

            shownLives = director.Endless.Lives;
            shownStreak = director.Endless.PerfectStreak;
        }

        /// <summary>
        /// Draws the clock: a wedge that empties and a hand that rides its edge.
        ///
        /// No number. A count of seconds is the most precise way to say how much time
        /// is left and the worst way to make somebody feel it — reading it costs a
        /// glance away from a ring in flight, which in a mode about speed is the one
        /// thing the HUD must not ask for. A dial is legible in peripheral vision.
        ///
        /// The colour bleeds towards the warning over the last few seconds rather than
        /// switching at a threshold. A dial that changes colour on one frame reads as a
        /// bug the first time and as a jump scare afterwards; one that bleeds into red
        /// over five seconds is the player being told to hurry while there is still
        /// time to do something about it.
        /// </summary>
        private void OnTimeChanged(float remaining, float max)
        {
            if (clockFill == null)
            {
                return;
            }

            float fraction = max <= 0f ? 0f : Mathf.Clamp01(remaining / max);

            clockFill.fillAmount = fraction;

            // Negative because a positive Z rotation turns anticlockwise in Unity, and
            // a clock does not.
            clockHand.localRotation = Quaternion.Euler(0f, 0f, -360f * fraction);

            float urgency = 1f - Mathf.Clamp01(remaining / TimeWarningSeconds);
            clockFill.color = Color.Lerp(lifeColor, warningColor, urgency);
        }

        /// <summary>
        /// The clock reacting to time won or lost.
        ///
        /// Two different motions rather than one motion in two colours, because the
        /// difference has to survive being seen out of the corner of an eye: a gain
        /// pops the dial *outwards* and flashes green, a loss punches it *inwards* and
        /// flashes red. Both settle on OutElastic, which is what turns a scale change
        /// into something that feels like it was struck.
        ///
        /// Kept small and quick on purpose. This fires several times a round, and
        /// feedback at the size of a level-clear celebration would be exhausting by
        /// the third board and would sit on top of the catch it is reporting on.
        /// </summary>
        private void OnTimeAwarded(float seconds)
        {
            if (clockRoot == null || Mathf.Abs(seconds) < 0.01f || !IsTimeAttack)
            {
                return;
            }

            bool gained = seconds > 0f;
            Color flash = gained ? timeGainColor : warningColor;
            float peak = gained ? 1.24f : 0.82f;
            float tilt = gained ? 9f : -13f;

            Restart(ref clockPop, Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(clockRoot, 1f, peak, 0.1f, Ease.OutCubic))
                .Chain(Tween.Scale(clockRoot, peak, 1f, 0.42f, Ease.OutElastic)));

            // The wobble rides on the same elastic settle. Starting at an angle and
            // easing to zero gives the decaying swing for free; hand-rolling an
            // oscillation here would be three more fields and a worse curve.
            Tween.Custom(this, tilt, 0f, 0.5f,
                (root, angle) => root.clockRoot.localRotation = Quaternion.Euler(0f, 0f, angle),
                Ease.OutElastic, useUnscaledTime: true);

            Tween.Custom(this, 0.85f, 0f, 0.44f,
                (root, alpha) => root.clockGlow.color = new Color(flash.r, flash.g, flash.b, alpha),
                Ease.OutQuad, useUnscaledTime: true);
        }

        private void OnScoreChanged(int total, int delta)
        {
            targetScore = total;

            if (delta <= 0)
            {
                displayedScore = total;
                scoreLabel.SetText("{0}", total);
                return;
            }

            int from = displayedScore;
            float duration = juiceConfig == null ? 0.42f : juiceConfig.ScoreCountUpDuration;
            float pop = juiceConfig == null ? 1.28f : juiceConfig.ScorePopScale;
            float popDuration = juiceConfig == null ? 0.3f : juiceConfig.ScorePopDuration;

            // Counting up rather than swapping is the whole point: the player watches
            // the number climb and reads the size of the reward from how long it takes.
            Tween.Custom(this, from, total, duration, (owner, value) =>
            {
                owner.displayedScore = Mathf.RoundToInt(value);
                owner.scoreLabel.SetText("{0}", owner.displayedScore);
            }, Ease.OutCubic, useUnscaledTime: true);

            Restart(ref scorePop, Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(scoreLabel.rectTransform, 1f, pop, popDuration * 0.35f, Ease.OutBack))
                .Chain(Tween.Scale(scoreLabel.rectTransform, pop, 1f, popDuration * 0.65f, Ease.OutCubic)));
        }

        private void OnComboChanged(int combo)
        {
            SetComboVisible(combo);
        }

        private void SetComboVisible(int combo)
        {
            bool visible = combo >= 2;

            if (visible)
            {
                comboLabel.SetText("x{0}", combo);

                StopIfAlive(ref comboPop);

                comboPop = Sequence.Create(useUnscaledTime: true)
                    .Group(Tween.Scale(comboBadge, comboBadge.localScale.x, 1.25f, 0.12f, Ease.OutCubic))
                    .Chain(Tween.Scale(comboBadge, 1.25f, 1f, 0.3f, Ease.OutElastic));

                return;
            }

            if (comboBadge.localScale.x <= 0.01f)
            {
                return;
            }

            StopIfAlive(ref comboPop);
            Tween.Scale(comboBadge, comboBadge.localScale.x, 0f, 0.18f, Ease.InBack, useUnscaledTime: true);
        }

        private void OnRingsRemainingChanged(int remaining)
        {
            RefreshRings(remaining);
        }

        private void OnPegClaimed(SlipperyZlope.RingToss.Gameplay.PegTarget peg, int pegsLeft)
        {
            // The event carries what is left rather than what has been taken, which is
            // the number the rest of the game cares about; the HUD wants the other one.
            pegsClaimed = Mathf.Max(0, pegsTotal - pegsLeft);
            RefreshPegs();

            Restart(ref pegsPop, Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(pegsLabel.rectTransform, 1f, 1.22f, 0.1f, Ease.OutCubic))
                .Chain(Tween.Scale(pegsLabel.rectTransform, 1.22f, 1f, 0.26f, Ease.OutBack)));
        }

        private void RefreshPegs()
        {
            if (pegsTotal <= 0)
            {
                pegsLabel.SetText(string.Empty);
                return;
            }

            pegsLabel.SetText("PEGS {0}/{1}", pegsClaimed, pegsTotal);
        }

        private void RefreshRings(int remaining)
        {
            if (IsGeneratedRun)
            {
                // Rings are not the resource in either generated mode — lives and the
                // clock are, and both have their own row.
                return;
            }

            LevelConfig level = director.CurrentLevel;
            int total = level == null ? 0 : level.RingCount;

            for (int i = 0; i < ringIcons.Count; i++)
            {
                bool used = i >= remaining;
                bool exists = i < total;

                ringIcons[i].gameObject.SetActive(exists);

                if (!exists)
                {
                    continue;
                }

                Color target = used ? new Color(1f, 1f, 1f, 0.18f) : textColor;

                if (ringIcons[i].color == target)
                {
                    continue;
                }

                Image icon = ringIcons[i];
                StopIfAlive(ref ringPops[i]);

                if (used)
                {
                    // A used ring dims and shrinks a little rather than disappearing, so
                    // the row keeps its shape and the count stays readable at a glance.
                    ringPops[i] = Sequence.Create(useUnscaledTime: true)
                        .Group(Tween.Scale(icon.rectTransform, 1f, 1.3f, 0.1f, Ease.OutCubic))
                        .Chain(Tween.Scale(icon.rectTransform, 1.3f, 0.82f, 0.24f, Ease.OutBack))
                        .Group(Tween.Color(icon, icon.color, target, 0.24f, Ease.OutQuad));
                }
                else
                {
                    icon.rectTransform.localScale = Vector3.one;
                    icon.color = target;
                }
            }
        }

        private void OnLevelEnded(LevelResult result)
        {
            // The same shape as the end of a run: the advert, if pacing allows one, goes
            // *before* the result, so the player reads their score once and not through
            // an advert. This path used to request nothing at all — TryShowInterstitial
            // existed, was tested, and had no caller — so the levels mode could never
            // show an interstitial whatever the rules said.
            if (adService == null)
            {
                ShowResult(result);
                return;
            }

            // Cleared levels may carry one, subject to pacing. A failed level may not:
            // phase 18 allowed it and the owner has now asked for it to stop, which is
            // the same rule as the run screen above and for the same reason.
            if (!result.Completed)
            {
                ShowResult(result);
                return;
            }

            adService.TryShowLevelEndInterstitial(result, _ => ShowResult(result));
        }

        private void ShowResult(LevelResult result)
        {
            resultTitleLabel.SetText(result.Completed ? "CLEARED" : "OUT OF RINGS");
            resultTitleLabel.color = result.Completed ? accentColor : mutedColor;

            resultScoreLabel.SetText("{0}", result.Score);

            int best = director.Progress == null ? result.Score : director.Progress.BestScoreFor(result.LevelIndex);
            resultBestLabel.SetText(result.IsNewBest ? "NEW BEST" : $"BEST {best}");

            bool hasNext = result.Completed && result.LevelIndex + 1 < director.LevelCount;
            nextButton.gameObject.SetActive(hasNext);
            nextButton.interactable = hasNext;

            retryLabel?.SetText("RETRY");

            // Levels do not get a continue. A level is a fixed budget of rings against a
            // fixed board — buying another throw would be buying the puzzle's answer —
            // and it already has the right offer next to it, which is RETRY.
            continueButton?.gameObject.SetActive(false);
            resultDepthLabel.gameObject.SetActive(false);
            ShowTicketPayout();

            for (int i = 0; i < resultStars.Count; i++)
            {
                resultStars[i].gameObject.SetActive(true);
                resultStars[i].rectTransform.localScale = Vector3.zero;
                resultStars[i].color = i < result.Stars ? accentColor : new Color(1f, 1f, 1f, 0.14f);
            }

            Show(Screen.Result);

            // Stars land one at a time. Awarding them all at once throws away the only
            // moment in the level where anticipation is free.
            for (int i = 0; i < resultStars.Count; i++)
            {
                RectTransform star = resultStars[i].rectTransform;
                float delay = 0.28f + i * 0.16f;

                Sequence.Create(useUnscaledTime: true)
                    .ChainDelay(delay)
                    .Group(Tween.Scale(star, 0f, 1.28f, 0.22f, Ease.OutBack))
                    .Chain(Tween.Scale(star, 1.28f, 1f, 0.24f, Ease.OutElastic));
            }

            RefreshLevelGrid();
        }

        /// <summary>
        /// The end of an endless run. Same panel, different headline: there is nothing
        /// to award here, only how deep the player got.
        /// </summary>
        /// <summary>
        /// Shows the rewarded continue when there is genuinely one to show.
        ///
        /// Hidden rather than greyed when the advert is not available, because this
        /// button is the difference between a run ending and not ending: a dead control
        /// in that position reads as the game refusing, and a player who has just lost
        /// is the last person to give the benefit of the doubt.
        /// </summary>
        private void RefreshContinueOffer()
        {
            if (continueButton == null)
            {
                return;
            }

            bool offer = director != null
                         && director.CanContinueRun
                         && adService != null
                         && adService.CanOffer(AdPlacement.RewardedContinue);

            continueButton.gameObject.SetActive(offer);
            continueButton.interactable = offer;

            if (offer && continueLabel != null)
            {
                continueLabel.SetText(IsTimeAttack
                    ? $"WATCH AD · +{Mathf.RoundToInt(TimeAttackRules.ContinueSeconds)}s"
                    : "WATCH AD · +1 LIFE");
            }
        }

        private void OnContinuePressed()
        {
            Click();

            if (director == null || adService == null || !director.CanContinueRun)
            {
                return;
            }

            continueButton.interactable = false;

            adService.ShowRewarded(AdPlacement.RewardedContinue, result =>
            {
                // Watched, and only watched. A skip, a no-fill or a network failure has
                // to leave the run over and the player exactly where they were — which
                // includes leaving the offer on screen to try again.
                if (result != AdResult.Watched)
                {
                    RefreshContinueOffer();
                    return;
                }

                continueButton.gameObject.SetActive(false);
                director.ContinueRun();
                Show(Screen.Hud);
            });
        }

        private void ShowRunResult(EndlessResult result)
        {
            // Time attack says what ran out. "RUN OVER" is true of both modes and tells
            // a player who has just watched a clock empty nothing they did not know.
            resultTitleLabel.SetText(IsTimeAttack ? "TIME UP" : "RUN OVER");
            resultTitleLabel.color = mutedColor;

            resultScoreLabel.SetText("{0}", result.Score);
            resultBestLabel.SetText(result.IsNewBest ? "NEW BEST" : $"BEST {result.Best}");

            nextButton.gameObject.SetActive(false);
            nextButton.interactable = false;
            retryLabel?.SetText("PLAY AGAIN");

            RefreshContinueOffer();

            for (int i = 0; i < resultStars.Count; i++)
            {
                resultStars[i].gameObject.SetActive(false);
            }

            ShowTicketPayout();

            resultDepthLabel.gameObject.SetActive(true);
            resultDepthLabel.SetText(
                result.RoundsCleared == 1 ? "1 ROUND" : $"{result.RoundsCleared} ROUNDS");
            resultDepthLabel.rectTransform.localScale = Vector3.zero;

            Show(Screen.Result);

            Sequence.Create(useUnscaledTime: true)
                .ChainDelay(0.26f)
                .Group(Tween.Scale(resultDepthLabel.rectTransform, 0f, 1.3f, 0.24f, Ease.OutBack))
                .Chain(Tween.Scale(resultDepthLabel.rectTransform, 1.3f, 1f, 0.26f, Ease.OutElastic));
        }

        private void RefreshLevelGrid()
        {
            ProgressStore progress = director.Progress;

            if (progress == null)
            {
                return;
            }

            int levelCount = director.LevelCount;
            int first = levelPage * LevelsPerPage;

            for (int slot = 0; slot < levelButtons.Count; slot++)
            {
                int index = first + slot;
                bool exists = index < levelCount;

                levelButtons[slot].gameObject.SetActive(exists);

                if (!exists)
                {
                    continue;
                }

                bool unlocked = progress.IsUnlocked(index);
                int stars = progress.StarsFor(index);

                levelButtons[slot].interactable = unlocked;
                levelButtons[slot].image.color =
                    unlocked ? secondaryButtonColor : new Color(0.16f, 0.15f, 0.22f, 1f);
                levelButtonLabels[slot].SetText(unlocked ? (index + 1).ToString() : "-");
                levelButtonLabels[slot].color = unlocked ? textColor : mutedColor;

                Image[] icons = levelButtonStars[slot];

                for (int s = 0; s < icons.Length; s++)
                {
                    icons[s].color = s < stars ? accentColor : new Color(1f, 1f, 1f, 0.12f);
                    icons[s].gameObject.SetActive(unlocked);
                }
            }

            RefreshStageTabs(progress, levelCount);

            totalStarsLabel.SetText("{0} / {1} STARS",
                progress.TotalStars(levelCount), levelCount * 3);
        }

        /// <summary>
        /// Marks the current page and locks the stages the player has not reached.
        /// A stage opens as soon as any level inside it does.
        /// </summary>
        private void RefreshStageTabs(ProgressStore progress, int levelCount)
        {
            for (int i = 0; i < stageTabs.Count; i++)
            {
                int firstOfStage = i * LevelsPerPage;
                bool open = firstOfStage < levelCount && progress.IsUnlocked(firstOfStage);

                stageTabs[i].interactable = open;
                stageTabs[i].image.color = i == levelPage
                    ? primaryButtonColor
                    : open ? secondaryButtonColor : new Color(0.16f, 0.15f, 0.22f, 1f);

                stageTabLabels[i].color = open ? textColor : mutedColor;
            }
        }

        /// <summary>
        /// Shows what the run just paid, counted from the last board that started
        /// rather than read off the balance — the balance is the shop's business, and
        /// what the player wants to know here is whether the run was worth making.
        /// </summary>
        private void ShowTicketPayout()
        {
            if (resultTicketLabel == null)
            {
                return;
            }

            if (ticketsThisRun <= 0)
            {
                resultTicketLabel.SetText(string.Empty);
                return;
            }

            if (ticketShower == null)
            {
                resultTicketLabel.SetText("+{0} TICKETS", ticketsThisRun);
                return;
            }

            // The lump sum, counted in as the panel settles: tickets drop into the line
            // from above, rather than the number simply being there when it opens.
            Vector2 above = ticketShower.LocalOf(resultTicketLabel.rectTransform) + new Vector2(0f, 380f);

            ticketShower.Play(
                TicketCelebration.Payout, TicketSource.Play, above,
                resultTicketLabel, 0, ticketsThisRun, "+{0} TICKETS", delay: 0.5f);
        }

        private void RefreshEndlessBest()
        {
            if (endlessBestLabel == null)
            {
                return;
            }

            ProgressStore progress = director == null ? null : director.Progress;
            int rounds = progress == null ? 0 : progress.BestEndlessRound;

            endlessBestLabel.SetText(
                rounds <= 0 ? "three lives, no end" : $"best: {rounds} rounds");

            if (timeAttackBestLabel == null)
            {
                return;
            }

            int timedRounds = progress == null ? 0 : progress.BestTimeAttackRound;

            timeAttackBestLabel.SetText(
                timedRounds <= 0 ? "beat the clock, not the odds" : $"best: {timedRounds} rounds");
        }

        private void RefreshTitleTickets()
        {
            if (titleTicketLabel == null)
            {
                return;
            }

            // Not while tickets are counting into it, or waiting to: the celebration owns
            // the number until the last one lands.
            if (shareCelebrationPending || (ticketShower != null && ticketShower.IsCounting(titleTicketLabel)))
            {
                return;
            }

            ProgressStore progress = director == null ? null : director.Progress;
            titleTicketLabel.SetText(progress == null ? string.Empty : $"{progress.Tickets} tickets");
        }

        /// <summary>The MODO DEV badges, on whenever the ProgressStore's switch is.</summary>
        private void RefreshModoDev()
        {
            bool on = director != null && director.Progress != null && director.Progress.ModoDev;

            if (modoDevTitleLabel != null) modoDevTitleLabel.gameObject.SetActive(on);
            if (modoDevShopLabel != null) modoDevShopLabel.gameObject.SetActive(on);
        }

        /// <summary>
        /// Sound, and the privacy controls both stores and the GDPR require to be
        /// reachable from inside the game.
        ///
        /// "Reachable" is not a formality. Consent that cannot be withdrawn as easily
        /// as it was given is not consent, and a policy that only exists on a store
        /// listing does not meet the in-app requirement either.
        /// </summary>
        private void BuildSettings()
        {
            RectTransform screen = UiFactory.Screen("Settings", canvas.transform);
            settingsGroup = UiFactory.Group(screen);

            Image dim = UiFactory.Image("Dim", screen, null, new Color(0.04f, 0.03f, 0.09f, 0.95f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            settingsPanel = UiFactory.Rect("Panel", screen);
            UiFactory.Place(settingsPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1200f));

            TextMeshProUGUI heading = UiFactory.Text(
                "Heading", settingsPanel, "SETTINGS", 72f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(UiFactory.ContentWidth, 100f));

            Button sound = UiFactory.Button(
                "Sound", settingsPanel, plaqueSprite, secondaryButtonColor, string.Empty, 0f, textColor);
            UiFactory.Place(sound.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -200f), new Vector2(UiFactory.ContentWidth, 120f));
            sound.onClick.AddListener(OnSoundPressed);

            soundLabel = UiFactory.Text(
                "Label", sound.transform, string.Empty, 44f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(soundLabel.rectTransform);

            Button privacyChoices = UiFactory.Button(
                "Privacy Choices", settingsPanel, plaqueSprite, secondaryButtonColor,
                "PRIVACY CHOICES", 40f, textColor);
            UiFactory.Place(privacyChoices.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -340f), new Vector2(UiFactory.ContentWidth, 120f));
            privacyChoices.onClick.AddListener(() => { Click(); Show(Screen.Consent); });

            // The documents themselves, in the game. The two link buttons below open
            // the published versions; this one opens the copy that works on a plane,
            // and it is deliberately the more prominent of the two routes.
            Button legal = UiFactory.Button(
                "Legal", settingsPanel, plaqueSprite, secondaryButtonColor,
                "LEGAL & PRIVACY", 40f, textColor);
            UiFactory.Place(legal.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -480f), new Vector2(UiFactory.ContentWidth, 120f));
            legal.onClick.AddListener(() => { Click(); Show(Screen.Legal); });

            deleteDataButton = UiFactory.Button(
                "Delete Data", settingsPanel, plaqueSprite, secondaryButtonColor,
                string.Empty, 0f, textColor);
            UiFactory.Place(deleteDataButton.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -620f), new Vector2(UiFactory.ContentWidth, 120f));
            deleteDataButton.onClick.AddListener(OnDeleteDataPressed);

            deleteDataLabel = UiFactory.Text(
                "Label", deleteDataButton.transform, string.Empty, 40f, textColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(deleteDataLabel.rectTransform);

            TextMeshProUGUI note = UiFactory.Text(
                "Privacy Note", settingsPanel,
                "Your progress is stored on this device only. Nothing is sent to us.",
                30f, mutedColor, TextAlignmentOptions.Top);
            note.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -756f), new Vector2(UiFactory.ContentWidth, 90f));

            Button policy = UiFactory.Button(
                "Privacy Policy", settingsPanel, plaqueSprite, secondaryButtonColor,
                "POLICY ONLINE", 34f, mutedColor);
            UiFactory.Place(policy.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-182f, -862f), new Vector2(340f, 96f));
            policy.onClick.AddListener(() => OpenUrl(consent == null ? string.Empty : consent.PrivacyPolicyUrl));

            Button terms = UiFactory.Button(
                "Terms", settingsPanel, plaqueSprite, secondaryButtonColor,
                "TERMS ONLINE", 34f, mutedColor);
            UiFactory.Place(terms.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(182f, -862f), new Vector2(340f, 96f));
            terms.onClick.AddListener(() => OpenUrl(consent == null ? string.Empty : consent.TermsUrl));

            // modo_dev, in the build that has it. A checkbox in the scene is no use on a
            // handset, and this is the switch the owner can actually reach — present only
            // where the build was made with it, because a build that can unlock its own
            // shop from its settings screen does not have a shop.
            modoDevButton = UiFactory.Button(
                "Modo Dev", settingsPanel, plaqueSprite, secondaryButtonColor,
                string.Empty, 0f, saleTagColor);
            UiFactory.Place(modoDevButton.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -974f), new Vector2(UiFactory.ContentWidth, 92f));
            modoDevButton.onClick.AddListener(OnModoDevPressed);

            modoDevLabel = UiFactory.Text(
                "Label", modoDevButton.transform, string.Empty, 36f, saleTagColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stretch(modoDevLabel.rectTransform);
            modoDevButton.gameObject.SetActive(false);

            BuildHomeButton(screen, "Home Settings");
        }

        /// <summary>
        /// The legal documents, readable in the game.
        ///
        /// Four tabs over one scrolling body rather than four screens, because the
        /// player arriving here has one question — usually "is this thing watching
        /// me" — and making them guess which of four menu items answers it is how a
        /// privacy screen ends up never being read.
        ///
        /// DATA is first and answers that question in its first line. The order is the
        /// order somebody actually wants them in, not the order a lawyer files them.
        /// </summary>
        private void BuildLegal()
        {
            RectTransform screen = UiFactory.Screen("Legal", canvas.transform);
            legalGroup = UiFactory.Group(screen);

            Image dim = UiFactory.Image("Dim", screen, null, new Color(0.04f, 0.03f, 0.09f, 0.97f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            RectTransform safe = UiFactory.SafeScreen("Safe", screen);

            legalPanel = UiFactory.Rect("Panel", safe);
            UiFactory.Place(legalPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1560f));

            legalHeading = UiFactory.Text(
                "Heading", legalPanel, string.Empty, 56f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(legalHeading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(UiFactory.ContentWidth, 80f));

            // Four across the content width, which is 720: 166 each with an 18 gap. The gap
            // was 10, and the layout audit measures two targets closer than sixteen as one
            // a thumb will hit by accident — on all four legal screens.
            const float tabWidth = 166f;
            const float tabGap = 18f;

            for (int i = 0; i < LegalText.Sections.Length; i++)
            {
                int index = i;
                float x = (i - (LegalText.Sections.Length - 1) * 0.5f) * (tabWidth + tabGap);

                Button tab = UiFactory.Button(
                    $"Legal Tab {i}", legalPanel, plaqueSprite, secondaryButtonColor,
                    LegalText.Sections[i].Tab, 30f, textColor);
                UiFactory.Place(tab.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -112f), new Vector2(tabWidth, 84f));
                tab.onClick.AddListener(() => { Click(); ShowLegalSection(index); });

                legalTabs.Add(tab);
            }

            legalScroll = UiFactory.Scroll("Body", legalPanel, out legalContent);
            UiFactory.Place(legalScroll.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -212f), new Vector2(UiFactory.ContentWidth, 1180f));

            legalBody = UiFactory.Text(
                "Text", legalContent, string.Empty, 28f, textColor, TextAlignmentOptions.TopLeft);

            // Wrapping, and emphatically not auto-sizing. The factory shrinks a label
            // to fit its box by default, which is right for a button and catastrophic
            // for a document: it would render the whole privacy policy at two points
            // rather than letting it scroll.
            legalBody.enableAutoSizing = false;
            legalBody.textWrappingMode = TextWrappingModes.Normal;
            legalBody.rectTransform.anchorMin = new Vector2(0f, 1f);
            legalBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            legalBody.rectTransform.pivot = new Vector2(0.5f, 1f);
            legalBody.rectTransform.anchoredPosition = Vector2.zero;

            // Zeroed rather than left alone. Stretched horizontally, sizeDelta.x is an
            // *inset* from the parent's edges, and the factory's fixed width is still
            // sitting in it — which made the body 145 units wider than the panel it
            // lives in, and 21 wider than the whole canvas.
            legalBody.rectTransform.sizeDelta = Vector2.zero;

            // Seeded with the first document so nothing measures an empty rect. The
            // real layout — and the content height the scrolling depends on — is done
            // in ShowLegalSection when the screen opens, because TMP can only report a
            // preferred height once the canvas has given the rect a width, and at
            // build time it has not.
            legalHeading.SetText(LegalText.Sections[0].Heading);
            legalBody.SetText(LegalText.Sections[0].Body);

            BuildHomeButton(screen, "Home Legal");

            // The one screen that keeps a second way out, at the bottom where a thumb
            // already is. It is the only screen a player arrives at *from* somewhere
            // rather than from the title, and having read three pages of a privacy
            // policy they are far more likely to want Settings back than the title.
            Button back = UiFactory.Button(
                "Legal Back", legalPanel, plaqueSprite, secondaryButtonColor,
                "SETTINGS", 44f, textColor);
            UiFactory.Place(back.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(420f, 110f));
            back.onClick.AddListener(() => { Click(); Show(Screen.Settings); });
        }

        /// <summary>Swaps which document is showing and puts the scroll back to the top.</summary>
        private void ShowLegalSection(int index)
        {
            legalSection = Mathf.Clamp(index, 0, LegalText.Sections.Length - 1);

            LegalSection section = LegalText.Sections[legalSection];

            legalHeading.SetText(section.Heading);
            legalBody.SetText(section.Body);

            // The content has to be told how tall it became. TMP only knows after it
            // has laid the text out, so the height is asked for rather than assumed.
            legalBody.ForceMeshUpdate();

            float height = legalBody.preferredHeight + 40f;
            legalBody.rectTransform.sizeDelta = new Vector2(0f, height);
            legalContent.sizeDelta = new Vector2(0f, height);

            // A player who taps TERMS after scrolling to the bottom of PRIVACY should
            // arrive at the top of the terms, not halfway down them.
            legalScroll.verticalNormalizedPosition = 1f;

            for (int i = 0; i < legalTabs.Count; i++)
            {
                legalTabs[i].image.color = i == legalSection
                    ? primaryButtonColor
                    : secondaryButtonColor;
            }
        }

        /// <summary>
        /// The advertising consent dialog.
        ///
        /// Two questions in one panel, because they are one decision: the age answer
        /// governs the personalisation answer, and splitting them across two steps
        /// invites a build where only one of them is recorded.
        ///
        /// Both buttons are live at all times and neither is styled as the obvious
        /// one. A dialog whose "yes" is a bright primary and whose "no" is grey text
        /// is a dark pattern, and under the GDPR a consent obtained that way is not
        /// freely given — which makes it no consent at all.
        /// </summary>
        private void BuildConsent()
        {
            RectTransform screen = UiFactory.Screen("Consent", canvas.transform);
            consentGroup = UiFactory.Group(screen);

            Image dim = UiFactory.Image("Dim", screen, null, new Color(0.04f, 0.03f, 0.09f, 0.96f));
            UiFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            consentPanel = UiFactory.Rect("Panel", screen);
            UiFactory.Place(consentPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(UiFactory.PanelWidth, 1320f));

            TextMeshProUGUI heading = UiFactory.Text(
                "Heading", consentPanel, "ADS AND YOUR DATA", 60f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(UiFactory.ContentWidth, 90f));

            TextMeshProUGUI body = UiFactory.Text(
                "Body", consentPanel,
                "Ring Toss is free and paid for by ads.\n\n" +
                "We can show you ads chosen using your device's advertising ID, or " +
                "ads chosen without it. Either way the game is exactly the same and " +
                "nothing is locked.\n\n" +
                "Your progress never leaves this device.",
                34f, textColor, TextAlignmentOptions.TopLeft);
            // Wrapping on, and no auto-fit. This is the only multi-line block in the
            // game, and the factory's defaults are both wrong for it: with wrapping
            // off it drew one 1747px line - 769px of it off the right-hand edge of the
            // screen - so most of the consent dialog was unreadable, on the one screen
            // in the game that is a legal obligation rather than a design choice.
            body.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -160f), new Vector2(UiFactory.ContentWidth, 400f));

            Button age = UiFactory.Button(
                "Age", consentPanel, plaqueSprite, secondaryButtonColor, string.Empty, 0f, textColor);
            UiFactory.Place(age.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -600f), new Vector2(UiFactory.ContentWidth, 120f));
            age.onClick.AddListener(OnConsentAgePressed);

            consentAgeTick = UiFactory.Image("Tick", age.transform, roundedSprite, secondaryButtonColor);
            UiFactory.Place(consentAgeTick.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(56f, 56f));

            consentAgeLabel = UiFactory.Text(
                "Label", age.transform, string.Empty, 36f, textColor, TextAlignmentOptions.Left);
            UiFactory.Place(consentAgeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(112f, 0f), new Vector2(560f, 100f));

            // Equal size, equal weight. See the note on this method.
            Button accept = UiFactory.Button(
                "Accept Personalised", consentPanel, plaqueSprite, secondaryButtonColor,
                "USE PERSONALISED ADS", 34f, textColor);
            UiFactory.Place(accept.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -740f), new Vector2(UiFactory.ContentWidth, 120f));
            accept.onClick.AddListener(() => OnConsentAnswered(true));

            Button decline = UiFactory.Button(
                "Decline Personalised", consentPanel, plaqueSprite, secondaryButtonColor,
                "USE ADS WITHOUT MY ID", 34f, textColor);
            UiFactory.Place(decline.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -880f), new Vector2(UiFactory.ContentWidth, 120f));
            decline.onClick.AddListener(() => OnConsentAnswered(false));

            Button policy = UiFactory.Button(
                "Consent Policy Link", consentPanel, plaqueSprite, secondaryButtonColor,
                "READ THE PRIVACY POLICY", 32f, mutedColor);
            UiFactory.Place(policy.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(660f, 96f));
            policy.onClick.AddListener(() => OpenUrl(consent == null ? string.Empty : consent.PrivacyPolicyUrl));
        }

        private void RefreshSettings()
        {
            if (soundLabel != null && director != null && director.Progress != null)
            {
                soundLabel.SetText(director.Progress.IsMuted ? "SOUND: OFF" : "SOUND: ON");
            }

            if (deleteDataLabel == null)
            {
                return;
            }

            deleteDataLabel.SetText(deleteArmed ? "TAP AGAIN TO ERASE" : "DELETE MY DATA");
            deleteDataLabel.color = deleteArmed ? warningColor : mutedColor;
            deleteDataButton.image.color = secondaryButtonColor;

            if (modoDevButton != null)
            {
                ProgressStore progress = director == null ? null : director.Progress;
                bool available = progress != null && progress.ModoDevAvailable;

                modoDevButton.gameObject.SetActive(available);

                if (available && modoDevLabel != null)
                {
                    modoDevLabel.SetText(progress.ModoDev ? "MODO DEV: ON" : "MODO DEV: OFF");
                }
            }
        }

        private void OnModoDevPressed()
        {
            Click();

            ProgressStore progress = director == null ? null : director.Progress;

            if (progress == null || !progress.ModoDevAvailable)
            {
                return;
            }

            progress.SetDevUnlock(!progress.ModoDev);

            RefreshSettings();
            RefreshModoDev();
            RefreshShop();
        }

        private void RefreshConsent()
        {
            if (consentAgeLabel != null)
            {
                int threshold = consent == null ? 16 : consent.AgeOfDigitalConsent;
                consentAgeLabel.SetText("I am {0} or over", threshold);
            }

            if (consentAgeTick != null)
            {
                consentAgeTick.color = consentAgeConfirmed ? ownedColor : panelColor;
            }
        }

        private void OnSoundPressed()
        {
            Click();

            if (director != null && director.Progress != null)
            {
                director.Progress.IsMuted = !director.Progress.IsMuted;
            }

            RefreshSettings();
        }

        /// <summary>
        /// Draws the voluntary ticket offer, including the reason it is unavailable.
        ///
        /// Hidden outright when there are no adverts to be had — a build with
        /// monetisation off, or a player whose consent answer forbids requesting one —
        /// because a permanently dead button is worse than no button. Present but
        /// disabled during its cooldown, with the wait on it, because that one *is*
        /// coming back and the player should be able to see when.
        /// </summary>
        /// <summary>True when the offer cannot be taken until the player has answered on consent.</summary>
        private bool AdsNeedAnAnswer =>
            adService != null && adService.AdsAvailable && consent != null && consent.MustAsk;

        private void RefreshFreeTickets()
        {
            bool offered = adService != null && adService.CanOffer(AdPlacement.RewardedFreeTickets);
            float cooldown = adService == null ? 0f : adService.TicketAdCooldownRemaining;

            // Present whenever the build has rewarded adverts in it at all, whatever
            // the SDK is doing this second. See ApplyFreeTicketsState.
            bool exists = adService != null && adService.RewardedOffered;

            ApplyFreeTicketsState(freeTicketsButton, freeTicketsLabel, exists, offered, cooldown);
            ApplyFreeTicketsState(titleAdButton, titleAdLabel, exists, offered, cooldown);

            // The 2x is disclosed on the offer itself, before the player opts in, and only
            // while taking it would actually buy one: a boost already waiting on SHARE is
            // not bought twice, and a button that cannot be pressed is not an offer.
            bool boostWaiting = director != null && director.Progress != null && director.Progress.HasShareBoost;
            bool dealOn = exists && (offered || AdsNeedAnAnswer) && !boostWaiting;

            SetTag(titleAdTag, dealOn);

            // The shop says it on the button instead: there is no room around it for a tag.
            if (dealOn && freeTicketsLabel != null && freeTicketsButton != null && freeTicketsButton.interactable)
            {
                freeTicketsLabel.SetText("WATCH +{0} · 2× SHARE", TicketRules.RewardedAd);
            }
        }

        private static void SetTag(RectTransform tag, bool on)
        {
            if (tag != null && tag.gameObject.activeSelf != on)
            {
                tag.gameObject.SetActive(on);
            }
        }

        /// <summary>
        /// Draws one copy of the voluntary ticket offer, including the reason it is
        /// unavailable.
        ///
        /// This has now been got wrong twice, in the same way, and the rule that comes
        /// out of it is worth stating plainly: **the button is visible whenever the
        /// build has rewarded adverts in it, and never conditional on whether one
        /// happens to be loaded.**
        ///
        /// The first version hid itself whenever an advert could not be *requested*.
        /// One reason an advert cannot be requested is that the player has not
        /// answered the consent dialog. Another — the common one — is that the SDK has
        /// no game ID, or is still starting, or did not fill. All of those are
        /// invisible to the player, so from where they sit the button simply does not
        /// exist, and the owner reported exactly that: "o botão de watch ad não
        /// existe". It was there the whole time, drawing itself with zero alpha.
        ///
        /// Hiding a control because of a transient backend condition is the worst of
        /// both worlds: the player cannot take the offer *and* cannot see there is
        /// one. So the only thing that removes it now is a build with rewarded adverts
        /// switched off, which is a decision rather than a state. Everything else is
        /// drawn on the button: needs an answer, on cooldown, or nothing to show yet.
        /// </summary>
        private void ApplyFreeTicketsState(
            Button button, TextMeshProUGUI label, bool exists, bool offered, float cooldown)
        {
            if (button == null || label == null)
            {
                return;
            }

            button.gameObject.SetActive(exists);

            if (!exists)
            {
                return;
            }

            bool needsAnswer = !offered && AdsNeedAnAnswer;
            bool waiting = !offered && !needsAnswer && cooldown > 0f;

            button.interactable = offered || needsAnswer;
            button.image.color = button.interactable ? endlessButtonColor : secondaryButtonColor;
            label.color = button.interactable ? textColor : mutedColor;

            if (waiting)
            {
                label.SetText("AGAIN IN {0:0}s", Mathf.Ceil(cooldown));
                return;
            }

            if (!offered && !needsAnswer)
            {
                // Nothing filled. Said plainly rather than hidden, because "no advert
                // right now" is information and a missing button is not.
                label.SetText("NO AD RIGHT NOW");
                return;
            }

            label.SetText("WATCH NOW +{0}", TicketRules.RewardedAd);
        }

        /// <summary>Draws the share button: always usable, and says whether this share pays.</summary>
        private void RefreshShare()
        {
            if (shareLabel == null)
            {
                return;
            }

            ProgressStore progress = director == null ? null : director.Progress;
            double wait = progress == null ? 0d : progress.SecondsUntilShareReward(ShareRules.NowUtc());
            bool boosted = progress != null && progress.HasShareBoost;

            // The tag stays up through a cooldown: the boost is waiting for the next
            // share that pays, and the countdown on the button says when that is.
            ShowShareTag(boosted);

            if (wait > 0d)
            {
                // Allocates once a second, on the title only — never during play.
                int seconds = Mathf.CeilToInt((float)wait);
                shareLabel.SetText($"SHARE  {seconds / 60}:{seconds % 60:00}");
                return;
            }

            shareLabel.SetText("SHARE +{0}", TicketRules.ForShare(boosted));
        }

        /// <summary>Puts the 2x tag on SHARE, popping it in so a new one is noticed.</summary>
        private void ShowShareTag(bool on)
        {
            if (shareTag == null || shareTag.gameObject.activeSelf == on)
            {
                return;
            }

            shareTag.gameObject.SetActive(on);

            if (on)
            {
                shareTagShownAt = Time.unscaledTime;
                shareTag.localScale = Vector3.zero;
            }
        }

        /// <summary>
        /// The tags breathe while they are up and the one on SHARE pops when it arrives:
        /// an offer that sits perfectly still is part of the furniture by the second look.
        /// </summary>
        private void TickOfferTags()
        {
            if (current != Screen.Title && current != Screen.Shop)
            {
                return;
            }

            float now = Time.unscaledTime;

            if (shareTag != null && shareTag.gameObject.activeSelf)
            {
                float age = now - shareTagShownAt;
                float pop = age < 0.45f ? TagPop(age / 0.45f) : 1f;
                float breathe = 1f + 0.07f * Mathf.Sin(now * 4.4f);
                shareTag.localScale = Vector3.one * (pop * breathe);
            }

            float watchBreathe = 1f + 0.04f * Mathf.Sin(now * 3.6f + 1.3f);

            if (titleAdTag != null && titleAdTag.gameObject.activeSelf)
            {
                titleAdTag.localScale = Vector3.one * watchBreathe;
            }
        }

        /// <summary>An overshooting ease for the tag's arrival.</summary>
        private static float TagPop(float t)
        {
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;
            float shifted = t - 1f;
            return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
        }

        /// <summary>
        /// Holds a share's celebration until the player can see it. The reward is paid on
        /// the tap, but by then Android's share sheet is covering the game: played at once,
        /// the biggest moment on the title would happen behind a system dialog, unseen.
        /// </summary>
        private void QueueShareCelebration(int amount, int total, TicketSource source, bool sheetOpened)
        {
            if (ticketShower == null || titleTicketLabel == null)
            {
                return;
            }

            ticketShower.Finish();

            shareCelebrationPending = true;
            pendingShareAmount = amount;
            pendingShareTotal = total;
            pendingShareSource = source;
            pendingShareSince = Time.unscaledTime;
            pendingShareLeftApp = false;

            // Long enough for a share sheet to take focus if one is coming; short where
            // none is, because then the player is already looking at the button.
            pendingShareFallback = sheetOpened ? 2.5f : 0.35f;

            // The balance is already paid; the number waits for its tickets.
            titleTicketLabel.SetText("{0} tickets", total - amount);
        }

        private void TickShareCelebration()
        {
            if (!shareCelebrationPending)
            {
                return;
            }

            float now = Time.unscaledTime;

            bool due = pendingShareLeftApp
                ? returnedToAppAt > pendingShareSince && now >= returnedToAppAt + 0.35f
                : now >= pendingShareSince + pendingShareFallback;

            if (due)
            {
                PlayShareCelebration();
            }
        }

        private void PlayShareCelebration()
        {
            shareCelebrationPending = false;

            if (ticketShower == null || current != Screen.Title)
            {
                RefreshTitleTickets();
                return;
            }

            bool jackpot = pendingShareSource == TicketSource.BoostedShare;

            ticketShower.Play(
                jackpot ? TicketCelebration.Jackpot : TicketCelebration.Payout,
                pendingShareSource,
                ticketShower.LocalOf(shareButton.image.rectTransform),
                titleTicketLabel,
                pendingShareTotal - pendingShareAmount,
                pendingShareTotal,
                "{0} tickets",
                jackpot ? "2× SHARE BONUS" : null,
                jackpot ? $"+{pendingShareAmount} TICKETS" : null);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            NoteFocus(hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            NoteFocus(!paused);
        }

        /// <summary>
        /// Tracks the share sheet taking the screen and giving it back. Focus and pause
        /// both, because which of the two Android reports for a chooser varies by version.
        /// </summary>
        private void NoteFocus(bool present)
        {
            if (present)
            {
                returnedToAppAt = Time.unscaledTime;
                return;
            }

            if (shareCelebrationPending)
            {
                pendingShareLeftApp = true;

                // A return only counts if it comes after this departure.
                returnedToAppAt = float.NegativeInfinity;
            }
        }

        /// <summary>An advert the player chose to watch: the banner, the burst, the count.</summary>
        private void CelebrateRewardedAd(int amount, int total, bool boostBought)
        {
            bool inShop = current == Screen.Shop;
            TextMeshProUGUI label = inShop ? shopTicketLabel : titleTicketLabel;
            Button button = inShop ? freeTicketsButton : titleAdButton;

            if (ticketShower == null || label == null || button == null)
            {
                return;
            }

            ticketShower.Play(
                TicketCelebration.Reward, TicketSource.RewardedAd,
                ticketShower.LocalOf(button.image.rectTransform),
                label, total - amount, total, inShop ? "{0} TICKETS" : "{0} tickets",
                $"+{amount} TICKETS",
                boostBought ? "+ 2× ON YOUR NEXT SHARE" : "THANKS FOR WATCHING");
        }

        private void OnSharePressed()
        {
            Click();

            ProgressStore progress = director == null ? null : director.Progress;
            int bestRound = progress == null ? 0 : progress.BestEndlessRound;

            bool opened = NativeShare.Share(
                ShareRules.Subject, ShareRules.Message(bestRound, ShareRules.StoreLink("title")));

            // Paid on the gesture, not on proof of delivery. Android's share sheet does
            // not report whether anything was sent, and a reward that waited for it would
            // pay nobody. The cooldown is what stops the button being a ticket tap.
            double now = ShareRules.NowUtc();

            if (progress != null && progress.SecondsUntilShareReward(now) <= 0d)
            {
                // The 2x is spent by the share that pays, and only by that one. A share
                // during the cooldown pays nothing, and doubling nothing is not what the
                // tag promised — so it stays on the button until a share can use it.
                bool boosted = progress.ConsumeShareBoost();
                int amount = TicketRules.ForShare(boosted);
                TicketSource source = boosted ? TicketSource.BoostedShare : TicketSource.Share;

                progress.RecordShareReward(now);
                progress.AddTickets(amount);
                GameEvents.RaiseTicketsAwarded(amount, progress.Tickets, source);

                QueueShareCelebration(amount, progress.Tickets, source, opened);
            }

            RefreshTitleTickets();

            if (!opened && shareLabel != null)
            {
                // Where there is no share sheet — the editor, or a platform this has not
                // been wired for yet — the link went to the clipboard instead, and the
                // button says so rather than appearing to do nothing.
                shareLabel.SetText("LINK COPIED");
                ShowShareTag(progress != null && progress.HasShareBoost);
                nextTitleRefresh = Time.unscaledTime + 1.6f;
                return;
            }

            RefreshShare();
        }

        private void OnFreeTicketsPressed()
        {
            Click();

            if (adService == null)
            {
                return;
            }

            // Nothing can be requested until the dialog has been answered, and the
            // player has just told us they want an advert — which makes this the most
            // honest moment in the game to ask them about advertising.
            if (AdsNeedAnAnswer)
            {
                Show(Screen.Consent);
                return;
            }

            if (freeTicketsButton != null) freeTicketsButton.interactable = false;
            if (titleAdButton != null) titleAdButton.interactable = false;

            adService.ShowRewarded(AdPlacement.RewardedFreeTickets, result =>
            {
                // Only a watched advert pays. A no-fill or a network failure must leave
                // the player exactly where they were, which includes leaving the offer
                // available rather than burning its cooldown.
                if (result == AdResult.Watched && director?.Progress != null)
                {
                    ProgressStore progress = director.Progress;

                    // The advert buys the 2x on the next paying share as well as the
                    // tickets: said on the WATCH button's tag before it was taken, and
                    // delivered here, only for an advert that was actually watched.
                    bool boostBought = !progress.HasShareBoost;
                    progress.GrantShareBoost();

                    int amount = TicketRules.ForRewardedAd();
                    progress.AddTickets(amount);
                    GameEvents.RaiseTicketsAwarded(amount, progress.Tickets, TicketSource.RewardedAd);

                    CelebrateRewardedAd(amount, progress.Tickets, boostBought);
                }

                RefreshShop();
                RefreshTitleTickets();
                RefreshFreeTickets();
                RefreshShare();
            });
        }

        /// <summary>
        /// Erases everything the game has stored, on the second tap.
        ///
        /// Two taps rather than a confirmation dialog: the button says what it will do
        /// and then says it again, which is enough friction for an action that is only
        /// reachable from inside Settings, and a good deal less ceremony than a modal
        /// asking "are you sure" that everybody clicks through anyway.
        ///
        /// It really does erase everything, including the advertising answer — so the
        /// consent dialog comes back on the next launch. That is the honest reading of
        /// erasure: the record of the question is data about the player too, and a
        /// "delete my data" that quietly kept the one row proving they were asked
        /// would be keeping the wrong row.
        /// </summary>
        private void OnDeleteDataPressed()
        {
            Click();

            if (!deleteArmed)
            {
                deleteArmed = true;
                RefreshSettings();
                return;
            }

            deleteArmed = false;

            director?.Progress?.ResetAll(director.LevelCount);
            consent?.Reset();

            RefreshSettings();
            RefreshTitleTickets();
            RefreshEndlessBest();
        }

        private void OnConsentAgePressed()
        {
            Click();
            consentAgeConfirmed = !consentAgeConfirmed;
            RefreshConsent();
        }

        /// <summary>
        /// Records the answer and starts, or updates, the mediator.
        ///
        /// Note that agreeing without confirming an age still produces
        /// non-personalised advertising: <see cref="ConsentPolicy"/> resolves the two
        /// answers together and the stricter one wins. That is deliberate, and it is
        /// why the age control is a plain unticked box rather than something the
        /// player has to notice in order to be protected by.
        /// </summary>
        private void OnConsentAnswered(bool personalised)
        {
            Click();

            if (consent != null)
            {
                consent.Record(personalised, consentAgeConfirmed);
            }

            if (adService != null)
            {
                adService.ApplyConsentChange();
            }

            Show(Screen.Title);
        }

        private void OpenUrl(string url)
        {
            Click();

            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("[UI] No URL configured. Set them on the AdConfig asset.");
                return;
            }

            Application.OpenURL(url);
        }

        private void Show(Screen screen)
        {
            if (current == screen)
            {
                return;
            }

            if (screen == Screen.LevelSelect)
            {
                // Open on the page the player is actually up to, not always on stage one.
                if (director != null && director.Progress != null)
                {
                    levelPage = Mathf.Clamp(
                        director.Progress.HighestUnlockedLevel / LevelsPerPage, 0, StageCount - 1);
                }

                RefreshLevelGrid();
            }

            if (screen == Screen.Title)
            {
                RefreshEndlessBest();
                RefreshTitleTickets();
                RefreshFreeTickets();
                RefreshShare();
                RefreshModoDev();
            }

            if (screen == Screen.Shop)
            {
                RefreshShop();
                RefreshModoDev();
            }

            if (screen == Screen.Settings)
            {
                // Disarmed on every entry. Coming back to a Settings screen still one
                // tap away from erasing everything is a trap, and the player who left
                // and came back is exactly the one who changed their mind.
                deleteArmed = false;
                RefreshSettings();
            }

            if (screen == Screen.Legal)
            {
                // Always reopened on DATA. It is the tab that answers the question
                // somebody came here to ask, and leaving them on whichever page they
                // last read would bury it.
                ShowLegalSection(0);
            }

            if (screen == Screen.Consent)
            {
                // Reopening the dialog starts from unconfirmed: the age question is
                // being asked again, so a previous answer must not be carried into it
                // as a default the player did not re-give.
                consentAgeConfirmed = false;
                RefreshConsent();
            }

            SetGroup(titleGroup, screen == Screen.Title, titlePanel);
            SetGroup(levelSelectGroup, screen == Screen.LevelSelect, levelSelectPanel);
            SetGroup(shopGroup, screen == Screen.Shop, shopPanel);
            SetGroup(hudGroup, screen == Screen.Hud, null);
            SetGroup(resultGroup, screen == Screen.Result, resultPanel);
            SetGroup(settingsGroup, screen == Screen.Settings, settingsPanel);
            SetGroup(legalGroup, screen == Screen.Legal, legalPanel);
            SetGroup(consentGroup, screen == Screen.Consent, consentPanel);

            current = screen;
        }

        private void SetGroup(CanvasGroup group, bool visible, RectTransform panel)
        {
            if (group == null)
            {
                return;
            }

            group.interactable = visible;
            group.blocksRaycasts = visible;

            float duration = juiceConfig == null ? 0.32f : juiceConfig.ScreenFadeDuration;

            Tween.StopAll(group);
            Tween.Alpha(group, group.alpha, visible ? 1f : 0f, duration,
                visible ? Ease.OutQuad : Ease.InQuad, useUnscaledTime: true);

            if (panel == null)
            {
                return;
            }

            Tween.StopAll(panel);

            if (visible)
            {
                panel.localScale = Vector3.one * 0.88f;
                Tween.Scale(panel, 0.88f, 1f, duration * 1.4f, Ease.OutBack, useUnscaledTime: true);
            }
            else
            {
                Tween.Scale(panel, panel.localScale.x, 0.94f, duration, Ease.InQuad, useUnscaledTime: true);
            }
        }

        private void Click()
        {
            audioDirector?.PlayUiTap();
        }
    }
}
