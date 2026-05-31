using System;
using UnityEngine;

public class FireSpawner : MonoBehaviour
{
    public static FireSpawner Instance;
    public static event Action<GameObject, FireNode> FireSpawned;

    [Header("Setup")]
    public GameObject firePrefab;
    private Terrain terrain;

    [Header("Height Limit")]
    public float minHeight = 10f;
    public float maxHeight = 15f;

    [Header("Spawn")]
    public int spawnCount = 3;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        terrain = GameManager.Instance.terrain;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            SpawnFire();
        }
    }

    public int SpawnFire()
    {
        if (terrain == null && GameManager.Instance != null)
        {
            terrain = GameManager.Instance.terrain;
        }

        if (terrain == null || firePrefab == null)
        {
            Debug.LogWarning($"[{nameof(FireSpawner)}] Terrain atau firePrefab belum diisi.", this);
            return 0;
        }

        int spawnedCount = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 pos = GetRandomPointOnTerrain();

            if (IsValidHeight(pos.y))
            {
                GameObject fire = Instantiate(firePrefab, pos, Quaternion.identity);
                fire.transform.rotation = Quaternion.Euler(-90, 0, 0);

                FireNode node = fire.GetComponent<FireNode>();
                if (node != null)
                {
                    node.Ignite();
                }

                FireSpawned?.Invoke(fire, node);
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    public bool IsValidHeight(float height)
    {
        return height >= minHeight && height <= maxHeight;
    }

    Vector3 GetRandomPointOnTerrain()
    {
        Vector3 terrainPos = terrain.transform.position;

        float x = UnityEngine.Random.Range(0, terrain.terrainData.size.x);
        float z = UnityEngine.Random.Range(0, terrain.terrainData.size.z);

        float y = terrain.SampleHeight(new Vector3(x, 0, z));

        return new Vector3(x, y, z) + terrainPos;
    }
}
