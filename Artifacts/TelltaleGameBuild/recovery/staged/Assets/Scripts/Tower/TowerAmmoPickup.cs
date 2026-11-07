using UnityEngine;

public class TowerAmmoPickup : MonoBehaviour
{
    float age;
    bool collected;
    Material material;
    Vector3 origin;
    public static void Spawn(Vector3 position)
    {
        if(FindObjectsByType<TowerAmmoPickup>(FindObjectsSortMode.None).Length>=24)return;
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Célula de munición";
        Destroy(go.GetComponent<Collider>());go.transform.position=position+Vector3.up*.4f;go.transform.localScale=new Vector3(.25f,.16f,.25f);
        var pickup=go.AddComponent<TowerAmmoPickup>();pickup.origin=go.transform.position;
        pickup.material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        pickup.material.SetColor("_BaseColor",new Color(.08f,.8f,1));
        go.GetComponent<Renderer>().sharedMaterial=pickup.material;
    }
    void Update()
    {
        var session=TowerSession.Instance;
        if(session==null||session.IsTransitioning||WeaponAction.CombatPaused)return;
        age+=Time.deltaTime;if(age>45){Destroy(gameObject);return;}
        transform.position=origin+Vector3.up*(Mathf.Sin(age*3)*.05f);transform.Rotate(0,55*Time.deltaTime,0);
        var point=session.player.transform.position;
        if(Vector3.Distance(point,transform.position)>1.8f)return;
        if(Physics.Linecast(transform.position,point,session.player.GroundMask,QueryTriggerInteraction.Ignore))return;
        TryCollect(session);
    }
    public bool TryCollect(TowerSession session)
    {
        if(collected||session==null||session.player.IsDead||WeaponAction.CombatPaused)return false;
        int added=0;
        foreach(var weapon in session.weapons.Weapons)
        {
            if(!(weapon is BaseGun gun))continue;
            int before=gun.reserveAmmo;
            gun.AddReserve(gun is ShotGun?1:gun is Pistol?3:8);added+=gun.reserveAmmo-before;
        }
        if(added==0)return false;
        collected=true;
        session.ShowMessage("MUNICIÓN RECUPERADA",1.3f);
        session.player.GetComponent<PlayerFeedback>()?.NotifyAmmo();Destroy(gameObject);return true;
    }
    void OnDestroy(){if(material!=null)Destroy(material);}
}
