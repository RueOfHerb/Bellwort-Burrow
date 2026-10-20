using UnityEngine;
using UnityEngine.InputSystem;
using BellwortBurrow.Core;
using BellwortBurrow.Systems;

namespace BellwortBurrow.UI
{
    /// <summary>
    /// TEMP DEBUG (sys-3): Ctrl+S or Alt+S shows a Save button in the top right corner, and pressing them again hides it.
    /// Only in the editor and development builds. The real game saves when the player goes to bed and whenever a scene loads.
    /// Lives on the Game Root, so it works in every scene.
    /// </summary>
    public class DebugSaveButton : MonoBehaviour
    {
        bool visible;
        string message = "";

        void Awake()
        {
            if (!Debug.isDebugBuild) Destroy(this);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.sKey.wasPressedThisFrame) return;
            if (!keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed) return;

            visible = !visible;
            message = "";
        }

        void OnGUI()
        {
            if (!visible) return;

            float scale = Mathf.Max(1f, Mathf.Floor(Screen.height / 540f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float width = 200f;
            GUILayout.BeginArea(new Rect(Screen.width / scale - width - 8f, 8f, width, 90f), GUI.skin.box);
            GUILayout.Label("Debug (temporary)");
            if (GUILayout.Button("Save")) message = Save();
            if (message.Length > 0) GUILayout.Label(message);
            GUILayout.EndArea();
        }

        static string Save()
        {
            if (!ServiceLocator.TryGet<IGameSession>(out var session)) return "There's no game session.";
            if (string.IsNullOrEmpty(session.Slot)) return "No game yet. Start one from the character creator.";
            return session.Save() ? $"Saved {session.Slot} at {System.DateTime.Now:t}" : "The save failed. See the Console.";
        }
    }
}
