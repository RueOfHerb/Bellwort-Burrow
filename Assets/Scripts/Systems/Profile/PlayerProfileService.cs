using System;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Profile
{
    public class PlayerProfileService : IPlayerProfileService, ISaveable
    {
        PlayerProfile profile;

        public string SaveKey => "Profile";
        public bool HasProfile => profile != null;
        public PlayerProfile Profile => profile?.Clone();

        public event Action<PlayerProfile> Changed;

        public void Set(PlayerProfile newProfile)
        {
            profile = newProfile?.Clone();
            Changed?.Invoke(Profile);
        }

        public string CaptureState() => profile != null ? JsonUtility.ToJson(profile) : "";

        public void RestoreState(string json)
        {
            profile = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PlayerProfile>(json);
            Changed?.Invoke(Profile);
        }
    }
}
