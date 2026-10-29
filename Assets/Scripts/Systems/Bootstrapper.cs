using UnityEngine;
using UnityEngine.SceneManagement;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Data.Professions.Botany;
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
        [Tooltip("Every crop, so saves can store crop ids and find the crops again.")]
        [SerializeField] BotanyCropCatalog cropCatalog;
        [Tooltip("In the editor and development builds, every new game goes in one dev slot (dev.json), so test saves don't pile up.")]
        [SerializeField] bool reuseDevSlot = true;

        static Bootstrapper instance;

        Scene enteredScene;

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
            if (reuseDevSlot && Debug.isDebugBuild) Session.NewGameSlot = SaveStore.DevSlot;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[Game Root] All services registered.");
        }

        GameServices MakeServices() =>
            new(calendarConfig, dayChangedChannel, itemCatalog != null ? itemCatalog.Find : null,
                cropCatalog != null ? cropCatalog.Find : null);

        // Pressing Play in a scene in the editor: that scene can finish loading before the Game Root listens, so its
        // load is missed and a zone wouldn't count as the world. Enter it here if it wasn't entered already.
        void Start()
        {
            var first = SceneManager.GetActiveScene();
            if (first.isLoaded) OnSceneLoaded(first, LoadSceneMode.Single);
        }

        // A zone is the world: the clock runs there. Every scene change saves a game in progress.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || scene == enteredScene) return;
            enteredScene = scene;
            Session.EnterScene(scene.name, scene.path.Contains("/Scenes/Zones/"));
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
