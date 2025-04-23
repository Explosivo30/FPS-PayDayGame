using UnityEngine;

public class GunSway : MonoBehaviour
{

    [Header("Sway Settings")]
    [SerializeField] private float smooth;
    [SerializeField] private float multiplier;
    InputReader player;

    private void OnEnable()
    {
        player = GetComponentInParent<InputReader>();
    }

    private void Update()
    {
        // get mouse input
        float mouseX = player.LookValue.y * multiplier;
        float mouseY = player.LookValue.x * multiplier;

        // calculate target rotation
        Quaternion rotationX = Quaternion.AngleAxis(-mouseY, Vector3.right);
        Quaternion rotationY = Quaternion.AngleAxis(mouseX, Vector3.up);

        Quaternion targetRotation = rotationX * rotationY;

        // rotate 
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smooth * Time.deltaTime);
    }
}
