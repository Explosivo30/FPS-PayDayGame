using UnityEngine;
using UnityEditor;

#if PROBUILDER_AVAILABLE
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
#endif

namespace DungeonPainter.Editor
{
    /// <summary>
    /// Convierte la jerarquía generada por DungeonGenerator a ProBuilderMesh,
    /// permitiendo editar la geometría directamente desde ProBuilder.
    /// 
    /// Requiere ProBuilder instalado via Package Manager:
    ///   Window → Package Manager → Unity Registry → ProBuilder
    /// 
    /// Para activar, añade PROBUILDER_AVAILABLE a:
    ///   Project Settings → Player → Scripting Define Symbols
    /// </summary>
    public static class ProBuilderConverter
    {
        public static void Convert(GameObject dungeonRoot)
        {
            if (dungeonRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "No hay dungeon generado. Genera el dungeon primero.", "OK");
                return;
            }

#if PROBUILDER_AVAILABLE
            ConvertWithProBuilder(dungeonRoot);
#else
            ShowInstallInstructions();
#endif
        }

#if PROBUILDER_AVAILABLE
        private static void ConvertWithProBuilder(GameObject dungeonRoot)
        {
            var renderers = dungeonRoot.GetComponentsInChildren<MeshRenderer>(true);

            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("Sin geometría", "El dungeon no tiene MeshRenderers.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Convertir a ProBuilder",
                $"Se convertirán {renderers.Length} objetos a ProBuilderMesh.\n\n" +
                "• Podrás editar vértices, bordes y caras directamente.\n" +
                "• La operación NO es reversible con Undo.\n" +
                "• Guarda la Scene antes de continuar.\n\n" +
                "¿Continuar?",
                "Convertir", "Cancelar");

            if (!confirmed) return;

            int converted = 0;
            int failed    = 0;

            EditorUtility.DisplayProgressBar("Convirtiendo a ProBuilder", "Iniciando...", 0f);

            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    EditorUtility.DisplayProgressBar(
                        "Convirtiendo a ProBuilder",
                        $"{renderer.gameObject.name} ({i + 1}/{renderers.Length})",
                        (float)i / renderers.Length);

                    try
                    {
                        ConvertSingleObject(renderer.gameObject);
                        converted++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[ProBuilderConverter] No se pudo convertir '{renderer.gameObject.name}': {ex.Message}");
                        failed++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Marcar Scene como modificada
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string summary = $"Conversión completada:\n• {converted} objetos convertidos a ProBuilder";
            if (failed > 0) summary += $"\n• {failed} objetos fallaron (ver Console)";
            summary += "\n\nGuarda la Scene (Ctrl+S) para persistir los cambios.";

            EditorUtility.DisplayDialog("ProBuilder ✓", summary, "OK");
            Debug.Log($"[ProBuilderConverter] {converted} convertidos, {failed} fallados.");
        }

        private static void ConvertSingleObject(GameObject go)
        {
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            // Crear ProBuilderMesh desde la Mesh existente
            var pbMesh = go.AddComponent<ProBuilderMesh>();
            
            // Importar la geometría actual
            MeshImporter importer = new MeshImporter(pbMesh);
            importer.Import(meshFilter.sharedMesh, new MeshImportSettings
            {
                quads        = true,
                smoothing    = true,
                smoothingAngle = 15f
            });

            // Reconstruir la mesh de ProBuilder
            pbMesh.ToMesh();
            pbMesh.Refresh();

            // Eliminar el MeshFilter original (ProBuilder usa el suyo)
            // pero mantener el MeshRenderer
            Object.DestroyImmediate(meshFilter);

            EditorUtility.SetDirty(go);
        }

#else
        private static void ShowInstallInstructions()
        {
            bool openPM = EditorUtility.DisplayDialog(
                "ProBuilder no detectado",
                "Para usar esta función necesitas ProBuilder instalado y activado.\n\n" +
                "PASOS:\n" +
                "1. Window → Package Manager\n" +
                "2. Unity Registry → buscar 'ProBuilder'\n" +
                "3. Install\n\n" +
                "4. Después añade 'PROBUILDER_AVAILABLE' en:\n" +
                "   Project Settings → Player → Scripting Define Symbols\n\n" +
                "¿Abrir Package Manager ahora?",
                "Abrir Package Manager", "Cerrar");

            if (openPM)
                EditorApplication.ExecuteMenuItem("Window/Package Manager");
        }
#endif
    }
}
