using UnityEngine;
using BellwortBurrow.Core;

namespace BellwortBurrow.Data.Professions.Botany
{
    [CreateAssetMenu(menuName = "Bellwort Burrow/Professions/Botany/Crop Definition", fileName = "NewCrop")]
    public class BotanyCropDefinition : ScriptableObject
    {
        [SerializeField] ProfessionDefinition profession;
        [SerializeField] ItemDefinition seedItem;
        [SerializeField] ItemDefinition harvestItem;
        [SerializeField, Min(1)] int growthStageCount = 4;
        [SerializeField, Min(1)] int daysPerStage = 1;
        [SerializeField] Season[] growableSeasons = { Season.Spring };
        [SerializeField, Min(0)] int harvestXp = 5;

        public ProfessionDefinition Profession => profession;
        public ItemDefinition SeedItem => seedItem;
        public ItemDefinition HarvestItem => harvestItem;
        public int GrowthStageCount => growthStageCount;
        public int DaysPerStage => daysPerStage;
        public int TotalDaysToMature => growthStageCount * daysPerStage;
        public int HarvestXp => harvestXp;

        public bool IsGrowableIn(Season season)
        {
            foreach (var s in growableSeasons)
            {
                if (s == season) return true;
            }
            return false;
        }

        internal void ConfigureForTesting(ProfessionDefinition profession, ItemDefinition seedItem, ItemDefinition harvestItem,
            int growthStageCount, int daysPerStage, int harvestXp)
        {
            this.profession = profession;
            this.seedItem = seedItem;
            this.harvestItem = harvestItem;
            this.growthStageCount = growthStageCount;
            this.daysPerStage = daysPerStage;
            this.harvestXp = harvestXp;
        }
    }
}
