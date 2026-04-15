using Project.Scripts.Fractures;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;



public class FractureRealTime : MonoBehaviour
{
    [SerializeField] private Anchor anchor = Anchor.Bottom;
    [SerializeField] private int chunks = 500;
    [SerializeField] private float density = 50;
    [SerializeField] private float internalStrength = 100;

    [SerializeField] private Material insideMaterial;
    [SerializeField] private Material outsideMaterial;

    private Random rng = new Random();


    private void Update()
    {
       if (Input.GetKeyUp(KeyCode.Escape))
        {
            FractureGameobject();
            gameObject.SetActive(false);
        }
    }

    //IF PROBLEMS WITH FRACTUREGAMEOBJECT Might Be that in ChunkGrapher or ChunkNode has a Layer called FreezedSomething and is not created
    public ChunkGraphManager FractureGameobject()
    {
        var seed = rng.Next();
        return Fracture.FractureGameObject(
            gameObject,
            anchor,
            seed,
            chunks,
            insideMaterial,
            outsideMaterial,
            internalStrength,
            density
        );
    }
}

