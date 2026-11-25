using System.Linq;
using System.Text;
using SlipperyZlope.RingToss.Monetisation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Says out loud what a build will actually do about advertising.
    ///
    /// Every setting that decides this lives somewhere different — a package in the
    /// manifest, a define computed from it, serialized fields on a component in the
    /// scene, a ScriptableObject, a URL — and each of them is individually sensible
    /// and invisible. The combination is what matters and nothing anywhere shows the
    /// combination. That is how a game ships in test mode, and how one ships with a
    /// placeholder that reads "Your advert here".
    ///
    /// So the build prints it. Not a check that passes or fails: a description of the
    /// posture, ending in the one sentence anybody actually wants — what a player who
    /// installs this build will be shown.
    ///
    /// The pre-launch answer is deliberately not an error. A game that is not live on
    /// a store gets no fill, so before launch the placeholder is the *correct*
    /// configuration and the only way to see the interruption on a handset at all.
    /// It stops being correct on the day the listing goes live, which is the day this
    /// report is worth reading.
    /// </summary>
    public static class ReleaseReadiness
    {
        private const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        private const string ConfigPath = "Assets/_Game/Data/AdConfig.asset";

        [MenuItem("SlipperyZlope/Report Release Readiness")]
        public static void Report()
        {
            Debug.Log(Describe());
        }

        /// <summary>The report, as a block of text. Called by the build before it runs.</summary>
        public static string Describe()
        {
            StringBuilder text = new StringBuilder();

            // First, because it is the one switch here that gives the shop away. The
            // Android bundle build refuses to run with it on; the APK allows it, since
            // testing the cosmetics on a handset is exactly what it is for.
            bool modoDev = ModoDevIsOn();
            text.AppendLine("[Release] Development switches");
            text.AppendLine($"  modo_dev ................. {(modoDev ? "ON — every skin and trail is free in this build" : "off")}");

            if (modoDev)
            {
                text.AppendLine("  Switch it off on the ProgressStore component in Main.unity before a store build.");
            }

            text.AppendLine();
            text.AppendLine("[Release] Monetisation posture of this build");

            AdSettings settings = ReadAdSettings();

            // Asked of the component rather than computed from a define here, and the
            // difference is not academic: `SLIPPERYZLOPE_UNITY_ADS` comes from a version
            // define on the *runtime* assembly, so this file — which is in the editor
            // assembly — never sees it. The first build to print this report announced
            // "ad SDK compiled in: none" over a project with `com.unity.ads` in its
            // manifest and a game ID in the scene. A tool that lies is worse than no
            // tool, and it lied about the one thing it was written to report.
            string sdk = settings.Found ? settings.Sdk : "unknown — no component found";

            text.AppendLine($"  ad SDK compiled in ....... {sdk}");

            AdConfig config = AssetDatabase.LoadAssetAtPath<AdConfig>(ConfigPath);

            if (config == null)
            {
                text.AppendLine($"  !! no AdConfig at {ConfigPath}");
            }
            else
            {
                text.AppendLine($"  ads enabled .............. {Yes(config.AdsEnabled)}");
                text.AppendLine($"  rewarded enabled ......... {Yes(config.RewardedEnabled)}");
                text.AppendLine($"  privacy policy ........... {Url(config.PrivacyPolicyUrl)}");
                text.AppendLine($"  terms .................... {Url(config.TermsUrl)}");
            }

            AdSettings ads = settings;

            if (!ads.Found)
            {
                text.AppendLine($"  !! no Unity Ads component found in {ScenePath}");
                return text.ToString();
            }

            text.AppendLine($"  android game ID .......... {Set(ads.AndroidGameId)}");
            text.AppendLine($"  ios game ID .............. {Set(ads.IosGameId)}");
            text.AppendLine($"  test mode ................ {(ads.TestMode ? "ON — demo adverts, no revenue" : "off — live adverts")}");
            text.AppendLine($"  placements ............... {ads.AndroidInterstitial} / {ads.AndroidRewarded}");
            text.AppendLine($"  placeholder when unfilled  {(ads.PlaceholderWhenUnfilled ? "ON" : "off")}");
            text.AppendLine($"  ALWAYS placeholder ....... {(ads.AlwaysPlaceholder ? "ON — no real advert is ever shown" : "off")}");

            text.AppendLine();
            text.AppendLine($"  A player who installs this build sees: {Verdict(sdk, config, ads)}");

            if (ads.AlwaysPlaceholder || ads.PlaceholderWhenUnfilled)
            {
                text.AppendLine(
                    "  Before the store listing goes live, turn OFF " +
                    (ads.AlwaysPlaceholder ? "Always Placeholder and " : string.Empty) +
                    "Placeholder When Unfilled on the Unity Ads component in the " +
                    "scene. Leaving them on shows real players a card that says " +
                    "\"Your advert here\" and earns nothing.");
            }

            return text.ToString();
        }

        /// <summary>What a player is shown, which is the only line most readers want.</summary>
        private static string Verdict(string sdk, AdConfig config, AdSettings ads)
        {
            if (config != null && !config.AdsEnabled)
            {
                return "no adverts at all — ads are switched off in AdConfig.";
            }

            if (sdk == "placeholder")
            {
                return ads.PlaceholderWhenUnfilled || ads.AlwaysPlaceholder
                    ? "placeholders only. No SDK is compiled in."
                    : "no adverts at all. No SDK is compiled in and the placeholder is off.";
            }

            if (string.IsNullOrEmpty(ads.AndroidGameId))
            {
                return ads.PlaceholderWhenUnfilled
                    ? "placeholders only. There is no game ID, so nothing is ever requested."
                    : "no adverts at all. There is no game ID, so nothing is ever requested.";
            }

            if (ads.AlwaysPlaceholder)
            {
                return "placeholders, always. The SDK still initialises with the real " +
                       "game ID and still requests the real placements — the log says " +
                       "when one filled — but the last step is the stand-in.";
            }

            if (ads.TestMode)
            {
                return "Unity's demo adverts (test mode), and placeholders when those do not fill."
                    + (ads.PlaceholderWhenUnfilled ? string.Empty : " No — placeholder is off, so nothing on a miss.");
            }

            return ads.PlaceholderWhenUnfilled
                ? "live adverts when the network fills, and a placeholder when it does not. " +
                  "Before the listing is live it will not fill, so in practice: placeholders."
                : "live adverts when the network fills, and nothing when it does not.";
        }

        /// <summary>
        /// Whether modo_dev is ticked on the scene's ProgressStore — every skin and trail
        /// owned. Read from the serialized scene like the advert settings, because that
        /// checkbox, not the C# default, is what a build ships.
        /// </summary>
        public static bool ModoDevIsOn()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;

            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                Save.ProgressStore store = scene.GetRootGameObjects()
                    .Select(root => root.GetComponentInChildren<Save.ProgressStore>(true))
                    .FirstOrDefault(found => found != null);

                if (store == null)
                {
                    return false;
                }

                SerializedProperty flag = new SerializedObject(store).FindProperty("modoDev");
                return flag != null && flag.boolValue;
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        /// <summary>
        /// Writes modo_dev into the scene and saves it. Used by the modo dev build, which
        /// turns it on, builds, and turns it back off.
        /// </summary>
        public static void SetModoDev(bool on)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;

            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                Save.ProgressStore store = scene.GetRootGameObjects()
                    .Select(root => root.GetComponentInChildren<Save.ProgressStore>(true))
                    .FirstOrDefault(found => found != null);

                if (store == null)
                {
                    Debug.LogError($"[Release] No ProgressStore in {ScenePath}; modo_dev not written.");
                    return;
                }

                SerializedObject serialized = new SerializedObject(store);
                SerializedProperty flag = serialized.FindProperty("modoDev");

                if (flag == null)
                {
                    Debug.LogError("[Release] ProgressStore has no modoDev field any more.");
                    return;
                }

                flag.boolValue = on;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"[Release] modo_dev is now {(on ? "ON" : "off")} in {ScenePath}.");
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static string Yes(bool value) => value ? "yes" : "NO";

        private static string Set(string value) =>
            string.IsNullOrEmpty(value) ? "NOT SET" : value;

        private static string Url(string value) =>
            string.IsNullOrEmpty(value) ? "NOT SET" : value;

        private struct AdSettings
        {
            public bool Found;
            public string Sdk;
            public string AndroidGameId;
            public string IosGameId;
            public bool TestMode;
            public string AndroidInterstitial;
            public string AndroidRewarded;
            public bool PlaceholderWhenUnfilled;
            public bool AlwaysPlaceholder;
        }

        /// <summary>
        /// Reads the component's serialized fields out of the scene.
        ///
        /// Through <see cref="SerializedObject"/> rather than through public getters,
        /// because these are settings a designer changes in the inspector and adding
        /// runtime API so a build report can read them would be the tail wagging the
        /// dog. The scene is opened only if it is not already open, and closed again
        /// if it had to be.
        /// </summary>
        private static AdSettings ReadAdSettings()
        {
            AdSettings settings = default;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;

            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                UnityAds ads = scene.GetRootGameObjects()
                    .Select(root => root.GetComponentInChildren<UnityAds>(true))
                    .FirstOrDefault(found => found != null);

                if (ads == null)
                {
                    return settings;
                }

                SerializedObject serialized = new SerializedObject(ads);

                settings.Found = true;
                settings.Sdk = ads.Name;
                settings.AndroidGameId = serialized.FindProperty("androidGameId").stringValue;
                settings.IosGameId = serialized.FindProperty("iosGameId").stringValue;
                settings.TestMode = serialized.FindProperty("testMode").boolValue;
                settings.AndroidInterstitial =
                    serialized.FindProperty("androidInterstitialPlacement").stringValue;
                settings.AndroidRewarded =
                    serialized.FindProperty("androidRewardedPlacement").stringValue;
                settings.PlaceholderWhenUnfilled =
                    serialized.FindProperty("placeholderWhenUnfilled").boolValue;
                settings.AlwaysPlaceholder =
                    serialized.FindProperty("alwaysPlaceholder").boolValue;

                return settings;
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }
    }
}
