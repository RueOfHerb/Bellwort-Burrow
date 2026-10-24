using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A bridge the player walks across, over ground that would stop them (the creek).
    /// - Its deck carries a trigger: whoever walks onto it stops colliding with the ground it crosses (the Tilemaps
    ///   named in <see cref="crosses"/>, the Water by default) until they step off at either end, so they walk over the
    ///   water on the deck. Its sides carry solid colliders, so nobody steps off the deck into the water, and nobody
    ///   walks onto its face from outside. Setup puts the colliders on (see Bridges in the editor tools).
    /// - The deck draws flat, under everything that stands (<see cref="DeckSortingOrder"/>), so a player on it is always
    ///   on top of it. Its rails stand up, so each rail is cut into thin strips, each sorting by where the rail stands
    ///   on the deck under it: a player on the deck is behind the near rail and in front of the far one, and a player
    ///   on the grass beside the bridge is in front of the near rail and behind the far one. The strips are made when the
    ///   bridge is enabled and never saved; the rails' pictures are slices of their own on the bridge's sheet.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Bridge : MonoBehaviour
    {
        /// <summary>Over the ground tilemaps (down to Tufts and Crop Circles at -10), under everything that stands (0).</summary>
        public const int DeckSortingOrder = -9;

        [Serializable]
        public sealed class Rail
        {
            [Tooltip("The rail's picture: a slice the same size as the bridge's, with the same feet.")]
            public Sprite sprite;
            [Tooltip("Its glow (lantern glass), if any: the matching _glow sprite.")]
            public Sprite glow;
            [Tooltip("Where the rail stands on the deck, from one end to the other, in units from the bridge's feet.")]
            public Vector2[] foot = Array.Empty<Vector2>();
            [Tooltip("The strips of the picture that have pixels: the first column of each, in pixels from its left edge.")]
            public int[] strips = Array.Empty<int>();
        }

        [Tooltip("The rail on the near (south) side, in front of a player on the deck.")]
        public Rail nearRail = new Rail();
        [Tooltip("The rail on the far (north) side, behind a player on the deck.")]
        public Rail farRail = new Rail();
        [Tooltip("How wide each rail strip is, in pixels. Narrower strips sort closer to the rail's own line.")]
        [Min(1)] public int stripWidth = 4;
        public Material litMaterial;
        public Material unlitMaterial;
        [Tooltip("The Tilemaps this bridge crosses: whoever is on the deck walks through their colliders.")]
        public string[] crosses = { "Water" };

        readonly List<GameObject> strips = new List<GameObject>();
        readonly List<Sprite> cutSprites = new List<Sprite>();
        readonly List<Collider2D> ground = new List<Collider2D>();
        readonly HashSet<Collider2D> walkers = new HashSet<Collider2D>();
        bool stale;

        // How many decks each walker is on, across every bridge, so stepping off one onto another keeps them across.
        static readonly Dictionary<Collider2D, int> Decks = new Dictionary<Collider2D, int>();

        /// <summary>The ground colliders the deck lets walkers through.</summary>
        public IReadOnlyList<Collider2D> Ground => ground;

        void OnEnable()
        {
            BuildRails();
            if (Application.isPlaying) FindGround();
        }

        void OnDisable()
        {
            foreach (var walker in new List<Collider2D>(walkers)) Step(walker, false);
            ClearRails();
        }

        void OnValidate() => stale = true;

        void Update()
        {
            if (!stale) return;
            stale = false;
            BuildRails();
        }

        void OnTriggerEnter2D(Collider2D other) => Step(other, true);

        void OnTriggerExit2D(Collider2D other) => Step(other, false);

        /// <summary>Finds the colliders of the Tilemaps named in <see cref="crosses"/>: their merged shape, or their cells.</summary>
        public void FindGround()
        {
            ground.Clear();
            foreach (var map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (Array.IndexOf(crosses, map.name) < 0) continue;
                if (map.TryGetComponent<CompositeCollider2D>(out var merged)) ground.Add(merged);
                else if (map.TryGetComponent<TilemapCollider2D>(out var cells)) ground.Add(cells);
            }
        }

        /// <summary>Crosses these colliders instead of the ones found by name.</summary>
        public void UseGround(IEnumerable<Collider2D> colliders)
        {
            ground.Clear();
            ground.AddRange(colliders);
        }

        /// <summary>
        /// A walker stepping onto the deck (on) or off it: while it's on any deck, it walks through the ground the bridge
        /// crosses. Only things that walk count: a solid collider on a moving (dynamic) body.
        /// </summary>
        public void Step(Collider2D walker, bool on)
        {
            if (!Walks(walker)) return;
            if (on ? !walkers.Add(walker) : !walkers.Remove(walker)) return;
            Decks.TryGetValue(walker, out int count);
            count = Mathf.Max(0, count + (on ? 1 : -1));
            if (count == 0) Decks.Remove(walker); else Decks[walker] = count;
            foreach (var collider in ground)
                if (collider != null) Physics2D.IgnoreCollision(walker, collider, count > 0);
        }

        /// <summary>Whether a walker is on this bridge's deck.</summary>
        public bool IsOnDeck(Collider2D walker) => walkers.Contains(walker);

        static bool Walks(Collider2D collider) =>
            collider != null && !collider.isTrigger && collider.attachedRigidbody != null &&
            collider.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic;

        /// <summary>
        /// The height of a line at <paramref name="x"/>: straight between its points, level past its ends. The line runs
        /// left to right or right to left.
        /// </summary>
        public static float HeightAt(Vector2[] line, float x)
        {
            if (line == null || line.Length == 0) return 0f;
            if (line.Length == 1) return line[0].y;
            bool rising = line[line.Length - 1].x >= line[0].x;
            Vector2 first = rising ? line[0] : line[line.Length - 1];
            Vector2 last = rising ? line[line.Length - 1] : line[0];
            if (x <= first.x) return first.y;
            if (x >= last.x) return last.y;
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                float lo = Mathf.Min(a.x, b.x), hi = Mathf.Max(a.x, b.x);
                if (x < lo || x > hi) continue;
                if (Mathf.Approximately(hi, lo)) return Mathf.Max(a.y, b.y);
                return Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
            }
            return last.y;
        }

        /// <summary>The rail strips, as built (for tests and checks).</summary>
        public IReadOnlyList<GameObject> RailStrips => strips;

        /// <summary>Cuts both rails into strips that sort by where the rail stands. Safe to call again.</summary>
        public void BuildRails()
        {
            ClearRails();
            BuildRail(nearRail, "Near rail");
            BuildRail(farRail, "Far rail");
        }

        void BuildRail(Rail rail, string label)
        {
            if (rail == null || rail.sprite == null || rail.strips == null) return;
            var rect = rail.sprite.rect;
            float ppu = rail.sprite.pixelsPerUnit;
            Vector2 pivot = rail.sprite.pivot;
            foreach (int left in rail.strips)
            {
                int width = Mathf.Min(stripWidth, (int)rect.width - left);
                if (left < 0 || width <= 0) continue;
                float middle = left + width / 2f;
                float x = (middle - pivot.x) / ppu;
                float feet = Mathf.Clamp(HeightAt(rail.foot, x) * ppu + pivot.y, 0f, rect.height);

                var strip = new GameObject($"{label} {left}") { hideFlags = HideFlags.HideAndDontSave };
                strip.transform.SetParent(transform, false);
                strip.transform.localPosition = new Vector3(x, (feet - pivot.y) / ppu, 0f);
                var picture = strip.AddComponent<SpriteRenderer>();
                picture.sprite = Cut(rail.sprite, left, width, feet);
                picture.spriteSortPoint = SpriteSortPoint.Pivot;
                if (litMaterial != null) picture.sharedMaterial = litMaterial;
                if (rail.glow != null)
                {
                    var glow = new GameObject("Glow") { hideFlags = HideFlags.HideAndDontSave };
                    glow.transform.SetParent(strip.transform, false);
                    var glowPicture = glow.AddComponent<SpriteRenderer>();
                    glowPicture.sprite = Cut(rail.glow, left, width, feet);
                    glowPicture.spriteSortPoint = SpriteSortPoint.Pivot;
                    glowPicture.sortingOrder = 1;
                    if (unlitMaterial != null) glowPicture.sharedMaterial = unlitMaterial;
                    // Keeps the lantern's glass drawn with its strip when sprites sort by height.
                    strip.AddComponent<SortingGroup>();
                }
                strips.Add(strip);
            }
        }

        Sprite Cut(Sprite source, int left, int width, float feet)
        {
            var rect = source.rect;
            var cut = Sprite.Create(source.texture, new Rect(rect.x + left, rect.y, width, rect.height),
                new Vector2(0.5f, feet / rect.height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cut.name = $"{source.name} {left}";
            cut.hideFlags = HideFlags.HideAndDontSave;
            cutSprites.Add(cut);
            return cut;
        }

        void ClearRails()
        {
            foreach (var strip in strips)
                if (strip != null) Discard(strip);
            strips.Clear();
            foreach (var cut in cutSprites)
                if (cut != null) Discard(cut);
            cutSprites.Clear();
        }

        static void Discard(UnityEngine.Object thing)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(thing);
            else UnityEngine.Object.DestroyImmediate(thing);
        }
    }
}
