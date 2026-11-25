using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The game reads input through the Input System and nothing else.
    ///
    /// That is enforced here because nothing else can enforce it. The project ships with
    /// active input handling set to the new system alone, and in that mode every call on
    /// the legacy input class throws the moment it runs — but it still compiles, and a
    /// batch run never executes the places most likely to use it. The failure therefore
    /// lands on a handset, in front of the owner, on whichever screen happened to use it.
    ///
    /// It arrived exactly that way. The boot splash let a tap skip it by reading the
    /// legacy class, so the first thing a player ever sees would have thrown on every
    /// frame it was drawn. 155 passing tests had nothing to say about it.
    ///
    /// This is the same family as the missing VIBRATE permission and the android JNI
    /// module: a platform-only path no editor compile ever reaches. The answer that
    /// generalises is not another careful review, it is a scan.
    /// </summary>
    public sealed class InputHandlingTests
    {
        private const string ScriptRoot = "Assets/_Game/Scripts";
        private const string SettingsPath = "ProjectSettings/ProjectSettings.asset";

        /// <summary>
        /// A read of the legacy class: <c>Input</c> followed by one of its members, where
        /// what precedes it is not part of a longer word. The lookbehind is what keeps a
        /// field called <c>playerInput</c> out of the results while still catching the
        /// fully qualified <c>UnityEngine</c> spelling, whose preceding dot is not a word
        /// character.
        /// </summary>
        private static readonly Regex Legacy = new Regex(
            @"(?<!\w)Input\.(touch|GetMouse|GetKey|GetButton|GetAxis|anyKey|mousePosition|" +
            @"mouseScrollDelta|acceleration|compositionString|location|compass|gyro)",
            RegexOptions.Compiled);

        [Test]
        public void The_project_still_uses_the_input_system_alone()
        {
            string settings = File.ReadAllText(SettingsPath);

            StringAssert.Contains("activeInputHandler: 1", settings,
                "Active input handling has moved. If the legacy Input Manager is switched " +
                "back on then the scan in this fixture is no longer the rule it was written " +
                "for — that is a decision to take deliberately, not a test to delete.");
        }

        [Test]
        public void No_runtime_script_reads_the_legacy_input_class()
        {
            List<string> offenders = new List<string>();

            foreach (string file in Directory.GetFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (Legacy.IsMatch(lines[i]))
                    {
                        offenders.Add($"{file}:{i + 1}  {lines[i].Trim()}");
                    }
                }
            }

            Assert.IsEmpty(offenders,
                "These read the legacy input class, which throws at runtime with " +
                "activeInputHandler: 1. Read Pointer.current or Keyboard.current instead, " +
                "as ThrowController and BootSplash do:\n  " + string.Join("\n  ", offenders));
        }
    }
}
