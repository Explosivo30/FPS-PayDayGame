using UnityEngine;
public class CameraTilt : MonoBehaviour
{
    private float target, current;
    public void DoTilt(float zTilt) { target = zTilt; }
    private void LateUpdate()
    {
        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        transform.localRotation = Quaternion.Euler(0, 0, current);
    }
}
