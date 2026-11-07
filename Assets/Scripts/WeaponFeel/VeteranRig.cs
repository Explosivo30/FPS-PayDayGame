using UnityEngine;

/// <summary>Authored visual and physical bindings for Ariete and Aguja, independent of normal models.</summary>
public sealed class VeteranRig : MonoBehaviour
{
    public string modelName;
    public Transform torso, head, leftArm, rightArm, leftForearm, rightForearm;
    public Transform leftThigh, rightThigh, leftShin, rightShin, gunPivot, muzzle;

    public static VeteranRig Install(NormalEnemyStateMachine owner)
    {
        var prefab = Resources.Load<VeteranRig>(owner.IsMelee ? "Veterans/ArieteVisual" : "Veterans/AgujaVisual");
        if (prefab == null) { Debug.LogError("Missing authored veteran model for " + owner.name); return null; }
        var rig = Instantiate(prefab, owner.transform, false);
        rig.name = prefab.modelName + " veteran visual";
        var presentation = owner.GetComponent<RobotPresentation>();
        presentation.ReplaceVisual(rig);
        var pose = owner.GetComponent<MeleeRobotPresentation>();
        if (pose != null) pose.BindRig(rig);
        // The new body's hit surfaces follow its articulated geometry, including the large fist.
        var oldCapsule = owner.GetComponent<CapsuleCollider>();
        if (oldCapsule != null) oldCapsule.enabled = false;
        return rig;
    }
}
