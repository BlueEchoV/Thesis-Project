using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Talks to OpenAIController to generate the appropriate world

public class ObjectSpawnManager : MonoBehaviour
{
    public GameObject OpenAIController;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /*
    void InstantiateGrid(string[,] grid)
    {
        for (int i = 0; i < grid.GetLength(0); i++)
        {
            for (int j = 0; j < grid.GetLength(1); j++)
            {
                // Get the prefab associated with ID
                GameObject prefab = GetPrefabById(grid[i, j]);
                // Instantiate the prefab at the correct location in my game world
                Instantiate(prefab, new Vector3(i, 0, j), Quaternion.identity);
            }
        }
    }
    */
}
