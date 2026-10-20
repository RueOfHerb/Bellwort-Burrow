using System;
using System.Collections.Generic;

namespace BellwortBurrow.Systems.Save
{
    /// <summary>What a Load screen shows for a slot. It sits at the top of the file, so listing slots never needs the rest.</summary>
    [Serializable]
    public class SaveHeader
    {
        public int version;
        public string slot = "";
        public string playerName = "";
        public string critterKind = "";
        public int day;
        public string season = "";
        public string zone = "";
        public float playSeconds;
        /// <summary>UTC, ISO 8601.</summary>
        public string savedAt = "";
    }

    /// <summary>One system's state: the JSON its own CaptureState wrote, under its SaveKey.</summary>
    [Serializable]
    public class SaveSection
    {
        public string key;
        public string json;
    }

    /// <summary>A whole save slot: the header, then one section per system (sys-3).</summary>
    [Serializable]
    public class SaveFile
    {
        public SaveHeader header = new();
        public List<SaveSection> sections = new();

        /// <summary>The section's JSON, or null when the file has no section by that name.</summary>
        public string Find(string key)
        {
            foreach (var section in sections)
            {
                if (section != null && section.key == key) return section.json;
            }
            return null;
        }
    }
}
