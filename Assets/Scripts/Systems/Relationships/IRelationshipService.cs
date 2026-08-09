using System;

namespace BellwortBurrow.Systems.Relationships
{
    public interface IRelationshipService
    {
        int GetFriendship(string npcId);
        void AddFriendship(string npcId, int amount);

        event Action<string, int> OnFriendshipChanged;
    }
}
