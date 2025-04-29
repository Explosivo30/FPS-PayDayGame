using UnityEngine;



public class GunSway : MonoBehaviour
{

    [Header("Sway Settings")]
    [SerializeField] private float smooth;
    [SerializeField] private float multiplier;
    InputReader player;
    [SerializeField] private SwayData swayData;
    [SerializeField] private GunController gunController;


    private void Awake()
    {
        player = GetComponentInParent<InputReader>();
        
    }

    private void Update()
    {
        if (player == null || gunController == null || gunController.currentWeapon == null)
            return;

        swayData = gunController.currentWeapon.swayData;

        if(gunController.currentWeapon is IAimable aimable)
        {
            if(aimable.IsAiming)
            {
                return;
            }
        }
        // get mouse input
        float mouseX = player.LookValue.x * swayData.multiplier;
        float mouseY = player.LookValue.y * swayData.multiplier;

        // calculate target rotation
        Quaternion rotationX = Quaternion.AngleAxis(-mouseY, Vector3.right);
        Quaternion rotationY = Quaternion.AngleAxis(mouseX, Vector3.up);

        Quaternion targetRotation = rotationX * rotationY;

        // rotate 
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, swayData.smooth * Time.deltaTime);
    }
}
