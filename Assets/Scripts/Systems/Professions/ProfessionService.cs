using System;
using System.Collections.Generic;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Professions
{
    /// <summary>
    /// Each profession's level and XP (sys-2). Kept by the profession's id, so a save holds ids and needs no catalog to
    /// load (sys-10); the definition only supplies the XP curve when XP is added.
    /// </summary>
    public class ProfessionService : IProfessionService, ISaveable
    {
        [Serializable]
        class Entry
        {
            public string profession;
            public int level = 1;
            public int xp;
        }

        [Serializable]
        class ProfessionsState
        {
            public List<Entry> professions = new();
        }

        readonly Dictionary<string, ProfessionState> states = new();

        public event Action<ProfessionDefinition, int> OnProfessionLeveledUp;

        public string SaveKey => "Professions";

        static string KeyOf(ProfessionDefinition profession) =>
            profession == null ? "" : !string.IsNullOrEmpty(profession.Id) ? profession.Id : profession.name;

        public ProfessionState GetState(ProfessionDefinition profession)
        {
            return states.TryGetValue(KeyOf(profession), out var state) ? state : new ProfessionState(1, 0);
        }

        public void AddXp(ProfessionDefinition profession, int amount)
        {
            if (profession == null || amount <= 0) return;

            var state = GetState(profession);
            int newXp = state.CurrentXp + amount;
            int newLevel = state.Level;

            while (newXp >= profession.XpRequiredForLevel(newLevel))
            {
                newXp -= profession.XpRequiredForLevel(newLevel);
                newLevel++;
            }

            states[KeyOf(profession)] = new ProfessionState(newLevel, newXp);

            if (newLevel != state.Level)
                OnProfessionLeveledUp?.Invoke(profession, newLevel);
        }

        public string CaptureState()
        {
            var state = new ProfessionsState();
            foreach (var kv in states)
                state.professions.Add(new Entry { profession = kv.Key, level = kv.Value.Level, xp = kv.Value.CurrentXp });
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            states.Clear();
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ProfessionsState>(json);
            foreach (var entry in state?.professions ?? new List<Entry>())
            {
                if (string.IsNullOrEmpty(entry.profession)) continue;
                states[entry.profession] = new ProfessionState(Mathf.Max(1, entry.level), Mathf.Max(0, entry.xp));
            }
        }
    }
}
