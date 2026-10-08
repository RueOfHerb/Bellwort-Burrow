using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// The art lives outside git, in a zip of Assets/Art (with its .meta files, so scenes keep their links).
    /// - Export: zips Assets/Art into ArtPackage/ next to the Assets folder, ready to upload.
    /// - Install: unzips a downloaded package into the project. Only files under Assets/Art are written.
    /// </summary>
    public static class ArtPackage
    {
        const string OutputFolder = "ArtPackage";

        static string ProjectRoot => Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));

        [MenuItem("Bellwort Burrow/Art Package/Export Art Package", false, 300)]
        public static void Export()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            AssetDatabase.SaveAssets();

            string root = ProjectRoot;
            string artFolder = Path.Combine(root, "Assets", "Art");
            string outputFolder = Path.Combine(root, OutputFolder);
            Directory.CreateDirectory(outputFolder);
            string zipPath = Path.Combine(outputFolder, $"BellwortBurrow-Art-{DateTime.Now:yyyy-MM-dd}.zip");
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

            Debug.Log($"Bellwort Burrow: packed {files} art files into {zipPath}. Upload it where the team downloads the art.");
            EditorUtility.RevealInFinder(zipPath);
        }

        [MenuItem("Bellwort Burrow/Art Package/Install Art Package...", false, 301)]
        public static void Install()
        {
            string zipPath = EditorUtility.OpenFilePanel("Choose the Bellwort Burrow art package", "", "zip");
            if (string.IsNullOrEmpty(zipPath)) return;

            string root = ProjectRoot;
            string artFolder = Path.Combine(root, "Assets", "Art");
            int written = 0, skipped = 0;
            using (var stream = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // a folder
                    string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    bool inArt = target.StartsWith(artFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(target, artFolder + ".meta", StringComparison.OrdinalIgnoreCase);
                    if (!inArt) { skipped++; continue; }

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = entry.Open())
                    using (var output = File.Create(target))
                        input.CopyTo(output);
                    written++;
                }
            }

            AssetDatabase.Refresh();
            if (written > 0) ArtLibrary.UseSheetImporter();
            if (written == 0)
                Debug.LogError($"Bellwort Burrow: {Path.GetFileName(zipPath)} has nothing under Assets/Art. Is it the art package?");
            else
                Debug.Log($"Bellwort Burrow: installed {written} art files" + (skipped > 0 ? $" (skipped {skipped} outside Assets/Art)." : "."));
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
