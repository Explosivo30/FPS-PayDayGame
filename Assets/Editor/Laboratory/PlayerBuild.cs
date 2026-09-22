using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class PlayerBuild
{
    const string Folder="Assets/Art/Player";
    [MenuItem("Tools/HELIX/Update player and run")]
    public static void Configure()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Artifacts/Player");AssetDatabase.Refresh();
        var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/-----Player.prefab");
        var state=player.GetComponentInChildren<PlayerStateMachine>();
        state.slideBoost=2;state.slideDuration=.7f;state.maxSlideSpeed=12;state.slideCooldown=.9f;
        state.groundAcceleration=65;state.airAcceleration=16;state.braking=55;state.turnAcceleration=100;state.heightTransitionSpeed=12;
        if(state.GetComponent<PlayerFeedback>()==null)state.gameObject.AddComponent<PlayerFeedback>();
        var serialized=new SerializedObject(state.GetComponent<Shield>());serialized.FindProperty("regenDelay").floatValue=3;
        serialized.FindProperty("regenRate").floatValue=12;serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(player,"Assets/Prefabs/Player/-----Player.prefab");PrefabUtility.UnloadPrefabContents(player);
        var catalog=new[]{
            Upgrade("Health","Vitalidad","Aumenta la salud máxima en 15.",UpgradeTarget.Player,PlayerStat.MaxHealth,WeaponStat.Damage,"",15,new[]{90,150,230}),
            Upgrade("Shield","Batería reforzada","Aumenta el escudo máximo en 10 y lo repone.",UpgradeTarget.Player,PlayerStat.Shield,WeaponStat.Damage,"",10,new[]{80,140,210}),
            Upgrade("Mobility","Servomotores","Aumenta la velocidad en 0,6 m/s.",UpgradeTarget.Player,PlayerStat.Acceleration,WeaponStat.Damage,"",.6f,new[]{80,130,190}),
            Upgrade("Plasma","Célula de plasma","La pistola causa 4 puntos más de daño.",UpgradeTarget.Weapon,0,WeaponStat.Damage,"Pistol",4,new[]{90,160,240}),
            Upgrade("Magazine","Cargador de carabina","Añade 6 plazas al cargador; no regala balas.",UpgradeTarget.Weapon,0,WeaponStat.AmmoCapacity,"MachineGun",6,new[]{70,120,180}),
            Upgrade("Shotgun","Cartuchos reforzados","Añade 1 de daño a cada perdigón.",UpgradeTarget.Weapon,0,WeaponStat.Damage,"ShotGun",1,new[]{100,170,250}),
            Upgrade("Stability","Estabilizador de carabina","Reduce el retroceso vertical por disparo.",UpgradeTarget.Weapon,0,WeaponStat.RecoilKickUp,"MachineGun",.12f,new[]{90,150},"carbine_stability"),
            Upgrade("Focus","Choke de escopeta","Cierra la dispersión de los perdigones.",UpgradeTarget.Weapon,0,WeaponStat.Spread,"ShotGun",.6f,new[]{100,170},"shotgun_focus"),
            Upgrade("Reload","Recarga optimizada","Reduce un 10% la recarga de las tres armas.",UpgradeTarget.Weapon,0,WeaponStat.ReloadTime,"",.1f,new[]{110,180},"quick_reload")
        };
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TowerCore.unity");
        var manager=Object.FindFirstObjectByType<UpgradeManager>();
        var managerData=new SerializedObject(manager);var entries=managerData.FindProperty("catalog");entries.arraySize=catalog.Length;
        for(int i=0;i<catalog.Length;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=catalog[i];
        managerData.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("PLAYER CONFIGURATION COMPLETE");
    }
    static StatUpgrade Upgrade(string name,string title,string description,UpgradeTarget target,PlayerStat player,WeaponStat weapon,string gun,float value,int[] costs,string unlock="")
    {
        string path=Folder+"/"+name+".asset";var upgrade=AssetDatabase.LoadAssetAtPath<StatUpgrade>(path);
        if(upgrade==null){upgrade=ScriptableObject.CreateInstance<StatUpgrade>();AssetDatabase.CreateAsset(upgrade,path);}
        upgrade.displayName=title;upgrade.description=description;upgrade.target=target;upgrade.playerStat=player;
        upgrade.weaponStat=weapon;upgrade.gunTypeID=gun;upgrade.persistentUnlockId=unlock;upgrade.upgradeMode=UpgradeMode.Additive;
        upgrade.levels=new StatUpgrade.LevelDefinition[costs.Length];
        for(int i=0;i<costs.Length;i++)upgrade.levels[i]=new StatUpgrade.LevelDefinition{cost=costs[i],value=value};
        EditorUtility.SetDirty(upgrade);return upgrade;
    }
    public static void Play(){Configure();TowerValidation.Play();}
    public static void Build(){TowerValidation.Build();}
}
