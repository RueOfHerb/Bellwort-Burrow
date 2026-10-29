using UnityEngine;
using BellwortBurrow.Core;

namespace BellwortBurrow.Data.Professions.Botany
{
    /// <summary>
    /// A crop (sys-2, sys-10): the seed that plants it, what it yields, and how it grows. It shows one picture per
    /// growth stage; it starts on the first the day it's planted, moves on one stage every Days Per Stage watered
    /// nights, and is ripe on the last. Saves keep its id, so never change an id once saves exist.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Professions/Botany/Crop Definition", fileName = "NewCrop")]
    public class BotanyCropDefinition : ScriptableObject
    {
        [Tooltip("The crop's id in save files, like bellwort. Never change it once saves exist.")]
        [SerializeField] string id;
        [SerializeField] ProfessionDefinition profession;
        [SerializeField] ItemDefinition seedItem;
        [SerializeField] ItemDefinition harvestItem;
        [Tooltip("How many pictures it grows through, the ripe one included (the bellwort: sprout, growing, ripe).")]
        [SerializeField, Min(1)] int growthStageCount = 3;
        [Tooltip("How many watered nights each stage takes.")]
        [SerializeField, Min(1)] int daysPerStage = 1;
        [SerializeField] Season[] growableSeasons = { Season.Spring };
        [SerializeField, Min(0)] int harvestXp = 5;

        public string Id => id;
        public ProfessionDefinition Profession => profession;
        public ItemDefinition SeedItem => seedItem;
        public ItemDefinition HarvestItem => harvestItem;
        public int GrowthStageCount => growthStageCount;
        public int DaysPerStage => daysPerStage;
        /// <summary>Watered nights from planting until it's ripe: it starts on the first stage and ripens on the last.</summary>
        public int TotalDaysToMature => (growthStageCount - 1) * daysPerStage;
        public int HarvestXp => harvestXp;

        /// <summary>The growth stage, from 0 (just planted) to the last (ripe), after this many watered nights.</summary>
        public int StageAfter(int daysGrown) => Mathf.Clamp(daysGrown / daysPerStage, 0, growthStageCount - 1);

        public bool IsGrowableIn(Season season)
        {
            foreach (var s in growableSeasons)
            {
                if (s == season) return true;
            }
            return false;
        }

        internal void ConfigureForTesting(ProfessionDefinition profession, ItemDefinition seedItem, ItemDefinition harvestItem,
            int growthStageCount, int daysPerStage, int harvestXp, string id = "test_crop")
        {
            this.id = id;
            this.profession = profession;
            this.seedItem = seedItem;
            this.harvestItem = harvestItem;
            this.growthStageCount = growthStageCount;
            this.daysPerStage = daysPerStage;
            this.harvestXp = harvestXp;
        }
    }
}
