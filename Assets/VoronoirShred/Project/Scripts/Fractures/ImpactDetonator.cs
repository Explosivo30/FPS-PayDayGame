using UnityEngine;

namespace Project.Scripts.Fractures
{
    public class ImpactDetonator : MonoBehaviour, IRaycastHitHandler
    {
        [Header("Settings")]
        public float impactThreshold = 2f; // Fuerza mínima para romper (Collision)
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
                if (col == null) continue; // MeshCollider may have been destroyed by ChunkNode
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

        /// <summary>
        /// Implementation of IRaycastHitHandler to handle raycast impacts.
        /// </summary>
        public void HandleRaycastHit(RaycastHit hit, float damage)
        {
            OnRaycastImpact(hit, damage);
        }

        /// <summary>
        /// Método para detonar al ser golpeado por un raycast de disparo (ej. pistola, rifle).
        /// </summary>
        /// <param name="hit">El RaycastHit del disparo</param>
        /// <param name="damageDealt">El daño causado por el arma (se usa como referencia de fuerza)</param>
        public void OnRaycastImpact(RaycastHit hit, float damageDealt)
        {
            Debug.Log($"ImpactDetonator hit on {gameObject.name}");
            // Verificamos las etiquetas ignoradas
            foreach (string tag in ignoreTags)
            {
                if (hit.transform.CompareTag(tag)) 
                {
                    Debug.Log($"ImpactDetonator: Ignoring hit because of tag {tag}");
                    return;
                }
            }

            // Estimamos la fuerza de impacto basada en el daño del arma
            // Puedes ajustar este cálculo según necesites más o menos fuerza
            float impactForce = damageDealt * 10f;

            DetonateRaycast(hit, impactForce);
        }

        /// <summary>
        /// Método para detonar al ser golpeado por un raycast.
        /// </summary>
        /// <param name="hit">El RaycastHit del disparo</param>
        /// <param name="forceMagnitude">La fuerza a aplicar (impulso)</param>
        public void DetonateRaycast(RaycastHit hit, float forceMagnitude)
        {
            if (fracturedObject == null)
            {
                Debug.LogError($"ImpactDetonator on {gameObject.name} has no fracturedObject assigned! Make sure Setup() is called or assign it in the inspector.");
                return;
            }

            // 2. INTERCAMBIO (SWAP)
            // Activamos la versión rota
            fracturedObject.SetActive(true);
            Debug.Log($"ImpactDetonator: Activated fractured version of {gameObject.name}");

            // 3. TRANSFERENCIA DE FUERZA (Física realista)
            // Usamos el punto de impacto del raycast
            Vector3 contactPoint = hit.point;
            Rigidbody bestChunkRb = null;
            float minDistance = float.MaxValue;

            foreach (var col in fracturedColliders)
            {
                if (col == null) continue; // MeshCollider may have been destroyed by ChunkNode
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
                
                // Obtenemos la normal de la superficie golpeada y aplicamos fuerza en dirección opuesta
                Vector3 impactDir = -hit.normal.normalized; 
                bestChunkRb.AddForceAtPosition(impactDir * forceMagnitude, contactPoint, ForceMode.Impulse);
                Debug.Log($"ImpactDetonator: Applied {forceMagnitude} force to chunk {bestChunkRb.name}");
            }
            else
            {
                Debug.LogWarning("ImpactDetonator: No Rigidbody found in chunks to apply force.");
            }

            // 4. Desactivamos el objeto original (este mismo)
                wholeObjectParent.SetActive(false);
            
           
        }

    }
}