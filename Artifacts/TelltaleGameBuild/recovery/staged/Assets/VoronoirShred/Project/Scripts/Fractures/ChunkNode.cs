using Project.Scripts.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Project.Scripts.Fractures
{
    public class ChunkNode : MonoBehaviour
    {
        public HashSet<ChunkNode> Neighbours = new HashSet<ChunkNode>();
        public ChunkNode[] NeighboursArray = new ChunkNode[0];
        private Dictionary<Joint, ChunkNode> JointToChunk = new Dictionary<Joint, ChunkNode>();
        private Dictionary<ChunkNode, Joint> ChunkToJoint = new Dictionary<ChunkNode, Joint>();
        private Rigidbody rb;
        private Vector3 frozenPos;
        private Quaternion forzenRot;
        private bool frozen;
        public bool IsStatic => rb != null && rb.isKinematic;
        public Color Color { get; set; } = Color.black;
        public bool HasBrokenLinks { get; private set; }
        public float maxTimerDestruction = 30f;
        public bool hasPassed = false;
        private float colliderDisableTimer = 6f;
        private bool Contains(ChunkNode chunkNode)
        {
            return Neighbours.Contains(chunkNode);
        }

        private void Update()
        {

            // Kinda hacky, but otherwise the chunks slowly drift apart.
            if (frozen)
            {
                transform.position = frozenPos;
                transform.rotation = forzenRot;
            }

            if (NeighboursArray.Length == 0)
            {
                if (!hasPassed)
                {
                    maxTimerDestruction = Random.Range(18f, 30f);
                    colliderDisableTimer = Random.Range(4f, 7f);
                    hasPassed = true;
                }

                colliderDisableTimer -= Time.deltaTime;
                if (colliderDisableTimer <= 0f && rb != null && rb.IsSleeping())
                {
                    MeshCollider meshCollider = GetComponent<MeshCollider>();
                    if (meshCollider != null)
                        meshCollider.enabled = false;
                    colliderDisableTimer = float.MaxValue;
                }

                maxTimerDestruction -= Time.deltaTime;
                if (maxTimerDestruction <= 0f)
                    Destroy(gameObject);
            }


        }

        public void Setup()
        {
            rb = GetComponent<Rigidbody>();
            Freeze();

            JointToChunk.Clear();
            ChunkToJoint.Clear();
            foreach (var joint in GetComponents<Joint>())
            {
                var chunk = joint.connectedBody.GetOrAddComponent<ChunkNode>();
                JointToChunk[joint] = chunk;
                ChunkToJoint[chunk] = joint;
            }

            foreach (var chunkNode in ChunkToJoint.Keys)
            {
                Neighbours.Add(chunkNode);

                if (chunkNode.Contains(this) == false)
                {
                    chunkNode.Neighbours.Add(this);
                }
            }

            NeighboursArray = Neighbours.ToArray();
        }

        private void OnJointBreak(float breakForce)
        {
            HasBrokenLinks = true;
        }

        public void CleanBrokenLinks()
        {
            var brokenLinks = JointToChunk.Keys.Where(j => j == false).ToList();
            foreach (var link in brokenLinks)
            {
                var body = JointToChunk[link];

                JointToChunk.Remove(link);
                ChunkToJoint.Remove(body);

                body.Remove(this);
                Neighbours.Remove(body);
            }

            NeighboursArray = Neighbours.ToArray();
            HasBrokenLinks = false;
        }

        private void Remove(ChunkNode chunkNode)
        {
            ChunkToJoint.Remove(chunkNode);
            Neighbours.Remove(chunkNode);
            NeighboursArray = Neighbours.ToArray();
        }

        public void ReleaseFromImpact(Vector3 impulse, Vector3 impactPoint)
        {
            Unfreeze();

            ChunkNode[] connectedChunks = Neighbours.ToArray();
            foreach (ChunkNode neighbour in connectedChunks)
            {
                if (neighbour == null)
                    continue;

                if (ChunkToJoint.TryGetValue(neighbour, out Joint ownJoint))
                {
                    JointToChunk.Remove(ownJoint);
                    if (ownJoint != null)
                        Destroy(ownJoint);
                }

                if (neighbour.ChunkToJoint.TryGetValue(this, out Joint neighbourJoint))
                {
                    neighbour.JointToChunk.Remove(neighbourJoint);
                    if (neighbourJoint != null)
                        Destroy(neighbourJoint);
                }

                ChunkToJoint.Remove(neighbour);
                neighbour.ChunkToJoint.Remove(this);
                neighbour.Neighbours.Remove(this);
                neighbour.NeighboursArray = neighbour.Neighbours.ToArray();
            }

            foreach (Joint remainingJoint in GetComponents<Joint>())
            {
                if (remainingJoint != null)
                    Destroy(remainingJoint);
            }

            JointToChunk.Clear();
            ChunkToJoint.Clear();
            Neighbours.Clear();
            NeighboursArray = new ChunkNode[0];
            HasBrokenLinks = true;

            if (rb != null)
            {
                rb.WakeUp();
                rb.AddForceAtPosition(impulse, impactPoint, ForceMode.Impulse);
            }
        }

        public void Unfreeze()
        {
            if (!rb) return;
            frozen = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.useGravity = true;
            rb.gameObject.layer = LayerMask.NameToLayer("Default");
        }

        private void Freeze()
        {
            if (!rb) return;
            frozen = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.useGravity = false;
            rb.gameObject.layer = LayerMask.NameToLayer("FrozenChunks");
            frozenPos = rb.transform.position;
            forzenRot = rb.transform.rotation;
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var worldCenterOfMass = transform.TransformPoint(transform.GetComponent<Rigidbody>().centerOfMass);
            if (rb != null)
            {
                if (IsStatic)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(worldCenterOfMass, 0.05f);
                }
                else
                {
                    Gizmos.color = Color.SetAlpha(0.5f);
                    Gizmos.DrawSphere(worldCenterOfMass, 0.1f);
                }
            }


            foreach (var joint in JointToChunk.Keys)
            {
                if (joint && rb)
                {
                    var from = transform.TransformPoint(rb.centerOfMass);
                    if (joint.connectedBody == null) continue;
                    var to = joint.connectedBody.transform.TransformPoint(joint.connectedBody.centerOfMass);
                    Gizmos.color = Color;
                    Gizmos.DrawLine(from, to);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var node in Neighbours)
            {
                var mesh = node.GetComponent<MeshFilter>().mesh;
                Gizmos.color = Color.yellow.SetAlpha(.2f);
                Gizmos.DrawMesh(mesh, node.transform.position, node.transform.rotation);
            }
        }
#endif
    }
}