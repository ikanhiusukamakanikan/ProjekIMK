using System;
using UnityEngine;

public class VRShovelDig : MonoBehaviour
{
    public static event Action<Vector3> Dug;

    [Header("Dig Settings")]
    public float digRadius = 2f;
    public float minVelocityToDig = 1.5f;
    public float raycastDistance = 1.5f;

    [Header("Dirt Spawn")]
    public GameObject dirtPrefab;
    public int spawnCount = 3;
    public float spawnSpread = 0.5f;
    public float dirtForce = 3f;
    public float destroyAfter = 10f;

    [Header("Tracking")]
    public Transform shovelHead;

    [Header("Performance")]
    public float cooldown = 0.2f;

    private Terrain terrain;
    private Vector3 lastPosition;
    private float velocity;
    private float lastDigTime;

    void Start()
    {
        terrain = GameManager.Instance.terrain;
        lastPosition = shovelHead.position;
    }

    void Update()
    {
        velocity = (shovelHead.position - lastPosition).magnitude / Time.deltaTime;
        lastPosition = shovelHead.position;
    }

    void OnTriggerStay(Collider other)
    {
        if (Time.time - lastDigTime < cooldown) return;

        // Pastikan ini terrain
        if (other.GetComponent<Terrain>())
        {
            if (velocity >= minVelocityToDig)
            {
                TryDig();
            }
        }
    }

    void TryDig()
    {
        RaycastHit hit;

        if (Physics.Raycast(shovelHead.position, Vector3.down, out hit, raycastDistance))
        {
            if (hit.collider.GetComponent<Terrain>())
            {
                Vector3 hitPoint = hit.point;

                RemoveGrass(hitPoint);
                SpawnDirt(hitPoint);
                Dug?.Invoke(hitPoint);

                lastDigTime = Time.time;
            }
        }
    }

    void SpawnDirt(Vector3 center)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-spawnSpread, spawnSpread),
                0,
                UnityEngine.Random.Range(-spawnSpread, spawnSpread)
            );

            Vector3 spawnPos = center + offset;

            spawnPos.y = terrain.SampleHeight(spawnPos) + terrain.transform.position.y;

            GameObject dirt = Instantiate(dirtPrefab, spawnPos, dirtPrefab.transform.rotation);

            dirt.transform.rotation = Quaternion.Euler(-90, UnityEngine.Random.Range(0, 360), 0);

            float scale = UnityEngine.Random.Range(0.8f, 1.2f);
            dirt.transform.localScale *= scale;

            Rigidbody rb = dirt.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(Vector3.up * UnityEngine.Random.Range(dirtForce * 0.5f, dirtForce), ForceMode.Impulse);
            }

            Destroy(dirt, destroyAfter);
        }
    }

    void RemoveGrass(Vector3 worldPos)
    {
        TerrainData data = terrain.terrainData;

        Vector3 terrainPos = worldPos - terrain.transform.position;

        int mapX = (int)((terrainPos.x / data.size.x) * data.detailWidth);
        int mapZ = (int)((terrainPos.z / data.size.z) * data.detailHeight);

        int radius = Mathf.RoundToInt((digRadius / data.size.x) * data.detailWidth);

        int layer = 0;

        int startX = Mathf.Clamp(mapX - radius, 0, data.detailWidth);
        int startZ = Mathf.Clamp(mapZ - radius, 0, data.detailHeight);

        int width = Mathf.Clamp(radius * 2, 0, data.detailWidth - startX);
        int height = Mathf.Clamp(radius * 2, 0, data.detailHeight - startZ);

        int[,] details = data.GetDetailLayer(startX, startZ, width, height, layer);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                float dist = Vector2.Distance(
                    new Vector2(x, z),
                    new Vector2(width / 2f, height / 2f)
                );

                if (dist <= width / 2f)
                {
                    details[x, z] = 0;
                }
            }
        }

        data.SetDetailLayer(startX, startZ, layer, details);
    }
}
