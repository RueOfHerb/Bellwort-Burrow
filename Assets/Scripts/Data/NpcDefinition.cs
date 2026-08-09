using UnityEngine;

namespace BellwortBurrow.Data
{
    [CreateAssetMenu(menuName = "Bellwort Burrow/NPCs/NPC Definition", fileName = "NewNpc")]
    public class NpcDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Sprite portrait;
        [SerializeField] string homeLocationId;
        [SerializeField] ScheduleDefinition schedule;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public string HomeLocationId => homeLocationId;
        public ScheduleDefinition Schedule => schedule;
    }
}
