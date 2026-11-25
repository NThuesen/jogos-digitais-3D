using SlipperyZlope.RingToss.Config;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Writes the tuned values into the configuration assets.
    ///
    /// The assets are still the thing the game reads, and anything here can still be
    /// nudged in the inspector without a recompile — that has not changed. What this
    /// adds is a way back: the authored tuning lives in code, can be read in a diff,
    /// and can be restored on a clean clone or after an experiment.
    ///
    /// It exists because of a specific failure. Changing a serialized field's meaning
    /// in code does not change the asset: an asset saved when the throw was tuned by
    /// launch speed still carried a six metres per second floor, and the new model —
    /// which needs to solve down to about four for a short lob — was silently clamped
    /// back to the old behaviour. Nothing about the asset looked wrong.
    ///
    /// Values are written through SerializedObject by field name rather than through
    /// runtime setters, so re-tuning does not mean adding public API to the config
    /// types for the editor's benefit. A name that does not resolve is reported rather
    /// than skipped, because a silent no-op here is exactly the bug this is for.
    /// </summary>
    public static class ConfigBootstrap
    {
        private const string PhysicsPath = "Assets/_Game/Data/PhysicsConfig.asset";
        private const string JuicePath = "Assets/_Game/Data/JuiceConfig.asset";

        [MenuItem("SlipperyZlope/Bootstrap Configs")]
        public static void Run()
        {
            int written = 0;

            written += WritePhysics();
            written += WriteJuice();

            AssetDatabase.SaveAssets();

            Debug.Log($"[ConfigBootstrap] Wrote {written} values.");
        }

        private static int WritePhysics()
        {
            PhysicsConfig config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>(PhysicsPath);

            if (config == null)
            {
                Debug.LogError($"[ConfigBootstrap] No PhysicsConfig at {PhysicsPath}.");
                return 0;
            }

            SerializedObject serialized = new SerializedObject(config);
            int written = 0;

            // Input. Power and yaw are separate axes: see ThrowSolver for why the
            // total drag length must not decide the power.
            written += SetFloat(serialized, "maxDragScreenFraction", 0.3f);
            written += SetFloat(serialized, "lateralDragScreenFraction", 0.18f);
            written += SetFloat(serialized, "deadzoneScreenFraction", 0.016f);
            written += SetFloat(serialized, "cancelScreenFraction", 0.012f);

            // Swipe direction is throw direction: push the ring forward and it goes
            // forward. The game spent a while as a slingshot — pull back, release —
            // and the owner's own testing came back the other way: the forward push is
            // what players reach for, and what they kept trying on their first throw.
            // The flag stays, because this is a feel decision and those get revisited,
            // but it is off.
            written += SetBool(serialized, "invertDragDirection", false);

            // Range. The near limit has to sit inside the nearest peg any level places
            // — the level library's near row is at 2.3 m — or that peg is unreachable
            // at any drag.
            written += SetFloat(serialized, "minThrowRange", 1.1f);
            written += SetFloat(serialized, "maxThrowRange", 6.4f);
            written += SetLinearCurve(serialized, "dragToRangeCurve");

            // Arc. Steep when short so a weak throw is a lob that drops, and still
            // steep enough when long that the ring arrives descending onto the peg
            // rather than into its side.
            written += SetFloat(serialized, "pitchAtMinRange", 56f);
            written += SetFloat(serialized, "pitchAtMaxRange", 42f);

            // Clamps only. The speed is solved from the range, not authored.
            written += SetFloat(serialized, "minThrowSpeed", 3f);
            written += SetFloat(serialized, "maxThrowSpeed", 13f);

            written += SetFloat(serialized, "maxYawDegrees", 20f);
            written += SetFloat(serialized, "gravityMultiplier", 1.9f);
            written += SetFloat(serialized, "minimumFlightTime", 0.3f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            return written;
        }

        private static int WriteJuice()
        {
            JuiceConfig config = AssetDatabase.LoadAssetAtPath<JuiceConfig>(JuicePath);

            if (config == null)
            {
                Debug.LogError($"[ConfigBootstrap] No JuiceConfig at {JuicePath}.");
                return 0;
            }

            SerializedObject serialized = new SerializedObject(config);
            int written = 0;

            // The hand has to visibly follow the drag on both axes. At the old pull of
            // 0.16 m the ring barely moved, which is most of why the gesture read as
            // unresponsive even when it was being tracked correctly.
            // Depth. Testers could not read how far away anything was, and the ring's
            // own shadow is the cheapest cue there is: it is the one thing on screen
            // whose size says "height" and whose position says "where this will land".
            // The old range barely moved — 1.05 down to 0.42 over three metres — so it
            // read as a smudge that travelled with the ring rather than as a shadow.
            written += SetFloat(serialized, "shadowMaxScale", 1.3f);
            written += SetFloat(serialized, "shadowMinScale", 0.26f);
            written += SetFloat(serialized, "shadowMaxAlpha", 0.55f);

            written += SetFloat(serialized, "aimRollDegrees", 16f);
            written += SetFloat(serialized, "aimDamping", 20f);

            // Shake, cut back hard after the first play test. The perfect catch has no
            // entry here at all any more: see the note in CameraJuice.
            written += SetFloat(serialized, "shakeOnThrow", 0.03f);
            written += SetFloat(serialized, "shakeOnBounce", 0.05f);
            written += SetFloat(serialized, "shakeOnRinger", 0.1f);
            written += SetFloat(serialized, "shakeDuration", 0.28f);

            // The combo's rising pitch, pulled well back. A long streak used to climb
            // most of a fifth and turn shrill exactly where it should have felt best.
            written += SetFloat(serialized, "comboPitchStep", 0.028f);
            written += SetFloat(serialized, "comboPitchCeiling", 1.26f);

            // The floor lights with a streak of perfects, and goes out on a miss. The
            // ceiling is the brightest frame the owner approved (0.67), reached by the
            // fourth perfect in a row: at six it "demorava muito".
            written += SetFloat(serialized, "floorGlowBase", 0.07f);
            written += SetFloat(serialized, "floorGlowPerPerfect", 0.15f);
            written += SetInt(serialized, "floorGlowMaxStreak", 4);
            written += SetFloat(serialized, "floorGlowRimBoost", 1.7f);
            written += SetFloat(serialized, "floorGlowSkirt", 1.05f);
            written += SetFloat(serialized, "floorGlowRise", 0.12f);
            written += SetFloat(serialized, "floorGlowFall", 0.4f);
            written += SetFloat(serialized, "floorGlowFlash", 0.08f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            return written;
        }

        private static int SetBool(SerializedObject serialized, string path, bool value)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property == null)
            {
                Debug.LogError($"[ConfigBootstrap] '{serialized.targetObject.name}' has no field '{path}'.");
                return 0;
            }

            property.boolValue = value;
            return 1;
        }

        private static int SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property == null)
            {
                Debug.LogError($"[ConfigBootstrap] '{serialized.targetObject.name}' has no field '{path}'.");
                return 0;
            }

            property.intValue = value;
            return 1;
        }

        private static int SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property == null)
            {
                Debug.LogError($"[ConfigBootstrap] '{serialized.targetObject.name}' has no field '{path}'.");
                return 0;
            }

            property.floatValue = value;
            return 1;
        }

        /// <summary>
        /// Replaces a curve with a straight 0..1 ramp.
        ///
        /// Written explicitly rather than left to the field initializer because a
        /// curve added to a type after its asset was saved does not necessarily
        /// deserialise as the initializer's value, and an empty curve evaluates to
        /// zero everywhere — every throw would land at the minimum range.
        /// </summary>
        private static int SetLinearCurve(SerializedObject serialized, string path)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property == null)
            {
                Debug.LogError($"[ConfigBootstrap] '{serialized.targetObject.name}' has no field '{path}'.");
                return 0;
            }

            property.animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            return 1;
        }
    }
}
