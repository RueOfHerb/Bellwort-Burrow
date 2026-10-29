using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Calendar;
using BellwortBurrow.Systems.Inventory;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A piece of ground clutter the player can pick (sys-8): bellworts, tall grass, a mossy rock. Interacting puts its
    /// item in the inventory, and its bonus item if it has one (bellworts give a seed too, sys-10), and hides the piece; it comes back after its respawn days, counted by the clock's day
    /// changes. The prefab maker adds this to the pieces the Forage Table lists. What's been picked isn't saved yet,
    /// so everything is back after a load.
    /// </summary>
    public class Forageable : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDefinition item;
        [SerializeField, Min(1)] int amount = 1;
        [Tooltip("How many day changes until it can be picked again.")]
        [SerializeField, Min(1)] int respawnDays = 1;
        [Tooltip("Something more it gives along with its item, like a Bellwort seed with the flower. Optional.")]
        [SerializeField] ItemDefinition bonusItem;
        [SerializeField, Min(0)] int bonusAmount = 1;

        bool taken;
        int daysLeft;
        int seenDay = int.MinValue;

        public ItemDefinition Item => item;
        public int Amount => amount;
        public bool Taken => taken;
        public Vector2 Position => transform.position;

        public ItemDefinition BonusItem => bonusItem;

        public void Configure(ItemDefinition item, int amount = 1, int respawnDays = 1, ItemDefinition bonusItem = null, int bonusAmount = 1)
        {
            this.item = item;
            this.amount = Mathf.Max(1, amount);
            this.respawnDays = Mathf.Max(1, respawnDays);
            this.bonusItem = bonusItem;
            this.bonusAmount = Mathf.Max(0, bonusAmount);
        }

        void OnEnable() => Interactables.Add(this);

        void OnDisable() => Interactables.Remove(this);

        public bool CanInteract(GameObject who) => !taken && item != null;

        public void Interact(GameObject who)
        {
            if (!CanInteract(who)) return;
            if (!ServiceLocator.TryGet<IInventoryService>(out var inventory) || !inventory.TryAdd(item, amount)) return;
            if (bonusItem != null && bonusAmount > 0) inventory.TryAdd(bonusItem, bonusAmount);

            taken = true;
            daysLeft = respawnDays;
            seenDay = ServiceLocator.TryGet<ITimeService>(out var time) ? time.CurrentDay : seenDay;
            Show(false);
        }

        // Taken pieces watch the clock's day rather than subscribing, since a new game or a load swaps the services.
        void Update()
        {
            if (!taken || !ServiceLocator.TryGet<ITimeService>(out var time)) return;
            if (seenDay == int.MinValue) seenDay = time.CurrentDay;
            while (time.CurrentDay > seenDay && taken)
            {
                seenDay++;
                DayPassed();
            }
            seenDay = time.CurrentDay;
        }

        /// <summary>One day has gone by for a taken piece; after its respawn days it's back.</summary>
        internal void DayPassed()
        {
            if (!taken) return;
            daysLeft--;
            if (daysLeft > 0) return;
            taken = false;
            Show(true);
        }

        void Show(bool shown)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = shown;
            foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = shown;
            foreach (var light in GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true)) light.enabled = shown;
        }
    }
}
