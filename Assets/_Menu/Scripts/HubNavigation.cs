using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameHub
{
    /// <summary>
    /// Moves between the menu and the games.
    ///
    /// The two games were separate projects, each with its own project-wide physics
    /// step. Only one can be the project setting here, so the other is applied when
    /// its scene is launched and the project value is restored on the way back.
    /// </summary>
    public static class HubNavigation
    {
        public const string MenuScenePath = "Assets/_Menu/Scenes/Menu.unity";
        public const string RingTossScenePath = "Assets/_Game/Scenes/Main.unity";
        public const string PocketPuttScenePath = "Assets/_PocketPutt/Scenes/C1-01_Prototype.unity";

        /// <summary>Pocket Putt was tuned at Unity's default 50 Hz physics step.</summary>
        public const float PocketPuttFixedDeltaTime = 0.02f;

        private static float projectFixedDeltaTime = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureProjectSettings()
        {
            projectFixedDeltaTime = Time.fixedDeltaTime;
        }

        public static void LaunchRingToss() => Launch(RingTossScenePath, projectFixedDeltaTime);

        public static void LaunchPocketPutt() => Launch(PocketPuttScenePath, PocketPuttFixedDeltaTime);

        /// <summary>Leaves whatever game is running and opens the menu.</summary>
        public static void ReturnToMenu() => Launch(MenuScenePath, projectFixedDeltaTime);

        private static void Launch(string scenePath, float fixedDeltaTime)
        {
            // A game may be left mid hit-stop or mid-advert with the clock slowed or
            // stopped. The next scene must start on a normal clock.
            Time.timeScale = 1f;
            if (fixedDeltaTime > 0f)
            {
                Time.fixedDeltaTime = fixedDeltaTime;
            }

            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
        }
    }
}
