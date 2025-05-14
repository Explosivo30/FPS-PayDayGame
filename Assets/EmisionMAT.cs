using UnityEngine;

public class EmisionMAT : MonoBehaviour
{
    public Color emissionColor = Color.white;

    // Referencia al material del objeto
    public Material material;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Obtiene el material del Renderer asociado al GameObject
       // Renderer renderer = GetComponent<Renderer>();
       // if (renderer != null)
       // {
       //     material = renderer.material;
            // Activa la propiedad de emisión en el material
        //    material.EnableKeyword("_EMISSION");
       // }
        //else
        //{
        //    Debug.LogWarning("No se encontró un Renderer en este GameObject.");
//}
    }

    // Update is called once per frame
    void Update()
    {
        if (material != null)
        {
            // Actualiza el color emisivo del material
            material.SetColor("_EmissionColor", emissionColor);
        }
    }
}
