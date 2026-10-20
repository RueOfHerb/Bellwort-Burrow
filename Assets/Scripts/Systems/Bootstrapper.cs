using UnityEngine;
using UnityEngine.SceneManagement;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Systems
{
    /// <summary>
    /// The Game Root (arc-9): made once from Assets/Resources/GameRoot.prefab before the first scene loads,
    /// whichever scene you press Play in, and kept through every scene change. It owns the game session,
    /// which builds the services. No scene places it.
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        const string PrefabName = "GameRoot";

        [SerializeField] CalendarConfig calendarConfig;
        [SerializeField] VoidEventChannel dayChangedChannel;
        [Tooltip("Every item, so saves can store item ids and find the items again.")]
        [SerializeField] ItemCatalog itemCatalog;

        static Bootstrapper instance;

        public GameSession Session { get; private set; }

        // Domain reload is off when entering Play mode, so clear what the last run left behind.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateGameRoot()
        {
            if (instance != null) return;

            var prefab = Resources.Load<Bootstrapper>(PrefabName);
            if (prefab == null)
            {
                Debug.LogError("[Game Root] Assets/Resources/GameRoot.prefab is missing, so no services will run.");
                return;
            }

            Instantiate(prefab).name = "Game Root";
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            Session = new GameSession(MakeServices, new SaveStore(SaveStore.DefaultFolder));
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[Game Root] All services registered.");
        }

        GameServices MakeServices() =>
            new(calendarConfig, dayChangedChannel, itemCatalog != null ? itemCatalog.Find : null);

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path.Contains("/Scenes/Zones/")) Session.Zone = scene.name;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        void Update() => Session.Tick(UnityEngine.Time.deltaTime);
    }
}
