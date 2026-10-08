using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Project settings the art depends on. These end up in ProjectSettings and Assets/Settings, which are in git,
    /// so this only needs running again if someone resets them.
    /// - Sprites sort by height: the lower a sprite's feet on screen, the more it draws in front (custom axis 0, 1, 0).
    /// - Every sheet under Assets/Art uses the Bellwort Sheet Importer (characters keep Unity's Aseprite importer).
    /// Sprite import settings (16 pixels per unit, point filtering, no compression, feet pivots) live in the art's .meta files.
    /// </summary>
    public static class PixelArtProjectSettings
    {
        [MenuItem("Bellwort Burrow/Setup/Forest Steps/1. Apply Pixel Art Settings", false, 201)]
        public static void Apply()
        {
            // The 2D renderer has its own sort settings, and it wins over Graphics settings.
            int renderers = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                var serialized = new SerializedObject(data);
                var mode = serialized.FindProperty("m_TransparencySortMode");
                var axis = serialized.FindProperty("m_TransparencySortAxis");
                if (mode == null || axis == null)
                {
                    Debug.LogWarning($"Bellwort Burrow: couldn't find the sort settings on {data.name}; set Transparency Sort Mode to Custom Axis (0, 1, 0) by hand.");
                    continue;
                }
                mode.intValue = (int)TransparencySortMode.CustomAxis;
                axis.vector3Value = Vector3.up;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                renderers++;
            }

            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            GraphicsSettings.transparencySortAxis = Vector3.up;

            AssetDatabase.SaveAssets();
            Debug.Log($"Bellwort Burrow: sprites now sort by height on {renderers} 2D renderer(s).");

            // The sheets need the Bellwort Sheet Importer to become named sprites.
            if (AssetDatabase.IsValidFolder(ArtLibrary.ArtRoot))
            {
                int switched = ArtLibrary.UseSheetImporter();
                if (switched > 0) Debug.Log($"Bellwort Burrow: switched {switched} sheet(s) to the Bellwort Sheet Importer.");
            }
        }
    }
}
