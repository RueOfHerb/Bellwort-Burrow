using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data.Professions.Botany
{
    /// <summary>Every crop, so saves can store crop ids and find the crops again (sys-10), as the Item Catalog does for items.</summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Professions/Botany/Crop Catalog", fileName = "CropCatalog")]
    public class BotanyCropCatalog : ScriptableObject
    {
        [SerializeField] List<BotanyCropDefinition> crops = new();

        public IReadOnlyList<BotanyCropDefinition> Crops => crops;

        public BotanyCropDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var crop in crops)
            {
                if (crop != null && crop.Id == id) return crop;
            }
            return null;
        }

        /// <summary>The crop a seed plants, or null.</summary>
        public BotanyCropDefinition ForSeed(ItemDefinition seed)
        {
            if (seed == null) return null;
            foreach (var crop in crops)
            {
                if (crop != null && crop.SeedItem == seed) return crop;
            }
            return null;
        }

        internal void ConfigureForTesting(IEnumerable<BotanyCropDefinition> crops) => this.crops = new List<BotanyCropDefinition>(crops);
    }
}
