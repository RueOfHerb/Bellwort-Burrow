using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Profile;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Dresses the Player from the player profile (sys-6): when the scene starts, and again whenever the profile changes.
    /// The critter kind picks the body sprite. Over it, back to front: the bottom (unless the top is a full outfit that
    /// covers it), the top, the waist and neck accessories, the face (eyes and brows, and the mouth unless the kind has a
    /// beak), then the earrings, moved to the kind's ears (none on a kind without ears). Each part is its own renderer under
    /// the body, all in a Sorting Group, so the Player sorts as one picture.
    /// Every part wears the catalog's ramp swap material. The body swaps fur and markings, the face takes the fur color
    /// on its brows and the eye color on its eyes, and the top and bottom take their own color rows; Natural keeps what's
    /// drawn. With no profile yet (pressing Play right in a zone) the Player stays as placed.
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] CritterCatalog catalog;
        [Tooltip("The SpriteRenderer that shows the critter: the player file's prefab under the Player.")]
        [SerializeField] SpriteRenderer body;
        [Header("Parts over the body (made under it when empty)")]
        [SerializeField] SpriteRenderer face;
        [SerializeField] SpriteRenderer mouth;
        [SerializeField] SpriteRenderer bottom;
        [SerializeField] SpriteRenderer top;
        [SerializeField] SpriteRenderer waist;
        [SerializeField] SpriteRenderer neck;
        [SerializeField] SpriteRenderer earLeft;
        [SerializeField] SpriteRenderer earRight;
        [Tooltip("Off for a preview that something else dresses, like the character creator's.")]
        [SerializeField] bool followProfile = true;

        // Sorting order of each part above the body, inside the Player's Sorting Group.
        const int BottomOrder = 1, TopOrder = 2, WaistOrder = 3, NeckOrder = 4, FaceOrder = 5, EarOrder = 6;
        const float PixelsPerUnit = 16f;

        static readonly int SwapFromId = Shader.PropertyToID("_SwapFrom");
        static readonly int SwapToId = Shader.PropertyToID("_SwapTo");
        static readonly int SwapCountId = Shader.PropertyToID("_SwapCount");

        readonly List<Color32> swapFrom = new();
        readonly List<Color32> swapTo = new();
        readonly Vector4[] fromVectors = new Vector4[RampSwap.MaxColors];
        readonly Vector4[] toVectors = new Vector4[RampSwap.MaxColors];
        MaterialPropertyBlock block;
        IPlayerProfileService profiles;

        /// <summary>The kind showing now, or null if nothing has been applied yet.</summary>
        public CritterKind Kind { get; private set; }

        /// <summary>The face showing now, or null if nothing has been applied yet or the catalog has no faces.</summary>
        public FaceStyle Face { get; private set; }

        /// <summary>The pieces showing now in each slot; null for none, or a bottom under a full outfit.</summary>
        public ClothingPiece Top { get; private set; }
        public ClothingPiece Bottom { get; private set; }
        public ClothingPiece Neck { get; private set; }
        public ClothingPiece Waist { get; private set; }
        public ClothingPiece Ears { get; private set; }

        /// <summary>How many colors the last Apply swapped on the body (five per recolored part).</summary>
        public int SwappedColors { get; private set; }

        /// <summary>How many colors the last Apply swapped on the face (its brows and its eyes).</summary>
        public int FaceSwappedColors { get; private set; }

        /// <summary>How many colors the last Apply swapped on the top and on the bottom.</summary>
        public int TopSwappedColors { get; private set; }
        public int BottomSwappedColors { get; private set; }

        internal SpriteRenderer FaceRenderer => face;
        internal SpriteRenderer MouthRenderer => mouth;
        internal SpriteRenderer TopRenderer => top;
        internal SpriteRenderer BottomRenderer => bottom;
        internal SpriteRenderer EarLeftRenderer => earLeft;
        internal SpriteRenderer EarRightRenderer => earRight;

        void Start()
        {
            if (!followProfile || !ServiceLocator.TryGet(out profiles)) return;

            profiles.Changed += Apply;
            if (profiles.HasProfile) Apply(profiles.Profile);
        }

        void OnDestroy()
        {
            if (profiles != null) profiles.Changed -= Apply;
        }

        /// <summary>Shows this profile. A kind the catalog doesn't know leaves the Player as it is.</summary>
        public void Apply(PlayerProfile profile)
        {
            if (profile == null || catalog == null || body == null) return;

            var kind = catalog.Find(profile.critterKind);
            if (kind == null || kind.Sprite == null) return;

            Kind = kind;
            body.sprite = kind.Sprite;

            Face = catalog.FindFace(profile.face);
            if (Face != null)
            {
                face = Part(face, "Face", FaceOrder);
                mouth = Part(mouth, "Mouth", FaceOrder);
                face.sprite = Face.eyes;
                mouth.sprite = kind.ShowsMouth ? Face.mouth : null;
            }

            var wardrobe = catalog.Wardrobe;
            if (wardrobe != null) Dress(wardrobe, kind, profile);

            MakeSortingGroup();
            Recolor(kind, profile);
        }

        void Dress(Wardrobe wardrobe, CritterKind kind, PlayerProfile profile)
        {
            Top = Piece(wardrobe.Find(ClothingSlot.Top, profile.top));
            Bottom = Top != null && Top.coversBottom ? null : Piece(wardrobe.Find(ClothingSlot.Bottom, profile.bottom));
            Waist = Piece(wardrobe.Find(ClothingSlot.Waist, profile.waist));
            Neck = Piece(wardrobe.Find(ClothingSlot.Neck, profile.neck));
            Ears = kind.HasEars ? Piece(wardrobe.Find(ClothingSlot.Ears, profile.ears)) : null;

            bottom = Show(bottom, "Bottom", BottomOrder, Bottom);
            top = Show(top, "Top", TopOrder, Top);
            waist = Show(waist, "Waist", WaistOrder, Waist);
            neck = Show(neck, "Neck", NeckOrder, Neck);

            // Earrings are drawn on the left ear at the wardrobe's anchor; move them to this kind's, and mirror a pair.
            var shift = kind.EarAnchor - wardrobe.EarAnchor;
            earLeft = Show(earLeft, "Ear Left", EarOrder, Ears);
            earLeft.flipX = false;
            earLeft.transform.localPosition = new Vector3(shift.x, -shift.y, 0f) / PixelsPerUnit;
            earRight = Show(earRight, "Ear Right", EarOrder, Ears != null && Ears.paired ? Ears : null);
            earRight.flipX = true;
            earRight.transform.localPosition = new Vector3(-shift.x, -shift.y, 0f) / PixelsPerUnit;
        }

        /// <summary>A piece to draw, or null for None (no sprite).</summary>
        static ClothingPiece Piece(ClothingPiece piece) => piece != null && piece.sprite != null ? piece : null;

        SpriteRenderer Show(SpriteRenderer renderer, string partName, int order, ClothingPiece piece)
        {
            renderer = Part(renderer, partName, order);
            renderer.sprite = piece?.sprite;
            return renderer;
        }

        SpriteRenderer Part(SpriteRenderer renderer, string partName, int order)
        {
            if (renderer == null)
            {
                renderer = new GameObject(partName).AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(body.transform, false);
                renderer.sharedMaterial = body.sharedMaterial;
            }
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + order;
            return renderer;
        }

        void MakeSortingGroup()
        {
            if (TryGetComponent<SortingGroup>(out _)) return;
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = body.sortingLayerID;
            group.sortingOrder = body.sortingOrder;
        }

        void Recolor(CritterKind kind, PlayerProfile profile)
        {
            var material = catalog.RecolorMaterial;
            if (material == null) return;
            var palette = catalog.Palette;

            SwappedColors = RampSwap.Build(kind, profile, palette, swapFrom, swapTo);
            SetSwaps(body, material);

            if (face != null)
            {
                FaceSwappedColors = RampSwap.BuildFace(kind, profile, palette, catalog.FaceFurRamp, catalog.FaceEyeRamp, swapFrom, swapTo);
                SetSwaps(face, material);
            }
            if (top != null)
            {
                TopSwappedColors = RampSwap.BuildCloth(Top, profile.topColor, palette, swapFrom, swapTo);
                SetSwaps(top, material);
            }
            if (bottom != null)
            {
                BottomSwappedColors = RampSwap.BuildCloth(Bottom, profile.bottomColor, palette, swapFrom, swapTo);
                SetSwaps(bottom, material);
            }

            swapFrom.Clear();
            swapTo.Clear();
            foreach (var part in new[] { mouth, waist, neck, earLeft, earRight })
            {
                if (part != null) SetSwaps(part, material);
            }
        }

        void SetSwaps(SpriteRenderer target, Material material)
        {
            if (target.sharedMaterial != material) target.sharedMaterial = material;

            int count = swapFrom.Count;
            for (int i = 0; i < RampSwap.MaxColors; i++)
            {
                fromVectors[i] = i < count ? ToVector(swapFrom[i]) : Vector4.zero;
                toVectors[i] = i < count ? ToVector(swapTo[i]) : Vector4.zero;
            }

            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetVectorArray(SwapFromId, fromVectors);
            block.SetVectorArray(SwapToId, toVectors);
            block.SetFloat(SwapCountId, count);
            target.SetPropertyBlock(block);
        }

        // sRGB, 0 to 1, as the shader compares them
        static Vector4 ToVector(Color32 color) => new(color.r / 255f, color.g / 255f, color.b / 255f, 1f);

        internal void ConfigureForTesting(CritterCatalog catalog, SpriteRenderer body)
        {
            this.catalog = catalog;
            this.body = body;
            followProfile = false;
        }
    }
}
