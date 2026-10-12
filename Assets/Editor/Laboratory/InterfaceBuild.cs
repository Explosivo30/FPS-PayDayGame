using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using System.IO;
public static class InterfaceBuild
{
    [MenuItem("Tools/HELIX/Configure futuristic interface")]
    public static void Configure()
    {
        Directory.CreateDirectory("Assets/Resources");AssetDatabase.Refresh();
        const string path="Assets/Resources/HelixInterface.asset";
        var theme=AssetDatabase.LoadAssetAtPath<InterfaceTheme>(path);
        if(theme==null){theme=ScriptableObject.CreateInstance<InterfaceTheme>();AssetDatabase.CreateAsset(theme,path);}
        const string fontPath="Assets/Resources/HelixDisplay.asset";
        var display=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if(display==null||display.material==null)
        {
            if(display!=null)AssetDatabase.DeleteAsset(fontPath);
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Scenes/Menu/Fuentes/AldotheApache.ttf");
            display=TMP_FontAsset.CreateFontAsset(source,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            display.name="Helix Display";AssetDatabase.CreateAsset(display,fontPath);
            string characters="";for(int c=32;c<256;c++)characters+=(char)c;
            display.TryAddCharacters(characters);
            display.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{TMP_Settings.defaultFontAsset};
            AssetDatabase.AddObjectToAsset(display.material,display);
            foreach(var atlas in display.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,display);
            EditorUtility.SetDirty(display);
        }
        theme.displayFont=display;
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
        Debug.Log("INTERFACE CONFIGURED");
    }
    public static void Play(){Configure();TowerValidation.Play();}
    public static void Build(){TowerValidation.Build();}
}
