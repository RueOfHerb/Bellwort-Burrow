using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
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
    ///
    /// Each machine remembers the last package it exported or installed (ArtPackage/last-sync.txt, a list of file hashes,
    /// also kept out of git). That's how the tools tell your own unshared changes apart from changes in a new package,
    /// without trusting file dates, which shift between time zones and whenever Unity touches a file.
    ///
    /// Both directions check before they do anything:
    /// - Export: checks that setup's prefabs and tiles match the sheets, says what changed since the last package,
    ///   and only zips Assets/Art once you confirm. Uploading stays a separate step.
    /// - Install: compares the package with the art already here, shows what would change, and keeps files you changed
    ///   since the last package unless you choose to replace everything. Only files under Assets/Art are written.
    /// </summary>
    public static class ArtPackage
    {
        public const string FolderName = "ArtPackage";
        const string ZipPattern = "BellwortBurrow-Art-*.zip";
        const string SyncFileName = "last-sync.txt";
        const string DriveFolder = "the Bellwort Burrow Art folder on Google Drive";
        const string DriveFolderUrl = "https://drive.google.com/drive/folders/1vh2IrLCfF2ghyih9uQhOeNi9hRS9URCC";
        const int ListedFiles = 3;

        static string ProjectRoot => Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
        static string ArtFolder => Path.Combine(ProjectRoot, "Assets", "Art");
        static string SyncFile => Path.Combine(PackageFolder, SyncFileName);

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
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();

            // 1. Check.
            var problems = ArtChecks.SetupProblems();
            var handEdited = HandEditedFiles();
            var current = HashArt();
            var lastSync = ReadLastSync(out string lastPackage);

            var report = new StringBuilder();
            if (problems.Count == 0) report.AppendLine("✓ Prefabs and tiles match the sheets.");
            else
            {
                report.AppendLine("⚠ Setup is behind the sheets:");
                AppendList(report, problems);
                report.AppendLine("  Run setup first so the package matches the art.");
            }
            if (handEdited.Count > 0)
                report.AppendLine($"• {handEdited.Count} prefab(s) or tile(s) were changed by hand. They go into the package as they are.");
            if (AnySceneUnsaved())
                report.AppendLine("• A scene has unsaved changes. Scenes go to git, not the package, so save it before you commit.");
            if (lastSync == null)
                report.AppendLine("• This is the first package from this machine.");
            else
            {
                int changed = ChangedSince(lastSync, current);
                report.AppendLine(changed == 0
                    ? $"• Nothing changed since the last package ({lastPackage})."
                    : $"• {changed} file(s) changed since the last package ({lastPackage}).");
            }
            string zipPath = Path.Combine(PackageFolder, $"BellwortBurrow-Art-{DateTime.Now:yyyy-MM-dd}.zip");
            if (File.Exists(zipPath)) report.AppendLine("• Today's package already exists and will be replaced.");
            report.AppendLine();
            report.Append("Are you done with this round of art and ready to export?");

            // 2. Confirm.
            int choice = problems.Count > 0
                ? EditorUtility.DisplayDialogComplex("Ready to export the art package?", report.ToString(), "Run Setup, Then Export", "Not Yet", "Export As Is")
                : (EditorUtility.DisplayDialog("Ready to export the art package?", report.ToString(), "Export", "Not Yet") ? 2 : 1);
            if (choice == 1) return;
            if (choice == 0)
            {
                ForestSetup.RunAll();
                problems = ArtChecks.SetupProblems();
                if (problems.Count > 0)
                {
                    var still = new StringBuilder("Setup ran, but these are still off:\n");
                    AppendList(still, problems);
                    still.Append("\nThey usually mean a prefab was changed by hand or a slice was removed. Export anyway?");
                    if (!EditorUtility.DisplayDialog("Setup didn't fix everything", still.ToString(), "Export Anyway", "Not Yet")) return;
                }
                current = HashArt();
            }

            // 3. Export.
            int files = WriteZip(zipPath);
            WriteLastSync(Path.GetFileName(zipPath), current);
            Debug.Log($"Bellwort Burrow: packed {files} art files into {zipPath}.");
            if (EditorUtility.DisplayDialog("Art package exported",
                    $"Packed {files} files into {Path.GetFileName(zipPath)}.\n\nNext, upload it to {DriveFolder}. " +
                    "If Drive asks, choose Replace existing file so the download link stays the same.", "Open Drive Folder", "Done"))
                Application.OpenURL(DriveFolderUrl);
            EditorUtility.RevealInFinder(zipPath);
        }

        static int WriteZip(string zipPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            string temp = zipPath + ".partial";
            if (File.Exists(temp)) File.Delete(temp);

            int files = 0;
            using (var stream = File.Create(temp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in ArtFiles())
                {
                    AddFile(zip, file);
                    files++;
                }
            }
            // Only replace the old zip once the new one is complete.
            if (File.Exists(zipPath)) File.Delete(zipPath);
            File.Move(temp, zipPath);
            return files;
        }

        /// <summary>Assets/Art.meta and everything under Assets/Art, as full paths.</summary>
        static IEnumerable<string> ArtFiles()
        {
            string folderMeta = ArtFolder + ".meta";
            if (File.Exists(folderMeta)) yield return folderMeta;
            foreach (var file in Directory.GetFiles(ArtFolder, "*", SearchOption.AllDirectories))
                yield return file;
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

        static bool AnySceneUnsaved()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
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

            var lastSync = ReadLastSync(out _);
            var added = new List<ZipArchiveEntry>();
            var updated = new List<ZipArchiveEntry>();
            var changedHere = new List<ZipArchiveEntry>();
            var packageHashes = new Dictionary<string, string>();
            int unchanged = 0, outside = 0;

            using (var stream = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                // 1. Compare the package with what's here and with the last package this machine had.
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // a folder
                    string target = TargetPath(ProjectRoot, ArtFolder, entry.FullName);
                    if (target == null) { outside++; continue; }

                    string relative = RelativePath(target);
                    string packageHash = Hash(ReadEntry(entry));
                    packageHashes[relative] = packageHash;
                    bool existsHere = File.Exists(target);
                    string hereHash = existsHere ? Hash(File.ReadAllBytes(target)) : null;
                    string lastHash = null;
                    if (lastSync != null) lastSync.TryGetValue(relative, out lastHash);

                    switch (Classify(existsHere, hereHash, packageHash, lastHash))
                    {
                        case Change.Add: added.Add(entry); break;
                        case Change.Same: unchanged++; break;
                        case Change.Update: updated.Add(entry); break;
                        case Change.ChangedHere: changedHere.Add(entry); break;
                    }
                }

                if (added.Count + updated.Count + changedHere.Count == 0)
                {
                    WriteLastSync(Path.GetFileName(zipPath), packageHashes);
                    EditorUtility.DisplayDialog("Art is up to date", $"The art here already matches {Path.GetFileName(zipPath)}.", "OK");
                    return;
                }

                // 2. Show what would change and ask.
                var report = new StringBuilder();
                if (packageCount > 1)
                    report.AppendLine($"Using the newest of the {packageCount} packages in {FolderName}/.");
                report.AppendLine($"{Path.GetFileName(zipPath)} would add {added.Count} and update {updated.Count} file(s); {unchanged} already match.");
                if (changedHere.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine(lastSync == null
                        ? $"⚠ {changedHere.Count} file(s) here differ and may be your own work (no earlier package is recorded here):"
                        : $"⚠ {changedHere.Count} file(s) here changed since the last package and may be your own work:");
                    AppendList(report, changedHere.Select(entry => entry.FullName).ToList());
                }
                report.AppendLine();
                report.Append("Files that aren't in the package are never touched.");

                bool replaceChanged;
                if (changedHere.Count > 0)
                {
                    int choice = EditorUtility.DisplayDialogComplex("Install the art package?", report.ToString(),
                        "Install, Keep My Changes", "Cancel", "Install, Replace Everything");
                    if (choice == 1) return;
                    replaceChanged = choice == 2;
                }
                else
                {
                    if (!EditorUtility.DisplayDialog("Install the art package?", report.ToString(), "Install", "Cancel")) return;
                    replaceChanged = false;
                }

                // 3. Install.
                var toWrite = added.Concat(updated).Concat(replaceChanged ? changedHere : Enumerable.Empty<ZipArchiveEntry>()).ToList();
                foreach (var entry in toWrite)
                {
                    string target = TargetPath(ProjectRoot, ArtFolder, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, ReadEntry(entry));
                }

                // Kept files still count as changed here next time, since the record now holds the package's version.
                WriteLastSync(Path.GetFileName(zipPath), packageHashes);
                AssetDatabase.Refresh();
                ArtLibrary.UseSheetImporter();
                string kept = !replaceChanged && changedHere.Count > 0 ? $", kept {changedHere.Count} changed file(s) of yours" : "";
                Debug.Log($"Bellwort Burrow: installed {toWrite.Count} art file(s) from {Path.GetFileName(zipPath)}{kept}" +
                          (outside > 0 ? $", skipped {outside} outside Assets/Art." : "."));
            }
        }

        internal enum Change { Add, Same, Update, ChangedHere }

        /// <summary>
        /// What installing one package file should do. A file that differs is updated only when it still matches what the
        /// last package had, meaning nobody changed it here since. Anything else that differs may be someone's work.
        /// </summary>
        internal static Change Classify(bool existsHere, string hereHash, string packageHash, string lastSyncHash)
        {
            if (!existsHere) return Change.Add;
            if (hereHash == packageHash) return Change.Same;
            if (lastSyncHash != null && hereHash == lastSyncHash) return Change.Update;
            return Change.ChangedHere;
        }

        /// <summary>Where a package entry goes, or null when it's outside Assets/Art (never written).</summary>
        internal static string TargetPath(string projectRoot, string artFolder, string entryName)
        {
            string target = Path.GetFullPath(Path.Combine(projectRoot, entryName));
            bool inArt = target.StartsWith(artFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(target, artFolder + ".meta", StringComparison.OrdinalIgnoreCase);
            return inArt ? target : null;
        }

        // ---------- Last package record ----------

        static Dictionary<string, string> HashArt()
        {
            var hashes = new Dictionary<string, string>();
            foreach (var file in ArtFiles()) hashes[RelativePath(file)] = Hash(File.ReadAllBytes(file));
            return hashes;
        }

        static int ChangedSince(Dictionary<string, string> before, Dictionary<string, string> now)
        {
            int changed = now.Count(pair => !before.TryGetValue(pair.Key, out var hash) || hash != pair.Value);
            changed += before.Keys.Count(path => !now.ContainsKey(path));
            return changed;
        }

        /// <summary>The file hashes of the last package exported or installed here, or null when there's no record.</summary>
        static Dictionary<string, string> ReadLastSync(out string packageName)
        {
            packageName = null;
            if (!File.Exists(SyncFile)) return null;
            var hashes = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(SyncFile))
            {
                if (line.StartsWith("# package: ")) { packageName = line.Substring("# package: ".Length); continue; }
                if (line.StartsWith("#") || line.Length < 42) continue;
                hashes[line.Substring(41)] = line.Substring(0, 40);
            }
            return hashes;
        }

        static void WriteLastSync(string packageName, Dictionary<string, string> hashes)
        {
            Directory.CreateDirectory(PackageFolder);
            var text = new StringBuilder();
            text.AppendLine("# The art package this machine last exported or installed. Bellwort Burrow > Art Package uses it to");
            text.AppendLine("# tell your own changes apart from a new package's. Safe to delete; the next install then treats every");
            text.AppendLine("# difference as yours.");
            text.AppendLine("# package: " + packageName);
            foreach (var pair in hashes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                text.Append(pair.Value).Append(' ').AppendLine(pair.Key);
            File.WriteAllText(SyncFile, text.ToString());
        }

        /// <summary>Path from the project root with forward slashes, like "Assets/Art/Forest/forest_trees.aseprite".</summary>
        static string RelativePath(string fullPath) =>
            fullPath.Substring(ProjectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');

        static string Hash(byte[] bytes)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var text = new StringBuilder(40);
                foreach (var b in hash) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }

        static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            using (var input = entry.Open())
            using (var memory = new MemoryStream())
            {
                input.CopyTo(memory);
                return memory.ToArray();
            }
        }

        static void AddFile(ZipArchive zip, string file)
        {
            var entry = zip.CreateEntry(RelativePath(file), System.IO.Compression.CompressionLevel.Optimal);
            entry.LastWriteTime = File.GetLastWriteTime(file);
            using (var input = File.OpenRead(file))
            using (var output = entry.Open())
                input.CopyTo(output);
        }

        static void AppendList(StringBuilder report, List<string> lines)
        {
            foreach (var line in lines.Take(ListedFiles)) report.AppendLine("    " + line);
            if (lines.Count <= ListedFiles) return;
            report.AppendLine($"    and {lines.Count - ListedFiles} more (all of them are in the Console)");
            Debug.Log("Bellwort Burrow:\n  " + string.Join("\n  ", lines));
        }
    }
}
