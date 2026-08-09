using System;
using UnityEngine;

namespace BellwortBurrow.Data
{
    [Serializable]
    public struct ScheduleEntry
    {
        [Tooltip("Minutes since midnight.")]
        public int timeOfDayMinutes;
        public string locationId;
        public string animationTag;
    }

    [CreateAssetMenu(menuName = "Bellwort Burrow/NPCs/Schedule Definition", fileName = "NewSchedule")]
    public class ScheduleDefinition : ScriptableObject
    {
        [SerializeField] ScheduleEntry[] entries = Array.Empty<ScheduleEntry>();

        public ScheduleEntry[] Entries => entries;

        public ScheduleEntry? GetEntryForTime(int minutesSinceMidnight)
        {
            ScheduleEntry? current = null;
            foreach (var entry in entries)
            {
                if (entry.timeOfDayMinutes <= minutesSinceMidnight &&
                    (current == null || entry.timeOfDayMinutes > current.Value.timeOfDayMinutes))
                {
                    current = entry;
                }
            }
            return current;
        }

        internal void ConfigureForTesting(ScheduleEntry[] entries) => this.entries = entries;
    }
}
