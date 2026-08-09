using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.Professions.Botany;

namespace BellwortBurrow.Player
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] BotanyCropDefinition equippedCrop;

        public void PlantAtCell(Vector3Int cell)
        {
            var plotService = ServiceLocator.Get<BotanyPlotService>();
            plotService.TryPlant(cell, equippedCrop);
        }

        public void HarvestAtCell(Vector3Int cell)
        {
            var plotService = ServiceLocator.Get<BotanyPlotService>();
            if (plotService.TryHarvest(cell, out var yield))
            {
                var inventory = ServiceLocator.Get<IInventoryService>();
                inventory.TryAdd(yield, 1);
            }
        }
    }
}
