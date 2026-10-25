using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using BellwortBurrow.Core;
using BellwortBurrow.Systems;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.UI
{
    /// <summary>
    /// The title screen (ui-4), the first scene of the game. New Game goes to the character creator, which starts the
    /// game; Continue loads the newest save and goes to the zone it was saved in. The Game Root is up by then (arc-9),
    /// so the session is there to ask.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class TitleView : MonoBehaviour
    {
        [Tooltip("The scene New Game loads.")]
        [SerializeField] string creatorScene = "CharacterCreator";
        [Tooltip("Where Continue goes when the save names no zone.")]
        [SerializeField] string fallbackZone = "BellwortBurrow";

        PanelRenderer panel;
        Button newGame;
        Button continueButton;
        Button quit;
        Label saveLine;
        Label version;
        SaveHeader newest;

        void OnEnable()
        {
            panel = GetComponent<PanelRenderer>();
            panel.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (panel != null) panel.UnregisterUIReloadCallback(OnUIReload);
            Unbind();
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root)
        {
            Unbind();
            newGame = root.Q<Button>("new-game");
            continueButton = root.Q<Button>("continue");
            quit = root.Q<Button>("quit");
            saveLine = root.Q<Label>("save-line");
            version = root.Q<Label>("version");

            if (newGame != null) newGame.clicked += NewGame;
            if (continueButton != null) continueButton.clicked += Continue;
            if (quit != null) quit.clicked += Quit;
            if (version != null) version.text = "v" + Application.version;

            newest = Newest(ServiceLocator.TryGet<IGameSession>(out var session) ? session.ListSlots() : null);
            if (continueButton != null) continueButton.SetEnabled(newest != null);
            if (saveLine != null) saveLine.text = newest != null ? SaveText(newest) : "No saved game yet";
            var first = newest != null ? continueButton : newGame;
            if (first != null) root.schedule.Execute(() => first.Focus());
        }

        void Unbind()
        {
            if (newGame != null) newGame.clicked -= NewGame;
            if (continueButton != null) continueButton.clicked -= Continue;
            if (quit != null) quit.clicked -= Quit;
            newGame = null;
            continueButton = null;
            quit = null;
            saveLine = null;
            version = null;
        }

        /// <summary>The most recently saved header, by its saved-at time; null when there are none.</summary>
        public static SaveHeader Newest(IReadOnlyList<SaveHeader> headers)
        {
            if (headers == null) return null;
            SaveHeader best = null;
            foreach (var header in headers)
            {
                if (header == null || string.IsNullOrEmpty(header.slot)) continue;
                if (best == null || string.CompareOrdinal(header.savedAt ?? "", best.savedAt ?? "") > 0) best = header;
            }
            return best;
        }

        /// <summary>"Rue · Day 3 · Bellwort Burrow": who, when and where the save is.</summary>
        public static string SaveText(SaveHeader header)
        {
            string who = string.IsNullOrWhiteSpace(header.playerName) ? "Someone" : header.playerName.Trim();
            string where = string.IsNullOrWhiteSpace(header.zone) ? "" : " · " + ZoneName(header.zone);
            return $"{who} · Day {header.day}{where}";
        }

        /// <summary>A scene name with spaces: "BellwortBurrow" reads "Bellwort Burrow".</summary>
        public static string ZoneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return "";
            var words = new System.Text.StringBuilder();
            for (int i = 0; i < sceneName.Length; i++)
            {
                if (i > 0 && char.IsUpper(sceneName[i]) && !char.IsUpper(sceneName[i - 1])) words.Append(' ');
                words.Append(sceneName[i]);
            }
            return words.ToString();
        }

        void NewGame() => SceneManager.LoadScene(creatorScene);

        void Continue()
        {
            if (newest == null) return;
            if (!ServiceLocator.TryGet<IGameSession>(out var session))
            {
                Debug.LogError("[Title] There's no game session, so nothing can load. Is Assets/Resources/GameRoot.prefab there?");
                return;
            }
            if (!session.Load(newest.slot))
            {
                if (saveLine != null) saveLine.text = "That save couldn't be loaded. See the Console.";
                return;
            }
            string zone = string.IsNullOrWhiteSpace(session.Zone) ? fallbackZone : session.Zone;
            SceneManager.LoadScene(zone);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
