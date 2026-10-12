#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public class InterfacePlaytest : MonoBehaviour
{
    const string Output="Artifacts/Interface";
    Camera view;RenderTexture target;PlayerStateMachine player;
    readonly List<string> results=new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("-ui-reference")&&!args.Contains("-ui-preview"))return;
        if(Object.FindFirstObjectByType<InterfacePlaytest>()!=null)return;
        var go=new GameObject("Interface preview");DontDestroyOnLoad(go);go.AddComponent<InterfacePlaytest>();
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;Directory.CreateDirectory(Output);
        bool reference=Environment.GetCommandLineArgs().Contains("-ui-reference");
        float deadline=Time.realtimeSinceStartup+60;
        while(!reference&&(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSeconds(1);
        player=Object.FindFirstObjectByType<PlayerStateMachine>();player.enabled=false;
        var weapons=player.GetComponentInChildren<GunController>();weapons.AutomatedInput=true;
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24);target.Create();view.targetTexture=target;
        if(reference)
        {
            GameManager.Instance.StopFloor();ShopManager.Instance.OpenShop();Bind();
            yield return new WaitForSecondsRealtime(.5f);Capture("level1-reference.png");
#if UNITY_EDITOR
            File.WriteAllLines(Output+"/level1-assets.txt",Object.FindObjectsByType<Image>(FindObjectsSortMode.None).Select(i=>i.name+" | "+UnityEditor.AssetDatabase.GetAssetPath(i.sprite)+" | "+i.color));
#endif
        }
        else
        {
            var session=TowerSession.Instance;var manager=GameManager.Instance;
            manager.StopFloor();player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);
            session.ShowMessage("",0);Bind();yield return new WaitForSecondsRealtime(.3f);Capture("hud-1080.png");
            GameManager.Instance.SetPlayerPoints(500);ShopManager.Instance.OpenShop();Bind();
            yield return new WaitForSecondsRealtime(.3f);Capture("shop-1080.png");AuditText("Shop");
            var upgrade=FindButton("Upgrade 3");int points=manager.GetPlayerPoints();
            Click(upgrade);Check(manager.GetPlayerPoints()<points,"Pointer click buys an affordable upgrade once");
            yield return new WaitForSecondsRealtime(.2f);Capture("shop-purchase.png");
            Click(FindButton("Collected cards"));yield return new WaitForSecondsRealtime(.2f);Capture("history-1080.png");
            ShopManager.Instance.CloseShop();
            session.CurrentFloor.introductionSeconds=.1f;manager.ConfigureFloor(session.CurrentFloor);
            deadline=Time.realtimeSinceStartup+20;
            while(session.Rewards.State!=RewardState.Choosing&&Time.realtimeSinceStartup<deadline)
            {
                foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))if(!enemy.IsDead)enemy.TakeDamage(10000);
                yield return null;
            }
            Bind();yield return new WaitForSecondsRealtime(.3f);Capture("cards-1080.png");AuditText("Cards");
            Click(FindButton("Card 2"));
            Check(session.Rewards.State==RewardState.Choosing,"Pointer selection does not confirm a reward");
            Click(FindButton("Confirm"));Check(session.Rewards.State==RewardState.Confirming,"Confirm button applies the selected card");
            yield return new WaitForSecondsRealtime(.6f);manager.StopFloor();
            // Inspect compact and wide viewports with the same responsive layout.
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(2560,1080)})
            {
                view.targetTexture=null;target.Release();Destroy(target);
                target=new RenderTexture(size.x,size.y,24);target.Create();view.targetTexture=target;
                ShopManager.Instance.OpenShop();Bind();yield return new WaitForSecondsRealtime(.3f);
                Capture("shop-"+size.x+".png");AuditText("Shop "+size.x);ShopManager.Instance.CloseShop();
            }
            Check(!RunPause.IsPaused&&Time.timeScale==1,"Closing interface restores gameplay");
            File.WriteAllLines(Output+"/interface-results.txt",results);
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(results.Any(r=>r.StartsWith("FAIL"))?1:0);
#else
        Application.Quit(results.Any(r=>r.StartsWith("FAIL"))?1:0);
#endif
    }
    Button FindButton(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name==name);
    void Check(bool pass,string text){results.Add((pass?"PASS ":"FAIL ")+text);Debug.Log(results.Last());}
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect=button.GetComponent<RectTransform>();
        var position=RectTransformUtility.WorldToScreenPoint(view,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        var recipient=hits.Count>0?ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject):null;
        Check(recipient==button.gameObject,"Raycast reaches "+button.name);
        if(recipient!=null)ExecuteEvents.Execute(recipient,pointer,ExecuteEvents.pointerClickHandler);
    }
    void AuditText(string screen)
    {
        Canvas.ForceUpdateCanvases();
        var text=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
            .Where(t=>t.GetComponentInParent<Canvas>()!=null&&(t.transform.root.name=="Run decisions"||t.transform.root.name=="Wave reward cards"));
        foreach(var label in text)label.ForceMeshUpdate();
        var overflow=text.Where(t=>t.isTextOverflowing).Select(t=>t.name).ToArray();
        Check(overflow.Length==0,screen+" text fits: "+string.Join(", ",overflow));
    }
    void Bind()
    {
        bool modal=TowerSession.Instance!=null?TowerSession.Instance.ModalVisible:ShopManager.Instance.IsOpen;
        foreach(var c in player.GetComponentsInChildren<Camera>())if(c.gameObject.layer==8)c.enabled=!modal;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f;}
    }
    void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
        File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());Destroy(texture);RenderTexture.active=previous;
    }
}
#endif
