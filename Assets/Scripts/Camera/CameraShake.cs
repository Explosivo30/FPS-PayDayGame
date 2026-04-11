using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    // Corutina que maneja el efecto de camera shake
    public IEnumerator Shake(float duration, float magnitude)
    {
        // Guarda la posición original de la cámara
        Vector3 originalPos = transform.localPosition;

        float elapsed = 0.0f;

        // Mientras el tiempo transcurrido sea menor que la duración
        while (elapsed < duration)
        {
            // Genera un desplazamiento aleatorio
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // Aplica el desplazamiento a la posición de la cámara
            transform.localPosition = new Vector3(originalPos.x + x, originalPos.y+ y, originalPos.z);

            // Aumenta el tiempo transcurrido
            elapsed += Time.deltaTime;

            // Espera un frame antes de continuar
            yield return null;
        }

        // Restablece la posición original de la cámara
        transform.localPosition = originalPos;
    }
}
