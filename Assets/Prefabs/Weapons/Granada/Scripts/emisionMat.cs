using UnityEngine;

public class emisionMat : MonoBehaviour
{
    [Header("Configuración de Emisión")]
    [Tooltip("Color base de la emisión.")]
    public Color baseEmissionColor = Color.white;

    [Tooltip("Intensidad de emisión (de 0 a 1).")]
    [Range(0, 10)]
    public float emissionIntensity = 1.0f;

    // Referencia al material del objeto
    private Material material;

    void Start()
    {
        // Obtiene el material del Renderer asociado al GameObject
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            // Activa la propiedad de emisión en el material
            material.EnableKeyword("_EMISSION");
        }
        else
        {
            Debug.LogWarning("No se encontró un Renderer en este GameObject.");
        }
    }

    void Update()
    {
        if (material != null)
        {
            // Calcula el color emisivo basado en la intensidad
            Color emissionColor = baseEmissionColor * emissionIntensity;
            // Actualiza el color emisivo del material
            material.SetColor("_EmissionColor", emissionColor);
        }
    }
}