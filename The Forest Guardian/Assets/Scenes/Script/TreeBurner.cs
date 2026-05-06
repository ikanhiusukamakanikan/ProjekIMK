using UnityEngine;
using System.Collections.Generic;

public class TreeBurner : MonoBehaviour
{
    public static TreeBurner Instance;

    private Terrain terrain;
    public GameObject burnedTreePrefab;

    public float burnRadius = 3f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        terrain = GameManager.Instance.terrain;
    }

    public void BurnTrees(Vector3 position)
    {
        TerrainData data = terrain.terrainData;

        var trees = data.treeInstances;
        List<TreeInstance> newTrees = new List<TreeInstance>();

        foreach (var tree in trees)
        {
            Vector3 worldPos = Vector3.Scale(tree.position, data.size) + terrain.transform.position;

            if (Vector3.Distance(worldPos, position) < burnRadius)
            {
                Instantiate(burnedTreePrefab, worldPos, Quaternion.identity);
            }
            else
            {
                newTrees.Add(tree);
            }
        }

        data.treeInstances = newTrees.ToArray();
    }
}