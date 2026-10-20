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

        public IReadOnlyList<CritterKind> Kinds => kinds;

        public CritterKind Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var kind in kinds)
            {
                if (kind != null && kind.Id == id) return kind;
            }
            return null;
        }

        internal void ConfigureForTesting(IEnumerable<CritterKind> kinds) => this.kinds = new List<CritterKind>(kinds);
    }
}
