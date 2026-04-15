using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

using System.Runtime.InteropServices;
using Unity.Mathematics; // Import Unity.Mathematics for quaternion functionality
using Random = Unity.Mathematics.Random; // Alias el Random de Unity.Mathematics
namespace Project.Scripts.Fractures
{
    public class FractureThis : MonoBehaviour
    {
        [SerializeField] private Anchor anchor = Anchor.Bottom;
        [SerializeField] private int chunks = 500;
        [SerializeField] private float density = 50;
        [SerializeField] private float internalStrength = 100;

        [SerializeField] private Material insideMaterial;
        [SerializeField] private Material outsideMaterial;

        private System.Random rng = new System.Random();
        private ChunkGraphManager graphManager;

        private void Start()
        {
            gameObject.SetActive(false);
            FractureObject();
            
        }

        public void FractureObject()
        {
            int seed = rng.Next();
            graphManager = Fracture.FractureGameObject(
                gameObject,
                anchor,
                seed,
                chunks,
                insideMaterial,
                outsideMaterial,
                internalStrength,
                density
            );
            //COSA AÑADIDA
            // Obtenemos el objeto raíz de la fractura ("Fracture")
            GameObject fractureRoot = graphManager.gameObject;

            // 2. CONFIGURACIÓN DEL SWAP
            // En lugar de ocultar 'this.gameObject' inmediatamente, 
            // le añadimos el detonador.

            // 3. AÑADIR DETONADORES A LOS HIJOS (La parte que pediste)
            // Buscamos todos los objetos hijos que tengan mesh (que son los que se ven y chocan)
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer rend in renderers)
            {
                GameObject childObj = rend.gameObject;

                // Evitamos ponerle el script al propio objeto Fracture si por error está dentro
                if (childObj.transform.IsChildOf(fractureRoot.transform)) continue;

                // Le añadimos el detonador a esta pieza específica
                var detonator = childObj.AddComponent<ImpactDetonator>();

                // Le decimos: "Si te golpean a ti, activa 'fractureRoot' y esconde 'this.gameObject' (el padre supremo)"
                detonator.Setup(fractureRoot, this.gameObject);
                detonator.impactThreshold = 5f;
            }

            // NO desactivamos gameObject aquí.
            // gameObject.SetActive(false); <--- BORRADO
            gameObject.SetActive(true);

            // El detonador se encargará de hacer el Swap cuando algo choque.

            //FIN COSA AÑADIDA
            
        }

        // Este método es llamado desde ChunkGraphManager cuando se debe generar fragmentos
        public void CreateFragments(
            Vector3[] vertices,
            int[] triangles,
            Vector3[] normals,
            Transform origin,
            float fragmentSize,
            float explosionForce,
            float explosionRadius,
            float fragmentLifeTime)
        {
            // 1. Crear NativeArrays y datos de TransformData
            NativeArray<Vector3> verticesArray = new NativeArray<Vector3>(vertices, Allocator.TempJob);
            NativeArray<int> trianglesArray = new NativeArray<int>(triangles, Allocator.TempJob);
            // normals no se usan dentro del job, así que podemos descartarlos o mantenerlos si en el futuro se necesitan
            //NativeArray<Vector3> normalsArray  = new NativeArray<Vector3>(normals, Allocator.TempJob);

            TransformData originData = new TransformData
            {
                position = origin.position,
                rotation = origin.rotation
            };

            // 2. Crear el NativeArray para resultados
            NativeArray<FragmentResult> results = new NativeArray<FragmentResult>(chunks, Allocator.TempJob);

            // 3. Montar y lanzar el job
            var job = new CreateFragmentsJob
            {
                vertices = verticesArray,
                triangles = trianglesArray,
                originData = originData,
                fragmentSize = fragmentSize,
                explosionForce = explosionForce,
                fragmentLifeTime = fragmentLifeTime,
                results = results
            };

            int batchSize = 32; // Ajustar según perfil
            JobHandle handle = job.Schedule(chunks, batchSize);
            handle.Complete();

            // 4. Procesar resultados
            ProcessFragmentResults(results);

            // 5. Liberar NativeArrays
            verticesArray.Dispose();
            trianglesArray.Dispose();
            //normalsArray.Dispose();
            results.Dispose();
        }

        private void ProcessFragmentResults(NativeArray<FragmentResult> results)
        {
            for (int i = 0; i < results.Length; i++)
            {
                FragmentResult r = results[i];

                // Convertir posición float3 → Vector3
                Vector3 fragmentPos = new Vector3(r.position.x, r.position.y, r.position.z);
                // Convertir rotación quaternion → UnityEngine.Quaternion
                quaternion qr = r.rotation;
                Quaternion fragmentRot = new Quaternion(qr.value.x, qr.value.y, qr.value.z, qr.value.w);
                // Convertir velocidad float3 → Vector3
                Vector3 fragmentVel = new Vector3(r.velocity.x, r.velocity.y, r.velocity.z);

                GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragment.transform.position = fragmentPos;
                fragment.transform.rotation = fragmentRot;
                fragment.transform.localScale = Vector3.one * r.size;

                if (insideMaterial != null)
                {
                    Renderer rend = fragment.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material = insideMaterial;
                    }
                }

                Rigidbody rb = fragment.AddComponent<Rigidbody>();
                rb.linearVelocity = fragmentVel;
                Destroy(fragment, r.lifeTime);
            }
        }

        // Datos simplificados de posición/rotación
        public struct TransformData
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        // Resultado de un fragmento: posición, rotación, velocidad, tamaño y tiempo de vida
        public struct FragmentResult
        {
            public float3 position;
            public quaternion rotation;
            public float3 velocity;
            public float size;
            public float lifeTime;
        }

        // Job que calcula un fragmento por cada índice
        [BurstCompile]
        struct CreateFragmentsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> vertices;
            [ReadOnly] public NativeArray<int> triangles;

            public TransformData originData;
            public float fragmentSize;
            public float explosionForce;
            public float fragmentLifeTime;
            [WriteOnly] public NativeArray<FragmentResult> results;

            public void Execute(int index)
            {
                // 1. Semilla única por índice
                var rng = new Unity.Mathematics.Random((uint)(0x6E624EB7u ^ index));

                // 2. Tomar un triángulo aleatorio (tres índices)
                int triStart = rng.NextInt(0, triangles.Length / 3) * 3;
                Vector3 v1Vec = vertices[triStart];
                Vector3 v2Vec = vertices[triStart + 1];
                Vector3 v3Vec = vertices[triStart + 2];

                // 3. Convertir Vector3 → float3
                float3 v1f = new float3(v1Vec.x, v1Vec.y, v1Vec.z);
                float3 v2f = new float3(v2Vec.x, v2Vec.y, v2Vec.z);
                float3 v3f = new float3(v3Vec.x, v3Vec.y, v3Vec.z);

                // 4. Centroid del triángulo en espacio local
                float3 localCentroid = (v1f + v2f + v3f) / 3f;

                // 5. Convertir originData.position → float3
                float3 originPos = new float3(
                    originData.position.x,
                    originData.position.y,
                    originData.position.z
                );

                // 6. Convertir originData.rotation → quaternion de Unity.Mathematics
                quaternion originRot = new quaternion(
                    originData.rotation.x,
                    originData.rotation.y,
                    originData.rotation.z,
                    originData.rotation.w
                );

                // 7. Posición del centro de triángulo en coordenadas de mundo
                float3 worldCentroid = originPos + math.mul(originRot, localCentroid);

                // 8. Rotación aleatoria para el fragmento
                quaternion fragmentRot = rng.NextQuaternionRotation();

                // 9. Velocidad de explosión: dirección del origen al centro
                float3 dir = math.normalizesafe(worldCentroid - originPos);
                float3 velocity = dir * explosionForce;

                // 10. Escribir resultado en el array
                results[index] = new FragmentResult
                {
                    position = worldCentroid,
                    rotation = fragmentRot,
                    velocity = velocity,
                    size = fragmentSize,
                    lifeTime = fragmentLifeTime
                };
            }
        }
    }
}
