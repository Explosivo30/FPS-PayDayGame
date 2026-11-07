using UnityEngine;
namespace Torbellino.Rendering{
public sealed class TelltaleSkinMotion:MonoBehaviour{public Transform tip;Quaternion rest;void Start(){rest=tip.localRotation;}void Update(){tip.localRotation=rest*Quaternion.Euler(0,0,Mathf.Sin(Time.time)*35);}}
}
