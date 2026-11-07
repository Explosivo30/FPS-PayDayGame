using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class TowerBuild
{
    const string ReactiveRootName = "Reactive arena gameplay";

    /// <summary>Adds gameplay objects to the saved floors without regenerating art, navigation or the player.</summary>
    [MenuItem("Tools/Tower/Install reactive arena gameplay")]
    public static void InstallReactiveArena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install the arena outside Play Mode.");
        var previous = SceneManager.GetActiveScene();
        foreach (string path in new[] { "Assets/Scenes/LaboratoryFloor.unity", "Assets/Scenes/RoboticForest.unity", "Assets/Scenes/CrystalGarden.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { ConfigureReactiveArena(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        Debug.Log("REACTIVE ARENA INSTALLED: three sight screens, one safe activatable garden petal; existing art/navigation preserved.");
    }

    public static void ConfigureReactiveArena(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded floor scene is required.");
        bool garden = scene.name == "CrystalGarden";
        bool forest = scene.name == "RoboticForest";
        if (!garden && !forest && scene.name != "LaboratoryFloor") return;
        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == ReactiveRootName);
        if (root == null)
        {
            root = new GameObject(ReactiveRootName); SceneManager.MoveGameObjectToScene(root, scene);
        }
        var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
        var dark = ReactiveMaterial("Ink");
        var screenMaterial = ReactiveMaterial(garden ? "Crystal violet" : forest ? "Jade enamel" : "Ivory enamel");
        var signal = ReactiveMaterial("Live mint");
        var warning = ReactiveMaterial("Signal gold");
        if (root.GetComponentInChildren<ArenaBreakableCover>(true) == null)
        {
            string mountName = scene.name == "LaboratoryFloor" ? "Equipment bench" : "Service cover";
            Vector3 preferred = scene.name == "LaboratoryFloor" ? new Vector3(-8, 0, -10) : new Vector3(6, 0, -7);
            var mount = all.Where(t => t.name == mountName).OrderBy(t => (t.position - preferred).sqrMagnitude).FirstOrDefault();
            if (mount == null) throw new InvalidOperationException("No structural cover mount found in " + scene.path);
            var holder = new GameObject("Prepared breakable sight screen"); holder.transform.SetParent(root.transform, false);
            holder.transform.SetPositionAndRotation(new Vector3(mount.position.x, 0, mount.position.z), mount.rotation);
            var cover = holder.AddComponent<ArenaBreakableCover>(); cover.structuralHealth = 60;
            cover.intactObject = ReactiveCube("Intact screen", holder.transform, new Vector3(0, 2.24f, -.79f), new Vector3(2.8f, 1.9f, .16f), screenMaterial, true);
            var cracks = new GameObject("Structural damage signal"); cracks.transform.SetParent(holder.transform, false);
            var crack = ReactiveCube("Damage seam", cracks.transform, new Vector3(-.25f, 2.24f, -.884f), new Vector3(.035f, 1.6f, .025f), warning, false);
            crack.transform.localRotation = Quaternion.Euler(0, 0, -25);
            ReactiveCube("Damage seam upper", cracks.transform, new Vector3(.35f, 2.64f, -.884f), new Vector3(1.1f, .035f, .025f), warning, false);
            cover.damageIndicator = cracks; cracks.SetActive(false);
            cover.fragments = new Transform[8];
            for (int i = 0; i < cover.fragments.Length; i++)
            {
                var fragment = ReactiveCube("Prepared cosmetic fragment " + i, holder.transform,
                    new Vector3(-1.05f + (i % 4) * .7f, 1.77f + (i / 4) * .95f, -.79f), new Vector3(.61f, .84f, .12f), screenMaterial, false);
                fragment.layer = 2; fragment.SetActive(false); cover.fragments[i] = fragment.transform;
            }
            ReactiveText("PANTALLA FRÁGIL", holder.transform, new Vector3(0, 3.36f, -.8f), 1.5f);
        }
        if (garden && root.GetComponentInChildren<ArenaMechanicalPetal>(true) == null)
        {
            Vector3 islandPosition = new Vector3(17, 0, 3);
            var island = all.Where(t => t.name == "Hexagonal botanical island")
                .OrderBy(t => (t.position - islandPosition).sqrMagnitude).FirstOrDefault();
            if (island == null || Vector3.Distance(island.position, islandPosition) > .2f)
                throw new InvalidOperationException("Expected garden island at (17,0,3) was not found; no scenery was removed.");
            var holder = new GameObject("Mechanical sight petal"); holder.transform.SetParent(root.transform, false); holder.transform.position = islandPosition;
            var petal = holder.AddComponent<ArenaMechanicalPetal>();
            // Retain every previous object for inspection/reversal. Only the upper decoration
            // of this one island is suspended; its structural hex, medium and sockets stay intact.
            var suspended = new List<GameObject>();
            foreach (var part in all)
            {
                if (part.name != "Bioluminescent crystal" && part.name != "Crystal luminous facet" && part.name != "Crystal fern") continue;
                Vector3 delta = part.position - islandPosition;
                if (new Vector2(delta.x, delta.z).magnitude > 3.2f) continue;
                suspended.Add(part.gameObject); part.gameObject.SetActive(false);
            }
            petal.preservedDecoration = suspended.ToArray();
            var pivot = new GameObject("Petal motor pivot"); pivot.transform.SetParent(holder.transform, false); pivot.transform.localPosition = new Vector3(0, 1.9f, 0);
            var body = pivot.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            petal.panel = pivot.transform;
            ReactiveCube("Opaque moving petal", pivot.transform, Vector3.zero, new Vector3(3.8f, 2.35f, .22f), screenMaterial, true);
            ReactiveCube("Petal top indicator", pivot.transform, new Vector3(0, 1.2f, 0), new Vector3(3.8f, .08f, .24f), warning, false);
            ReactiveCube("Petal fixed motor", holder.transform, new Vector3(0, .85f, 0), new Vector3(.65f, .5f, .65f), dark, false);
            var button = ReactiveCube("Petal conductor - E or shoot", holder.transform, new Vector3(-3.55f, 1.3f, -2.2f), new Vector3(.65f, 1.1f, .45f), signal, true);
            var receiver = button.AddComponent<ArenaPetalConductor>(); receiver.petal = petal;
            petal.conductor = button.GetComponent<Renderer>();
            petal.statusText = ReactiveText("E / DISPARA\nCAMBIAR ÁNGULO", button.transform, new Vector3(0, 1.05f, -.27f), 1.1f);
        }
    }

    static Material ReactiveMaterial(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + name + ".mat");
        if (material == null) throw new InvalidOperationException("Existing tower material missing: " + name);
        return material;
    }
    static GameObject ReactiveCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(parent, false);
        part.transform.localPosition = position; part.transform.localScale = scale; part.GetComponent<Renderer>().sharedMaterial = material;
        part.layer = collision ? 0 : 2; // visible to shots; excluded from the structural layer-3 NavMesh bake
        GameObjectUtility.SetStaticEditorFlags(part, 0);
        if (!collision) Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }
    static TextMeshPro ReactiveText(string text, Transform parent, Vector3 position, float size)
    {
        var holder = new GameObject("Functional arena label"); holder.transform.SetParent(parent, false); holder.transform.localPosition = position; holder.layer = 2;
        var label = holder.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = size; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(5, 1.3f);
        return label;
    }
}
