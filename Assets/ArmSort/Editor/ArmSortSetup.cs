using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;

namespace ArmSort.EditorTools
{
    // One-time project setup: main scene, build list, app name, bundle IDs, portrait.
    [InitializeOnLoad]
    static class ArmSortSetup
    {
        // The Universal 2D template ships SampleScene with a Global Light 2D, so sprites are lit. Use it when present.
        const string TemplateScene = "Assets/Scenes/SampleScene.unity";
        const string NewScenePath = "Assets/Scenes/Main.unity";
        static string ScenePath => System.IO.File.Exists(TemplateScene) ? TemplateScene : NewScenePath;
        const string BundleId = "com.martinvizcaino.armsort";

        static ArmSortSetup()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Arm Sort/Run Project Setup")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!System.IO.File.Exists(TemplateScene) && !System.IO.File.Exists(NewScenePath))
            {
                System.IO.Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, NewScenePath);
                AssetDatabase.Refresh();
            }

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (PlayerSettings.productName != "Arm Sort")
            {
                PlayerSettings.companyName = "Martin Vizcaino";
                PlayerSettings.productName = "Arm Sort";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            }
        }
    }
}
