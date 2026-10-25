using UnityEngine;
using UnityEngine.UIElements;
using BellwortBurrow.Core;
using BellwortBurrow.Systems;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.UI
{
    /// <summary>
    /// The HUD's clock (ui-3): the day and season, and the time of day, top left. It lives on the Game Root with the
    /// inventory row (InventoryView) on the same Panel Renderer, so it stays up through scene changes. It shows only in
    /// the world (a zone scene, whether or not a game has started, so pressing Play in a zone shows it too), and
    /// hides in the character creator and menus.
    /// The services are looked up each frame rather than cached, because a new game or a loaded save builds a fresh set.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class HudPresenter : MonoBehaviour
    {
        PanelRenderer panel;
        VisualElement root;
        Label dayLabel;
        Label timeLabel;
        string shownDay = "";
        string shownTime = "";
        bool shown = true;

        void OnEnable()
        {
            panel = GetComponent<PanelRenderer>();
            panel.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (panel != null) panel.UnregisterUIReloadCallback(OnUIReload);
            root = null;
            dayLabel = null;
            timeLabel = null;
        }

        void OnUIReload(PanelRenderer renderer, VisualElement built)
        {
            root = built.Q<VisualElement>("hud") ?? built;
            dayLabel = built.Q<Label>("day");
            timeLabel = built.Q<Label>("time");
            shownDay = "";
            shownTime = "";
            shown = !InWorld();
            Refresh();
        }

        void Update() => Refresh();

        static bool InWorld() =>
            ServiceLocator.TryGet<IGameSession>(out var session) && session.InWorld;

        void Refresh()
        {
            if (root == null) return;

            bool inWorld = InWorld();
            if (inWorld != shown)
            {
                root.style.display = inWorld ? DisplayStyle.Flex : DisplayStyle.None;
                shown = inWorld;
            }
            if (!inWorld || !ServiceLocator.TryGet<ITimeService>(out var time)) return;

            string day = ClockText.Day(time.CurrentDay, time.CurrentSeason);
            if (day != shownDay && dayLabel != null) { dayLabel.text = day; shownDay = day; }

            string clock = ClockText.Time(time.MinutesSinceMidnight);
            if (clock != shownTime && timeLabel != null) { timeLabel.text = clock; shownTime = clock; }
        }
    }
}
