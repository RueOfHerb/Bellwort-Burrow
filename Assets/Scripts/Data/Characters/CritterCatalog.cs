using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// Every critter kind the player can be, in the order the character creator shows them, and every face (sys-6).
    /// Every player file needs a kind here; an editor test checks.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Characters/Critter Catalog", fileName = "CritterCatalog")]
    public class CritterCatalog : ScriptableObject
    {
        [SerializeField] List<CritterKind> kinds = new();
        [Tooltip("Assets/Art/Palettes/characters.gpl, the ramps the color rows choose from.")]
        [SerializeField] RampPalette palette;
        [Tooltip("The lit sprite material that swaps ramps (Sprite Lit Ramp Swap). The Player wears it once dressed.")]
        [SerializeField] Material recolorMaterial;

        [Header("Faces")]
        [Tooltip("Every face the player can pick, Default first. Each is drawn once and fits every kind.")]
        [SerializeField] List<FaceStyle> faces = new();
        [Tooltip("The coat color the faces' brows, lids and lashes are drawn in. On a Player they take its fur color.")]
        [SerializeField] string faceFurRamp = "Grey";
        [Tooltip("The ramp the faces' eyes are drawn in. On a Player they take its eye color, or the kind's own.")]
        [SerializeField] string faceEyeRamp = "Eyes";

        public IReadOnlyList<CritterKind> Kinds => kinds;
        public RampPalette Palette => palette;
        public Material RecolorMaterial => recolorMaterial;
        public IReadOnlyList<FaceStyle> Faces => faces;
        public string FaceFurRamp => faceFurRamp;
        public string FaceEyeRamp => faceEyeRamp;

        public CritterKind Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var kind in kinds)
            {
                if (kind != null && kind.Id == id) return kind;
            }
            return null;
        }

        /// <summary>The face with this id. An empty or unknown id gets the first face (Default); no faces, null.</summary>
        public FaceStyle FindFace(string id)
        {
            FaceStyle first = null;
            foreach (var face in faces)
            {
                if (face == null) continue;
                first ??= face;
                if (!string.IsNullOrEmpty(id) && face.id == id) return face;
            }
            return first;
        }

        internal void ConfigureForTesting(IEnumerable<CritterKind> kinds, RampPalette palette = null, Material recolorMaterial = null,
            IEnumerable<FaceStyle> faces = null, string faceFurRamp = "Grey", string faceEyeRamp = "Eyes")
        {
            this.kinds = new List<CritterKind>(kinds);
            this.palette = palette;
            this.recolorMaterial = recolorMaterial;
            this.faces = faces != null ? new List<FaceStyle>(faces) : new List<FaceStyle>();
            this.faceFurRamp = faceFurRamp;
            this.faceEyeRamp = faceEyeRamp;
        }
    }
}
