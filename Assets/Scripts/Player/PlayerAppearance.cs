using System.Collections.Generic;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Profile;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Dresses the Player from the player profile (sys-6): when the scene starts, and again whenever the profile changes.
    /// The critter kind picks the sprite. Fur, markings and eye colors recolor it in engine: the Player wears the
    /// catalog's ramp swap material, and the kind's own ramps are swapped shade for shade for the ones the profile picked
    /// (Natural keeps the kind's own). With no profile yet (pressing Play right in a zone) the Player stays as placed.
    /// Face and outfit come with the new character art.
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] CritterCatalog catalog;
        [Tooltip("The SpriteRenderer that shows the critter: the player file's prefab under the Player.")]
        [SerializeField] SpriteRenderer body;
        [Tooltip("Off for a preview that something else dresses, like the character creator's.")]
        [SerializeField] bool followProfile = true;

        static readonly int SwapFromId = Shader.PropertyToID("_SwapFrom");
        static readonly int SwapToId = Shader.PropertyToID("_SwapTo");
        static readonly int SwapCountId = Shader.PropertyToID("_SwapCount");

        readonly List<Color32> swapFrom = new();
        readonly List<Color32> swapTo = new();
        readonly Vector4[] fromVectors = new Vector4[RampSwap.MaxColors];
        readonly Vector4[] toVectors = new Vector4[RampSwap.MaxColors];
        MaterialPropertyBlock block;
        IPlayerProfileService profiles;

        /// <summary>The kind showing now, or null if nothing has been applied yet.</summary>
        public CritterKind Kind { get; private set; }

        /// <summary>How many colors the last Apply swapped (five per recolored part).</summary>
        public int SwappedColors { get; private set; }

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
            Recolor(kind, profile);
        }

        void Recolor(CritterKind kind, PlayerProfile profile)
        {
            var material = catalog.RecolorMaterial;
            if (material == null) return;
            if (body.sharedMaterial != material) body.sharedMaterial = material;

            SwappedColors = RampSwap.Build(kind, profile, catalog.Palette, swapFrom, swapTo);
            for (int i = 0; i < RampSwap.MaxColors; i++)
            {
                fromVectors[i] = i < SwappedColors ? ToVector(swapFrom[i]) : Vector4.zero;
                toVectors[i] = i < SwappedColors ? ToVector(swapTo[i]) : Vector4.zero;
            }

            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetVectorArray(SwapFromId, fromVectors);
            block.SetVectorArray(SwapToId, toVectors);
            block.SetFloat(SwapCountId, SwappedColors);
            body.SetPropertyBlock(block);
        }

        // sRGB, 0 to 1, as the shader compares them
        static Vector4 ToVector(Color32 color) => new(color.r / 255f, color.g / 255f, color.b / 255f, 1f);

        internal void ConfigureForTesting(CritterCatalog catalog, SpriteRenderer body)
        {
            this.catalog = catalog;
            this.body = body;
            followProfile = false;
        }
    }
}
