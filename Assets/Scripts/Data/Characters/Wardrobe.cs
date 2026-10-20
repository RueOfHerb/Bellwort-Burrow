using System;
using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>Where on the player a piece of clothing goes. Neck, Waist and Ears are the accessories.</summary>
    public enum ClothingSlot { Top, Bottom, Neck, Waist, Ears }

    /// <summary>
    /// One piece of clothing or accessory (sys-6). Pieces are drawn once and fit every critter, because they all share
    /// the same body; they live in Assets/Art/Characters/Clothes/player_clothes.aseprite, one whole 24 by 48 player
    /// frame per slice. Names describe the garment, never a gender: anyone can wear anything.
    /// </summary>
    [Serializable]
    public class ClothingPiece
    {
        [Tooltip("Saved in the player profile. Never change it once saves exist. \"none\" with no sprite is the empty choice.")]
        public string id;
        public string displayName;
        public ClothingSlot slot;
        [Tooltip("Its <slot>_<name> slice from player_clothes.aseprite. Empty for None.")]
        public Sprite sprite;
        [Tooltip("The color ramp it's drawn in, its natural color. The Top color or Bottom color row swaps it. Empty keeps its colors.")]
        public string ramp;
        [Tooltip("A full outfit, like a dress or overalls: worn as a top, it covers the bottom too.")]
        public bool coversBottom;
        [Tooltip("Ears only: worn on both ears (mirrored onto the right), rather than the left alone.")]
        public bool paired;

        public ClothingPiece() { }

        public ClothingPiece(string id, string displayName, ClothingSlot slot, Sprite sprite, string ramp = "", bool coversBottom = false, bool paired = false)
        {
            this.id = id;
            this.displayName = displayName;
            this.slot = slot;
            this.sprite = sprite;
            this.ramp = ramp;
            this.coversBottom = coversBottom;
            this.paired = paired;
        }

        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>
    /// Every piece of clothing and accessory the player can wear, in the order the character creator shows them (sys-6).
    /// The first piece of each slot is the one a new character wears: Tunic, Trousers, Belt, and None for the neck and ears.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Characters/Wardrobe", fileName = "Wardrobe")]
    public class Wardrobe : ScriptableObject
    {
        public const string NoneId = "none";

        [SerializeField] List<ClothingPiece> pieces = new();
        [Tooltip("The pixel (from the top left of the player frame) of the left ear that the ear pieces are drawn on. Each " +
                 "Critter Kind's own Ear Anchor moves them to its ears.")]
        [SerializeField] Vector2Int earAnchor = new(5, 9);

        public IReadOnlyList<ClothingPiece> Pieces => pieces;
        public Vector2Int EarAnchor => earAnchor;

        public List<ClothingPiece> For(ClothingSlot slot)
        {
            var list = new List<ClothingPiece>();
            foreach (var piece in pieces)
            {
                if (piece != null && piece.slot == slot) list.Add(piece);
            }
            return list;
        }

        /// <summary>The piece with this id in this slot; an empty or unknown id gets the slot's first piece.</summary>
        public ClothingPiece Find(ClothingSlot slot, string id)
        {
            ClothingPiece first = null;
            foreach (var piece in pieces)
            {
                if (piece == null || piece.slot != slot) continue;
                first ??= piece;
                if (!string.IsNullOrEmpty(id) && piece.id == id) return piece;
            }
            return first;
        }

        public static bool TryGetSlot(AppearancePart part, out ClothingSlot slot)
        {
            switch (part)
            {
                case AppearancePart.Top: slot = ClothingSlot.Top; return true;
                case AppearancePart.Bottom: slot = ClothingSlot.Bottom; return true;
                case AppearancePart.Neck: slot = ClothingSlot.Neck; return true;
                case AppearancePart.Waist: slot = ClothingSlot.Waist; return true;
                case AppearancePart.Ears: slot = ClothingSlot.Ears; return true;
                default: slot = default; return false;
            }
        }

        internal void ConfigureForTesting(IEnumerable<ClothingPiece> pieces, Vector2Int earAnchor)
        {
            this.pieces = new List<ClothingPiece>(pieces);
            this.earAnchor = earAnchor;
        }
    }
}
