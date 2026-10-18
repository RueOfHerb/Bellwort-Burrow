using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Remembers what each sheet's Glow layer and slices looked like the last time Set Up Forest Art ran, in
    /// Assets/Art/setup-stamp.txt (it travels in the art package with the prefabs it describes).
    /// The 2D lights in the prefabs are worked out from the Glow layer, and the prefabs from the slices, so when either
    /// changed since the stamp, setup needs to run again. Plain pixel edits on the Art layer don't need it.
    /// </summary>
    public static class SetupStamp
    {
        public const string StampPath = ArtLibrary.ArtRoot + "/setup-stamp.txt";

        /// <summary>Called by Set Up Forest Art after it finishes cleanly.</summary>
        public static void Write()
        {
            var lines = new List<string> { "# sheet\tglow\tslices (fingerprints written by Set Up Forest Art)" };
            foreach (var entry in Current())
                lines.Add($"{entry.Key}\t{entry.Value.glow}\t{entry.Value.slices}");
            File.WriteAllText(StampPath, string.Join("\n", lines) + "\n");
            AssetDatabase.ImportAsset(StampPath);
        }

        /// <summary>Sheets whose Glow layer or slices changed since setup last ran, as short lines. Empty when none did.</summary>
        public static List<string> Changes()
        {
            var changes = new List<string>();
            if (!File.Exists(StampPath))
            {
                changes.Add("setup hasn't stamped the sheets yet: run Set Up Forest Art once");
                return changes;
            }
            var stamped = new Dictionary<string, string[]>();
            foreach (var line in File.ReadAllLines(StampPath))
            {
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length == 3) stamped[parts[0]] = parts;
            }
            foreach (var entry in Current())
            {
                string name = Path.GetFileNameWithoutExtension(entry.Key);
                if (!stamped.TryGetValue(entry.Key, out var old))
                    changes.Add($"{name} is new since setup last ran");
                else if (old[1] != entry.Value.glow)
                    changes.Add($"{name}: the Glow layer changed since setup last ran, so the lights are out of date");
                else if (old[2] != entry.Value.slices)
                    changes.Add($"{name}: slices were added, renamed, moved or resized since setup last ran");
            }
            if (changes.Count > 0) changes.Add("run Set Up Forest Art once to bring the prefabs and lights up to date");
            return changes;
        }

        static SortedDictionary<string, (string glow, string slices)> Current()
        {
            var result = new SortedDictionary<string, (string, string)>(System.StringComparer.Ordinal);
            foreach (var path in ArtLibrary.SheetPaths(false).Concat(ArtLibrary.SheetPaths(true)))
            {
                AsepriteSheet sheet;
                try { sheet = AsepriteSheet.Read(File.ReadAllBytes(path)); }
                catch (System.Exception) { continue; }
                string glow = sheet.HasGlow ? ArtRules.Fingerprint(sheet.Glow) : "none";
                var text = new StringBuilder();
                foreach (var s in sheet.Slices)
                    text.Append($"{s.Name} {s.X} {s.Y} {s.Width} {s.Height} {s.HasPivot} {s.PivotX} {s.PivotY}\n");
                result[path] = (glow, ArtRules.Fingerprint(Encoding.UTF8.GetBytes(text.ToString())));
            }
            return result;
        }
    }
}
