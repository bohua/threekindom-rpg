using System;
using System.IO;
using UnityEngine;

namespace ThreeKingdoms.Save
{
    public enum SaveReadStatus { Ok, Empty, Damaged, Unsupported }

    // The slot files on this machine (ADR-0004): manual_1..N and one rolling pre-battle "auto". Writes go to a
    // temporary file first and then replace the slot, so a crash mid-write never leaves half a save. Reading never
    // changes or deletes a file, even a damaged one (rule 7).
    public sealed class SaveSlots
    {
        public const string Auto = "auto";

        public string Directory { get; }
        public int ManualCount { get; }

        public SaveSlots(string directory, int manualCount)
        {
            Directory = directory;
            ManualCount = Math.Max(1, manualCount);
        }

        public static string Manual(int index) => $"manual_{index}";

        public string PathOf(string slot) => Path.Combine(Directory, slot + ".json");

        public bool Exists(string slot) => File.Exists(PathOf(slot));

        public void Write(string slot, SaveData data)
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = PathOf(slot), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true), System.Text.Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public SaveReadStatus Read(string slot, out SaveData data)
        {
            data = null;
            string path = PathOf(slot);
            if (!File.Exists(path)) return SaveReadStatus.Empty;
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception) { data = null; }
            if (data == null || string.IsNullOrEmpty(data.location)) { data = null; return SaveReadStatus.Damaged; }
            // Only the current format is read; others are reported and left alone (answer 7).
            if (data.formatVersion != SaveData.CurrentFormat) { data = null; return SaveReadStatus.Unsupported; }
            return SaveReadStatus.Ok;
        }
    }
}
