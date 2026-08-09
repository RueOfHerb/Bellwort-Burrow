using System.Collections.Generic;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Npcs
{
    public class NpcScheduleService : INpcScheduleService
    {
        readonly HashSet<NpcDefinition> registeredNpcs = new();

        public void Register(NpcDefinition npc) => registeredNpcs.Add(npc);

        public string GetCurrentLocationId(NpcDefinition npc, int minutesSinceMidnight)
        {
            var entry = npc.Schedule != null ? npc.Schedule.GetEntryForTime(minutesSinceMidnight) : null;
            return entry?.locationId ?? npc.HomeLocationId;
        }
    }
}
