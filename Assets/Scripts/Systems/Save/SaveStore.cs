using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BellwortBurrow.Systems.Save
{
    /// <summary>
    /// Save slots on disk (sys-3): one JSON file per slot, named slot1.json, slot2.json and so on.
    /// Each write goes to a temp file first and then replaces the old file, which is kept as a .bak,
    /// so a crash in the middle of a save can't wreck a slot. Reading falls back to the .bak if the file is broken.
    /// </summary>
    public class SaveStore
    {
        public const string SlotPrefix = "slot";
        public const string Extension = ".json";

        readonly string folder;

        /// <summary>Unity's save folder for the player, plus Saves.</summary>
        public static string DefaultFolder => Path.Combine(Application.persistentDataPath, "Saves");

        public SaveStore(string folder)
        {
            this.folder = folder;
        }

        public string Folder => folder;

        public string PathFor(string slot) => Path.Combine(folder, slot + Extension);

        public bool Exists(string slot) => File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak");

        public void Write(string slot, SaveFile file)
        {
            Directory.CreateDirectory(folder);
            string path = PathFor(slot);
            string temp = path + ".tmp";
            string backup = path + ".bak";

            File.WriteAllText(temp, JsonUtility.ToJson(file, true));
            if (File.Exists(path))
                File.Replace(temp, path, backup);
            else
                File.Move(temp, path);
        }

        public bool TryRead(string slot, out SaveFile file)
        {
            string path = PathFor(slot);
            if (TryParse(path, out file)) return true;
            if (!TryParse(path + ".bak", out file)) return false;

            Debug.LogWarning($"[Save] {path} couldn't be read, so its backup was loaded instead.");
            return true;
        }

        /// <summary>The first slot with no file yet: slot1, then slot2, and so on.</summary>
        public string FirstFreeSlot()
        {
            for (int number = 1; ; number++)
            {
                string slot = SlotPrefix + number;
                if (!Exists(slot)) return slot;
            }
        }

        /// <summary>The header of every slot that can be read, in slot order.</summary>
        public IReadOnlyList<SaveHeader> ListSlots()
        {
            var headers = new List<(int number, SaveHeader header)>();
            if (!Directory.Exists(folder)) return new List<SaveHeader>();

            foreach (string path in Directory.GetFiles(folder, SlotPrefix + "*" + Extension))
            {
                string slot = Path.GetFileNameWithoutExtension(path);
                if (!int.TryParse(slot.Substring(SlotPrefix.Length), out int number)) continue;
                if (TryRead(slot, out var file)) headers.Add((number, file.header));
            }

            headers.Sort((a, b) => a.number.CompareTo(b.number));
            return headers.ConvertAll(entry => entry.header);
        }

        static bool TryParse(string path, out SaveFile file)
        {
            file = null;
            if (!File.Exists(path)) return false;

            try
            {
                file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                file = null;
            }
            return file != null && file.header != null && file.header.version > 0;
        }
    }
}
