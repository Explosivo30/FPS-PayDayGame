#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class MutationPlaytest
{
    IEnumerator GunCases()
    {
        Give(MutationKind.RamChamber);
        var first=Spawn(5);var second=Spawn(7);var third=Spawn(9);var fourth=Spawn(11);
        Aim(first.AimPoint);int ammo=pistol.currentAmmo;
        // The production controller reads the same held/released input the real binding supplies.
        weapons.ProcessCombatInput(true,false);
        yield return new WaitForSeconds(.72f);weapons.ProcessCombatInput(false,false);yield return null;
        Check(pistol.currentAmmo==ammo-3,"Ram chamber uses held/released production input and consumes three bullets");
        Check(first.Health==940&&second.Health==940&&third.Health==940&&fourth.Health==1000,"Charged physical ray pierces exactly three distinct robots");
        Capture("01-ram-chamber.png");
        float hp=first.Health;ammo=pistol.currentAmmo;
        pistol.GetType().GetField("lastShotTime",Private).SetValue(pistol,float.NegativeInfinity);
        run.ProcessAttack(pistol,true,false);Advance(.1f);run.ProcessAttack(pistol,false,true);
        Check(pistol.currentAmmo==ammo-1&&first.Health==hp-30,"Brief charge release preserves normal pistol shot");
        yield return Reset();Give(MutationKind.RamChamber);first=Spawn(7);
        Wall(new Vector3(0,1.2f,-12),new Vector3(3,3,.2f));Aim(first.AimPoint);pistol.FireCharged();
        Check(first.Health==1000,"Charged ray cannot penetrate solid cover");

        yield return Reset();Give(MutationKind.DoubleFuse);first=Spawn(5);second=Spawn(5,1.5f);
        Fire(pistol,first.AimPoint);float neighbor=second.Health;
        Check(Find(MutationWorldKind.FuseMark)!=null&&second.Health==neighbor,"First direct shot arms a visible fuse without exploding");
        Fire(pistol,first.AimPoint);
        Check(second.Health<neighbor&&Find(MutationWorldKind.FuseMark)==null,"Second distinct shot explodes the fuse and reaches neighbors");
        Check(run.DirectHits==2&&run.DerivedHits<=4,"Fuse reaction is bounded and does not recursively create direct hits");
        yield return Reset();Give(MutationKind.DoubleFuse);first=Spawn(5);Fire(shotgun,first.AimPoint);
        Check(run.DirectHits==1&&run.DerivedHits==0,"Shotgun pellets count as one direct attack for a fuse");

        yield return Reset();Give(MutationKind.Fork);first=Spawn(5);second=Spawn(6,-1.6f);third=Spawn(6,1.6f);
        for(int i=0;i<3;i++)Fire(carbine,first.AimPoint);
        Check(second.Health==1000&&third.Health==1000,"Fork waits for the fourth successful carbine shot");
        Fire(carbine,first.AimPoint);
        Check(second.Health==994&&third.Health==994,"Fourth hit sends exactly two half-damage branches");
        Capture("02-fork.png");

        yield return Reset();Give(MutationKind.ScrapFunnel);first=Spawn(5,1.8f);second=Spawn(5,-1.8f,true,true);
        float x=first.transform.position.x,vx=second.transform.position.x;Fire(shotgun,new Vector3(0,.9f,-10));yield return null;
        Check(Mathf.Abs(first.transform.position.x)<Mathf.Abs(x)-.3f,"Scrap funnel actually moves a normal laterally into the shot axis");
        Check(Mathf.Abs(second.transform.position.x-vx)<=.42f,"Funnel keeps the veteran displacement smaller");

        yield return Reset();Give(MutationKind.OpeningCut);first=Spawn(2);second=Spawn(3,1.3f);
        Equip(knife);Aim(first.AimPoint);knife.ResolveStrike();
        Check(Find(MutationWorldKind.CutMark)!=null,"A real knife contact places the opening-cut clamp");
        float marked=first.Health;neighbor=second.Health;Fire(pistol,first.AimPoint);
        Check(first.Health<marked-30&&second.Health<neighbor,"Shooting the knife mark adds the heavy contact and nearby explosion");
        Capture("03-opening-cut.png");

        yield return Reset();Give(MutationKind.ReturningBlade);first=Spawn(5);second=Spawn(7);third=Spawn(9);
        Equip(knife);Aim(first.AimPoint);run.ProcessAttack(knife,true,false);Advance(.55f);run.ProcessAttack(knife,false,true);
        Check(run.BladeInFlight,"Holding knife and releasing launches the actual blade");
        Advance(.35f);Check(third.Health==1000,"Outgoing blade limits its leg to two distinct victims");
        Equip(pistol);Advance(1.15f);
        Check(!run.BladeInFlight&&first.Health<1000&&second.Health<1000,"Blade hits, returns and allows weapon switching during flight");
        Check(3000-first.Health-second.Health-third.Health<=120.01f,"Both blade legs together deal at most four knife contacts");
        Check(3000-first.Health-second.Health-third.Health>=89.99f,"The returning leg makes additional physical knife contact");
        Check(knife.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),"Blade return restores the held knife renderers");

        yield return Reset();Give(MutationKind.Carrier);first=Spawn(5);second=Spawn(7,1.1f);
        pistol.currentAmmo=1;Reload(pistol,true);Fire(pistol,first.AimPoint);
        var bomb=Find(MutationWorldKind.Carrier);Check(bomb!=null&&bomb.Target==first,"Reload arms the next pistol impact as a carrier");
        float remaining=bomb!=null?bomb.Remaining:0;Advance(.4f);Fire(pistol,first.AimPoint);
        Check(bomb!=null&&bomb.Target==second&&bomb.Transfers==1&&bomb.Remaining<remaining,"Carrier transfers to a visible neighbor without restarting the fuse");
        Advance(2.2f);Check(Find(MutationWorldKind.Carrier)==null&&second.Health<1000,"Carrier explodes at its committed timer and latest position");

        yield return Reset();Give(MutationKind.GroundArc);
        for(int i=0;i<3;i++){var weak=Spawn(5+i*1.4f,0,false,false,1);Fire(pistol,weak.AimPoint);}
        Check(run.ArcCharge==3,"Three real direct kills charge ground arc");
        Aim(new Vector3(0,0,-11));pistol.Action.Cancel();pistol.GetType().GetField("lastShotTime",Private).SetValue(pistol,float.NegativeInfinity);pistol.Use();
        var arc=Find(MutationWorldKind.GroundArc);Check(arc!=null&&run.ArcCharge==0,"A physical ground hit consumes the charge and places the arc");
        first=Spawn(5);Advance(.05f);float damaged=first.Health;Advance(.2f);
        Check(damaged<1000&&first.Health==damaged,"Crossing the arc deals one pulse, not repeated damage per frame");
        Capture("04-ground-arc.png");Advance(2);Check(Find(MutationWorldKind.GroundArc)==null,"Ground arc expires without leaving a permanent hazard");
    }
    IEnumerator MotionCases()
    {
        yield return Reset();Give(MutationKind.SlagTrail,MutationKind.EmergencyBrake);
        player.PlayerVelocity=Vector3.forward*8;
        player.StepMovement(.02f,Vector2.up,true);run.Tick(.02f);
        for(int i=0;i<18;i++){player.StepMovement(.02f,Vector2.up,true);run.Tick(.02f);}
        var trail=Find(MutationWorldKind.SlagTrail);
        Check(trail!=null&&trail.Path.Count>=2,"Real slide movement of two metres produces a ground-following slag path");
        var enemy=Spawn(3,0);float hp=enemy.Health;Advance(.03f);
        Check(enemy.Health<hp,"An enemy crossing the real slide path is damaged");
        hp=enemy.Health;Advance(.1f);Check(enemy.Health==hp,"Slag applies damage once per enemy and trail");
        var front=Spawn(player.transform.position.z+16+1.8f);
        float z=front.transform.position.z;
        player.StepMovement(.02f,Vector2.up,false);run.Tick(.02f);yield return null;
        Check(front.transform.position.z>z+.2f,"Ending the actual slide triggers a forward brake displacement");
        Check(run.Cooldown(MutationKind.EmergencyBrake)>4,"Brake has a cooldown and grants no repeated free wave");
        Aim(front.AimPoint);Capture("05-slag-and-brake.png");

        yield return Reset();Give(MutationKind.Retrothruster);Equip(shotgun);run.JumpHeld=true;
        var start=player.transform.position;Fire(shotgun,new Vector3(0,.9f,-10));
        for(int i=0;i<12;i++){player.StepMovement(.02f,Vector2.zero,false);run.Tick(.02f);}
        Check(Vector3.Distance(start,player.transform.position)>2.8f&&Vector3.Distance(start,player.transform.position)<4.6f,"Retrothruster cancels inertia and makes a bounded physical withdrawal");
        Check(run.Cooldown(MutationKind.Retrothruster)>5,"Retrothruster spends a charge and waits six seconds");
        for(int i=0;i<75;i++){player.StepMovement(.02f,Vector2.zero,false);run.Tick(.02f);}
        start=player.transform.position;Fire(shotgun,new Vector3(0,.9f,-10));
        Check(player.Grounded&&player.CardImpulseRemaining==0,"Landing does not bypass the retrothruster cooldown");
        Advance(6.1f);run.JumpHeld=false;Fire(shotgun,new Vector3(0,.9f,-10));
        Check(player.CardImpulseRemaining==0&&run.Ready(MutationKind.Retrothruster),"A normal shotgun shot never spends the recovered optional impulse");
        run.JumpHeld=true;Fire(shotgun,new Vector3(0,.9f,-10));
        Check(player.CardImpulseRemaining>0,"Landing and cooldown together restore the retrothruster charge");
        yield return Reset();Give(MutationKind.Retrothruster);run.JumpHeld=true;
        Wall(new Vector3(0,1,-17.4f),new Vector3(5,3,.15f));start=player.transform.position;Fire(shotgun,new Vector3(0,.9f,-10));
        for(int i=0;i<12;i++){player.StepMovement(.02f,Vector2.zero,false);run.Tick(.02f);}
        Check(player.transform.position.z>-17.4f,"Retrothruster's CharacterController cannot pass through the wall behind the player");
        Check(player.Health==100&&player.GetComponent<Shield>().Current==50,"Retrothruster does not buy invulnerability or alter health/shield");
    }
}
#endif
