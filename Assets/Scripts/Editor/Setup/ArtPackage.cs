using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// The art lives outside git, in a zip of Assets/Art (with its .meta files, so scenes keep their links).
    /// Zips live in the ArtPackage folder next to Assets. Git keeps the empty folder (ArtPackage/.gitkeep) but never the zips,
    /// so exports land there and downloads go there, and both tools know where to look.
    /// Both directions check before they do anything:
    /// - Export: first checks that the work is finished (scenes saved, setup run since the sheets last changed),
    ///   shows what it found, and only zips Assets/Art into ArtPackage/ once you confirm. Uploading stays a separate step.
    /// - Install: first compares the package with the art already here, shows what would change, and keeps any file
    ///   that is newer here than in the package unless you choose to replace everything. Only files under Assets/Art are written.
    /// </summary>
    public static class ArtPackage
    {
        public const string FolderName = "ArtPackage";
        const string ZipPattern = "BellwortBurrow-Art-*.zip";
        const string DriveFolder = "the Bellwort Burrow Art folder on Google Drive";
        const string DriveFolderUrl = "https://drive.google.com/drive/folders/1vh2IrLCfF2ghyih9uQhOeNi9hRS9URCC";

        static string ProjectRoot => Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));

        /// <summary>The ArtPackage folder next to Assets, where exports land and downloads go.</summary>
        public static string PackageFolder => Path.Combine(ProjectRoot, FolderName);

        /// <summary>The most recently saved or downloaded package in the ArtPackage folder, or null when there is none.</summary>
        public static string NewestPackage(out int packageCount)
        {
            packageCount = 0;
            if (!Directory.Exists(PackageFolder)) return null;
            var zips = Directory.GetFiles(PackageFolder, ZipPattern);
            packageCount = zips.Length;
            return zips.Length == 0 ? null : zips.OrderByDescending(zip => File.GetLastWriteTimeUtc(zip)).First();
        }

        // ---------- Export ----------

        [MenuItem("Bellwort Burrow/Art Package/Check And Export Art Package...", false, 300)]
        public static void Export()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;

            // 1. Unsaved scenes would be left out of the work being shared.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();

            // 2. What the check found.
            var report = new StringBuilder();
            var stale = SheetsChangedSinceSetup();
            var handEdited = HandEditedFiles();
            if (stale.Count == 0) report.AppendLine("✓ Prefabs and tiles are up to date with the sheets.");
            else
            {
                report.AppendLine("⚠ These sheets changed after their prefabs or tiles were made:");
                foreach (var sheet in stale.Take(4)) report.AppendLine("    " + sheet);
                if (stale.Count > 4) report.AppendLine($"    and {stale.Count - 4} more");
                report.AppendLine("  Run setup first so the package matches the art.");
            }
            if (handEdited.Count > 0)
            {
                report.AppendLine($"• {handEdited.Count} prefab(s) or tile(s) were changed by hand. They go into the package as they are.");
            }
            report.AppendLine("✓ Open scenes are saved.");
            string zipPath = Path.Combine(PackageFolder, $"BellwortBurrow-Art-{DateTime.Now:yyyy-MM-dd}.zip");
            if (File.Exists(zipPath)) report.AppendLine($"• Today's package already exists and will be replaced.");
            report.AppendLine();
            report.Append("Are you done with this round of art and ready to export?");

            int choice = stale.Count > 0
                ? EditorUtility.DisplayDialogComplex("Ready to export the art package?", report.ToString(), "Run Setup, Then Export", "Not Yet", "Export As Is")
                : (EditorUtility.DisplayDialog("Ready to export the art package?", report.ToString(), "Export", "Not Yet") ? 2 : 1);
            if (choice == 1) return;
            if (choice == 0) ForestSetup.RunAll();

            // 3. Export.
            int files = WriteZip(zipPath);
            Debug.Log($"Bellwort Burrow: packed {files} art files into {zipPath}.");
            EditorUtility.DisplayDialog("Art package exported",
                $"Packed {files} files into {Path.GetFileName(zipPath)}.\n\nNext, upload it to {DriveFolder}. " +
                "If Drive asks, choose Replace existing file so the download link stays the same.", "Show In Explorer");
            EditorUtility.RevealInFinder(zipPath);
        }

        static int WriteZip(string zipPath)
        {
            string root = ProjectRoot;
            string artFolder = Path.Combine(root, "Assets", "Art");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            if (File.Exists(zipPath)) File.Delete(zipPath);

            int files = 0;
            using (var stream = File.Create(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                string folderMeta = artFolder + ".meta";
                if (File.Exists(folderMeta)) { AddFile(zip, root, folderMeta); files++; }
                foreach (var file in Directory.GetFiles(artFolder, "*", SearchOption.AllDirectories))
                {
                    AddFile(zip, root, file);
                    files++;
                }
            }
            return files;
        }

        /// <summary>Sheets saved after the newest prefab or tile made from them.</summary>
        static List<string> SheetsChangedSinceSetup()
        {
            var stale = new List<string>();
            foreach (var sheet in ArtLibrary.SheetPaths(tileSheets: false))
            {
                string folder = $"{ArtLibrary.PrefabRoot}/{Path.GetFileNameWithoutExtension(sheet)}";
                if (NewestWrite(folder, "*.prefab") < File.GetLastWriteTimeUtc(sheet)) stale.Add(sheet);
            }
            foreach (var sheet in ArtLibrary.SheetPaths(tileSheets: true))
            {
                if (NewestWrite(GroundAutoTiles.Folder, "*.asset") < File.GetLastWriteTimeUtc(sheet)) stale.Add(sheet);
            }
            return stale;
        }

        static DateTime NewestWrite(string folder, string pattern)
        {
            if (!Directory.Exists(folder)) return DateTime.MinValue;
            var files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
            return files.Length == 0 ? DateTime.MinValue : files.Max(file => File.GetLastWriteTimeUtc(file));
        }

        static List<string> HandEditedFiles()
        {
            var edited = new List<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith(ArtLibrary.ArtRoot + "/")) continue;
                if (!path.EndsWith(".prefab") && !path.EndsWith(".asset")) continue;
                if (GeneratedAssets.ChangedByHand(path)) edited.Add(path);
            }
            return edited;
        }

        // ---------- Install ----------

        [MenuItem("Bellwort Burrow/Art Package/Check And Install Art Package...", false, 301)]
        public static void Install()
        {
            // 0. Find the package: the newest zip in the ArtPackage folder.
            string zipPath = NewestPackage(out int packageCount);
            if (zipPath == null)
            {
                Directory.CreateDirectory(PackageFolder);
                int next = EditorUtility.DisplayDialogComplex("No art package to install",
                    $"There's no {ZipPattern} in the {FolderName} folder yet.\n\n" +
                    $"Download the latest one from {DriveFolder} into:\n{PackageFolder}\n\nThen choose Check And Install again.",
                    "Open Drive Folder", "Cancel", $"Open {FolderName} Folder");
                if (next == 0) Application.OpenURL(DriveFolderUrl);
                if (next == 2) EditorUtility.OpenWithDefaultApp(PackageFolder);
                return;
            }

            string root = ProjectRoot;
            string artFolder = Path.Combine(root, "Assets", "Art");
            var added = new List<ZipArchiveEntry>();
            var updated = new List<ZipArchiveEntry>();
            var newerHere = new List<ZipArchiveEntry>();
            int unchanged = 0, outside = 0;

            using (var stream = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                // 1. Compare the package with what's here.
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // a folder
                    string target = Target(root, artFolder, entry);
                    if (target == null) { outside++; continue; }
                    if (!File.Exists(target)) { added.Add(entry); continue; }
                    if (SameContent(entry, target)) { unchanged++; continue; }
                    // Zip times are only accurate to two seconds.
                    if (File.GetLastWriteTime(target) > entry.LastWriteTime.LocalDateTime.AddSeconds(2)) newerHere.Add(entry);
                    else updated.Add(entry);
                }

                if (added.Count + updated.Count + newerHere.Count == 0)
                {
                    EditorUtility.DisplayDialog("Art is up to date", $"The art here already matches {Path.GetFileName(zipPath)}.", "OK");
                    return;
                }

                // 2. Show what would change and ask.
                var report = new StringBuilder();
                if (packageCount > 1)
                    report.AppendLine($"Using the newest of the {packageCount} packages in {FolderName}/.");
                report.AppendLine($"{Path.GetFileName(zipPath)} would add {added.Count} file(s) and update {updated.Count}.");
                if (unchanged > 0) report.AppendLine($"{unchanged} file(s) already match.");
                if (newerHere.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine($"⚠ {newerHere.Count} file(s) here are newer than the package, so they may hold your own changes:");
                    foreach (var entry in newerHere.Take(4)) report.AppendLine("    " + entry.FullName);
                    if (newerHere.Count > 4) report.AppendLine($"    and {newerHere.Count - 4} more");
                }
                report.AppendLine();
                report.Append("Files here that aren't in the package are never touched.");

                bool replaceNewer;
                if (newerHere.Count > 0)
                {
                    int choice = EditorUtility.DisplayDialogComplex("Install the art package?", report.ToString(),
                        "Install, Keep My Newer Files", "Cancel", "Install, Replace Everything");
                    if (choice == 1) return;
                    replaceNewer = choice == 2;
                }
                else
                {
                    if (!EditorUtility.DisplayDialog("Install the art package?", report.ToString(), "Install", "Cancel")) return;
                    replaceNewer = false;
                }

                // 3. Install.
                var toWrite = added.Concat(updated).Concat(replaceNewer ? newerHere : Enumerable.Empty<ZipArchiveEntry>()).ToList();
                foreach (var entry in toWrite)
                {
                    string target = Target(root, artFolder, entry);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = entry.Open())
                    using (var output = File.Create(target))
                        input.CopyTo(output);
                    File.SetLastWriteTime(target, entry.LastWriteTime.LocalDateTime);
                }

                AssetDatabase.Refresh();
                ArtLibrary.UseSheetImporter();
                string kept = !replaceNewer && newerHere.Count > 0 ? $", kept {newerHere.Count} newer file(s) of yours" : "";
                Debug.Log($"Bellwort Burrow: installed {toWrite.Count} art file(s) from {Path.GetFileName(zipPath)}{kept}" +
                          (outside > 0 ? $", skipped {outside} outside Assets/Art." : "."));
            }
        }

        /// <summary>Where an entry goes, or null when it's outside Assets/Art (never written).</summary>
        static string Target(string root, string artFolder, ZipArchiveEntry entry)
        {
            string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            bool inArt = target.StartsWith(artFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(target, artFolder + ".meta", StringComparison.OrdinalIgnoreCase);
            return inArt ? target : null;
        }

        static bool SameContent(ZipArchiveEntry entry, string file)
        {
            var local = File.ReadAllBytes(file);
            if (local.Length != entry.Length) return false;
            using (var input = entry.Open())
            using (var memory = new MemoryStream())
            {
                input.CopyTo(memory);
                return memory.ToArray().SequenceEqual(local);
            }
        }

        static void AddFile(ZipArchive zip, string root, string file)
        {
            string entryName = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            var entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
            entry.LastWriteTime = File.GetLastWriteTime(file);
            using (var input = File.OpenRead(file))
            using (var output = entry.Open())
                input.CopyTo(output);
        }
    }
}
