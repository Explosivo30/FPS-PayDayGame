using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private List <Transform> players = new List<Transform>();

    private void Awake()
    {
        if (Instance != null)
            return;

        Instance = this;

        //DontDestroyOnLoad(gameObject);
    }


    public List <Transform> GetPlayerTransforms() {  return players;  }

    public void AddPlayerTransforms(Transform player){ if(!players.Contains(player)) players.Add(player); }

}
