using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Profile;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Dresses the Player from the player profile (sys-6): when the scene starts, and again whenever the profile changes.
    /// With no profile yet (pressing Play right in a zone) the Player stays as placed.
    /// Only the critter kind changes the picture for now; the colors, face and outfit come with the new character art.
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] CritterCatalog catalog;
        [Tooltip("The SpriteRenderer that shows the critter: the player file's prefab under the Player.")]
        [SerializeField] SpriteRenderer body;
        [Tooltip("Off for a preview that something else dresses, like the character creator's.")]
        [SerializeField] bool followProfile = true;

        IPlayerProfileService profiles;

        /// <summary>The kind showing now, or null if nothing has been applied yet.</summary>
        public CritterKind Kind { get; private set; }

        void Start()
        {
            if (!followProfile || !ServiceLocator.TryGet(out profiles)) return;

            profiles.Changed += Apply;
            if (profiles.HasProfile) Apply(profiles.Profile);
        }

        void OnDestroy()
        {
            if (profiles != null) profiles.Changed -= Apply;
        }

        /// <summary>Shows this profile. A kind the catalog doesn't know leaves the Player as it is.</summary>
        public void Apply(PlayerProfile profile)
        {
            if (profile == null || catalog == null || body == null) return;

            var kind = catalog.Find(profile.critterKind);
            if (kind == null || kind.Sprite == null) return;

            Kind = kind;
            body.sprite = kind.Sprite;
        }

        internal void ConfigureForTesting(CritterCatalog catalog, SpriteRenderer body)
        {
            this.catalog = catalog;
            this.body = body;
            followProfile = false;
        }
    }
}
