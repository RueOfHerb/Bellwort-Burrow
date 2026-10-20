using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// Every critter kind the player can be, in the order the character creator shows them (sys-6).
    /// Every player file needs a kind here; an editor test checks.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Characters/Critter Catalog", fileName = "CritterCatalog")]
    public class CritterCatalog : ScriptableObject
    {
        [SerializeField] List<CritterKind> kinds = new();
        [Tooltip("Assets/Art/Palettes/characters.gpl, the ramps the color rows choose from.")]
        [SerializeField] RampPalette palette;
        [Tooltip("The lit sprite material that swaps ramps (Sprite Lit Ramp Swap). The Player wears it once dressed.")]
        [SerializeField] Material recolorMaterial;

        public IReadOnlyList<CritterKind> Kinds => kinds;
        public RampPalette Palette => palette;
        public Material RecolorMaterial => recolorMaterial;

        public CritterKind Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var kind in kinds)
            {
                if (kind != null && kind.Id == id) return kind;
            }
            return null;
        }

        internal void ConfigureForTesting(IEnumerable<CritterKind> kinds, RampPalette palette = null, Material recolorMaterial = null)
        {
            this.kinds = new List<CritterKind>(kinds);
            this.palette = palette;
            this.recolorMaterial = recolorMaterial;
        }
    }
}
