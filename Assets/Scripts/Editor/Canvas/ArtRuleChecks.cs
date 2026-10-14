using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Bellwort Burrow > Checks > Check Art Rules: checks every sheet against the specs that code can check
    /// (see <see cref="ArtRules"/>), and whether setup needs to run again (see <see cref="SetupStamp"/>).
    /// Results go to the Console, one line each, starting with "Bellwort Burrow: art rules".
    /// Capture Shots runs the same checks and saves them to Captures/art-rules.txt.
    /// </summary>
    public static class ArtRuleChecks
    {
        [MenuItem("Bellwort Burrow/Checks/Check Art Rules", false, 450)]
        static void CheckFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            var problems = Problems();
            if (problems.Count == 0)
            {
                Debug.Log("Bellwort Burrow: art rules: every sheet follows the specs, and setup is up to date.");
                EditorUtility.DisplayDialog("Bellwort Burrow", "Every sheet follows the specs, and setup is up to date.", "OK");
                return;
            }
            foreach (var line in problems) Debug.LogWarning($"Bellwort Burrow: art rules: {line}");
            EditorUtility.DisplayDialog("Bellwort Burrow", $"{problems.Count} thing(s) to look at:\n\n- " + string.Join("\n- ", problems), "OK");
        }

        /// <summary>Everything that breaks a spec or needs setup, as short lines. Empty when all is well.</summary>
        public static List<string> Problems()
        {
            var problems = new List<string>();
            foreach (var tileSheets in new[] { false, true })
            {
                foreach (var path in ArtLibrary.SheetPaths(tileSheets))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    AsepriteSheet sheet;
                    try { sheet = AsepriteSheet.Read(File.ReadAllBytes(path)); }
                    catch (System.Exception e)
                    {
                        problems.Add($"{name}: couldn't read it ({e.Message})");
                        continue;
                    }
                    problems.AddRange(ArtRules.Check(name, sheet.Width, sheet.Height, sheet.Art, sheet.HasGlow ? sheet.Glow : null,
                                                     sheet.Slices, tileSheets));
                }
            }
            problems.AddRange(SetupStamp.Changes());
            return problems;
        }

        /// <summary>Writes the results to a text file (used by Capture Shots).</summary>
        public static void WriteReport(string path)
        {
            var problems = Problems();
            var lines = new List<string> { problems.Count == 0 ? "All clear: every sheet follows the specs, and setup is up to date." : $"{problems.Count} thing(s) to look at:" };
            foreach (var line in problems) lines.Add("- " + line);
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
        }
    }
}
