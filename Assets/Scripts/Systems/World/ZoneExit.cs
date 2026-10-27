using UnityEngine;
using UnityEngine.SceneManagement;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A way out of a zone into another one (lvl-4, sys-10): walking into its trigger loads <see cref="targetScene"/>
    /// and puts the player at the spawn point named <see cref="targetSpawn"/> there. Drag it anywhere in a scene; its
    /// box is the trigger. Entering a zone saves the game, as any scene change does (arc-9).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ZoneExit : MonoBehaviour
    {
        [Tooltip("The scene to load, by name, as it is in the build settings.")]
        public string targetScene = "";
        [Tooltip("The spawn point in that scene to arrive at.")]
        public string targetSpawn = "";

        void Reset()
        {
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(3f, 1f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player") || other.isTrigger) return;
            ZoneTravel.Go(targetScene, targetSpawn);
        }
    }

    /// <summary>Carries which spawn point to arrive at across a scene load.</summary>
    public static class ZoneTravel
    {
        static string pending = "";
        static bool travelling;

        // Domain reload is off when entering Play mode, so clear what the last run left behind.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear() { pending = ""; travelling = false; }

        public static void Go(string scene, string spawn)
        {
            if (travelling || string.IsNullOrEmpty(scene)) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[Zone exit] There's no scene called {scene} in the build settings.");
                return;
            }
            travelling = true;
            pending = spawn ?? "";
            SceneManager.LoadScene(scene);
        }

        /// <summary>The spawn point the player was sent to, once; empty when they arrived some other way.</summary>
        public static string Take()
        {
            string spawn = pending;
            pending = "";
            travelling = false;
            return spawn;
        }

        /// <summary>The spawn point with this id in the loaded scenes, or null.</summary>
        public static SpawnPoint Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var point in Object.FindObjectsByType<SpawnPoint>())
                if (point.id == id) return point;
            return null;
        }
    }
}
