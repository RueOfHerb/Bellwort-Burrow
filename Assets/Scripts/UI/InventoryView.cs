using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Systems.Inventory;

namespace BellwortBurrow.UI
{
    public class InventoryView : MonoBehaviour
    {
        IInventoryService inventoryService;

        void Start()
        {
            inventoryService = ServiceLocator.Get<IInventoryService>();
            inventoryService.OnInventoryChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (inventoryService != null)
                inventoryService.OnInventoryChanged -= Refresh;
        }

        void Refresh()
        {
        }
    }
}
