using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RewardState { Idle, Celebrating, Choosing, Confirming }

/// <summary>Owns offers and run state; views never award stats or alter wave counters.</summary>
public class RunRewards : MonoBehaviour
{
    public RewardCard[] catalog;
    public RewardCard chipsFallback;
    readonly Dictionary<string,int> levels=new Dictionary<string,int>();
    readonly HashSet<string> issued=new HashSet<string>();
    readonly List<IDisposable> specials=new List<IDisposable>();
    readonly HashSet<string> specialIds=new HashSet<string>();
    readonly List<RewardCard> offer=new List<RewardCard>();
    System.Random random;
    TowerSession session;
    IDisposable pause;
    RewardContext context;
    bool specialsUnlocked;
    readonly IRewardEffect weaponEffect=new WeaponCardEffect(),playerEffect=new PlayerCardEffect(),
        supplyEffect=new SupplyCardEffect(),specialEffect=new SpecialCardEffect(),chipsEffect=new ChipsCardEffect();
    public RewardState State { get; private set; }
    public bool Pending=>State!=RewardState.Idle;
    public IReadOnlyList<RewardCard> Offer=>offer;
    public RewardContext Context=>context??(context=new RewardContext(GetComponent<TowerSession>()));
    public string LastResult { get; private set; }
    public RewardCard Chosen { get; private set; }
    public int OffersIssued { get; private set; }
    public event Action Changed;
    public event Action<RewardCard> Applied;
    void Awake(){session=GetComponent<TowerSession>();random=new System.Random(Guid.NewGuid().GetHashCode());}
    public int Level(RewardCard card)=>card!=null&&levels.TryGetValue(card.id,out int level)?level:0;
    public IRewardEffect Effect(RewardCard card)
    {
        switch(card.kind) {
            case RewardKind.WeaponStat:return weaponEffect;case RewardKind.Supply:return supplyEffect;
            case RewardKind.KillShield:case RewardKind.AutoLoader:return specialEffect;
            case RewardKind.Chips:return chipsEffect;default:return playerEffect;
        }
    }
    public bool Eligible(RewardCard card)=>card!=null&&!string.IsNullOrEmpty(card.id)&&
        (card.maxLevel==0||Level(card)<card.maxLevel)&&Effect(card).Eligible(card,Context);
    public string Preview(RewardCard card)=>Effect(card).Preview(card,Context,Level(card)+1);
    public bool QueueWave(int wave,int totalWaves,bool floorComplete)
    {
        if(!isActiveAndEnabled||Pending||session.RunEnded||session.player.IsDead)return false;
        string key=session.RunId+"/"+session.FloorVisit+"/"+wave;
        if(issued.Contains(key))return false;
        issued.Add(key);OffersIssued++;Chosen=null;LastResult=null;
        if(floorComplete&&totalWaves>2)specialsUnlocked=true;
        BuildOffer(specialsUnlocked&&totalWaves>2,floorComplete);
        State=RewardState.Celebrating;Changed?.Invoke();StartCoroutine(Present());return true;
    }
    void BuildOffer(bool allowSpecial,bool guarantee)
    {
        offer.Clear();
        var pool=(catalog??Array.Empty<RewardCard>()).Where(c=>Eligible(c)&&(!c.special||allowSpecial))
            .GroupBy(c=>c.id).Select(g=>g.First()).ToList();
        RewardCard guaranteed=guarantee?Pick(pool.Where(c=>c.special).ToList()):null;
        foreach(RewardGroup group in Enum.GetValues(typeof(RewardGroup)))
        {
            RewardCard card=guaranteed!=null&&guaranteed.group==group?guaranteed:null;
            var options=pool.Where(c=>c.group==group&&!offer.Contains(c)).ToList();
            if(card==null&&group==RewardGroup.Offense)
            {
                var equipped=options.Where(c=>c.weaponId==Context.EquippedId).ToList();
                card=Pick(equipped.Count>0?equipped:options);
            }
            if(card==null)card=Pick(options);
            if(card!=null&&!offer.Contains(card))offer.Add(card);
        }
        while(offer.Count<3)
        {
            var next=Pick(pool.Where(c=>!offer.Contains(c)).ToList());
            if(next==null)break;offer.Add(next);
        }
        if(offer.Count<3&&chipsFallback!=null)offer.Add(chipsFallback);
    }
    RewardCard Pick(List<RewardCard> cards)=>cards.Count==0?null:cards[random.Next(cards.Count)];
    IEnumerator Present()
    {
        // Finish the hit/kill frame (including LateUpdate feedback) before taking the pause.
        yield return null;
        if(session.RunEnded||session.player.IsDead) { Abort();yield break; }
        pause=RunPause.Acquire(true);
        ShopManager.Instance?.CloseShop();
        session.weapons.CancelForModal();
        yield return new WaitForSecondsRealtime(.6f);
        if(offer.Count==0) { Abort();yield break; }
        State=RewardState.Choosing;Changed?.Invoke();
        if(offer.Count==1&&offer[0]==chipsFallback)Confirm(0);
    }
    public bool Confirm(int index)
    {
        if(State!=RewardState.Choosing||session.RunEnded||session.player.IsDead||index<0||index>=offer.Count)return false;
        var card=offer[index];if(!Eligible(card))return false;
        State=RewardState.Confirming;Chosen=card;LastResult=Preview(card);
        levels[card.id]=Level(card)+1;Effect(card).Apply(card,Context,Level(card));
        Applied?.Invoke(card);Changed?.Invoke();StartCoroutine(FinishChoice());return true;
    }
    IEnumerator FinishChoice()
    {
        yield return new WaitForSecondsRealtime(.45f);
        session.ShowMessage("MEJORA INSTALADA · "+Chosen.title+" · "+LastResult.Replace("\n"," / "),4);
        Release();Changed?.Invoke();
    }
    public void EnableSpecial(RewardCard card)
    {
        if(!specialIds.Add(card.id))return;
        specials.Add(card.kind==RewardKind.KillShield?(IDisposable)new KillShieldReward(Context,card.value):new AutoLoaderReward(Context,card.value));
    }
    public string Summary()
    {
        var cards=(catalog??Array.Empty<RewardCard>()).Where(c=>Level(c)>0);
        string result=string.Join("\n",cards.Select(c=>c.title+" · "+RewardContext.Name(c.weaponId)+"  ×"+Level(c)));
        return string.IsNullOrEmpty(result)?"Todavía no has elegido cartas. Completa una oleada para recibir la primera.":result;
    }
    void Release()
    {
        State=RewardState.Idle;pause?.Dispose();pause=null;
        if(session!=null&&session.weapons!=null)session.weapons.RequireTriggerRelease();
    }
    public void Abort(){StopAllCoroutines();offer.Clear();Release();Changed?.Invoke();}
    void OnDestroy()
    {
        StopAllCoroutines();pause?.Dispose();
        foreach(var special in specials)special.Dispose();specials.Clear();
    }
}
