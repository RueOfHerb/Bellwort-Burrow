using UnityEngine;

namespace BellwortBurrow.Data
{
    public enum ItemCategory
    {
        Miscellaneous,
        Seed,
        Crop,
        Tool,
        Gift
    }

    [CreateAssetMenu(menuName = "Bellwort Burrow/Items/Item Definition", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Sprite icon;
        [SerializeField] ItemCategory category = ItemCategory.Miscellaneous;
        [SerializeField, Min(1)] int maxStackSize = 99;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public int MaxStackSize => maxStackSize;
    }
}
