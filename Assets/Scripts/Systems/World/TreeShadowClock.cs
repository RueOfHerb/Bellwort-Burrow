using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using BellwortBurrow.Core;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// The light under the Bellwort Tree, and the trunk's shadow that tells the time (see <see cref="TreeShadow"/>). Goes
    /// in a zone under the tree, once.
    /// - It sets the zone's global light to the shade: what reaches everywhere, in the shadow or not.
    /// - It makes its own Sprite light over the ground for the moon or the sun, with the shadow painted into its cookie,
    ///   one cookie pixel to a world pixel so the edges stay crisp. That light isn't saved in the scene.
    /// - The fallen log rests on the stump and lies overhead, out of the map, past its lower left edge: with a stump set,
    ///   the shadow is the log's, starting at the top of the stump on its southwest side, running off the map and
    ///   swinging with the light (see <see cref="TreeShadow.FromStump"/>).
    /// - In Play mode the game clock drives it. In the editor, tick Preview to see any hour in the Scene view; the
    ///   global light goes back to how it was when Preview is off.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TreeShadowClock : MonoBehaviour
    {
        [Tooltip("The zone's global light. It becomes the shade under the tree.")]
        [SerializeField] Light2D ambient;
        [Tooltip("The ground tilemap: the shadow covers its cells.")]
        [SerializeField] Tilemap area;
        [SerializeField] int pixelsPerUnit = 16;
        [Tooltip("The stump the fallen log rests on. With one set, the shadow is the log's: it starts at the top of the " +
                 "stump, on its southwest side, runs off the map's lower left edge under the log overhead, and swings " +
                 "through the night (see the settings' From the stump). Leave empty for a band right across the map.")]
        [SerializeField] Transform stump;
        [Tooltip("The middle of the stump's top, in world units from its feet (its pivot), on burrow_stump.")]
        [SerializeField] Vector2 stumpTop = new(0f, 34.8125f);
        [Tooltip("The stump's rim as seen from above: half its width and half its depth on screen, in world units.")]
        [SerializeField] Vector2 stumpRim = new(17.5f, 8.75f);
        [Tooltip("The ground's heights, worked out from the banks (Bellwort Burrow > Tiles > Work Out Heights). With them " +
                 "the log's shadow steps at banks and runs down cliff faces (lvl-5); leave empty for flat ground.")]
        [SerializeField] TerrainHeights heights;
        [SerializeField] TreeShadowSettings settings = new();

        [Header("Editor preview")]
        [Tooltip("Shows the hour below in the Scene view while not playing.")]
        [SerializeField] bool preview;
        [Tooltip("The hour to show, 0 to 24 (19 is 7:00 PM, 4.5 is 4:30 AM).")]
        [SerializeField, Range(0f, 24f)] float previewHour = 19f;

        Light2D direct;
        Texture2D cookie;
        Sprite cookieSprite;
        Color32[] pixels;
        BoundsInt cells;
        int lastMinute = -1;
        (int lo, int hi) lastBand = (int.MinValue, int.MinValue);
        (Vector2Int start, int bearing, int length, int width, int scale) lastStrip;
        float[] levels, blockLevels;
        float baseLevel;
        int levelsVersion = -1;
        // A repaint on heights spread over a few frames in Play (lvl-5): the next row of blocks, and what it paints.
        const int SpreadFrames = 8;
        int pendingRow = -1;
        (Vector2 start, Vector2 dir, int across, Vector2 shift) pending;
        bool authoredSaved;
        Color authoredColor;
        float authoredIntensity;

        public TreeShadowSettings Settings => settings;

        /// <summary>Picks up newly worked out heights on the next refresh (Work Out Heights calls this).</summary>
        public void ReloadHeights()
        {
            lastMinute = -1;
            levelsVersion = -1;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        /// <summary>The minute shown last, or -1 before the first.</summary>
        public int ShownMinute => lastMinute;

        void OnEnable() => lastMinute = -1;

        void OnDisable()
        {
            RestoreAuthored();
            Release();
        }

        void OnValidate()
        {
            lastMinute = -1;
#if UNITY_EDITOR
            // In the editor, show a change to Preview or its hour straight away, without waiting for the scene to change.
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && isActiveAndEnabled) Refresh();
                };
#endif
        }

        void Update() => Refresh();

        void Refresh()
        {
            int minute;
            if (Application.isPlaying)
            {
                if (!ServiceLocator.TryGet<ITimeService>(out var time)) return;
                minute = time.MinutesSinceMidnight;
            }
            else
            {
                if (!preview)
                {
                    RestoreAuthored();
                    Release();
                    return;
                }
                SaveAuthored();
                minute = Mathf.RoundToInt(previewHour * 60f) % (24 * 60);
            }

            if (minute != lastMinute) Show(minute, Application.isPlaying);
            else if (pendingRow >= 0) ContinuePaint();
        }

        /// <summary>Lights the zone for a minute of the day (also for tests and captures).</summary>
        public void Show(int minuteOfDay) => Show(minuteOfDay, false);

        // spread: paint a shadow on heights over a few frames instead of at once (in Play).
        void Show(int minuteOfDay, bool spread)
        {
            if (ambient == null || area == null) return;
            if (!Build()) return;
            lastMinute = minuteOfDay;

            var sky = TreeShadow.At(minuteOfDay, settings);
            ambient.color = sky.ambient;
            ambient.intensity = sky.ambientIntensity;
            direct.color = sky.direct;
            direct.intensity = sky.directIntensity;
            direct.enabled = sky.directIntensity > 0f;

            if (stump != null)
            {
                ShowFromStump(sky, spread);
            }
            else
            {
            // No shadow, or one run off the map: the light gets through everywhere.
            var band = sky.shadow
                ? TreeShadow.BandAt(sky.travel, cookie.width, cookie.height, settings.widthMiddle, settings.widthEnds)
                : (int.MaxValue / 2, int.MaxValue / 2);
            if (band != lastBand)
            {
                TreeShadow.Paint(pixels, cookie.width, cookie.height, band.Item1, band.Item2);
                cookie.SetPixels32(pixels);
                cookie.Apply(false);
                lastBand = band;
            }
            }
#if UNITY_EDITOR
            // Out of Play mode the lights only rebuild when the editor runs a frame, so ask for one.
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        // The fallen log's shadow from the top of the stump, on its southwest side, under the log overhead (Jocelyn, Oct 22).
        void ShowFromStump(TreeShadow.Sky sky, bool spread)
        {
            (Vector2Int, int, int, int, int) strip;
            Vector2 start = default, dir = default;
            float length = 0f;
            int across = 0;
            if (sky.shadow)
            {
                (dir, length, across) = TreeShadow.FromStump(sky.travel, settings);
                var top = ((Vector2)stump.position + stumpTop - (Vector2)direct.transform.position) * pixelsPerUnit;
                start = TreeShadow.StartOnRim(top, stumpRim * pixelsPerUnit, dir);
                float scale = levels != null ? ShadowCurve.ScaleAt(sky.travel, settings.elevationRise, settings.elevationPeak) : 0f;
                strip = (Vector2Int.RoundToInt(start), Mathf.RoundToInt(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg * 100f),
                         Mathf.RoundToInt(length), across, Mathf.RoundToInt(scale * 1000f));
            }
            else strip = (new Vector2Int(int.MinValue, int.MinValue), 0, 0, 0, 0);
            if (strip == lastStrip) return;
            pendingRow = -1;
            if (sky.shadow && levels != null)
            {
                pending = (start, dir, across, TreeShadow.ShiftPerLevel(dir, strip.Item5 / 1000f));
                lastStrip = strip;
                pendingRow = 0;
                if (spread) ContinuePaint();
                else while (pendingRow >= 0) ContinuePaint();
                return;
            }
            if (sky.shadow) TreeShadow.PaintFromStump(pixels, cookie.width, cookie.height, start, dir, length, across);
            else TreeShadow.Paint(pixels, cookie.width, cookie.height, int.MaxValue / 2, int.MaxValue / 2);
            cookie.SetPixels32(pixels);
            cookie.Apply(false);
            lastStrip = strip;
        }

        bool Build()
        {
            var bounds = area.cellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0) return false;
            if (direct != null && cookie != null && bounds == cells)
            {
                if (HeightsChanged()) LoadLevels();
                Place();
                return true;
            }

            Release();
            cells = bounds;
            int width = bounds.size.x * pixelsPerUnit, height = bounds.size.y * pixelsPerUnit;
            cookie = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Tree shadow cookie",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            pixels = new Color32[width * height];
            cookieSprite = Sprite.Create(cookie, new Rect(0, 0, width, height), Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cookieSprite.name = "Tree shadow cookie";
            cookieSprite.hideFlags = HideFlags.DontSave;

            var go = new GameObject("Moonlight and sunlight (made by the clock, not saved)") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            direct = go.AddComponent<Light2D>();
            direct.lightType = Light2D.LightType.Sprite;
            direct.lightCookieSprite = cookieSprite;
            direct.targetSortingLayers = ambient.targetSortingLayers;
            lastBand = (int.MinValue, int.MinValue);
            lastStrip = default;
            LoadLevels();
            Place();
            return true;
        }

        // Paints the next rows of a pending shadow on heights, and uploads the cookie once the last are done.
        void ContinuePaint()
        {
            if (pendingRow < 0 || cookie == null || levels == null) { pendingRow = -1; return; }
            int rows = (cookie.height + TreeShadow.Block - 1) / TreeShadow.Block;
            int step = (rows + SpreadFrames - 1) / SpreadFrames;
            TreeShadow.PaintOnHeights(pixels, cookie.width, cookie.height, pending.start, pending.dir, pending.across, levels,
                baseLevel, pending.shift, blockLevels, pendingRow, pendingRow + step);
            pendingRow += step;
            if (pendingRow < rows) return;
            pendingRow = -1;
            cookie.SetPixels32(pixels);
            cookie.Apply(false);
        }

        bool HeightsChanged() => heights == null ? levels != null : heights.version != levelsVersion;

        // Each cookie pixel's ground level from the heights, with the stump itself at its own ground's level, so the
        // shadow still starts on its rim (lvl-5).
        void LoadLevels()
        {
            levels = null;
            levelsVersion = -1;
            lastStrip = default;
            if (heights == null || stump == null) return;
            if (heights.origin != (Vector2Int)cells.min || heights.width != cells.size.x || heights.height != cells.size.y ||
                heights.tilePixels != pixelsPerUnit)
            {
                Debug.LogWarning($"{name}: the heights don't cover the ground's cells; run Bellwort Burrow > Tiles > Work Out Heights.", this);
                return;
            }
            levels = heights.PixelLevels();
            blockLevels = TreeShadow.BlockLevels(levels, cookie.width, cookie.height);
            levelsVersion = heights.version;
            var feet = area.WorldToCell(stump.position);
            baseLevel = heights.CellLevel(feet.x, feet.y);
            if (float.IsNaN(baseLevel)) baseLevel = heights.CellLevel(feet.x, feet.y - 1);
            if (float.IsNaN(baseLevel)) baseLevel = 0f;

            if (!stump.TryGetComponent<SpriteRenderer>(out var body) || body.sprite == null) return;
            var corner = area.CellToWorld(cells.min);
            int w = cookie.width, h = cookie.height;
            var min = Vector2Int.RoundToInt(((Vector2)body.bounds.min - (Vector2)corner) * pixelsPerUnit);
            var sprite = body.sprite;
            var rect = sprite.textureRect;
            int sw = Mathf.RoundToInt(rect.width), sh = Mathf.RoundToInt(rect.height);
            Color32[] art = sprite.texture.isReadable
                ? sprite.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), sw, sh) is var c ? System.Array.ConvertAll(c, v => (Color32)v) : null
                : null;
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                int px = min.x + (body.flipX ? sw - 1 - x : x), py = min.y + y;
                if (px < 0 || py < 0 || px >= w || py >= h) continue;
                if (art != null && art[y * sw + x].a == 0) continue;
                levels[py * w + px] = baseLevel;
            }
            blockLevels = TreeShadow.BlockLevels(levels, w, h);
        }

        // The cookie's lower left corner on the ground's lower left cell, snapped to whole pixels.
        void Place()
        {
            var corner = area.CellToWorld(cells.min);
            float Snap(float v) => Mathf.Round(v * pixelsPerUnit) / pixelsPerUnit;
            direct.transform.SetPositionAndRotation(new Vector3(Snap(corner.x), Snap(corner.y), transform.position.z), Quaternion.identity);
            direct.transform.localScale = Vector3.one;
        }

        void Release()
        {
            if (direct != null) Discard(direct.gameObject);
            if (cookieSprite != null) Discard(cookieSprite);
            if (cookie != null) Discard(cookie);
            direct = null;
            cookieSprite = null;
            cookie = null;
            pixels = null;
            levels = null;
            levelsVersion = -1;
            pendingRow = -1;
            lastMinute = -1;
            lastBand = (int.MinValue, int.MinValue);
            lastStrip = default;
        }

        void SaveAuthored()
        {
            if (authoredSaved || ambient == null) return;
            authoredColor = ambient.color;
            authoredIntensity = ambient.intensity;
            authoredSaved = true;
        }

        void RestoreAuthored()
        {
            if (!authoredSaved || ambient == null || Application.isPlaying) return;
            ambient.color = authoredColor;
            ambient.intensity = authoredIntensity;
            authoredSaved = false;
        }

        static void Discard(Object thing)
        {
            if (Application.isPlaying) Destroy(thing);
            else DestroyImmediate(thing);
        }
    }
}
