using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Opening a scene points the Scene view at it. Unity keeps one Scene view camera for every scene, so without this
    /// the view stays wherever the last scene left it (over the Burrow at x 128, say) and a smaller map like the Faerie
    /// Field looks empty. Every zone map starts at (0, 0), so the view frames the map from there: the painted tiles'
    /// bounds, or the origin when nothing is painted yet. Additive opens (a second scene beside the first) leave the
    /// view alone.
    /// </summary>
    [InitializeOnLoad]
    static class SceneViewOnOpen
    {
        static SceneViewOnOpen() => EditorSceneManager.sceneOpened += Opened;

        static void Opened(Scene scene, OpenSceneMode mode)
        {
            if (mode != OpenSceneMode.Single || Application.isPlaying) return;
            // Unity settles the Scene view after the scene opens, so frame it a few editor updates later.
            int wait = 3;
            void Later()
            {
                if (--wait > 0) return;
                EditorApplication.update -= Later;
                Frame(scene);
            }
            EditorApplication.update += Later;
        }

        static void Frame(Scene scene)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || !scene.IsValid() || !scene.isLoaded) return;
            var bounds = MapBounds(scene, out bool any);
            view.in2DMode = true;
            if (any) view.Frame(bounds, true);
            else view.LookAtDirect(Vector3.zero, Quaternion.identity, view.size);
            view.Repaint();
        }

        /// <summary>The painted tiles of every Tilemap in the scene, in world space, with (0, 0) always inside.</summary>
        internal static Bounds MapBounds(Scene scene, out bool any)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            any = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(true))
                {
                    if (tilemap.cellBounds.size.x <= 0 || tilemap.cellBounds.size.y <= 0) continue;
                    var b = tilemap.localBounds;
                    bounds.Encapsulate(tilemap.transform.TransformPoint(b.min));
                    bounds.Encapsulate(tilemap.transform.TransformPoint(b.max));
                    any = true;
                }
            }
            return bounds;
        }
    }
}
