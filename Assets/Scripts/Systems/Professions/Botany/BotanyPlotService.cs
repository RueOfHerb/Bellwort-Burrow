using System.Collections.Generic;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.Systems.Professions.Botany
{
    public class BotanyPlotService
    {
        class PlantedCrop
        {
            public BotanyCropDefinition Definition;
            public int DaysGrown;
        }

        readonly IProfessionService professionService;
        readonly Dictionary<Vector3Int, PlantedCrop> plots = new();

        public BotanyPlotService(IProfessionService professionService, ITimeService timeService)
        {
            this.professionService = professionService;
            timeService.OnDayChanged += HandleDayChanged;
        }

        public bool TryPlant(Vector3Int cell, BotanyCropDefinition crop)
        {
            if (plots.ContainsKey(cell)) return false;

            plots[cell] = new PlantedCrop { Definition = crop, DaysGrown = 0 };
            return true;
        }

        public bool IsMature(Vector3Int cell)
        {
            return plots.TryGetValue(cell, out var crop) && crop.DaysGrown >= crop.Definition.TotalDaysToMature;
        }

        public bool TryHarvest(Vector3Int cell, out ItemDefinition yield)
        {
            yield = null;
            if (!plots.TryGetValue(cell, out var crop) || !IsMature(cell)) return false;

            yield = crop.Definition.HarvestItem;
            professionService.AddXp(crop.Definition.Profession, crop.Definition.HarvestXp);
            plots.Remove(cell);
            return true;
        }

        void HandleDayChanged()
        {
            foreach (var crop in plots.Values)
            {
                if (crop.DaysGrown < crop.Definition.TotalDaysToMature)
                    crop.DaysGrown++;
            }
        }
    }
}
