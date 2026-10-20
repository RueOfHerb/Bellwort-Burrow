using System;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Profile
{
    /// <summary>Who the player is, kept for the whole game (sys-6). Player Appearance dresses each scene's Player from it.</summary>
    public interface IPlayerProfileService
    {
        /// <summary>False until a game starts or loads. Pressing Play right in a zone has no profile.</summary>
        bool HasProfile { get; }

        /// <summary>A copy of the profile, or null when there's none.</summary>
        PlayerProfile Profile { get; }

        void Set(PlayerProfile profile);

        /// <summary>Raised with a copy of the new profile whenever it's set or loaded.</summary>
        event Action<PlayerProfile> Changed;
    }
}
