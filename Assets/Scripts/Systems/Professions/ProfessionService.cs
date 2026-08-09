using System;
using System.Collections.Generic;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Professions
{
    public class ProfessionService : IProfessionService
    {
        readonly Dictionary<ProfessionDefinition, ProfessionState> states = new();

        public event Action<ProfessionDefinition, int> OnProfessionLeveledUp;

        public ProfessionState GetState(ProfessionDefinition profession)
        {
            return states.TryGetValue(profession, out var state) ? state : new ProfessionState(1, 0);
        }

        public void AddXp(ProfessionDefinition profession, int amount)
        {
            if (amount <= 0) return;

            var state = GetState(profession);
            int newXp = state.CurrentXp + amount;
            int newLevel = state.Level;

            while (newXp >= profession.XpRequiredForLevel(newLevel))
            {
                newXp -= profession.XpRequiredForLevel(newLevel);
                newLevel++;
            }

            states[profession] = new ProfessionState(newLevel, newXp);

            if (newLevel != state.Level)
                OnProfessionLeveledUp?.Invoke(profession, newLevel);
        }
    }
}
