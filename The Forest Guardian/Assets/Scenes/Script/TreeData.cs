using UnityEngine;

public class TreeData : MonoBehaviour
{
    [Header("Tree Type")]
    public bool isBadTree;

    [Range(0, 100)]
    public int badTreeChance = 50;

    void Start()
    {
        GenerateTreeType();
    }

    void GenerateTreeType()
    {
        int worldSeed = GameManager.Instance.seed;

        Vector3 pos = transform.position;

        int x = Mathf.RoundToInt(pos.x * 100);
        int z = Mathf.RoundToInt(pos.z * 100);

        // Manual deterministic hash
        int uniqueSeed =
            worldSeed ^
            (x * 73856093) ^
            (z * 19349663);

        System.Random rng =
            new System.Random(uniqueSeed);

        isBadTree =
            rng.Next(0, 100) < badTreeChance;
    }
}