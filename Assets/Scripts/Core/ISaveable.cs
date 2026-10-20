namespace BellwortBurrow.Core
{
    /// <summary>
    /// Anything that goes into a save. Each one writes its own section as JSON and reads it back,
    /// holding ids and plain values only, never asset references (sys-3).
    /// </summary>
    public interface ISaveable
    {
        /// <summary>The section's name in the save file. Never change it once saves exist.</summary>
        string SaveKey { get; }

        /// <summary>This system's state, as JSON.</summary>
        string CaptureState();

        /// <summary>Replaces this system's state with one that CaptureState wrote.</summary>
        void RestoreState(string json);
    }
}
