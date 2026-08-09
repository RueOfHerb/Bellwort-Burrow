using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BellwortBurrow.Data;
using BellwortBurrow.Systems;

namespace BellwortBurrow.EditorTools
{
    public static class BootstrapSceneBuilder
    {
        const string DataFolder = "Assets/Data";
        const string SceneFolder = "Assets/Scenes/Core";
        const string CalendarConfigPath = DataFolder + "/CalendarConfig.asset";
        const string ScenePath = SceneFolder + "/Bootstrap.unity";

        [MenuItem("Bellwort Burrow/Setup/Create Bootstrap Scene")]
        public static void Create()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(SceneFolder);

            var calendarConfig = AssetDatabase.LoadAssetAtPath<CalendarConfig>(CalendarConfigPath);
            if (calendarConfig == null)
            {
                calendarConfig = ScriptableObject.CreateInstance<CalendarConfig>();
                AssetDatabase.CreateAsset(calendarConfig, CalendarConfigPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapObject = new GameObject("Bootstrapper");
            var bootstrapper = bootstrapObject.AddComponent<Bootstrapper>();

            var serializedBootstrapper = new SerializedObject(bootstrapper);
            serializedBootstrapper.FindProperty("calendarConfig").objectReferenceValue = calendarConfig;
            serializedBootstrapper.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[BootstrapSceneBuilder] Created {ScenePath} with a wired Bootstrapper.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
