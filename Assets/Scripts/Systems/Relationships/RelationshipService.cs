using System;
using System.Collections.Generic;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Relationships
{
    public class RelationshipService : IRelationshipService, ISaveable
    {
        readonly Dictionary<string, int> friendshipByNpcId = new();

        public string SaveKey => "Relationships";
        public event Action<string, int> OnFriendshipChanged;

        public int GetFriendship(string npcId) => friendshipByNpcId.TryGetValue(npcId, out var v) ? v : 0;

        public void AddFriendship(string npcId, int amount)
        {
            if (amount == 0) return;

            int updated = GetFriendship(npcId) + amount;
            friendshipByNpcId[npcId] = updated;
            OnFriendshipChanged?.Invoke(npcId, updated);
        }

        public object CaptureState() => new Dictionary<string, int>(friendshipByNpcId);

        public void RestoreState(object state)
        {
            friendshipByNpcId.Clear();
            if (state is Dictionary<string, int> saved)
            {
                foreach (var kvp in saved)
                    friendshipByNpcId[kvp.Key] = kvp.Value;
            }
        }
    }
}
