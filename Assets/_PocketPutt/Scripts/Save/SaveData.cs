using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PocketPutt.Save
{
    [Serializable]
    public sealed class LevelRecord
    {
        public string id;
        public int stars;      // best, 1..3 (0 = never completed)
        public int bestShots;  // lowest stroke count ever
        public int plays;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public List<LevelRecord> levels = new List<LevelRecord>();
        public List<string> trophies = new List<string>();        // cup ids with a won final
        public List<string> finalsUnlocked = new List<string>();  // cup ids whose final has been opened (never regresses)
        public bool sound = true;
        public bool music = true;
        public bool vibration = true;
        public string lastLevelId = "";
        public bool seenIntro;
        public bool seenGestureTip;   // "drag anywhere" tip shown until the first real drag
        public int language;   // 0 = follow device, 1 = Portuguese, 2 = English
    }

    /// <summary>Atomic JSON persistence. A corrupt or missing file falls back to a fresh save.</summary>
    public static class SaveStore
    {
        public static string PathOverride;
        static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "pocketputt_save.json");

        public static SaveData Load()
        {
            string path = FilePath;
            // .tmp is only trusted when the main file is missing/corrupt (a crash between delete and move); it must parse completely.
            foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(candidate));
                    if (data != null && data.levels != null) return data;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PocketPutt] Save unreadable (" + Path.GetFileName(candidate) + "): " + e.Message);
                }
            }
            return new SaveData();
        }

        public static void Write(SaveData data)
        {
            try
            {
                string path = FilePath;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", true);
                    File.Delete(path);
                }
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PocketPutt] Save failed: " + e.Message);
            }
        }
    }
}
