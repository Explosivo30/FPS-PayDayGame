using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class MutationCardBuild
{
    const string Folder="Assets/Art/Rewards/Mutations";
    struct Definition
    {
        public MutationKind Kind;public string Name,Weapon,Description,Hint;public RewardGroup Group;public float Damage;
        public Definition(MutationKind kind,string name,string weapon,RewardGroup group,float damage,string description,string hint)
        {Kind=kind;Name=name;Weapon=weapon;Group=group;Damage=damage;Description=description;Hint=hint;}
    }
    static Definition[] Definitions=>new[]{
        new Definition(MutationKind.RamChamber,"Cámara de ariete","Pistol",RewardGroup.Offense,0,"Mantén disparo y suelta: lanza una carga que atraviesa una fila. Clic breve conserva el tiro normal.","Carga 0,6 s · 3 balas · doble daño · hasta 3 robots"),
        new Definition(MutationKind.DoubleFuse,"Fusible doble","",RewardGroup.Offense,40,"Un disparo coloca un fusible; otro disparo lo detona. Los perdigones del mismo tiro no cuentan como dos disparos.","Marca 3 s · explosión 2,6 m · hasta 4 blancos"),
        new Definition(MutationKind.Fork,"Horquilla","MachineGun",RewardGroup.Offense,0,"Cada cuarto disparo acertado de carabina envía dos ramales hacia otros robots visibles cercanos.","Ramales al 50% · alcance 5 m · sin rebotes adicionales"),
        new Definition(MutationKind.ScrapFunnel,"Embudo de chatarra","ShotGun",RewardGroup.Utility,0,"Cada escopetazo agrupa lateralmente a los robots hacia el eje del cono, sin acercarlos a ti.","Hasta 1,5 m lateral · veteranos hasta 0,4 m · respeta paredes"),
        new Definition(MutationKind.ReturningBlade,"Hoja de retorno","Knife",RewardGroup.Offense,0,"Mantén ataque y suelta para lanzar la hoja. Regresa después; puedes cambiar de arma mientras vuela.","Carga 0,5 s · 12 m · 2 víctimas por trayecto"),
        new Definition(MutationKind.ExplosiveReload,"Recarga explosiva","",RewardGroup.Utility,45,"Tras gastar tres balas, recargar pistola o carabina expulsa un cargador que explota con temporizador.","Detona tras 1 s · 2,5 m · un cargador activo"),
        new Definition(MutationKind.SlagTrail,"Estela de escoria","",RewardGroup.Offense,24,"Recorre dos metros deslizándote para dejar una franja caliente que quema a los perseguidores que la cruzan.","Duración 2 s · recuperación 4 s · un impacto por robot"),
        new Definition(MutationKind.EmergencyBrake,"Freno de emergencia","",RewardGroup.Survival,0,"Terminar el deslizamiento genera una onda frontal que aparta enemigos y abre una salida.","Alcance 3 m · recuperación 5 s · sin invulnerabilidad"),
        new Definition(MutationKind.OpeningCut,"Corte de apertura","Knife",RewardGroup.Offense,55,"Golpea con cuchillo y dispara después al marcado para detonar la abrazadera y alcanzar a sus vecinos.","Marca 2 s · un marcado · hasta 3 vecinos"),
        new Definition(MutationKind.MagneticScrap,"Chatarra imantada","",RewardGroup.Utility,0,"Una baja directa convierte los restos en un punto de reunión que atrae brevemente a robots cercanos.","Imán 1,2 s · recuperación 8 s · hasta 4 robots"),
        new Definition(MutationKind.Carrier,"Portador","Pistol",RewardGroup.Offense,60,"El primer impacto de pistola tras recargar coloca una carga. Disparar al portador la transfiere a otro robot.","Temporizador fijo 2,5 s · 2 transferencias · hasta 6 blancos"),
        new Definition(MutationKind.GroundArc,"Arco de suelo","",RewardGroup.Offense,35,"Tres bajas directas cargan un arco. Dispara al suelo para colocar una franja eléctrica que daña y ralentiza.","Franja 4 m / 1,5 s · hasta 4 robots · sin atravesar muros"),
        new Definition(MutationKind.MagazineBomb,"Cargador bomba","",RewardGroup.Utility,60,"Recargar pistola o carabina con balas restantes deja una bomba. Dispararle la detona manualmente.","Caduca a los 6 s · recuperación 10 s · una bomba activa"),
        new Definition(MutationKind.DecoyBeacon,"Baliza señuelo","",RewardGroup.Survival,0,"Comienza una recarga con el arma vacía para desplegar un señuelo que desvía la atención enemiga.","Señuelo 2 s · recuperación 14 s · atacar al robot rompe su distracción"),
        new Definition(MutationKind.MagneticAnchor,"Ancla magnética","",RewardGroup.Utility,0,"Recarga apuntando a suelo cercano para colocar un imán. Agrupa normales y ralentiza veteranos.","Radio 3 m · dura 2 s · recuperación 14 s"),
        new Definition(MutationKind.WorkHammer,"Martillo de obra","",RewardGroup.Offense,85,"Acierta tres disparos al mismo robot y recarga para golpear su última posición con un martillo suspendido.","Impacto tras 0,8 s · recuperación 12 s · cambiar blanco reinicia marca"),
        new Definition(MutationKind.RecoverableSaw,"Sierra recuperable","",RewardGroup.Utility,50,"Tres bajas con disparos preparan una sierra. Recargar la lanza; recogerla permite volver a usarla.","Una sierra · un impacto por robot · recogida antes de 8 s"),
        new Definition(MutationKind.DischargeCoil,"Bobina de descarga","",RewardGroup.Survival,25,"Tras perder el escudo, recargar despliega una bobina que captura disparos. Dispararle libera la energía.","Captura hasta 3 tiros · dura 4 s · recuperación 18 s"),
        new Definition(MutationKind.Retrothruster,"Retropropulsor","ShotGun",RewardGroup.Survival,0,"Mantén salto al disparar la escopeta para impulsarte hacia atrás. Un disparo normal no te desplaza.","Hasta 4 m · una carga · suelo y 6 s para recuperar")
    };
    [MenuItem("Tools/HELIX/Configure selected mutation cards")]
    public static void Configure()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Resources/Mutations");AssetDatabase.Refresh();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Mutations/Effect.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,"Assets/Resources/Mutations/Effect.mat");}
        material.SetColor("_BaseColor",Color.white);material.enableInstancing=true;EditorUtility.SetDirty(material);
        var cards=new List<RewardCard>();
        foreach(var def in Definitions)
        {
            string path=Folder+"/"+def.Kind+".asset";
            var card=AssetDatabase.LoadAssetAtPath<RewardCard>(path);
            if(card==null){card=ScriptableObject.CreateInstance<RewardCard>();AssetDatabase.CreateAsset(card,path);}
            card.id="mutation_"+def.Kind;card.title=def.Name;card.kind=RewardKind.Mutation;card.mutation=def.Kind;
            card.weaponId=def.Weapon;card.group=def.Group;card.maxLevel=1;card.special=false;
            card.value=def.Damage;card.description=def.Description;card.activationHint=def.Hint;card.icon=Icon(def.Kind);
            EditorUtility.SetDirty(card);cards.Add(card);
        }
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TowerCore.unity");
        var session=Object.FindFirstObjectByType<TowerSession>();
        if(session==null)throw new Exception("TowerCore has no session");
        var rewards=session.GetComponent<RunRewards>();
        if(rewards==null)throw new Exception("Configure the base reward service first");
        rewards.catalog=(rewards.catalog??Array.Empty<RewardCard>()).Where(c=>c!=null&&c.kind!=RewardKind.Mutation).Concat(cards).ToArray();
        if(session.GetComponent<MutationRun>()==null)session.gameObject.AddComponent<MutationRun>();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("MUTATIONS CONFIGURED: 19 selected cards, base rewards retained");
    }
    static Sprite Icon(MutationKind kind)
    {
        string path=Folder+"/Icon-"+kind+".png";
        if(File.Exists(path))return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
        int key=(int)kind;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            Vector2 p=new Vector2((x-64)/49f,(y-64)/49f);
            float radius=p.magnitude;bool ring=radius>.72f&&radius<.80f;
            bool on=ring&&!(p.y>.5f&&Mathf.Abs(p.x)<.3f);
            for(int i=0;i<1+key%4;i++)on|=Mathf.Abs(p.x-((i-(key%4)*.5f)*.25f))<.045f&&Mathf.Abs(p.y)<.40f;
            if(key%3==0)on|=Mathf.Abs(p.x-p.y*.6f)<.055f&&Mathf.Abs(p.y)<.6f;
            if(key%3==1)on|=Mathf.Abs(p.x+p.y*.6f)<.055f&&Mathf.Abs(p.y)<.6f;
            if(key%3==2)on|=Mathf.Abs(p.y)<.055f&&Mathf.Abs(p.x)<.55f;
            pixels[y*size+x]=on?Color.white:Color.clear;
        }
        texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    public static void ConfigureAndQuit(){Configure();EditorApplication.Exit(0);}
    public static void Play(){Configure();TowerValidation.Play();}
    public static void Build()
    {
        Configure();ShareBuild.BuildPackage("Builds/MutationCards/RobotTower.exe",BuildOptions.Development);
        Debug.Log("MUTATION WINDOWS BUILD SUCCEEDED");
    }
}
