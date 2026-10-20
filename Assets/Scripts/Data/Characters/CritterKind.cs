using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// One kind of Critterfolk the player can be, made from one player file in Assets/Art/Characters/Player (sys-6).
    /// List it in the Critter Catalog to show it in the character creator.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Characters/Critter Kind", fileName = "NewCritterKind")]
    public class CritterKind : ScriptableObject
    {
        [Tooltip("Saved in the player profile. Never change it once saves exist.")]
        [SerializeField] string id;
        [SerializeField] string displayName;
        [Tooltip("The player file's sprite, standing and facing front. Walk frames and the other views come later.")]
        [SerializeField] Sprite sprite;

        [Header("Recoloring (sys-6)")]
        [Tooltip("The color ramp in characters.gpl this kind's fur is drawn in, on the same steps as every coat: Shadow outline, Dark shading, Base main color, Light lit side. The Fur color row swaps it, and the face's brows take it.")]
        [SerializeField] string furRamp;
        [Tooltip("The ramp its markings are drawn in. Nothing else in the file may use it. The Markings color row swaps it.")]
        [SerializeField] string markingsRamp;
        [Tooltip("Its natural eye color: the ramp the face's eyes take while Eye color is Natural.")]
        [SerializeField] string eyeRamp = "Eyes";
        [Tooltip("Off for a kind whose beak sits where a mouth would be, like the finch.")]
        [SerializeField] bool showsMouth = true;

        [Header("Clothes")]
        [Tooltip("Off for a kind with no ears to wear earrings on, like the finch: the Ears row has nothing to show.")]
        [SerializeField] bool hasEars = true;
        [Tooltip("The pixel (from the top left of the player frame) on its left ear's outer edge where earrings sit. The " +
                 "Wardrobe's Ear Anchor is where they're drawn; the difference moves them here, mirrored for the right ear.")]
        [SerializeField] Vector2Int earAnchor = new(5, 9);

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public Sprite Sprite => sprite;
        public string FurRamp => furRamp;
        public string MarkingsRamp => markingsRamp;
        public string EyeRamp => eyeRamp;
        public bool ShowsMouth => showsMouth;
        public bool HasEars => hasEars;
        public Vector2Int EarAnchor => earAnchor;

        internal void ConfigureForTesting(string id, string displayName, Sprite sprite, string furRamp = "", string markingsRamp = "", string eyeRamp = "", bool showsMouth = true,
            bool hasEars = true, Vector2Int? earAnchor = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.sprite = sprite;
            this.furRamp = furRamp;
            this.markingsRamp = markingsRamp;
            this.eyeRamp = eyeRamp;
            this.showsMouth = showsMouth;
            this.hasEars = hasEars;
            this.earAnchor = earAnchor ?? new Vector2Int(5, 9);
        }
    }
}
