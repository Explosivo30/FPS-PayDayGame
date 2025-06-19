using UnityEngine;
using DG.Tweening;
public class CameraTilt : MonoBehaviour
{
    public void DoTilt(float zTilt)
    {
        transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
    }
}
