using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>One ramp of a palette: its name and its colors, darkest first (Shadow, Dark, Base, Light, Highlight).</summary>
    [Serializable]
    public class Ramp
    {
        public string name = "";
        public Color32[] colors = Array.Empty<Color32>();

        public string Id => RampPalette.Slug(name);
    }

    /// <summary>
    /// A palette of ramps, read from a .gpl file: one ramp per row of Columns colors, named by what its colors' names
    /// share ("Blue grey Shadow" to "Blue grey Highlight" make "Blue grey"). Unity imports every .gpl as one of these, so a
    /// palette saved from Aseprite reaches the game on its own. The characters palette drives player recoloring (sys-6).
    /// </summary>
    public class RampPalette : ScriptableObject
    {
        static readonly Regex ColorLine = new(@"^\s*(\d+)\s+(\d+)\s+(\d+)\s*(.*)$");

        [SerializeField] List<Ramp> ramps = new();

        public IReadOnlyList<Ramp> Ramps => ramps;

        /// <summary>The ramp with this name ("Blue grey") or id ("blue-grey"). Null for Natural, empty or unknown.</summary>
        public Ramp Find(string nameOrId)
        {
            string id = Slug(nameOrId);
            if (id.Length == 0) return null;
            foreach (var ramp in ramps)
            {
                if (ramp != null && ramp.Id == id) return ramp;
            }
            return null;
        }

        public static string Slug(string name) => Regex.Replace((name ?? "").Trim().ToLowerInvariant(), @"\s+", "-");

        public static List<Ramp> Parse(string gpl)
        {
            int columns = 5;
            var entries = new List<(Color32 color, string name)>();

            foreach (string raw in (gpl ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("Columns:", StringComparison.Ordinal))
                {
                    if (int.TryParse(line.Substring("Columns:".Length).Trim(), out int read) && read > 0) columns = read;
                    continue;
                }

                var match = ColorLine.Match(line);
                if (!match.Success) continue;

                var color = new Color32(ToByte(match.Groups[1].Value), ToByte(match.Groups[2].Value), ToByte(match.Groups[3].Value), 255);
                entries.Add((color, match.Groups[4].Value.Trim()));
            }

            var ramps = new List<Ramp>();
            for (int start = 0; start < entries.Count; start += columns)
            {
                int count = Math.Min(columns, entries.Count - start);
                var group = entries.GetRange(start, count);
                ramps.Add(new Ramp
                {
                    name = SharedName(group.ConvertAll(entry => entry.name)),
                    colors = group.ConvertAll(entry => entry.color).ToArray()
                });
            }
            return ramps;
        }

        static byte ToByte(string value) => (byte)Math.Clamp(int.Parse(value), 0, 255);

        // The words every name in the row starts with; the whole first name if they share none or all match.
        static string SharedName(List<string> names)
        {
            var first = names[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int shared = first.Length;
            foreach (string name in names)
            {
                var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int i = 0;
                while (i < shared && i < words.Length && words[i] == first[i]) i++;
                shared = i;
            }
            return shared == 0 ? names[0] : string.Join(" ", first, 0, shared);
        }

        internal void SetRamps(IEnumerable<Ramp> newRamps) => ramps = new List<Ramp>(newRamps);
    }
}
