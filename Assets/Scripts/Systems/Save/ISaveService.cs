using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Save
{
    public interface ISaveService
    {
        void Register(ISaveable saveable);

        /// <summary>Keeps a snapshot of every registered system in memory.</summary>
        void SaveAll();

        /// <summary>Puts every registered system back to the last in-memory snapshot.</summary>
        void LoadAll();

        /// <summary>A save file of every registered system, under this header.</summary>
        SaveFile Capture(SaveHeader header);

        /// <summary>Puts every registered system back from a save file. False, and nothing changes, if the file is newer than the game.</summary>
        bool Restore(SaveFile file);
    }
}
