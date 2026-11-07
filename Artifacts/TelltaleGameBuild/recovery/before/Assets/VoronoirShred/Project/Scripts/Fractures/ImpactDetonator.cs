using System.Collections.Generic;
using UnityEngine;

namespace Project.Scripts.Fractures
{
    public class ImpactDetonator : MonoBehaviour, IRaycastHitHandler
    {
        [Header("Collision settings")]
        public float impactThreshold = 5f;
        public float forceMultiplier = 1f;
        public string[] ignoreTags = { "Ground" };

        [Header("Ballistic destruction")]
        [SerializeField, Min(1f)] private float structuralHealth = 35f;
        [SerializeField, Min(0.1f)] private float impulsePerDamage = 1.2f;
        [SerializeField, Min(0.1f)] private float damageRadius = 1.15f;
        [SerializeField, Min(1)] private int maxReleasedChunks = 18;
        [SerializeField, Min(0.1f)] private float maximumImpulse = 24f;

        private GameObject fracturedObject;
        private Collider[] fracturedColliders;
        private GameObject wholeObjectParent;
        private float currentStructuralHealth;
        private bool hasDetonated;

        public void Setup(GameObject fracturedRef, GameObject originalParent)
        {
            fracturedObject = fracturedRef;
            wholeObjectParent = originalParent;
            currentStructuralHealth = structuralHealth;
            hasDetonated = false;

            fracturedColliders = fracturedObject != null
                ? fracturedObject.GetComponentsInChildren<Collider>(true)
                : new Collider[0];

            if (fracturedObject != null && fracturedObject.activeSelf)
                fracturedObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (ShouldIgnore(collision.gameObject))
                return;

            if (collision.relativeVelocity.magnitude < impactThreshold || collision.contactCount == 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            float impulse = Mathf.Clamp(
                collision.relativeVelocity.magnitude * forceMultiplier,
                1f,
                maximumImpulse);

            ActivateFracturedVersion();
            ApplyLocalizedForce(contact.point, collision.relativeVelocity.normalized, impulse);
        }

        public void HandleRaycastHit(RaycastHit hit, float damage)
        {
            OnRaycastImpact(hit, damage);
        }

        public void OnRaycastImpact(RaycastHit hit, float damageDealt)
        {
            if (ShouldIgnore(hit.transform.gameObject))
                return;

            // When the original object is already disabled, the ray hit a visible chunk.
            // Further shots should affect that local area immediately.
            if (wholeObjectParent != null && !wholeObjectParent.activeSelf)
            {
                ApplyLocalizedForce(
                    hit.point,
                    -hit.normal,
                    Mathf.Clamp(damageDealt * impulsePerDamage, 1f, maximumImpulse));
                return;
            }

            if (hasDetonated)
            {
                ApplyLocalizedForce(
                    hit.point,
                    -hit.normal,
                    Mathf.Clamp(damageDealt * impulsePerDamage, 1f, maximumImpulse));
                return;
            }

            currentStructuralHealth -= Mathf.Max(0f, damageDealt);
            if (currentStructuralHealth > 0f)
                return;

            DetonateRaycast(hit, damageDealt * impulsePerDamage);
        }

        public void DetonateRaycast(RaycastHit hit, float forceMagnitude)
        {
            if (fracturedObject == null)
            {
                Debug.LogError($"ImpactDetonator on {gameObject.name} has no fractured object assigned.");
                return;
            }

            ActivateFracturedVersion();
            ApplyLocalizedForce(
                hit.point,
                -hit.normal,
                Mathf.Clamp(forceMagnitude, 1f, maximumImpulse));
        }

        private void ActivateFracturedVersion()
        {
            if (fracturedObject == null)
                return;

            fracturedObject.SetActive(true);
            hasDetonated = true;

            if (wholeObjectParent != null && wholeObjectParent != fracturedObject)
                wholeObjectParent.SetActive(false);
        }

        private void ApplyLocalizedForce(Vector3 point, Vector3 direction, float forceMagnitude)
        {
            if (fracturedObject == null || fracturedColliders == null)
                return;

            if (!fracturedObject.activeSelf)
                fracturedObject.SetActive(true);

            Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            HashSet<Rigidbody> seenBodies = new HashSet<Rigidbody>();
            List<ChunkCandidate> candidates = new List<ChunkCandidate>();

            foreach (Collider chunkCollider in fracturedColliders)
            {
                if (chunkCollider == null)
                    continue;

                Rigidbody body = chunkCollider.attachedRigidbody;
                if (body == null || body.isKinematic || !seenBodies.Add(body))
                    continue;

                float distance = Vector3.Distance(chunkCollider.bounds.ClosestPoint(point), point);
                if (distance <= damageRadius)
                    candidates.Add(new ChunkCandidate(body, distance));
            }

            // Very small objects may have no bounds inside the configured radius.
            // In that case, release the nearest movable chunk so the hit still reads.
            if (candidates.Count == 0)
            {
                Rigidbody nearest = null;
                float nearestDistance = float.MaxValue;
                foreach (Collider chunkCollider in fracturedColliders)
                {
                    if (chunkCollider == null || chunkCollider.attachedRigidbody == null || chunkCollider.attachedRigidbody.isKinematic)
                        continue;

                    float distance = Vector3.Distance(chunkCollider.bounds.center, point);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = chunkCollider.attachedRigidbody;
                    }
                }

                if (nearest != null)
                    candidates.Add(new ChunkCandidate(nearest, 0f));
            }

            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            int releaseCount = Mathf.Min(maxReleasedChunks, candidates.Count);

            for (int i = 0; i < releaseCount; i++)
            {
                ChunkCandidate candidate = candidates[i];
                float falloff = 1f - Mathf.Clamp01(candidate.Distance / damageRadius);
                Vector3 scatter = Random.insideUnitSphere * 0.16f + Vector3.up * 0.08f;
                Vector3 impulse = (normalizedDirection + scatter).normalized * forceMagnitude * Mathf.Lerp(0.25f, 1f, falloff);

                ChunkNode node = candidate.Body.GetComponent<ChunkNode>();
                if (node != null)
                    node.ReleaseFromImpact(impulse, point);
                else
                {
                    candidate.Body.constraints = RigidbodyConstraints.None;
                    candidate.Body.useGravity = true;
                    candidate.Body.WakeUp();
                    candidate.Body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
                }
            }
        }

        private bool ShouldIgnore(GameObject hitObject)
        {
            if (hitObject == null)
                return true;

            foreach (string ignoredTag in ignoreTags)
            {
                if (!string.IsNullOrEmpty(ignoredTag) && hitObject.CompareTag(ignoredTag))
                    return true;
            }

            return false;
        }

        private readonly struct ChunkCandidate
        {
            public readonly Rigidbody Body;
            public readonly float Distance;

            public ChunkCandidate(Rigidbody body, float distance)
            {
                Body = body;
                Distance = distance;
            }
        }
    }
}
