using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Save
{
    public interface ISaveService
    {
        void Register(ISaveable saveable);
        void SaveAll();
        void LoadAll();
    }
}
