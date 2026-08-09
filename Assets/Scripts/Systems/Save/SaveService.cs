using System.Collections.Generic;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Save
{
    public class SaveService : ISaveService
    {
        readonly List<ISaveable> saveables = new();
        readonly Dictionary<string, object> lastSnapshot = new();

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
    }
}
