using System;
using System.Collections.Generic;
using UnityEngine;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Relationships
{
    public class RelationshipService : IRelationshipService, ISaveable
    {
        [Serializable]
        class Friendship
        {
            public string npc;
            public int points;
        }

        [Serializable]
        class RelationshipsState
        {
            public List<Friendship> friendships = new();
        }

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

        public string CaptureState()
        {
            var state = new RelationshipsState();
            foreach (var pair in friendshipByNpcId)
                state.friendships.Add(new Friendship { npc = pair.Key, points = pair.Value });
            state.friendships.Sort((a, b) => string.CompareOrdinal(a.npc, b.npc));
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            friendshipByNpcId.Clear();
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RelationshipsState>(json);
            foreach (var friendship in state?.friendships ?? new List<Friendship>())
            {
                if (!string.IsNullOrEmpty(friendship.npc))
                    friendshipByNpcId[friendship.npc] = friendship.points;
            }
        }
    }
}
