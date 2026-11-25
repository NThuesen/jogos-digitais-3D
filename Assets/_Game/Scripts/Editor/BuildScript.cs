using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Produces a shippable Android build from the command line.
    ///
    /// This exists so that "the game builds" is something the repository can
    /// demonstrate rather than assume. A project can pass every test in the editor
    /// and still fail to produce a bundle — IL2CPP compiles code the editor's Mono
    /// runtime never does, stripping removes what reflection was quietly relying on,
    /// and the manifest merger fails on plugins that behave perfectly in play mode.
    /// None of those show up until an actual build runs.
    ///
    /// The output is deliberately unsigned. Signing keys do not belong in a
    /// repository, and an unsigned bundle proves everything a signed one would about
    /// whether the code compiles and packages.
    /// </summary>
    public static class BuildScript
    {
        private const string OutputFolder = "Builds";

        [MenuItem("SlipperyZlope/Build Android Bundle")]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, "ring-toss.aab", appBundle: true);
        }

        [MenuItem("SlipperyZlope/Build Android APK")]
        public static void BuildAndroidApk()
        {
            Build(BuildTarget.Android, "ring-toss.apk", appBundle: false);
        }

        /// <summary>
        /// The handset build for testing cosmetics: modo_dev on, every skin and trail
        /// owned, and MODO DEV on the screen so it cannot be mistaken for the real thing.
        ///
        /// The switch lives in the scene, so this turns it on, builds, and turns it back
        /// off — and it has to put it back even when the build fails, or the project is
        /// left with a scene that gives the shop away. `Build` is told not to exit, since
        /// exiting batch mode at the end of a build would skip the restore.
        /// </summary>
        [MenuItem("SlipperyZlope/Build Android APK (modo dev)")]
        public static void BuildAndroidApkModoDev()
        {
            bool ok = false;

            try
            {
                ReleaseReadiness.SetModoDev(true);
                ok = Build(BuildTarget.Android, "ring-toss-dev.apk", appBundle: false, exitWhenDone: false);
            }
            finally
            {
                ReleaseReadiness.SetModoDev(false);
            }

            if (ok)
            {
                Debug.Log("[BuildScript] ring-toss-dev.apk has modo_dev on. The scene's switch is off again.");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static bool Build(
            BuildTarget target, string fileName, bool appBundle, bool exitWhenDone = true)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // A build with no scenes succeeds and produces something that opens to
                // a blank screen, which is a far worse outcome than a failure.
                Fail("No scenes are enabled in the build settings.", exitWhenDone);
                return false;
            }

            // The one thing that does stop a build: a store bundle with modo_dev on would
            // ship every skin and trail free. The APK is the build for testing on a
            // handset, and that is exactly what modo_dev is for, so it is allowed there.
            if (appBundle && ReleaseReadiness.ModoDevIsOn())
            {
                Fail("modo_dev is on (ProgressStore in Main.unity). A store bundle with every " +
                     "skin and trail free is not one to upload — switch it off, or build the APK " +
                     "to test.", exitWhenDone);
                return false;
            }

            // Printed before the build rather than after, so it is still on screen
            // when a twenty-minute IL2CPP compile finishes. Nothing here can fail the
            // build: it is a description, and the pre-launch answer is a placeholder.
            Debug.Log(ReleaseReadiness.Describe());

            Directory.CreateDirectory(OutputFolder);
            EditorUserBuildSettings.buildAppBundle = appBundle;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputFolder, fileName),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail(
                    $"Build {summary.result} with {summary.totalErrors} errors " +
                    $"after {summary.totalTime}.", exitWhenDone);
                return false;
            }

            Debug.Log(
                $"[BuildScript] {fileName} built in {summary.totalTime:mm\\:ss}, " +
                $"{summary.totalSize / (1024 * 1024)} MB, from {scenes.Length} scene(s).");

            // Not when the caller has something left to do — the modo dev build has to
            // put the scene's switch back, and exiting here would skip that.
            if (exitWhenDone && Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }

            return true;
        }

        private static void Fail(string message, bool exitWhenDone = true)
        {
            Debug.LogError($"[BuildScript] {message}");

            // Batch mode has to exit non-zero, or a failed build looks like a
            // successful one to whatever invoked it.
            if (exitWhenDone && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
