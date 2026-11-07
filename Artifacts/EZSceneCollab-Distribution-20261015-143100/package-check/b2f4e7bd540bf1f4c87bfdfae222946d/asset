using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EZCollabTool
{
    public static class EZSceneCollabDemoMenu
    {
        const string DemoScenePath =
            "Assets/EZSceneCollab/Samples/Demo/Scenes/EZSceneCollab_Demo.unity";

        static readonly string[] DemoMaterialPaths =
        {
            "Assets/EZSceneCollab/Samples/Demo/Materials/DemoBlue.mat",
            "Assets/EZSceneCollab/Samples/Demo/Materials/DemoGreen.mat",
            "Assets/EZSceneCollab/Samples/Demo/Materials/DemoRed.mat"
        };

        [MenuItem("Window/EZ Scene Collab Demo", false, 2101)]
        static void OpenDemoScene()
        {
            PrepareDemoMaterials();

            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
            if (scene == null)
            {
                EditorUtility.DisplayDialog(
                    "EZ Scene Collab",
                    "The demo scene could not be found. Reimport the complete EZ Scene Collab package.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(DemoScenePath);
        }

        public static void PrepareDemoMaterials()
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[EZ Scene Collab] Unity's built-in Unlit/Color shader was not found. " +
                    "The demo materials were left unchanged.");
                return;
            }

            bool changed = false;
            foreach (string path in DemoMaterialPaths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == shader)
                    continue;

                Color color = material.color;
                material.shader = shader;
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
                EditorUtility.SetDirty(material);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();
        }
    }
}
