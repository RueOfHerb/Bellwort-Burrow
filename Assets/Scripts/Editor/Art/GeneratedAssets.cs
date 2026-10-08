using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Keeps the Bellwort tools from writing over hand edits.
    /// Whenever a tool makes a prefab, tile, palette or scene, it stamps a fingerprint of the file into the file's .meta.
    /// Before making it again, the tool checks the fingerprint: if the file changed since (someone edited it by hand),
    /// or never had a stamp, the tool leaves it alone and says so.
    /// To let the tools rebuild something you changed, select it (or its folder) and choose
    /// Assets > Bellwort Burrow > Let Setup Rebuild Selected. That throws your changes away on the next rebuild.
    /// </summary>
    public static class GeneratedAssets
    {
        const string Prefix = "bellwort-generated:";
        const string RebuildMenu = "Assets/Bellwort Burrow/Let Setup Rebuild Selected";

        /// <summary>
        /// True when nothing is at the path yet, or the file is exactly what a Bellwort tool last wrote there.
        /// False when it was changed by hand, or was never stamped, so it may hold someone's work.
        /// </summary>
        public static bool CanOverwrite(string path)
        {
            if (!File.Exists(path)) return true;
            string recorded = RecordedFingerprint(path);
            return recorded != null && recorded == Fingerprint(path);
        }

        /// <summary>True when the file was made by a Bellwort tool and changed by hand since.</summary>
        public static bool ChangedByHand(string path) =>
            File.Exists(path) && RecordedFingerprint(path) != null && RecordedFingerprint(path) != Fingerprint(path);

        /// <summary>Call right after a tool writes the file (and after AssetDatabase.SaveAssets). Records its fingerprint.</summary>
        public static void Stamp(string path)
        {
            if (!File.Exists(path)) return;
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) return;
            string userData = Prefix + Fingerprint(path);
            if (importer.userData == userData) return;
            importer.userData = userData;
            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }

        /// <summary>Logs one warning listing everything a tool left alone.</summary>
        public static void ReportKept(string toolName, List<string> keptPaths)
        {
            if (keptPaths.Count == 0) return;
            var message = new StringBuilder();
            message.Append($"Bellwort Burrow: {toolName} left {keptPaths.Count} file(s) alone because they were changed by hand ");
            message.Append($"(or made before change tracking). Your changes are kept. To let it rebuild one, select it and choose {RebuildMenu.Replace("/", " > ")}.");
            foreach (var path in keptPaths) message.Append("\n  ").Append(path);
            Debug.LogWarning(message.ToString());
        }

        [MenuItem(RebuildMenu)]
        static void LetSetupRebuildSelected()
        {
            var paths = SelectedFiles();
            if (paths.Count == 0) return;
            if (!EditorUtility.DisplayDialog("Let setup rebuild these?",
                    $"{paths.Count} file(s) will be rebuilt the next time Set Up Forest Art runs, and any hand changes in them will be lost.",
                    "Let Setup Rebuild", "Cancel"))
                return;
            foreach (var path in paths) Stamp(path);
            Debug.Log($"Bellwort Burrow: the next setup run will rebuild {paths.Count} file(s).");
        }

        [MenuItem(RebuildMenu, true)]
        static bool CanLetSetupRebuildSelected() => SelectedFiles().Count > 0;

        /// <summary>Selected prefabs, tile assets and scenes, including those inside selected folders.</summary>
        static List<string> SelectedFiles()
        {
            var result = new List<string>();
            foreach (var guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var inner in AssetDatabase.FindAssets("t:Prefab t:TileBase t:Scene", new[] { path }))
                        AddIfTrackable(result, AssetDatabase.GUIDToAssetPath(inner));
                }
                else AddIfTrackable(result, path);
            }
            return result;
        }

        static void AddIfTrackable(List<string> paths, string path)
        {
            if ((path.EndsWith(".prefab") || path.EndsWith(".asset") || path.EndsWith(".unity")) && !paths.Contains(path))
                paths.Add(path);
        }

        static string RecordedFingerprint(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || string.IsNullOrEmpty(importer.userData) || !importer.userData.StartsWith(Prefix)) return null;
            return importer.userData.Substring(Prefix.Length);
        }

        /// <summary>A hash of the file's text, ignoring Windows vs Unix line endings.</summary>
        static string Fingerprint(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var normalized = new List<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                if (!(bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n')) normalized.Add(bytes[i]);
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(normalized.ToArray());
                var text = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
