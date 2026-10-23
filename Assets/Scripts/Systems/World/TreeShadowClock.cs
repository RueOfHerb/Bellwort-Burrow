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
    /// - The log rests on the stump: with a stump set, the shadow starts at the top of the stump on its southwest side
    ///   and swings and stretches from there with the light (see <see cref="TreeShadow.FromStump"/>).
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
        [Tooltip("The stump the log rests on. With one set, the shadow starts at the top of the stump, on its southwest " +
                 "side, and swings and stretches from there through the night (see the settings' From the stump). Leave " +
                 "empty for a band right across the map.")]
        [SerializeField] Transform stump;
        [Tooltip("The middle of the stump's top, in world units from its feet (its pivot), on burrow_stump.")]
        [SerializeField] Vector2 stumpTop = new(0f, 34.8125f);
        [Tooltip("The stump's rim as seen from above: half its width and half its depth on screen, in world units.")]
        [SerializeField] Vector2 stumpRim = new(17.5f, 8.75f);
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
        (Vector2Int start, int bearing, int length, int width) lastStrip;
        bool authoredSaved;
        Color authoredColor;
        float authoredIntensity;

        public TreeShadowSettings Settings => settings;

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

            if (minute != lastMinute) Show(minute);
        }

        /// <summary>Lights the zone for a minute of the day (also for tests and captures).</summary>
        public void Show(int minuteOfDay)
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
                ShowFromStump(sky);
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

        // The log's shadow from the top of the stump, on its southwest side (Jocelyn, Oct 22).
        void ShowFromStump(TreeShadow.Sky sky)
        {
            (Vector2Int, int, int, int) strip;
            Vector2 start = default, dir = default;
            float length = 0f;
            int across = 0;
            if (sky.shadow)
            {
                (dir, length, across) = TreeShadow.FromStump(sky.travel, settings);
                var top = ((Vector2)stump.position + stumpTop - (Vector2)direct.transform.position) * pixelsPerUnit;
                start = TreeShadow.StartOnRim(top, stumpRim * pixelsPerUnit, dir);
                strip = (Vector2Int.RoundToInt(start), Mathf.RoundToInt(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg * 100f),
                         Mathf.RoundToInt(length), across);
            }
            else strip = (new Vector2Int(int.MinValue, int.MinValue), 0, 0, 0);
            if (strip == lastStrip) return;
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
            Place();
            return true;
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
