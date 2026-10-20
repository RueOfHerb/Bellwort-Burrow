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
    /// The critter kind picks the body sprite, and the face puts its eyes and brows and its mouth on top (a kind with a
    /// beak shows no mouth). Fur, markings and eye colors recolor them in engine: every part wears the catalog's ramp swap
    /// material, and ramps are swapped shade for shade for the ones the profile picked (Natural keeps the kind's own).
    /// The face's brows take the fur color and its eyes the eye color. The parts sit in a Sorting Group, so the Player
    /// sorts as one picture. With no profile yet (pressing Play right in a zone) the Player stays as placed.
    /// Outfits come with the new character art.
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] CritterCatalog catalog;
        [Tooltip("The SpriteRenderer that shows the critter: the player file's prefab under the Player.")]
        [SerializeField] SpriteRenderer body;
        [Tooltip("Shows the face's eyes and brows over the body. One is made under the body when this is empty.")]
        [SerializeField] SpriteRenderer face;
        [Tooltip("Shows the face's mouth over the body. One is made under the body when this is empty.")]
        [SerializeField] SpriteRenderer mouth;
        [Tooltip("Off for a preview that something else dresses, like the character creator's.")]
        [SerializeField] bool followProfile = true;

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

        /// <summary>How many colors the last Apply swapped on the body (five per recolored part).</summary>
        public int SwappedColors { get; private set; }

        /// <summary>How many colors the last Apply swapped on the face (its brows and its eyes).</summary>
        public int FaceSwappedColors { get; private set; }

        internal SpriteRenderer FaceRenderer => face;
        internal SpriteRenderer MouthRenderer => mouth;

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
                MakeParts();
                face.sprite = Face.eyes;
                mouth.sprite = kind.ShowsMouth ? Face.mouth : null;
            }
            Recolor(kind, profile);
        }

        void MakeParts()
        {
            if (face == null) face = MakePart("Face");
            if (mouth == null) mouth = MakePart("Mouth");
            if (!TryGetComponent<SortingGroup>(out _))
            {
                var group = gameObject.AddComponent<SortingGroup>();
                group.sortingLayerID = body.sortingLayerID;
                group.sortingOrder = body.sortingOrder;
            }
        }

        SpriteRenderer MakePart(string partName)
        {
            var part = new GameObject(partName).AddComponent<SpriteRenderer>();
            part.transform.SetParent(body.transform, false);
            part.sortingLayerID = body.sortingLayerID;
            part.sortingOrder = body.sortingOrder + 1;
            part.sharedMaterial = body.sharedMaterial;
            return part;
        }

        void Recolor(CritterKind kind, PlayerProfile profile)
        {
            var material = catalog.RecolorMaterial;
            if (material == null) return;

            SwappedColors = RampSwap.Build(kind, profile, catalog.Palette, swapFrom, swapTo);
            SetSwaps(body, material);

            if (face != null)
            {
                FaceSwappedColors = RampSwap.BuildFace(kind, profile, catalog.Palette, catalog.FaceFurRamp, catalog.FaceEyeRamp, swapFrom, swapTo);
                SetSwaps(face, material);
            }
            if (mouth != null)
            {
                swapFrom.Clear();
                swapTo.Clear();
                SetSwaps(mouth, material);
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
