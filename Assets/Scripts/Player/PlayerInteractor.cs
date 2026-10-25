using UnityEngine;
using UnityEngine.InputSystem;
using BellwortBurrow.Core;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.Professions.Botany;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// The player's hands (sys-5, sys-8). The Interact action (E, or a gamepad button) uses the nearest interactable
    /// within reach of a point just in front of the player's feet: a forageable gives its item. With a PlayerInput on
    /// the same object it reads Interact itself; without one, wire OnInteract up to an input event.
    /// PlantAtCell and HarvestAtCell are the Botany stubs, for a farmland tile later.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] BotanyCropDefinition equippedCrop;
        [Tooltip("How far from the point in front of the feet an interactable may be, in units (16 px each).")]
        [SerializeField, Min(0.1f)] float reach = 1.25f;
        [Tooltip("How far in front of the feet the reach is centred, in the direction the player faces.")]
        [SerializeField, Min(0f)] float ahead = 0.5f;

        InputAction interact;
        PlayerController controller;

        public float Reach => reach;

        void Awake()
        {
            TryGetComponent(out controller);
            if (TryGetComponent<PlayerInput>(out var input) && input.actions != null)
                interact = input.actions.FindAction("Interact");
        }

        void Update()
        {
            if (interact != null && interact.WasPerformedThisFrame()) InteractNearby();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed) InteractNearby();
        }

        /// <summary>The point the reach is measured from: a little in front of the feet, the way the player faces.</summary>
        public Vector2 ReachPoint
        {
            get
            {
                Vector2 facing = controller != null ? controller.Facing : Vector2.down;
                return (Vector2)transform.position + facing * ahead;
            }
        }

        /// <summary>Uses the nearest interactable within reach. True when something was used.</summary>
        public bool InteractNearby()
        {
            var target = Interactables.Nearest(ReachPoint, reach, gameObject);
            if (target == null) return false;
            target.Interact(gameObject);
            return true;
        }

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
