using System.Collections.Generic;
using UnityEngine;

public class TerrainBurner : MonoBehaviour
{
    public static TerrainBurner Instance;

    private Terrain terrain;
    private readonly List<Vector3> burnedWorldPositions = new();

    [Header("Detail Layers")]
    public int normalLayer = 0;
    public int burnedLayer = 1;

    public int burnRadius = 5;

    [Header("Debug")]
    public bool enableDebug = true;

    public bool HasBurnedArea => burnedWorldPositions.Count > 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        terrain = GameManager.Instance.terrain;

        if (terrain == null)
        {
            Error("Terrain NULL! Check GameManager.");
            return;
        }

        Log("TerrainBurner initialized");

        int count = terrain.terrainData.detailPrototypes.Length;
        Log("Detail Layer Count: " + count);

        if (normalLayer >= count || burnedLayer >= count)
        {
            Error($"Layer index invalid! normal:{normalLayer} burned:{burnedLayer}");
        }

        var prototypes = terrain.terrainData.detailPrototypes;

        for (int i = 0; i < prototypes.Length; i++)
        {
            string name = "UNKNOWN";

            if (prototypes[i].prototype != null)
                name = prototypes[i].prototype.name;
            else if (prototypes[i].prototypeTexture != null)
                name = prototypes[i].prototypeTexture.name;

            Debug.Log($"[TerrainBurner] Detail Layer {i} = {name}");
        }
    }

    public void BurnAtPosition(Vector3 worldPos)
    {
        if (terrain == null)
        {
            Error("Terrain NULL on Burn!");
            return;
        }

        TerrainData data = terrain.terrainData;

        Vector3 terrainPos = worldPos - terrain.transform.position;

        int x = (int)((terrainPos.x / data.size.x) * data.detailWidth);
        int z = (int)((terrainPos.z / data.size.z) * data.detailHeight);

        Log($"WorldPos: {worldPos}");
        Log($"TerrainPos: {terrainPos}");
        Log($"Mapped Detail Coord: ({x}, {z})");

        int size = burnRadius * 2;

        int startX = Mathf.Clamp(x - burnRadius, 0, data.detailWidth - size);
        int startZ = Mathf.Clamp(z - burnRadius, 0, data.detailHeight - size);

        Log($"Burn Area Start: ({startX}, {startZ}) Size: {size}x{size}");

        int[,] normal = data.GetDetailLayer(startX, startZ, size, size, normalLayer);
        int[,] burned = data.GetDetailLayer(startX, startZ, size, size, burnedLayer);

        int normalCountBefore = 0;
        int burnedCountBefore = 0;

        // 🔍 Hitung sebelum
        for (int zIdx = 0; zIdx < size; zIdx++)
        {
            for (int xIdx = 0; xIdx < size; xIdx++)
            {
                if (normal[zIdx, xIdx] > 0) normalCountBefore++;
                if (burned[zIdx, xIdx] > 0) burnedCountBefore++;
            }
        }

        Log($"Before → NormalCount: {normalCountBefore}, BurnedCount: {burnedCountBefore}");

        int applied = 0;

        for (int zIdx = 0; zIdx < size; zIdx++)
        {
            for (int xIdx = 0; xIdx < size; xIdx++)
            {
                // hanya burn kalau ada grass normal
                if (normal[zIdx, xIdx] > 0)
                {
                    int amount = normal[zIdx, xIdx];

                    // hapus dari normal
                    normal[zIdx, xIdx] = 0;

                    // pindah ke burned
                    burned[zIdx, xIdx] = amount;

                    applied++;
                }
            }
        }

        data.SetDetailLayer(startX, startZ, normalLayer, normal);
        data.SetDetailLayer(startX, startZ, burnedLayer, burned);

        terrain.Flush();

        // 🔍 VALIDASI setelah set
        int[,] check = data.GetDetailLayer(startX, startZ, size, size, burnedLayer);

        int burnedCountAfter = 0;

        for (int zIdx = 0; zIdx < size; zIdx++)
        {
            for (int xIdx = 0; xIdx < size; xIdx++)
            {
                if (check[zIdx, xIdx] > 0) burnedCountAfter++;
            }
        }

        Log($"After → BurnedCount: {burnedCountAfter}");
        Log($"Applied Cells: {applied}");

        if (burnedCountAfter == 0)
        {
            Warn("Burned layer tetap 0 setelah apply! (Render atau layer issue)");
        }
        else
        {
            RegisterBurnedPosition(worldPos);
            Log("Burn SUCCESS (data applied)");
        }
    }

    public bool IsPositionInBurnedArea(Vector3 worldPos, float worldRadius)
    {
        if (terrain == null)
        {
            return IsNearRegisteredBurnedPosition(worldPos, worldRadius);
        }

        if (!TryGetDetailArea(worldPos, worldRadius, out int startX, out int startZ, out int width, out int height))
        {
            return IsNearRegisteredBurnedPosition(worldPos, worldRadius);
        }

        int[,] burned = terrain.terrainData.GetDetailLayer(startX, startZ, width, height, burnedLayer);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (burned[x, z] > 0)
                {
                    return true;
                }
            }
        }

        return IsNearRegisteredBurnedPosition(worldPos, worldRadius);
    }

    public void ClearBurnedAtPosition(Vector3 worldPos, float worldRadius)
    {
        if (terrain == null)
        {
            RemoveRegisteredBurnedPositions(worldPos, worldRadius);
            return;
        }

        if (!TryGetDetailArea(worldPos, worldRadius, out int startX, out int startZ, out int width, out int height))
        {
            RemoveRegisteredBurnedPositions(worldPos, worldRadius);
            return;
        }

        TerrainData data = terrain.terrainData;
        int[,] normal = data.GetDetailLayer(startX, startZ, width, height, normalLayer);
        int[,] burned = data.GetDetailLayer(startX, startZ, width, height, burnedLayer);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (burned[x, z] <= 0)
                {
                    continue;
                }

                normal[x, z] = Mathf.Max(normal[x, z], burned[x, z]);
                burned[x, z] = 0;
            }
        }

        data.SetDetailLayer(startX, startZ, normalLayer, normal);
        data.SetDetailLayer(startX, startZ, burnedLayer, burned);
        terrain.Flush();

        RemoveRegisteredBurnedPositions(worldPos, worldRadius);
    }

    private bool TryGetDetailArea(
        Vector3 worldPos,
        float worldRadius,
        out int startX,
        out int startZ,
        out int width,
        out int height
    )
    {
        startX = 0;
        startZ = 0;
        width = 0;
        height = 0;

        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData data = terrain.terrainData;
        if (data.detailWidth <= 0 || data.detailHeight <= 0)
        {
            return false;
        }

        Vector3 terrainPos = worldPos - terrain.transform.position;
        int centerX = Mathf.RoundToInt((terrainPos.x / data.size.x) * data.detailWidth);
        int centerZ = Mathf.RoundToInt((terrainPos.z / data.size.z) * data.detailHeight);
        int radiusX = Mathf.Max(1, Mathf.RoundToInt((worldRadius / data.size.x) * data.detailWidth));
        int radiusZ = Mathf.Max(1, Mathf.RoundToInt((worldRadius / data.size.z) * data.detailHeight));

        width = Mathf.Clamp(radiusX * 2, 1, data.detailWidth);
        height = Mathf.Clamp(radiusZ * 2, 1, data.detailHeight);
        startX = Mathf.Clamp(centerX - radiusX, 0, data.detailWidth - width);
        startZ = Mathf.Clamp(centerZ - radiusZ, 0, data.detailHeight - height);

        return true;
    }

    private void RegisterBurnedPosition(Vector3 worldPos)
    {
        float minDistanceSqr = burnRadius * burnRadius;

        for (int i = 0; i < burnedWorldPositions.Count; i++)
        {
            if ((burnedWorldPositions[i] - worldPos).sqrMagnitude <= minDistanceSqr)
            {
                return;
            }
        }

        burnedWorldPositions.Add(worldPos);
    }

    private bool IsNearRegisteredBurnedPosition(Vector3 worldPos, float worldRadius)
    {
        float radius = Mathf.Max(worldRadius, burnRadius);
        float radiusSqr = radius * radius;

        for (int i = 0; i < burnedWorldPositions.Count; i++)
        {
            if ((burnedWorldPositions[i] - worldPos).sqrMagnitude <= radiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveRegisteredBurnedPositions(Vector3 worldPos, float worldRadius)
    {
        float radius = Mathf.Max(worldRadius, burnRadius);
        float radiusSqr = radius * radius;

        for (int i = burnedWorldPositions.Count - 1; i >= 0; i--)
        {
            if ((burnedWorldPositions[i] - worldPos).sqrMagnitude <= radiusSqr)
            {
                burnedWorldPositions.RemoveAt(i);
            }
        }
    }

    // ===== DEBUG HELPERS =====
    void Log(string msg)
    {
        if (enableDebug)
            Debug.Log($"[TerrainBurner] {msg}", this);
    }

    void Warn(string msg)
    {
        if (enableDebug)
            Debug.LogWarning($"[TerrainBurner] {msg}", this);
    }

    void Error(string msg)
    {
        Debug.LogError($"[TerrainBurner] {msg}", this);
    }

    // 🔍 BONUS: visual debug di scene
    void OnDrawGizmosSelected()
    {
        if (!enableDebug) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, burnRadius);
    }
}
