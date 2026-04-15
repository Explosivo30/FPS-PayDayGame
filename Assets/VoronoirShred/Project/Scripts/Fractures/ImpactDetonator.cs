using UnityEngine;

namespace Project.Scripts.Fractures
{
    public class ImpactDetonator : MonoBehaviour
    {
        [Header("Settings")]
        public float impactThreshold = 2f; // Fuerza mínima para romper
        public float forceMultiplier = 1.0f; // Potenciador del golpe
        public string[] ignoreTags = { "Ground" };
        // Referencia al objeto fracturado (que estará oculto al principio)
        private GameObject fracturedObject;
        private Collider[] fracturedColliders;
        private GameObject wholeObjectParent; // El padre original que contiene todas las piezas sanas

        public void Setup(GameObject fracturedRef, GameObject originalParent)
        {
            fracturedObject = fracturedRef;
            wholeObjectParent = originalParent;

            fracturedColliders = fracturedObject.GetComponentsInChildren<Collider>(true);
            

            // Nos aseguramos que la versión rota empiece apagada
            if (fracturedObject.activeSelf) fracturedObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // 1. Verificamos si el golpe es suficientemente fuerte

            foreach (string tag in ignoreTags)
            {
                if (collision.gameObject.CompareTag(tag)) return;
            }

            if (collision.relativeVelocity.magnitude >= impactThreshold)
            {
                Detonate(collision);
            }
        }

        private void Detonate(Collision collision)
        {
            // 2. INTERCAMBIO (SWAP)
            // Activamos la versión rota
            fracturedObject.SetActive(true);

            // 3. TRANSFERENCIA DE FUERZA (Física realista)
            // Buscamos qué trozo estaba más cerca del punto de impacto
            Vector3 contactPoint = collision.contacts[0].point;
            Rigidbody bestChunkRb = null;
            float minDistance = float.MaxValue;

            foreach (var col in fracturedColliders)
            {
                float dist = Vector3.SqrMagnitude(col.bounds.center - contactPoint);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestChunkRb = col.GetComponent<Rigidbody>();
                }
            }

            // Aplicamos la fuerza del impacto a ese trozo específico
            if (bestChunkRb != null)
            {
                // Despertamos el RB si estaba dormido
                bestChunkRb.WakeUp();
                // Aplicamos la velocidad del impacto multiplicada
                bestChunkRb.AddForceAtPosition(collision.relativeVelocity * forceMultiplier, contactPoint, ForceMode.Impulse);
            }

            // 4. Desactivamos el objeto original (este mismo)
            
            wholeObjectParent.SetActive(false);
            // Opcional: Destroy(gameObject); si no vas a regenerarlo
        }
    }
}