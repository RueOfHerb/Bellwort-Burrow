using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.Core;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.Professions.Botany;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// The Faerie Field's farm (sys-10): the Interact action on the ground in front of the player, when nothing else is
    /// in reach. On the tillable dirt it tills; on tilled ground it plants a seed from the inventory, or waters when
    /// there's no seed or something already grows; on a ripe crop it harvests. What's tilled, watered and growing lives in
    /// the Botany plots (saved); this draws it on the scene's own Tilemaps, which hold nothing else:
    /// Farmland (the tilled ground, two Orders in Layer above the tillable dirt so it covers the glints), Watered over it,
    /// and Crops over that.
    /// </summary>
    public class FarmField : MonoBehaviour
    {
        [Serializable]
        public class CropLook
        {
            public BotanyCropDefinition crop;
            [Tooltip("One tile per growth stage, first to ripe (the bellwort: bellwort_crop_0 to _2).")]
            public TileBase[] stages = new TileBase[0];
        }

        [Tooltip("Where the tillable dirt is painted.")]
        [SerializeField] Tilemap tillable;
        [Tooltip("The tile that can be tilled (Faerie Field Tillable Dirt). Empty: any tile on the Tillable Tilemap.")]
        [SerializeField] TileBase tillableTile;
        [SerializeField] Tilemap farmland;
        [Tooltip("Tilled ground (Faerie Field Farmland).")]
        [SerializeField] TileBase tilledTile;
        [SerializeField] Tilemap watered;
        [Tooltip("Watered ground (Faerie Field Watered Farmland).")]
        [SerializeField] TileBase wateredTile;
        [SerializeField] Tilemap crops;
        [Tooltip("The crops that grow here and how each looks; planting uses the first whose seed is in the inventory.")]
        [SerializeField] List<CropLook> cropLooks = new();

        static readonly List<FarmField> active = new();

        BotanyPlotService plots;
        bool dirty = true;

        /// <summary>The farms in the loaded scenes (one, in the Faerie Field).</summary>
        public static IReadOnlyList<FarmField> Active => active;

        /// <summary>What the last use did, for tests and messages: "till", "plant", "water", "harvest" or "".</summary>
        public string LastAction { get; private set; } = "";

        public void Configure(Tilemap tillable, TileBase tillableTile, Tilemap farmland, TileBase tilledTile,
            Tilemap watered, TileBase wateredTile, Tilemap crops, IEnumerable<CropLook> looks)
        {
            this.tillable = tillable;
            this.tillableTile = tillableTile;
            this.farmland = farmland;
            this.tilledTile = tilledTile;
            this.watered = watered;
            this.wateredTile = wateredTile;
            this.crops = crops;
            cropLooks = new List<CropLook>(looks);
            dirty = true;
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            dirty = true;
        }

        void OnDisable()
        {
            active.Remove(this);
            Watch(null);
        }

        /// <summary>Tries every farm in the loaded scenes at a point. True when one did something.</summary>
        public static bool TryUseAny(Vector2 at, GameObject who)
        {
            foreach (var farm in active)
                if (farm != null && farm.TryUse(at, who)) return true;
            return false;
        }

        /// <summary>The farm's cell under a point in the world.</summary>
        public Vector3Int CellAt(Vector2 at) => (farmland != null ? farmland : tillable).WorldToCell(at);

        /// <summary>Uses the ground at a point: harvest, plant, water or till, in that order. True when something happened.</summary>
        public bool TryUse(Vector2 at, GameObject who)
        {
            LastAction = "";
            if (tillable == null || farmland == null) return false;
            var service = Plots();
            if (service == null) return false;
            var cell = CellAt(at);

            if (service.IsMature(cell))
            {
                if (!ServiceLocator.TryGet<IInventoryService>(out var inventory)) return false;
                var crop = service.CropAt(cell);
                if (!inventory.TryAdd(crop.HarvestItem, 1)) return false;   // no room: leave it in the ground
                service.TryHarvest(cell, out _);
                return Did("harvest");
            }

            if (service.IsTilled(cell))
            {
                if (!service.HasCrop(cell) && TryPlantFromInventory(service, cell)) return Did("plant");
                if (service.TryWater(cell)) return Did("water");
                return false;
            }

            if (CanTill(cell) && service.TryTill(cell)) return Did("till");
            return false;
        }

        /// <summary>Whether the ground at a cell is tillable dirt that isn't tilled yet.</summary>
        public bool CanTill(Vector3Int cell)
        {
            var tile = tillable != null ? tillable.GetTile(cell) : null;
            if (tile == null) return false;
            return tillableTile == null || tile == tillableTile;
        }

        bool TryPlantFromInventory(BotanyPlotService service, Vector3Int cell)
        {
            if (!ServiceLocator.TryGet<IInventoryService>(out var inventory)) return false;
            foreach (var look in cropLooks)
            {
                var crop = look?.crop;
                if (crop == null || crop.SeedItem == null || !HasItem(inventory, crop.SeedItem)) continue;
                if (!service.TryPlant(cell, crop)) return false;
                inventory.TryRemove(crop.SeedItem, 1);
                return true;
            }
            return false;
        }

        static bool HasItem(IInventoryService inventory, Data.ItemDefinition item)
        {
            foreach (var slot in inventory.Slots)
                if (slot.Item == item && slot.Quantity > 0) return true;
            return false;
        }

        bool Did(string action)
        {
            LastAction = action;
            Redraw();
            return true;
        }

        // A new game or a load swaps the services, so look for the current plots each frame and follow them.
        void Update()
        {
            Plots();
            if (dirty) Redraw();
        }

        BotanyPlotService Plots()
        {
            var current = ServiceLocator.TryGet<BotanyPlotService>(out var service) ? service : null;
            if (current != plots) Watch(current);
            return plots;
        }

        void Watch(BotanyPlotService service)
        {
            if (plots != null) plots.OnPlotsChanged -= MarkDirty;
            plots = service;
            if (plots != null) plots.OnPlotsChanged += MarkDirty;
            dirty = true;
        }

        void MarkDirty() => dirty = true;

        /// <summary>Draws the plots: tilled ground, watered ground and each crop's stage.</summary>
        public void Redraw()
        {
            dirty = false;
            if (farmland != null) farmland.ClearAllTiles();
            if (watered != null) watered.ClearAllTiles();
            if (crops != null) crops.ClearAllTiles();
            if (plots == null) return;
            foreach (var cell in plots.TilledCells)
            {
                if (farmland != null) farmland.SetTile(cell, tilledTile);
                if (watered != null && plots.IsWatered(cell)) watered.SetTile(cell, wateredTile);
                if (crops == null) continue;
                var stage = plots.StageAt(cell);
                var look = LookFor(plots.CropAt(cell));
                if (stage >= 0 && look != null && look.stages.Length > 0)
                    crops.SetTile(cell, look.stages[Mathf.Min(stage, look.stages.Length - 1)]);
            }
        }

        CropLook LookFor(BotanyCropDefinition crop)
        {
            if (crop == null) return null;
            foreach (var look in cropLooks)
                if (look != null && look.crop == crop) return look;
            return null;
        }
    }
}
