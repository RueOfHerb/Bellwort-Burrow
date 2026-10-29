using System;
using System.Collections.Generic;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.Systems.Professions.Botany
{
    /// <summary>
    /// The farm's ground, cell by cell (sys-2, sys-10): which cells are tilled, which are watered today, and the crop
    /// growing in each. Tilled ground takes a crop; watered ground makes it grow a stage overnight, and every cell dries
    /// out at the day change, so a crop needs watering each night. Harvesting a ripe crop gives its item and the
    /// profession's XP and leaves the ground tilled. The scene only draws this (FarmField); this is what's saved, by
    /// crop id.
    /// </summary>
    public class BotanyPlotService : ISaveable
    {
        class Plot
        {
            public bool Watered;
            public BotanyCropDefinition Crop;
            public int DaysGrown;
        }

        [Serializable]
        class PlotState
        {
            public int x, y;
            public bool watered;
            public string crop;
            public int daysGrown;
        }

        [Serializable]
        class PlotsState
        {
            public List<PlotState> plots = new();
        }

        readonly IProfessionService professionService;
        readonly Func<string, BotanyCropDefinition> findCrop;
        readonly Dictionary<Vector3Int, Plot> plots = new();

        /// <summary>Something changed on the ground: a cell tilled, planted, watered or harvested, a night passed, a load.</summary>
        public event Action OnPlotsChanged;

        /// <param name="findCrop">Finds a crop by id when a save is loaded (the Crop Catalog's Find).</param>
        public BotanyPlotService(IProfessionService professionService, ITimeService timeService,
            Func<string, BotanyCropDefinition> findCrop = null)
        {
            this.professionService = professionService;
            this.findCrop = findCrop;
            timeService.OnDayChanged += HandleDayChanged;
        }

        public string SaveKey => "Botany";

        /// <summary>Every tilled cell.</summary>
        public IEnumerable<Vector3Int> TilledCells => plots.Keys;

        public bool IsTilled(Vector3Int cell) => plots.ContainsKey(cell);

        public bool IsWatered(Vector3Int cell) => plots.TryGetValue(cell, out var plot) && plot.Watered;

        public bool HasCrop(Vector3Int cell) => plots.TryGetValue(cell, out var plot) && plot.Crop != null;

        public BotanyCropDefinition CropAt(Vector3Int cell) => plots.TryGetValue(cell, out var plot) ? plot.Crop : null;

        /// <summary>The crop's growth stage at a cell (0 when just planted), or -1 when nothing grows there.</summary>
        public int StageAt(Vector3Int cell) =>
            plots.TryGetValue(cell, out var plot) && plot.Crop != null ? plot.Crop.StageAfter(plot.DaysGrown) : -1;

        /// <summary>Tills a cell. Whether the ground there can be tilled is the scene's to say (FarmField).</summary>
        public bool TryTill(Vector3Int cell)
        {
            if (plots.ContainsKey(cell)) return false;
            plots[cell] = new Plot();
            OnPlotsChanged?.Invoke();
            return true;
        }

        /// <summary>Plants a crop on tilled ground with nothing growing. Using up the seed is the caller's to do.</summary>
        public bool TryPlant(Vector3Int cell, BotanyCropDefinition crop)
        {
            if (crop == null || !plots.TryGetValue(cell, out var plot) || plot.Crop != null) return false;
            plot.Crop = crop;
            plot.DaysGrown = 0;
            OnPlotsChanged?.Invoke();
            return true;
        }

        /// <summary>Waters tilled ground that isn't watered yet today.</summary>
        public bool TryWater(Vector3Int cell)
        {
            if (!plots.TryGetValue(cell, out var plot) || plot.Watered) return false;
            plot.Watered = true;
            OnPlotsChanged?.Invoke();
            return true;
        }

        public bool IsMature(Vector3Int cell)
        {
            return plots.TryGetValue(cell, out var plot) && plot.Crop != null && plot.DaysGrown >= plot.Crop.TotalDaysToMature;
        }

        /// <summary>Harvests a ripe crop: its item and its XP; the ground stays tilled.</summary>
        public bool TryHarvest(Vector3Int cell, out ItemDefinition yield)
        {
            yield = null;
            if (!IsMature(cell)) return false;

            var plot = plots[cell];
            yield = plot.Crop.HarvestItem;
            professionService.AddXp(plot.Crop.Profession, plot.Crop.HarvestXp);
            plot.Crop = null;
            plot.DaysGrown = 0;
            OnPlotsChanged?.Invoke();
            return true;
        }

        // Overnight: what was watered grows a day, then everything dries out.
        void HandleDayChanged()
        {
            foreach (var plot in plots.Values)
            {
                if (plot.Watered && plot.Crop != null && plot.DaysGrown < plot.Crop.TotalDaysToMature)
                    plot.DaysGrown++;
                plot.Watered = false;
            }
            OnPlotsChanged?.Invoke();
        }

        // Saves crop ids, never the Crop Definitions themselves (sys-3).
        public string CaptureState()
        {
            var state = new PlotsState();
            foreach (var kv in plots)
            {
                state.plots.Add(new PlotState
                {
                    x = kv.Key.x, y = kv.Key.y, watered = kv.Value.Watered,
                    crop = kv.Value.Crop != null ? kv.Value.Crop.Id : "", daysGrown = kv.Value.DaysGrown,
                });
            }
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            plots.Clear();
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PlotsState>(json);
            foreach (var saved in state?.plots ?? new List<PlotState>())
            {
                var plot = new Plot { Watered = saved.watered, DaysGrown = Mathf.Max(0, saved.daysGrown) };
                if (!string.IsNullOrEmpty(saved.crop))
                {
                    plot.Crop = findCrop?.Invoke(saved.crop);
                    if (plot.Crop == null)
                    {
                        Debug.LogWarning($"[Botany] No crop '{saved.crop}' in the Crop Catalog, so the crop at ({saved.x}, {saved.y}) was left out of the loaded save; the ground stays tilled.");
                        plot.DaysGrown = 0;
                    }
                }
                plots[new Vector3Int(saved.x, saved.y, 0)] = plot;
            }
            OnPlotsChanged?.Invoke();
        }
    }
}
