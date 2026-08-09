using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Npcs
{
    public interface INpcScheduleService
    {
        void Register(NpcDefinition npc);
        string GetCurrentLocationId(NpcDefinition npc, int minutesSinceMidnight);
    }
}
