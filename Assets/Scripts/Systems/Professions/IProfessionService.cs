using System;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Professions
{
    public readonly struct ProfessionState
    {
        public readonly int Level;
        public readonly int CurrentXp;

        public ProfessionState(int level, int currentXp)
        {
            Level = level;
            CurrentXp = currentXp;
        }
    }

    public interface IProfessionService
    {
        ProfessionState GetState(ProfessionDefinition profession);
        void AddXp(ProfessionDefinition profession, int amount);

        event Action<ProfessionDefinition, int> OnProfessionLeveledUp;
    }
}
