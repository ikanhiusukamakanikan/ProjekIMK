using UnityEngine;
using System.Collections.Generic;

public class TerrainTreeConverter : MonoBehaviour
{
    private Terrain terrain;

    [Header("Replace Settings")]
    public GameObject replacementPrefab;

    [Header("Optional")]
    public bool removeOriginalTrees = true;
    public bool randomYRotation = true;

    void Start()
    {
        terrain = GameManager.Instance.terrain;
        ConvertTrees();
    }

    [ContextMenu("Convert Trees")]
    public void ConvertTrees()
    {
        if (terrain == null || replacementPrefab == null)
        {
            Debug.LogError("Terrain atau prefab belum diisi.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;

        TreeInstance[] trees = terrainData.treeInstances;

        Debug.Log("Total tree: " + trees.Length);

        foreach (TreeInstance tree in trees)
        {
            // Posisi tree painter itu normalized (0-1)
            Vector3 worldPos = Vector3.Scale(tree.position, terrainData.size);
            worldPos += terrain.transform.position;

            // Ambil tinggi terrain biar akurat
            worldPos.y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;

            Quaternion rot = Quaternion.identity;

            if (randomYRotation)
            {
                rot = Quaternion.Euler(0, Random.Range(0, 360), 0);
            }

            GameObject spawned = Instantiate(
                replacementPrefab,
                worldPos,
                rot
            );

            // Scale mengikuti tree painter
            spawned.transform.localScale = new Vector3(
                tree.widthScale,
                tree.heightScale,
                tree.widthScale
            );
        }

        // Hapus tree terrain lama
        if (removeOriginalTrees)
        {
            terrainData.treeInstances = new TreeInstance[0];
        }

        Debug.Log("Selesai convert tree.");
    }
}