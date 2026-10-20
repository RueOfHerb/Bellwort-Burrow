using System.Collections.Generic;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Save
{
    /// <summary>
    /// Gathers every ISaveable's section into a save file and hands them back (sys-3).
    /// Writing the file to disk is the Save Store's job; when to save is the Game Session's.
    /// </summary>
    public class SaveService : ISaveService
    {
        /// <summary>Bump this when the save format changes, and teach Restore to read the older one.</summary>
        public const int CurrentVersion = 1;

        readonly List<ISaveable> saveables = new();
        readonly Dictionary<string, string> lastSnapshot = new();

        public void Register(ISaveable saveable) => saveables.Add(saveable);

        public void SaveAll()
        {
            foreach (var saveable in saveables)
                lastSnapshot[saveable.SaveKey] = saveable.CaptureState();
        }

        public void LoadAll()
        {
            foreach (var saveable in saveables)
            {
                if (lastSnapshot.TryGetValue(saveable.SaveKey, out var state))
                    saveable.RestoreState(state);
            }
        }

        public SaveFile Capture(SaveHeader header)
        {
            var file = new SaveFile { header = header ?? new SaveHeader() };
            file.header.version = CurrentVersion;

            foreach (var saveable in saveables)
                file.sections.Add(new SaveSection { key = saveable.SaveKey, json = saveable.CaptureState() });

            return file;
        }

        public bool Restore(SaveFile file)
        {
            if (file?.header == null || file.header.version > CurrentVersion) return false;

            // A system with no section in the file (one added after the save was made) keeps its fresh state.
            foreach (var saveable in saveables)
            {
                string json = file.Find(saveable.SaveKey);
                if (json != null) saveable.RestoreState(json);
            }
            return true;
        }
    }
}
