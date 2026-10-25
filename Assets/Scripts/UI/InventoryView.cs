using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using BellwortBurrow.Core;
using BellwortBurrow.Systems.Inventory;

namespace BellwortBurrow.UI
{
    /// <summary>
    /// The HUD's inventory row (ui-3): the first slots of the inventory along the bottom of the screen, each with the
    /// item's icon (or its first letters while it has no icon) and a count. It shares the Game Root's Panel Renderer
    /// with HudPresenter, which shows and hides the whole HUD. It follows whichever inventory service is registered,
    /// re-subscribing when a new game or a loaded save swaps the services.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class InventoryView : MonoBehaviour
    {
        const string FilledClass = "hud__slot--filled";

        struct SlotView
        {
            public VisualElement Slot;
            public Image Icon;
            public Label Short;
            public Label Count;
        }

        readonly List<SlotView> slots = new();
        PanelRenderer panel;
        IInventoryService bound;
        bool dirty;

        void OnEnable()
        {
            panel = GetComponent<PanelRenderer>();
            panel.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (panel != null) panel.UnregisterUIReloadCallback(OnUIReload);
            Unbind();
            slots.Clear();
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root)
        {
            slots.Clear();
            for (int i = 0; ; i++)
            {
                var slot = root.Q<VisualElement>($"slot-{i}");
                if (slot == null) break;
                slots.Add(new SlotView
                {
                    Slot = slot,
                    Icon = slot.Q<Image>("icon"),
                    Short = slot.Q<Label>("short"),
                    Count = slot.Q<Label>("count")
                });
            }
            dirty = true;
        }

        void Update()
        {
            ServiceLocator.TryGet<IInventoryService>(out var current);
            if (current != bound)
            {
                Unbind();
                bound = current;
                if (bound != null) bound.OnInventoryChanged += MarkDirty;
                dirty = true;
            }
            if (dirty) Refresh();
        }

        void Unbind()
        {
            if (bound != null) bound.OnInventoryChanged -= MarkDirty;
            bound = null;
        }

        void MarkDirty() => dirty = true;

        void Refresh()
        {
            dirty = false;
            var items = bound?.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var view = slots[i];
                bool filled = items != null && i < items.Count && items[i].Item != null && items[i].Quantity > 0;
                view.Slot.EnableInClassList(FilledClass, filled);
                if (!filled)
                {
                    if (view.Icon != null) view.Icon.sprite = null;
                    if (view.Short != null) view.Short.text = "";
                    if (view.Count != null) view.Count.text = "";
                    continue;
                }

                var slot = items[i];
                var icon = slot.Item.Icon;
                if (view.Icon != null) view.Icon.sprite = icon;
                if (view.Short != null) view.Short.text = icon != null ? "" : ShortName(slot.Item.DisplayName);
                if (view.Count != null) view.Count.text = slot.Quantity > 1 ? slot.Quantity.ToString() : "";
            }
        }

        /// <summary>The first two letters of a name, for an item with no icon yet.</summary>
        internal static string ShortName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            name = name.Trim();
            return name.Length <= 2 ? name : name.Substring(0, 2);
        }
    }
}
