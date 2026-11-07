using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace EZCollabTool.ProBuilderIntegration
{
    [InitializeOnLoad]
    public sealed class EZCollabProBuilderIntegration : IEZCollabProBuilderIntegration
    {
        static readonly EZCollabProBuilderIntegration instance = new EZCollabProBuilderIntegration();

        static EZCollabProBuilderIntegration()
        {
            EZCollabIntegrations.RegisterProBuilder(instance);

            ProBuilderEditor.afterMeshModification -= OnMeshModified;
            ProBuilderEditor.afterMeshModification += OnMeshModified;
            ProBuilderMesh.elementSelectionChanged -= OnElementSelectionChanged;
            ProBuilderMesh.elementSelectionChanged += OnElementSelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        EZCollabProBuilderIntegration()
        {
        }

        public bool TryFinalizeObject(GameObject gameObject)
        {
            ProBuilderMesh mesh = gameObject.GetComponent<ProBuilderMesh>();
            if (mesh == null)
                return false;

            try
            {
                mesh.ToMesh();
                mesh.Refresh();
                UnityEditor.EditorUtility.SetDirty(mesh);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(EZCollabLocalization.Tr(
                    EZCollabText.ProBuilderRebuildFailedLog,
                    gameObject.name,
                    exception.Message));
                return false;
            }
        }

        static void OnMeshModified(IEnumerable<ProBuilderMesh> meshes)
        {
            if (!EZCollabIntegrations.proBuilderEnabled || !EZCollabState.inSession ||
                EZCollabState.isApplyingRemoteChange)
            {
                return;
            }

            foreach (ProBuilderMesh mesh in meshes)
            {
                if (mesh != null)
                {
                    EZCollabAdvancedSync.QueueObjectState(
                        mesh.gameObject,
                        $"EZ Scene Collab {EZCollabLocalization.Tr(EZCollabText.ProBuilderEdit)}",
                        true);
                }
            }
        }

        static void OnElementSelectionChanged(ProBuilderMesh mesh)
        {
            if (!EZCollabIntegrations.proBuilderEnabled || !EZCollabState.inSession ||
                EZCollabState.isApplyingRemoteChange || mesh == null ||
                !EZCollabState.TryGetGuid(mesh.gameObject, out string guid) ||
                !EZCollabState.IsLockedByOther(guid))
            {
                return;
            }

            bool previous = EZCollabState.isApplyingRemoteChange;
            EZCollabState.isApplyingRemoteChange = true;
            try
            {
                mesh.ClearSelection();
                Selection.activeGameObject = null;
            }
            finally
            {
                EZCollabState.isApplyingRemoteChange = previous;
            }
        }

        [MenuItem("GameObject/EZ Scene Collab/ProBuilder Demo Object", false, 10)]
        static void CreateProBuilderDemoObject(MenuCommand command)
        {
            ProBuilderMesh mesh = ShapeGenerator.CreateShape(ShapeType.Cube);
            GameObject gameObject = mesh.gameObject;
            gameObject.name = "ProBuilder_Edit_Faces_Vertices_UVs";

            GameObject parent = command.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(gameObject, parent);
                gameObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            }
            else
            {
                gameObject.transform.position = new Vector3(0f, 1f, 5f);
            }

            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "Create EZ Scene Collab ProBuilder demo object");
            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);
        }

        [MenuItem("GameObject/EZ Scene Collab/ProBuilder Demo Object", true)]
        static bool CanCreateProBuilderDemoObject()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        static void BeforeAssemblyReload()
        {
            ProBuilderEditor.afterMeshModification -= OnMeshModified;
            ProBuilderMesh.elementSelectionChanged -= OnElementSelectionChanged;
            EZCollabIntegrations.UnregisterProBuilder(instance);
        }
    }
}
