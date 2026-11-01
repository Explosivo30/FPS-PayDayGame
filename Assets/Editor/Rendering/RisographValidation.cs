using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Torbellino.Rendering;
using Object = UnityEngine.Object;

// Batch entry point: -executeMethod RisographValidation.Run -quit
public static class RisographValidation
{
    const string Output = "Artifacts/Risograph";

    public static void RunVivid()
    {
        RisographSetup.ApplyVividReference();
        Run();
    }

    public static void RunFantasy()
    {
        RisographSetup.ApplyFantasy();
        Run();
        File.Copy(Output + "/forest-risograph.png", Output + "/forest-risograph-fantasy.png", true);
        File.Copy(Output + "/validation.txt", Output + "/validation-fantasy.txt", true);
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var report = new StringBuilder("Risograph — Unity GPU validation\n");
        try
        {
            RisographSetup.Install();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Rendering/Risograph/Risograph.shader");
            Check(shader != null && shader.isSupported, "Shader supported", report);
            ValidatePalette(report);
            CaptureGame(report);
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(m => m.severity == ShaderCompilerMessageSeverity.Error).ToArray();
            Check(errors.Length == 0, "No shader compiler errors: " + string.Join("; ", errors.Select(e => e.message)), report);
            report.AppendLine("PASS");
            File.WriteAllText(Output + "/validation.txt", report.ToString());
            Debug.Log("RISOGRAPH VALIDATION PASSED\n" + report);
        }
        catch (Exception error)
        {
            report.AppendLine("FAIL: " + error);
            File.WriteAllText(Output + "/validation.txt", report.ToString());
            throw;
        }
    }

    static void ValidatePalette(StringBuilder report)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Riso calibration camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 1;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(1);
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;

        // Empty overlay reproduces the player's world + weapon camera stack.
        var overlay = new GameObject("Riso calibration overlay").AddComponent<Camera>();
        overlay.CopyFrom(camera);
        overlay.cullingMask = 0;
        var overlayData = overlay.GetUniversalAdditionalCameraData();
        overlayData.SetRenderer(1);
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;
        data.cameraStack.Add(overlay);

        var volume = new GameObject("Calibration Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10000;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        var settings = profile.Add<RisographVolume>(true);
        var activeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RisographSetup.DefaultProfilePath);
        if (activeProfile.TryGet<RisographVolume>(out var activeStyle))
        {
            settings.shadowInk.Override(activeStyle.shadowInk.value);
            settings.blueInk.Override(activeStyle.blueInk.value);
            settings.pinkInk.Override(activeStyle.pinkInk.value);
            settings.paper.Override(activeStyle.paper.value);
        }
        settings.paletteSize.Override(RisoPaletteSize.Four);
        settings.intensity.Override(1);
        settings.exposure.Override(0);
        settings.midtoneLift.Override(0);
        settings.paperHighlights.Override(0);
        settings.paperGrain.Override(0);
        settings.inkWear.Override(0);
        settings.misregistration.Override(0);
        settings.halftone.Override(0);
        settings.contrast.Override(1);
        settings.saturation.Override(1);
        var palette = new[] { settings.shadowInk.value, settings.blueInk.value, settings.pinkInk.value, settings.paper.value };
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Check(unlit != null, "Calibration unlit shader found", report);
        var swatchMaterials = new Material[4];
        for (int i = 0; i < 4; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.position = new Vector3(-1.5f + i, 0, 0);
            quad.transform.localScale = new Vector3(1, 2, 1);
            var material = new Material(unlit);
            swatchMaterials[i] = material;
            material.SetColor("_BaseColor", palette[i]);
            quad.GetComponent<Renderer>().sharedMaterial = material;
        }

        var four = Capture(camera, 800, 400);
        Save(four, "calibration-four.png");
        CheckPalette(four, palette, report, "Four colors, final overlay with postprocessing disabled");
        foreach (var ink in palette)
            Check(four.GetPixels().Any(p => Distance(p, ink) < 0.035f), "Palette ink appears: " + ink, report);

        settings.paletteSize.Override(RisoPaletteSize.Three);
        var three = Capture(camera, 800, 400);
        Save(three, "calibration-three.png");
        CheckPalette(three, new[] { palette[0], palette[2], palette[3] }, report, "Three colors excludes medium blue");
        settings.paletteSize.Override(RisoPaletteSize.Four);
        for (int i = 0; i < 4; i++)
            swatchMaterials[i].SetColor("_BaseColor", Color.Lerp(palette[i], palette[(i + 1) % 4], 0.5f));
        var flat = Capture(camera, 800, 400);
        settings.intensity.Override(0);
        var original = Capture(camera, 800, 400);
        Check(Difference(flat, original) > 0.005f, "Intensity zero bypasses the effect", report);

        settings.intensity.Override(1);
        settings.halftone.Override(1);
        var dots = Capture(camera, 800, 400);
        Save(dots, "calibration-halftone.png");
        Check(Difference(dots, flat) > 0.01f, "Halftone replaces flat midtones with dots", report);
        settings.paperGrain.Override(0.2f);
        settings.inkWear.Override(0.12f);
        var textured = Capture(camera, 800, 400);
        var repeated = Capture(camera, 800, 400);
        Save(textured, "calibration-texture.png");
        Check(Difference(textured, repeated) < 0.00001f, "Paper grain is temporally stable", report);
        Check(Difference(textured, dots) > 0.005f, "Paper grain and wear change the print", report);
        foreach (var image in new[] { four, three, original, flat, dots, textured, repeated })
            Object.DestroyImmediate(image);
        foreach (var material in swatchMaterials) Object.DestroyImmediate(material);
        Object.DestroyImmediate(profile);
    }

    static void CaptureGame(StringBuilder report)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RoboticForest.unity");
        // Floors obtain the player from TowerCore at runtime. Use a temporary
        // inspection camera without loading gameplay or modifying the scene asset.
        var camera = new GameObject("Riso forest preview").AddComponent<Camera>();
        camera.transform.position = new Vector3(11, 4, -18);
        camera.transform.LookAt(new Vector3(0, 5, 3));
        camera.fieldOfView = 70;
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(1);
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        Check(Object.FindFirstObjectByType<TowerFloor>() != null, "Actual forest scene loaded", report);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RisographSetup.DefaultProfilePath);
        Check(profile.TryGet<RisographVolume>(out var settings), "Global Risograph override installed", report);
        var originalIntensity = settings.intensity.value;
        try
        {
            settings.intensity.value = 0;
            VolumeManager.instance.SetGlobalDefaultProfile(profile);
            var before = Capture(camera, 1280, 720);
            Save(before, "forest-before.png");
            settings.intensity.value = 1;
            VolumeManager.instance.SetGlobalDefaultProfile(profile);
            var after = Capture(camera, 1280, 720);
            Save(after, "forest-risograph.png");
            Check(Difference(before, after) > 0.02f, "Renderer feature changes the actual game scene", report);
            Object.DestroyImmediate(before);
            Object.DestroyImmediate(after);
        }
        finally
        {
            settings.intensity.value = originalIntensity;
            VolumeManager.instance.SetGlobalDefaultProfile(profile);
        }
    }

    static Texture2D Capture(Camera camera, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.Create();
        var request = new RenderPipeline.StandardRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera, request);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        target.Release();
        Object.DestroyImmediate(target);
        return image;
    }

    static void Save(Texture2D image, string name) => File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG());

    static void CheckPalette(Texture2D image, Color[] palette, StringBuilder report, string label)
    {
        var pixels = image.GetPixels();
        var matching = pixels.Count(p => palette.Min(ink => Distance(p, ink)) < 0.035f);
        Check(matching > pixels.Length * 0.98f, label + ": " + matching + "/" + pixels.Length + " pixels", report);
    }

    static float Distance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

    static float Difference(Texture2D a, Texture2D b)
    {
        var x = a.GetPixels();
        var y = b.GetPixels();
        float sum = 0;
        for (int i = 0; i < x.Length; i++) sum += Distance(x[i], y[i]);
        return sum / x.Length;
    }

    static void Check(bool condition, string message, StringBuilder report)
    {
        report.AppendLine((condition ? "OK: " : "ERROR: ") + message);
        if (!condition) throw new InvalidOperationException(message);
    }
}
